/*
Port of Extruder.cs (SharpDX) to Silk.NET 2.22.0.
Original source: https://github.com/mrvux/dx11-vvvv (BSD 3-Clause, Julien Vulliet) —
full license header retained in src/ during Phase 2.

Pipeline (1:1 with original): Simplify(Lines, tol 0.1) -> Outline (planarize/union) ->
GetBounds (UV range; top/bottom deliberately swapped as original) -> Simplify into the
extruding sink (side walls) + Tessellate (front/back caps). Where SharpDX overloads used
the implicit default flattening tolerance, 0.25f (D2D1_DEFAULT_FLATTENING_TOLERANCE) is
passed explicitly.

Intentional fix vs original (agreed bug policy): null geometry (empty text) now returns
the degenerate 3-vertex mesh instead of falling through into a NullReferenceException.
*/

using System.Runtime.InteropServices;
using Silk.NET.Direct2D;
using Silk.NET.Maths;
using Spike.Interop;
using static Spike.Interop.ComCallbackHelper;
using Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Spike;

internal sealed unsafe class SilkExtruder
{
    private const float FlatteningTolerance = .1f;
    private const float D2DDefaultFlatteningTolerance = 0.25f;

    private readonly ID2D1Factory* factory;

    public SilkExtruder(ID2D1Factory* factory)
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

    public void GetVertices(ID2D1Geometry* geometry, List<VertexPositionNormalTexture> vertices, float height = 24.0f)
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

        ID2D1PathGeometry* flattened = FlattenGeometry(geometry, FlatteningTolerance);
        ID2D1PathGeometry* outlined = OutlineGeometry((ID2D1Geometry*)flattened);

        Box2D<float> bounds = default;
        ThrowOnFailure(((ID2D1Geometry*)outlined)->GetBounds(null, &bounds));

        // Top and bottom switched for uv calculation (as original)
        var min = new Vector2(bounds.Min.X, bounds.Max.Y);
        var max = new Vector2(bounds.Max.X, bounds.Min.Y);

        var sink = new SilkExtrudingSink(vertices, height, min, max);
        var simplifiedPtr = (ID2D1SimplifiedGeometrySink*)GetComPointer(sink, GuidOf<ID2D1SimplifiedGeometrySinkCallback>());
        var tessellationPtr = (ID2D1TessellationSink*)GetComPointer(sink, GuidOf<ID2D1TessellationSinkCallback>());
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

    private static Guid GuidOf<T>() => typeof(T).GUID;
}
