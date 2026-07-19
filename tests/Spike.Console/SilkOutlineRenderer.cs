/*
Port of OutlineRenderer.cs (SharpDX) to Silk.NET 2.22.0.
Original source: https://github.com/mrvux/dx11-vvvv (BSD 3-Clause, Julien Vulliet) —
full license header retained in src/ during Phase 2; this spike file graduates there.

Behavior parity notes:
 - Pixel snapping disabled, identity transform, 1.0 pixels-per-dip (as original).
 - DrawInlineObject returns E_NOTIMPL (SharpDX TextRendererBase default, never overridden).
 - clientDrawingEffect was read in the original but the resulting color was never used —
   ignored here (PORT-NOTE).
 - Glyph-run geometry: outline -> translate(baseline) x scale(1,-1) -> union-combined.
   SharpDX row-vector math Translation(bx,by) * Scaling(1,-1) yields
   [1 0; 0 -1; bx -by], constructed directly below.
*/

using Silk.NET.Direct2D;
using Silk.NET.Maths;
using Spike.Interop;
using static Spike.Interop.ComCallbackHelper;
using DWMatrix = Silk.NET.DirectWrite.Matrix;
using GlyphRun = Silk.NET.DirectWrite.GlyphRun;

namespace Spike;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
internal sealed unsafe partial class SilkOutlineRenderer : IDWriteTextRendererCallback
{
    private readonly ID2D1Factory* factory;
    private ID2D1Geometry* geometry;

    public SilkOutlineRenderer(ID2D1Factory* factory)
    {
        this.factory = factory;
    }

    /// <summary>Accumulated geometry (may be null for empty text). Caller takes ownership.</summary>
    public ID2D1Geometry* GetGeometry() => geometry;

    public int DrawGlyphRun(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        int measuringMode, GlyphRun* glyphRun, void* glyphRunDescription, void* clientDrawingEffect)
    {
        if (glyphRun->GlyphCount == 0)
            return S_OK;

        ID2D1PathGeometry* pg = null;
        ThrowOnFailure(factory->CreatePathGeometry(&pg));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(pg->Open(&sink));

        ThrowOnFailure(glyphRun->FontFace->GetGlyphRunOutline(
            glyphRun->FontEmSize,
            glyphRun->GlyphIndices,
            glyphRun->GlyphAdvances,
            glyphRun->GlyphOffsets,
            glyphRun->GlyphCount,
            glyphRun->IsSideways,
            (glyphRun->BidiLevel % 2) == 1,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        AddTransformedGeometry(pg, baselineOriginX, baselineOriginY);
        return S_OK;
    }

    public int DrawUnderline(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Silk.NET.DirectWrite.Underline* underline, void* clientDrawingEffect)
    {
        AddDecorationRect(baselineOriginX, baselineOriginY,
            underline->Offset, underline->Width, underline->Thickness);
        return S_OK;
    }

    public int DrawStrikethrough(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Silk.NET.DirectWrite.Strikethrough* strikethrough, void* clientDrawingEffect)
    {
        AddDecorationRect(baselineOriginX, baselineOriginY,
            strikethrough->Offset, strikethrough->Width, strikethrough->Thickness);
        return S_OK;
    }

    public int DrawInlineObject(void* clientDrawingContext, float originX, float originY,
        void* inlineObject, int isSideways, int isRightToLeft, void* clientDrawingEffect)
    {
        return E_NOTIMPL;
    }

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

    private void AddDecorationRect(float baselineOriginX, float baselineOriginY,
        float offset, float width, float thickness)
    {
        ID2D1PathGeometry* pg = null;
        ThrowOnFailure(factory->CreatePathGeometry(&pg));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(pg->Open(&sink));

        var topLeft = new Vector2D<float>(0.0f, offset);
        sink->BeginFigure(topLeft, FigureBegin.Filled);
        topLeft.X += width;
        sink->AddLine(topLeft);
        topLeft.Y += thickness;
        sink->AddLine(topLeft);
        topLeft.X -= width;
        sink->AddLine(topLeft);
        sink->EndFigure(FigureEnd.Closed);
        ThrowOnFailure(sink->Close());
        sink->Release();

        AddTransformedGeometry(pg, baselineOriginX, baselineOriginY);
    }

    /// <summary>Wraps pg in translate(baseline) x scale(1,-1), releases pg, accumulates.</summary>
    private void AddTransformedGeometry(ID2D1PathGeometry* pg, float baselineOriginX, float baselineOriginY)
    {
        var mat = new Matrix3X2<float>(1.0f, 0.0f, 0.0f, -1.0f, baselineOriginX, -baselineOriginY);
        ID2D1TransformedGeometry* tg = null;
        ThrowOnFailure(factory->CreateTransformedGeometry((ID2D1Geometry*)pg, &mat, &tg));
        pg->Release();
        AddGeometry((ID2D1Geometry*)tg);
    }

    private void AddGeometry(ID2D1Geometry* geom)
    {
        if (geometry == null)
        {
            geometry = geom;
            return;
        }

        ID2D1PathGeometry* pg = null;
        ThrowOnFailure(factory->CreatePathGeometry(&pg));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(pg->Open(&sink));
        // 0.25f = D2D1_DEFAULT_FLATTENING_TOLERANCE (SharpDX Combine overload default)
        ThrowOnFailure(geometry->CombineWithGeometry(geom, CombineMode.Union, null, 0.25f,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        geometry->Release();
        geom->Release();
        geometry = (ID2D1Geometry*)pg;
    }
}
