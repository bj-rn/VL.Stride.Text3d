// Procedural-model classes generating the extruded text mesh, ported from the SharpDX
// Text3dNodes.cs (the [Obsolete] "Upddate" Mesh builder and its helpers were dropped).
// The former public SharpDX TextLayout property of Text3dAdvanced is replaced by the
// managed TextLayoutHandle (produced by FontAndParagraph).

using System.Runtime.InteropServices;
using Silk.NET.Direct2D;
using Stride.Graphics;
using Stride.Rendering.ProceduralModels;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;
using IDWriteTextRenderer = Silk.NET.DirectWrite.IDWriteTextRenderer;
using SilkParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using SilkTextAlignment = Silk.NET.DirectWrite.TextAlignment;
using SilkWordWrapping = Silk.NET.DirectWrite.WordWrapping;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;

namespace VL.Stride.Text3d.Core;

public abstract unsafe class Text3dBase : PrimitiveProceduralModelBase
{
    protected readonly List<VertexPositionNormalTexture> vertexList = new(1024);

    public float ExtrudeAmount { get; set; } = 1.0f;

    /// <summary>Where the extruded mesh sits relative to Z = 0.</summary>
    public ExtrudeOrigin ExtrudeOrigin { get; set; } = ExtrudeOrigin.Center;

    /// <summary>Maximum outline flattening deviation; smaller values yield finer curves (and more vertices).</summary>
    public float FlatteningTolerance { get; set; } = Extruder.DefaultFlatteningTolerance;

    /// <summary>Side-wall edges sharper than this angle (degrees) stay hard; flatter ones are smoothed.</summary>
    public float SmoothingAngle { get; set; } = Extruder.DefaultSmoothingAngle;

    protected static int[] GetDefaultIndicesArray(int size)
    {
        var result = new int[size];
        for (int i = 0; i < size; ++i)
            result[i] = i;
        return result;
    }

    /// <summary>Draws the layout into an OutlineRenderer and extrudes the result into vertexList.</summary>
    protected void ExtractVertices(IDWriteTextLayout* textLayout)
    {
        var renderer = new OutlineRenderer(Native.D2DFactory);
        var rendererPtr = (IDWriteTextRenderer*)GetComPointer(renderer, typeof(IDWriteTextRendererCallback).GUID);
        try
        {
            ThrowOnFailure(textLayout->Draw(null, rendererPtr, 0.0f, 0.0f));

            var geometry = renderer.GetGeometry();
            var extruder = new Extruder(Native.D2DFactory);
            extruder.GetVertices(geometry, vertexList, ExtrudeAmount, ExtrudeOrigin, FlatteningTolerance, SmoothingAngle);
            if (geometry != null)
                geometry->Release();
        }
        finally
        {
            Marshal.Release((nint)rendererPtr);
        }
    }

    protected GeometricMeshData<VertexPositionNormalTexture> BuildMeshData(string name)
    {
        var vertices = vertexList.ToArray();
        return new GeometricMeshData<VertexPositionNormalTexture>(
            vertices, GetDefaultIndicesArray(vertices.Length), isLeftHanded: false)
        { Name = name };
    }
}

/// <summary>Generates an extruded 3D text mesh from simple text/font/alignment settings.</summary>
public sealed unsafe class Text3dModel : Text3dBase
{
    public string Text { get; set; } = "hello world";

    public string Font { get; set; } = "Arial";

    public int FontSize { get; set; } = 32;

    public TextAlignment HorizontalAlignment { get; set; } = TextAlignment.Leading;

    public ParagraphAlignment VerticalAlignment { get; set; } = ParagraphAlignment.Near;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var fmt = Native.CreateTextFormat(Font, FontSize);
        var layout = Native.CreateTextLayout(Text, fmt, 0.0f, 32.0f);
        try
        {
            // Same settings and order as the SharpDX object initializer
            ThrowOnFailure(layout->SetWordWrapping(SilkWordWrapping.NoWrap));
            ThrowOnFailure(layout->SetTextAlignment((SilkTextAlignment)HorizontalAlignment));
            ThrowOnFailure(layout->SetParagraphAlignment((SilkParagraphAlignment)VerticalAlignment));

            ExtractVertices(layout);
        }
        finally
        {
            layout->Release();
            fmt->Release();
        }

        return BuildMeshData("Text3d");
    }
}

/// <summary>Generates an extruded 3D text mesh from an externally built text layout.</summary>
public sealed unsafe class Text3dAdvancedModel : Text3dBase
{
    public TextLayoutHandle? TextLayout { get; set; }

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        // PORT-NOTE: the original returned null when no layout was set; kept as-is
        // (callers guard against it, as the old patched glue did).
        if (TextLayout == null)
            return null!;

        ExtractVertices(TextLayout.Pointer);
        return BuildMeshData("Text3dAdvanced");
    }
}
