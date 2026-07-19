// The user-facing Text3dMesh nodes (category Stride.Models.Meshes), replacing the
// formerly patched Text3dMesh process definitions. Like Text3d but outputting the
// generated Stride Mesh directly. The obsolete "Text3dMesh (Async)" was dropped.
// Since 2.2 the Text3dMeshes variants output one mesh per glyph plus per-glyph
// transforms for typography animation.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using VL.Stride.Text3d.Nodes.Models;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using SilkParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using SilkTextAlignment = Silk.NET.DirectWrite.TextAlignment;
using SilkWordWrapping = Silk.NET.DirectWrite.WordWrapping;
using StrideModel = Stride.Rendering.Model;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Nodes.Meshes;

internal static unsafe class GlyphMeshNodeHelper
{
    /// <summary>Builds one Stride mesh per visible glyph plus its translation matrix.</summary>
    public static void Build(Game game, IDWriteTextLayout* textLayout,
        float extrudeAmount, ExtrudeOrigin extrudeOrigin, float flatteningTolerance, float smoothingAngle,
        bool weldVertices, out Spread<Mesh> meshes, out Spread<Matrix> transformations)
    {
        var glyphs = GlyphMeshBuilder.ExtractGlyphVertices(textLayout, extrudeAmount,
            extrudeOrigin, flatteningTolerance, smoothingAngle);
        BuildMeshes(game, glyphs, weldVertices, out meshes, out transformations);
    }

    /// <summary>Turns precomputed per-glyph vertices into GPU meshes (main thread).</summary>
    public static void BuildMeshes(Game game,
        List<(VertexPositionNormalTexture[] Vertices, Vector2 Position)> glyphs,
        bool weldVertices, out Spread<Mesh> meshes, out Spread<Matrix> transformations)
    {
        var meshBuilder = new SpreadBuilder<Mesh>(glyphs.Count);
        var transformationBuilder = new SpreadBuilder<Matrix>(glyphs.Count);
        var model = new PrebuiltMeshModel { WeldVertices = weldVertices };
        foreach (var (vertices, position) in glyphs)
        {
            model.Vertices = vertices;
            var strideModel = new StrideModel();
            model.Generate(game.Services, strideModel);
            if (strideModel.Meshes.Count > 0)
            {
                meshBuilder.Add(strideModel.Meshes[0]);
                transformationBuilder.Add(Matrix.Translation(position.X, position.Y, 0f));
            }
        }
        meshes = meshBuilder.ToSpread();
        transformations = transformationBuilder.ToSpread();
    }

    /// <summary>Builds a whole-text mesh from precomputed vertices (main thread).</summary>
    public static Mesh? BuildMesh(Game game, PrebuiltMeshModel model, VertexPositionNormalTexture[] vertices)
    {
        model.Vertices = vertices;
        var strideModel = new StrideModel();
        model.Generate(game.Services, strideModel);
        return strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
    }

    /// <summary>Creates a layout with the same settings the simple Text3d nodes use.</summary>
    public static IDWriteTextLayout* CreateSimpleLayout(string text, string font, float fontSize,
        TextAlignment textAlignment, ParagraphAlignment paragraphAlignment)
    {
        var fmt = Native.CreateTextFormat(font, fontSize);
        var layout = Native.CreateTextLayout(text, fmt, 0.0f, 32.0f);
        fmt->Release();
        layout->SetWordWrapping(SilkWordWrapping.NoWrap);
        layout->SetTextAlignment((SilkTextAlignment)textAlignment);
        layout->SetParagraphAlignment((SilkParagraphAlignment)paragraphAlignment);
        return layout;
    }
}

/// <summary>Renders a string of text as an extruded 3D mesh.</summary>
[ProcessNode]
public class Text3dMesh : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dModel model = new();
    private Mesh? mesh;
    private int lastHash;

    public Text3dMesh(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="output">The generated text mesh.</param>
    /// <param name="text">The string to render.</param>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh — visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    public void Update(out Mesh? output,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(weldVertices);
        int hash = hashCode.ToHashCode();
        if (hash != lastHash || mesh == null)
        {
            model.Text = text ?? "";
            model.Font = font?.Value ?? "Arial";
            model.FontSize = fontSize;
            model.HorizontalAlignment = textAlignment;
            model.VerticalAlignment = paragraphAlignment;
            model.ExtrudeAmount = extrudeAmount;
            model.ExtrudeOrigin = extrudeOrigin;
            model.FlatteningTolerance = flatteningTolerance;
            model.SmoothingAngle = smoothingAngle;
            model.WeldVertices = weldVertices;
            var strideModel = Text3dModelBuilder.Build(model, services.Game);
            mesh = strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
            lastHash = hash;
        }
        output = mesh;
    }

    public void Dispose() => services.Dispose();
}

