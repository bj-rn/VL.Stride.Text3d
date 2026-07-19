// Per-glyph geometry extraction for the Text3dMeshes nodes: instead of union-combining
// all glyph runs into one geometry (OutlineRenderer), each glyph becomes its own
// geometry in LOCAL coordinates (origin on the baseline at the glyph's pen position,
// Y up) plus a world offset — so every glyph mesh has a natural pivot for typography
// animation.
//
// Notes/limitations (documented on the nodes):
//  - Underline/strikethrough decorations are not part of the per-glyph output.
//  - Glyphs, not characters: ligatures merge characters, spaces produce no mesh.
//  - Placement assumes horizontal baselines (vertical reading directions unsupported).

using System.Runtime.InteropServices;
using Silk.NET.Direct2D;
using Silk.NET.Maths;
using Stride.Graphics;
using Stride.Rendering.ProceduralModels;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using DWMatrix = Silk.NET.DirectWrite.Matrix;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
using GlyphRun = Silk.NET.DirectWrite.GlyphRun;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;
using IDWriteTextRenderer = Silk.NET.DirectWrite.IDWriteTextRenderer;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace VL.Stride.Text3d.Core;

/// <summary>Collects one outline geometry per glyph (local coordinates) plus its baseline pen position.</summary>
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public sealed unsafe partial class GlyphOutlineCollector : IDWriteTextRendererCallback
{
    public struct GlyphOutline
    {
        public ID2D1Geometry* Geometry;
        public float X;
        public float Y;
    }

    private readonly ID2D1Factory* factory;
    private readonly List<GlyphOutline> glyphs = new();

    public GlyphOutlineCollector(ID2D1Factory* factory)
    {
        this.factory = factory;
    }

    public IReadOnlyList<GlyphOutline> Glyphs => glyphs;

    public int DrawGlyphRun(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        int measuringMode, GlyphRun* glyphRun, void* glyphRunDescription, void* clientDrawingEffect)
    {
        if (glyphRun->GlyphCount == 0)
            return S_OK;

        bool isRtl = (glyphRun->BidiLevel % 2) == 1;

        if (glyphRun->GlyphAdvances == null)
        {
            // Without advances the pen positions are unknown; degrade gracefully to one
            // entry for the whole run at the run origin.
            AddGlyph(glyphRun, 0, glyphRun->GlyphCount, isRtl, baselineOriginX, baselineOriginY);
            return S_OK;
        }

        float pen = 0f;
        for (uint i = 0; i < glyphRun->GlyphCount; i++)
        {
            float advance = glyphRun->GlyphAdvances[i];
            // LTR runs advance rightwards from the run origin. RTL runs are drawn
            // leftwards from it, and a single RTL glyph's outline already extends into
            // negative X from its pen position (validated empirically against the
            // whole-text mesh by GlyphMeshTests).
            float originX = isRtl
                ? baselineOriginX - pen
                : baselineOriginX + pen;
            AddGlyph(glyphRun, i, 1, isRtl, originX, baselineOriginY);
            pen += advance;
        }
        return S_OK;
    }

    private void AddGlyph(GlyphRun* glyphRun, uint index, uint count, bool isRtl,
        float originX, float baselineOriginY)
    {
        ID2D1PathGeometry* pg = null;
        ThrowOnFailure(factory->CreatePathGeometry(&pg));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(pg->Open(&sink));
        ThrowOnFailure(glyphRun->FontFace->GetGlyphRunOutline(
            glyphRun->FontEmSize,
            glyphRun->GlyphIndices + index,
            glyphRun->GlyphAdvances != null ? glyphRun->GlyphAdvances + index : null,
            glyphRun->GlyphOffsets != null ? glyphRun->GlyphOffsets + index : null,
            count,
            glyphRun->IsSideways,
            isRtl,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        // Local geometry is only Y-flipped; placement is delivered as the offset.
        var mat = new Matrix3X2<float>(1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f);
        ID2D1TransformedGeometry* tg = null;
        ThrowOnFailure(factory->CreateTransformedGeometry((ID2D1Geometry*)pg, &mat, &tg));
        pg->Release();

        glyphs.Add(new GlyphOutline { Geometry = (ID2D1Geometry*)tg, X = originX, Y = -baselineOriginY });
    }

    public void ReleaseGeometries()
    {
        foreach (var glyph in glyphs)
        {
            if (glyph.Geometry != null)
                glyph.Geometry->Release();
        }
        glyphs.Clear();
    }

    // Decorations are not part of the per-glyph output.
    public int DrawUnderline(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Silk.NET.DirectWrite.Underline* underline, void* clientDrawingEffect) => S_OK;

    public int DrawStrikethrough(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Silk.NET.DirectWrite.Strikethrough* strikethrough, void* clientDrawingEffect) => S_OK;

    public int DrawInlineObject(void* clientDrawingContext, float originX, float originY,
        void* inlineObject, int isSideways, int isRightToLeft, void* clientDrawingEffect) => E_NOTIMPL;

    public int IsPixelSnappingDisabled(void* clientDrawingContext, int* isDisabled)
    {
        *isDisabled = 1;
        return S_OK;
    }

    public int GetCurrentTransform(void* clientDrawingContext, DWMatrix* transform)
    {
        *transform = new DWMatrix { M11 = 1.0f, M12 = 0.0f, M21 = 0.0f, M22 = 1.0f, Dx = 0.0f, Dy = 0.0f };
        return S_OK;
    }

    public int GetPixelsPerDip(void* clientDrawingContext, float* pixelsPerDip)
    {
        *pixelsPerDip = 1.0f;
        return S_OK;
    }
}

public static unsafe class GlyphMeshBuilder
{
    /// <summary>
    /// Extracts one extruded vertex array per visible glyph (local coordinates, pivot on
    /// the baseline at the glyph's pen position) plus the world-space baseline position.
    /// Glyphs without ink (spaces) are skipped.
    /// </summary>
    public static List<(VertexPositionNormalTexture[] Vertices, Vector2 Position)> ExtractGlyphVertices(
        IDWriteTextLayout* textLayout, float extrudeAmount,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Extruder.DefaultSmoothingAngle)
    {
        var result = new List<(VertexPositionNormalTexture[], Vector2)>();
        var collector = new GlyphOutlineCollector(Native.D2DFactory);
        var rendererPtr = (IDWriteTextRenderer*)GetComPointer(collector, typeof(IDWriteTextRendererCallback).GUID);
        try
        {
            ThrowOnFailure(textLayout->Draw(null, rendererPtr, 0.0f, 0.0f));

            var extruder = new Extruder(Native.D2DFactory);
            var vertices = new List<VertexPositionNormalTexture>(1024);
            foreach (var glyph in collector.Glyphs)
            {
                extruder.GetVertices(glyph.Geometry, vertices, extrudeAmount,
                    extrudeOrigin, flatteningTolerance, smoothingAngle);
                if (vertices.Count > 0)
                    result.Add((vertices.ToArray(), new Vector2(glyph.X, glyph.Y)));
            }
        }
        finally
        {
            collector.ReleaseGeometries();
            Marshal.Release((nint)rendererPtr);
        }
        return result;
    }
}

/// <summary>Procedural model over an already-computed vertex array (used to turn per-glyph vertices into Stride meshes).</summary>
public sealed class PrebuiltMeshModel : PrimitiveProceduralModelBase
{
    public VertexPositionNormalTexture[] Vertices { get; set; } = Array.Empty<VertexPositionNormalTexture>();

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var indices = new int[Vertices.Length];
        for (int i = 0; i < indices.Length; ++i)
            indices[i] = i;
        return new GeometricMeshData<VertexPositionNormalTexture>(Vertices, indices, isLeftHanded: false)
        { Name = "Text3dGlyph" };
    }
}
