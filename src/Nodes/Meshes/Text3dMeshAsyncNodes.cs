// Async variants of the mesh nodes: the expensive geometry work (DirectWrite layout
// drawing, Direct2D flattening/outlining/tessellation, extrusion) runs on a background
// task so long or frequently changing texts don't stall the frame; GPU buffers are
// created on the main thread the moment a result is adopted. Latest-wins semantics:
// while a computation runs, further input changes are coalesced into one follow-up run.
//
// For the (Advanced) variants the layout pointer is AddRef'd for the task's lifetime,
// so a FontAndParagraph rebuild on the main thread cannot release it mid-draw (built
// layouts are never mutated afterwards — changes always produce a new layout).

using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Nodes.Models;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
using GlyphList = System.Collections.Generic.List<(Stride.Graphics.VertexPositionNormalTexture[] Vertices, Stride.Core.Mathematics.Vector2 Position)>;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using SideUVMapping = VL.Stride.Text3d.Enums.SideUVMapping;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Nodes.Meshes;

/// <summary>Like Text3dMesh, but generates the mesh on a background thread; the last completed mesh is output while a new one is computed.</summary>
[ProcessNode(Name = "Text3dMesh (Async)")]
public class Text3dMeshAsync : IDisposable
{
    private readonly GameServices services;
    private readonly PrebuiltMeshModel model = new();
    private readonly BackgroundComputation<VertexPositionNormalTexture[]> computation = new();
    private Mesh? mesh;
    private bool lastWeld;

    public Text3dMeshAsync(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="output">The generated text mesh (the last completed one while a computation is in progress).</param>
    /// <param name="inProgress">True while a mesh is being computed in the background.</param>
    /// <param name="text">The string to render.</param>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="sideUVMapping">How the side walls of the extrusion are UV-mapped.\nSilhouette (the previous behavior) projects the flat text silhouette onto the walls, so texture coordinates do not change along the depth and textures smear into stripes there; use it for backwards compatibility or when only the caps matter.\nContourDepth unrolls each wall like a paper strip: U runs once around each contour (0 to 1, with the wrap seam where the contour starts) and V runs along the depth (0 on the front face, 1 on the back face); use it for gradients, ribbons or any texture that should fit exactly once around a letter.\nContourDepthTiled uses the same unrolling but with absolute surface distances divided by Texture Scale, applied to the caps as well, so a pattern tiles at the same physical size everywhere; coordinates exceed 1 on larger surfaces, so set the texture addressing to wrap; use it for seamless materials such as noise or fabric. Every contour (outline or hole) is mapped independently.</param>
    /// <param name="textureScale">Surface distance covered by one texture repeat; only used by ContourDepthTiled.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    public unsafe void Update(out Mesh? output, out bool inProgress,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        SideUVMapping sideUVMapping = SideUVMapping.Silhouette,
        float textureScale = Core.Extruder.DefaultTextureScale,
        bool weldVertices = false)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(sideUVMapping); hashCode.Add(textureScale);
        int hash = hashCode.ToHashCode();

        bool adopted = computation.Poll(hash, out var vertices, out bool needsStart, out inProgress);
        if (needsStart)
        {
            var t = text ?? "";
            var f = font?.Value ?? "Arial";
            var size = fontSize;
            var ta = textAlignment;
            var pa = paragraphAlignment;
            var ea = extrudeAmount;
            var eo = extrudeOrigin;
            var ft = flatteningTolerance;
            var sa = smoothingAngle;
            var uv = sideUVMapping;
            var ts = textureScale;
            computation.Start(hash, () => ComputeVertices(t, f, size, ta, pa, ea, eo, ft, sa, uv, ts));
            inProgress = true;
        }
        // Welding is a main-thread post-process: toggling it re-welds the cached
        // vertices without re-running the background extraction.
        if ((adopted || weldVertices != lastWeld) && vertices != null)
        {
            model.WeldVertices = weldVertices;
            mesh = GlyphMeshNodeHelper.BuildMesh(services.Game, model, vertices);
            lastWeld = weldVertices;
        }

