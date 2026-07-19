/*
Source: https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/OutlineRenderer.cs
Ported from SharpDX to Silk.NET 2.22.0 (managed IDWriteTextRenderer via .NET 8
source-generated COM); geometry behavior is 1:1 with the original.

License:

dx11-vvvv

BSD 3-Clause License

Copyright (c) 2016, Julien Vulliet (mrvux)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

// Behavior parity notes (verified bit-exact against the SharpDX build in Phase 1):
//  - Pixel snapping disabled, identity transform, 1.0 pixels-per-dip (as original).
//  - DrawInlineObject returns E_NOTIMPL (SharpDX TextRendererBase default, which the
//    original never overrode).
//  - PORT-NOTE: the original read clientDrawingEffect (SolidColorBrush color) but never
//    used the result — dead code, dropped here.
//  - Glyph-run geometry: outline -> translate(baseline) x scale(1,-1) -> union-combined.
//    SharpDX row-vector math Translation(bx,by) * Scaling(1,-1) yields the matrix
//    [1 0; 0 -1; bx -by], constructed directly below.

using Silk.NET.Direct2D;
using Silk.NET.Maths;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using DWMatrix = Silk.NET.DirectWrite.Matrix;
using GlyphRun = Silk.NET.DirectWrite.GlyphRun;

namespace VL.Stride.Text3d.Core;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public sealed unsafe partial class OutlineRenderer : IDWriteTextRendererCallback
{
    private readonly ID2D1Factory* factory;
    private ID2D1Geometry* geometry;

    public OutlineRenderer(ID2D1Factory* factory)
    {
        this.factory = factory;
    }

    /// <summary>Accumulated geometry (null for empty text). The caller takes ownership.</summary>
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
        // 0.25f = D2D1_DEFAULT_FLATTENING_TOLERANCE (the SharpDX Combine overload default)
        ThrowOnFailure(geometry->CombineWithGeometry(geom, CombineMode.Union, null, 0.25f,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        geometry->Release();
        geom->Release();
        geometry = (ID2D1Geometry*)pg;
    }
}
