/*
Source: https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/Extruder.cs
Ported from SharpDX to Silk.NET 2.22.0; the geometry pipeline is 1:1 with the original.

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

// Pipeline (1:1 with original): Simplify(Lines, tol 0.1) -> Outline (planarize/union) ->
// GetBounds (UV range; top/bottom deliberately swapped as original) -> Simplify into the
// extruding sink (side walls) + Tessellate (front/back caps). Where the SharpDX
// overloads used the implicit default flattening tolerance, 0.25f
// (D2D1_DEFAULT_FLATTENING_TOLERANCE) is passed explicitly.
//
// Intentional fix vs original (agreed bug policy): null geometry (empty text) returns
// the degenerate 3-vertex mesh instead of falling through into a NullReferenceException.

using System.Runtime.InteropServices;
using Silk.NET.Direct2D;
using Silk.NET.Maths;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using Stride.Graphics;
using VL.Stride.Text3d.Enums;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace VL.Stride.Text3d.Core;

public sealed unsafe class Extruder
{
    /// <summary>Default outline flattening tolerance (as the original implementation).</summary>
    public const float DefaultFlatteningTolerance = .1f;

    /// <summary>Default smoothing angle in cycles (vvvv standard unit; 1/6 = 60°);
    /// its cosine matches the original hard-coded 0.5 threshold.</summary>
    public const float DefaultSmoothingAngle = 1f / 6f;

    private const float D2DDefaultFlatteningTolerance = 0.25f;

    private readonly ID2D1Factory* factory;

    public Extruder(ID2D1Factory* factory)
    {
        this.factory = factory;
    }

    private ID2D1PathGeometry* FlattenGeometry(ID2D1Geometry* geometry, float tolerance)
    {
        ID2D1PathGeometry* path = null;
        ThrowOnFailure(factory->CreatePathGeometry(&path));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(path->Open(&sink));
        ThrowOnFailure(geometry->Simplify(GeometrySimplificationOption.Lines, null, tolerance,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        return path;
    }

    private ID2D1PathGeometry* OutlineGeometry(ID2D1Geometry* geometry)
    {
        ID2D1PathGeometry* path = null;
        ThrowOnFailure(factory->CreatePathGeometry(&path));

        ID2D1GeometrySink* sink = null;
        ThrowOnFailure(path->Open(&sink));
        ThrowOnFailure(geometry->Outline(null, D2DDefaultFlatteningTolerance,
            (ID2D1SimplifiedGeometrySink*)sink));
        ThrowOnFailure(sink->Close());
        sink->Release();

        return path;
    }

    public void GetVertices(ID2D1Geometry* geometry, List<VertexPositionNormalTexture> vertices,
        float height = 24.0f,
        ExtrudeOrigin origin = ExtrudeOrigin.Center,
        float flatteningTolerance = DefaultFlatteningTolerance,
        float smoothingAngle = DefaultSmoothingAngle)
    {
        vertices.Clear();

        if (geometry == null)
        {
            // Empty mesh (intentional fix: early return instead of NRE, see file header)
            var zero = new VertexPositionNormalTexture();
            vertices.Add(zero);
            vertices.Add(zero);
            vertices.Add(zero);
            return;
        }

        // D2D requires a positive tolerance
        flatteningTolerance = Math.Max(flatteningTolerance, 1e-4f);

        // Smoothing angle is in cycles (vvvv standard unit, 0..0.5 covers 0°..180°).
        // The default 1/6 must reproduce the original hard-coded 0.5 threshold exactly,
        // which float(1/6)*2π*cos would miss by 1 ulp — hence the special case.
        float smoothingThreshold = smoothingAngle == DefaultSmoothingAngle
            ? 0.5f
            : (float)Math.Cos(Math.Clamp(smoothingAngle, 0f, 0.5f) * 2.0 * Math.PI);

        var (zFront, zBack) = origin switch
        {
            ExtrudeOrigin.Front => (0f, -height),
            ExtrudeOrigin.Back => (height, 0f),
            _ => (height / 2, -height / 2),
        };

        ID2D1PathGeometry* flattened = FlattenGeometry(geometry, flatteningTolerance);
        ID2D1PathGeometry* outlined = OutlineGeometry((ID2D1Geometry*)flattened);

        Box2D<float> bounds = default;
        ThrowOnFailure(((ID2D1Geometry*)outlined)->GetBounds(null, &bounds));

        // Top and bottom switched for uv calculation (as original)
        var min = new Vector2(bounds.Min.X, bounds.Max.Y);
        var max = new Vector2(bounds.Max.X, bounds.Min.Y);

        var sink = new ExtrudingSink(vertices, zFront, zBack, smoothingThreshold, min, max);
        var simplifiedPtr = (ID2D1SimplifiedGeometrySink*)GetComPointer(sink, typeof(ID2D1SimplifiedGeometrySinkCallback).GUID);
        var tessellationPtr = (ID2D1TessellationSink*)GetComPointer(sink, typeof(ID2D1TessellationSinkCallback).GUID);
        try
        {
            ThrowOnFailure(((ID2D1Geometry*)outlined)->Simplify(GeometrySimplificationOption.Lines, null,
                D2DDefaultFlatteningTolerance, simplifiedPtr));
            ThrowOnFailure(((ID2D1Geometry*)outlined)->Tessellate(null,
                D2DDefaultFlatteningTolerance, tessellationPtr));
        }
        finally
        {
            Marshal.Release((nint)simplifiedPtr);
            Marshal.Release((nint)tessellationPtr);
            outlined->Release();
            flattened->Release();
        }
    }
}