        output = mesh;
    }

    private static unsafe VertexPositionNormalTexture[] ComputeVertices(
        string text, string font, int fontSize, TextAlignment textAlignment,
        ParagraphAlignment paragraphAlignment, float extrudeAmount, ExtrudeOrigin extrudeOrigin,
        float flatteningTolerance, float smoothingAngle, SideUVMapping sideUVMapping, float textureScale)
    {
        var layout = GlyphMeshNodeHelper.CreateSimpleLayout(text, font, fontSize, textAlignment, paragraphAlignment);
        try
        {
            var vertices = new List<VertexPositionNormalTexture>(1024);
            TextOutlineExtractor.ExtractVertices(layout, vertices, extrudeAmount, extrudeOrigin,
                flatteningTolerance, smoothingAngle, sideUVMapping, textureScale);
            return vertices.ToArray();
        }
        finally
        {
            layout->Release();
        }
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Like Text3dMesh (Advanced), but generates the mesh on a background thread; the last completed mesh is output while a new one is computed.</summary>
[ProcessNode(Name = "Text3dMesh (Advanced Async)")]
public class Text3dMeshAdvancedAsync : IDisposable
{
    private readonly GameServices services;
    private readonly PrebuiltMeshModel model = new();
    private readonly BackgroundComputation<VertexPositionNormalTexture[]> computation = new();
    private Mesh? mesh;
    private bool lastWeld;

    public Text3dMeshAdvancedAsync(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="output">The generated text mesh (the last completed one while a computation is in progress).</param>
    /// <param name="inProgress">True while a mesh is being computed in the background.</param>
    /// <param name="fontAndParagraph">The FontAndParagraph providing the text layout to render.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="sideUVMapping">How the side walls of the extrusion are UV-mapped.\nSilhouette (the previous behavior) projects the flat text silhouette onto the walls, so texture coordinates do not change along the depth and textures smear into stripes there; use it for backwards compatibility or when only the caps matter.\nContourDepth unrolls each wall like a paper strip: U runs once around each contour (0 to 1, with the wrap seam where the contour starts) and V runs along the depth (0 on the front face, 1 on the back face); use it for gradients, ribbons or any texture that should fit exactly once around a letter.\nContourDepthTiled uses the same unrolling but with absolute surface distances divided by Texture Scale, applied to the caps as well, so a pattern tiles at the same physical size everywhere; coordinates exceed 1 on larger surfaces, so set the texture addressing to wrap; use it for seamless materials such as noise or fabric. Every contour (outline or hole) is mapped independently.</param>
    /// <param name="textureScale">Surface distance covered by one texture repeat; only used by ContourDepthTiled.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    public unsafe void Update(out Mesh? output, out bool inProgress,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        SideUVMapping sideUVMapping = SideUVMapping.Silhouette,
        float textureScale = Core.Extruder.DefaultTextureScale,
        bool weldVertices = false)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            mesh = null;
            inProgress = false;
            output = null;
            return;
        }

        var hashCode = new HashCode();
        hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(sideUVMapping); hashCode.Add(textureScale);
        int hash = hashCode.ToHashCode();

        bool adopted = computation.Poll(hash, out var vertices, out bool needsStart, out inProgress);
        if (needsStart)
        {
            // Keep the layout alive for the task's lifetime (see file header).
            nint layoutPtr = (nint)layout.Pointer;
            Marshal.AddRef(layoutPtr);
            var ea = extrudeAmount;
            var eo = extrudeOrigin;
            var ft = flatteningTolerance;
            var sa = smoothingAngle;
            var uv = sideUVMapping;
            var ts = textureScale;
            computation.Start(hash, () => ComputeVertices(layoutPtr, ea, eo, ft, sa, uv, ts));
            inProgress = true;
        }
        // Welding is a main-thread post-process: toggling it re-welds the cached
        // vertices without re-running the background extraction.
        if ((adopted || weldVertices != lastWeld) && vertices != null)
        {
            model.WeldVertices = weldVertices;
            mesh = GlyphMeshNodeHelper.BuildMesh(services.Game, model, vertices);
            lastWeld = weldVertices;
        }

        output = mesh;
    }

    private static unsafe VertexPositionNormalTexture[] ComputeVertices(nint layoutPtr,
        float extrudeAmount, ExtrudeOrigin extrudeOrigin, float flatteningTolerance, float smoothingAngle,
        SideUVMapping sideUVMapping, float textureScale)
    {
        try
        {
            var vertices = new List<VertexPositionNormalTexture>(1024);
            TextOutlineExtractor.ExtractVertices((Silk.NET.DirectWrite.IDWriteTextLayout*)layoutPtr,
                vertices, extrudeAmount, extrudeOrigin, flatteningTolerance, smoothingAngle,
                sideUVMapping, textureScale);
            return vertices.ToArray();
        }
        finally
        {
            Marshal.Release(layoutPtr);
        }
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Like Text3dMeshes, but generates the per-glyph meshes on a background thread; the last completed set is output while a new one is computed.</summary>
[ProcessNode(Name = "Text3dMeshes (Async)")]
public class Text3dMeshesAsync : IDisposable
{
    private readonly GameServices services;
    private readonly BackgroundComputation<GlyphList> computation = new();
    private Spread<Mesh> meshes = Spread<Mesh>.Empty;
    private Spread<Matrix> transformations = Spread<Matrix>.Empty;
    private bool lastWeld;

    public Text3dMeshesAsync(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="meshes">One mesh per visible glyph, in draw order (the last completed set while a computation is in progress).</param>
    /// <param name="transformations">Per glyph: the translation placing the mesh at its position in the text.</param>
    /// <param name="inProgress">True while the meshes are being computed in the background.</param>
    /// <param name="text">The string to render.</param>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded meshes sit relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="sideUVMapping">How the side walls of the extrusion are UV-mapped.\nSilhouette (the previous behavior) projects the flat text silhouette onto the walls, so texture coordinates do not change along the depth and textures smear into stripes there; use it for backwards compatibility or when only the caps matter.\nContourDepth unrolls each wall like a paper strip: U runs once around each contour (0 to 1, with the wrap seam where the contour starts) and V runs along the depth (0 on the front face, 1 on the back face); use it for gradients, ribbons or any texture that should fit exactly once around a letter.\nContourDepthTiled uses the same unrolling but with absolute surface distances divided by Texture Scale, applied to the caps as well, so a pattern tiles at the same physical size everywhere; coordinates exceed 1 on larger surfaces, so set the texture addressing to wrap; use it for seamless materials such as noise or fabric. Every contour (outline or hole) is mapped independently.</param>
    /// <param name="textureScale">Surface distance covered by one texture repeat; only used by ContourDepthTiled.</param>
    /// <param name="weldVertices">Welds identical vertices into indexed meshes: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle lists).</param>
    public unsafe void Update(out Spread<Mesh> meshes, out Spread<Matrix> transformations, out bool inProgress,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        SideUVMapping sideUVMapping = SideUVMapping.Silhouette,
        float textureScale = Core.Extruder.DefaultTextureScale,
        bool weldVertices = false)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(sideUVMapping); hashCode.Add(textureScale);
        int hash = hashCode.ToHashCode();

        bool adopted = computation.Poll(hash, out var glyphs, out bool needsStart, out inProgress);
        if (needsStart)
        {
            var t = text ?? "";
            var f = font?.Value ?? "Arial";
            var size = fontSize;
            var ta = textAlignment;
            var pa = paragraphAlignment;
            var ea = extrudeAmount;
            var eo = extrudeOrigin;
            var ft = flatteningTolerance;
            var sa = smoothingAngle;
            var uv = sideUVMapping;
            var ts = textureScale;
            computation.Start(hash, () => ComputeGlyphs(t, f, size, ta, pa, ea, eo, ft, sa, uv, ts));
            inProgress = true;
        }
        // Welding is a main-thread post-process: toggling it re-welds the cached
        // vertices without re-running the background extraction.
        if ((adopted || weldVertices != lastWeld) && glyphs != null)
        {
            GlyphMeshNodeHelper.BuildMeshes(services.Game, glyphs, weldVertices, out this.meshes, out this.transformations);
            lastWeld = weldVertices;
        }

        meshes = this.meshes;
        transformations = this.transformations;
    }

    private static unsafe GlyphList ComputeGlyphs(
        string text, string font, int fontSize, TextAlignment textAlignment,
        ParagraphAlignment paragraphAlignment, float extrudeAmount, ExtrudeOrigin extrudeOrigin,
        float flatteningTolerance, float smoothingAngle, SideUVMapping sideUVMapping, float textureScale)
    {
        var layout = GlyphMeshNodeHelper.CreateSimpleLayout(text, font, fontSize, textAlignment, paragraphAlignment);
        try
        {
            return GlyphMeshBuilder.ExtractGlyphVertices(layout, extrudeAmount, extrudeOrigin,
                flatteningTolerance, smoothingAngle, sideUVMapping, textureScale);
        }
        finally
        {
            layout->Release();
        }
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Like Text3dMeshes (Advanced), but generates the per-glyph meshes on a background thread; the last completed set is output while a new one is computed.</summary>
[ProcessNode(Name = "Text3dMeshes (Advanced Async)")]
public class Text3dMeshesAdvancedAsync : IDisposable
{
    private readonly GameServices services;
    private readonly BackgroundComputation<GlyphList> computation = new();
    private Spread<Mesh> meshes = Spread<Mesh>.Empty;
    private Spread<Matrix> transformations = Spread<Matrix>.Empty;
    private bool lastWeld;

    public Text3dMeshesAdvancedAsync(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="meshes">One mesh per visible glyph, in draw order (the last completed set while a computation is in progress).</param>
    /// <param name="transformations">Per glyph: the translation placing the mesh at its position in the text.</param>
    /// <param name="inProgress">True while the meshes are being computed in the background.</param>
    /// <param name="fontAndParagraph">The FontAndParagraph providing the text layout to render.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded meshes sit relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="sideUVMapping">How the side walls of the extrusion are UV-mapped.\nSilhouette (the previous behavior) projects the flat text silhouette onto the walls, so texture coordinates do not change along the depth and textures smear into stripes there; use it for backwards compatibility or when only the caps matter.\nContourDepth unrolls each wall like a paper strip: U runs once around each contour (0 to 1, with the wrap seam where the contour starts) and V runs along the depth (0 on the front face, 1 on the back face); use it for gradients, ribbons or any texture that should fit exactly once around a letter.\nContourDepthTiled uses the same unrolling but with absolute surface distances divided by Texture Scale, applied to the caps as well, so a pattern tiles at the same physical size everywhere; coordinates exceed 1 on larger surfaces, so set the texture addressing to wrap; use it for seamless materials such as noise or fabric. Every contour (outline or hole) is mapped independently.</param>
    /// <param name="textureScale">Surface distance covered by one texture repeat; only used by ContourDepthTiled.</param>
    /// <param name="weldVertices">Welds identical vertices into indexed meshes: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle lists).</param>
    public unsafe void Update(out Spread<Mesh> meshes, out Spread<Matrix> transformations, out bool inProgress,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        SideUVMapping sideUVMapping = SideUVMapping.Silhouette,
        float textureScale = Core.Extruder.DefaultTextureScale,
        bool weldVertices = false)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            this.meshes = Spread<Mesh>.Empty;
            this.transformations = Spread<Matrix>.Empty;
            inProgress = false;
            meshes = this.meshes;
            transformations = this.transformations;
            return;
        }

        var hashCode = new HashCode();
        hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(sideUVMapping); hashCode.Add(textureScale);
        int hash = hashCode.ToHashCode();

        bool adopted = computation.Poll(hash, out var glyphs, out bool needsStart, out inProgress);
        if (needsStart)
        {
            // Keep the layout alive for the task's lifetime (see file header).
            nint layoutPtr = (nint)layout.Pointer;
            Marshal.AddRef(layoutPtr);
            var ea = extrudeAmount;
            var eo = extrudeOrigin;
            var ft = flatteningTolerance;
            var sa = smoothingAngle;
            var uv = sideUVMapping;
            var ts = textureScale;
            computation.Start(hash, () => ComputeGlyphs(layoutPtr, ea, eo, ft, sa, uv, ts));
            inProgress = true;
        }
        // Welding is a main-thread post-process: toggling it re-welds the cached
        // vertices without re-running the background extraction.
        if ((adopted || weldVertices != lastWeld) && glyphs != null)
        {
            GlyphMeshNodeHelper.BuildMeshes(services.Game, glyphs, weldVertices, out this.meshes, out this.transformations);
            lastWeld = weldVertices;
        }

        meshes = this.meshes;
        transformations = this.transformations;
    }

    private static unsafe GlyphList ComputeGlyphs(nint layoutPtr,
        float extrudeAmount, ExtrudeOrigin extrudeOrigin, float flatteningTolerance, float smoothingAngle,
        SideUVMapping sideUVMapping, float textureScale)
    {
        try
        {
            return GlyphMeshBuilder.ExtractGlyphVertices((Silk.NET.DirectWrite.IDWriteTextLayout*)layoutPtr,
                extrudeAmount, extrudeOrigin, flatteningTolerance, smoothingAngle,
                sideUVMapping, textureScale);
        }
        finally
        {
            Marshal.Release(layoutPtr);
        }
    }

    public void Dispose() => services.Dispose();
}