/// <summary>
/// Renders a string of text as one extruded 3D mesh per glyph, with per-glyph transforms
/// for typography animation. The pivot of each mesh is on the baseline at the glyph's pen
/// position. Spaces produce no mesh; ligatures may merge characters; underline and
/// strikethrough are not included.
/// </summary>
[ProcessNode]
public class Text3dMeshes : IDisposable
{
    private readonly GameServices services;
    private Spread<Mesh> meshes = Spread<Mesh>.Empty;
    private Spread<Matrix> transformations = Spread<Matrix>.Empty;
    private int lastHash;
    private bool built;

    public Text3dMeshes(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="meshes">One mesh per visible glyph, in draw order, each in local coordinates around its baseline pivot.</param>
    /// <param name="transformations">Per glyph: the translation placing the mesh at its position in the text.</param>
    /// <param name="text">The string to render.</param>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded meshes sit relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into indexed meshes — visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle lists).</param>
    public unsafe void Update(out Spread<Mesh> meshes, out Spread<Matrix> transformations,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(weldVertices);
        int hash = hashCode.ToHashCode();
        if (hash != lastHash || !built)
        {
            var layout = GlyphMeshNodeHelper.CreateSimpleLayout(text ?? "", font?.Value ?? "Arial", fontSize,
                textAlignment, paragraphAlignment);
            try
            {
                GlyphMeshNodeHelper.Build(services.Game, layout, extrudeAmount, extrudeOrigin,
                    flatteningTolerance, smoothingAngle, weldVertices, out this.meshes, out this.transformations);
            }
            finally
            {
                layout->Release();
            }
            lastHash = hash;
            built = true;
        }
        meshes = this.meshes;
        transformations = this.transformations;
    }

    public void Dispose() => services.Dispose();
}

/// <summary>
/// Renders a FontAndParagraph text layout as one extruded 3D mesh per glyph, with
/// per-glyph transforms for typography animation. The pivot of each mesh is on the
/// baseline at the glyph's pen position. Spaces produce no mesh; ligatures may merge
/// characters; underline and strikethrough are not included.
/// </summary>
[ProcessNode(Name = "Text3dMeshes (Advanced)")]
public class Text3dMeshesAdvanced : IDisposable
{
    private readonly GameServices services;
    private Spread<Mesh> meshes = Spread<Mesh>.Empty;
    private Spread<Matrix> transformations = Spread<Matrix>.Empty;
    private TextLayoutHandle? lastLayout;
    private int lastHash;

    public Text3dMeshesAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="meshes">One mesh per visible glyph, in draw order, each in local coordinates around its baseline pivot.</param>
    /// <param name="transformations">Per glyph: the translation placing the mesh at its position in the text.</param>
    /// <param name="fontAndParagraph">The FontAndParagraph providing the text layout to render.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded meshes sit relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into indexed meshes — visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle lists).</param>
    public unsafe void Update(out Spread<Mesh> meshes, out Spread<Matrix> transformations,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            this.meshes = Spread<Mesh>.Empty;
            this.transformations = Spread<Matrix>.Empty;
            lastLayout = null;
        }
        else
        {
            var hashCode = new HashCode();
            hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
            hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
            hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
            hashCode.Add(weldVertices);
            int hash = hashCode.ToHashCode();
            if (!ReferenceEquals(layout, lastLayout) || hash != lastHash)
            {
                GlyphMeshNodeHelper.Build(services.Game, layout.Pointer, extrudeAmount, extrudeOrigin,
                    flatteningTolerance, smoothingAngle, weldVertices, out this.meshes, out this.transformations);
                lastLayout = layout;
                lastHash = hash;
            }
        }
        meshes = this.meshes;
        transformations = this.transformations;
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Renders a FontAndParagraph text layout as an extruded 3D mesh.</summary>
[ProcessNode(Name = "Text3dMesh (Advanced)")]
public class Text3dMeshAdvanced : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dAdvancedModel model = new();
    private Mesh? mesh;
    private TextLayoutHandle? lastLayout;
    private int lastHash;

    public Text3dMeshAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    /// <param name="output">The generated text mesh.</param>
    /// <param name="fontAndParagraph">The FontAndParagraph providing the text layout to render.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh — visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    public void Update(out Mesh? output,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            mesh = null;
            lastLayout = null;
        }
        else
        {
            var hashCode = new HashCode();
            hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
            hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
            hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
            hashCode.Add(weldVertices);
            int hash = hashCode.ToHashCode();
            if (!ReferenceEquals(layout, lastLayout) || hash != lastHash)
            {
                model.TextLayout = layout;
                model.ExtrudeAmount = extrudeAmount;
                model.ExtrudeOrigin = extrudeOrigin;
                model.FlatteningTolerance = flatteningTolerance;
                model.SmoothingAngle = smoothingAngle;
                model.WeldVertices = weldVertices;
                var strideModel = Text3dModelBuilder.Build(model, services.Game);
                mesh = strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
                lastLayout = layout;
                lastHash = hash;
            }
        }
        output = mesh;
    }

    public void Dispose() => services.Dispose();
}
