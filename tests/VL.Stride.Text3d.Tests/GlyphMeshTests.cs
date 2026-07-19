// Tests for the per-glyph extraction behind the Text3dMeshes nodes. Placement (incl.
// the RTL pen math) is validated empirically: the union of all per-glyph bounding boxes
// placed at their transforms must match the whole-text mesh's bounding box.

using NUnit.Framework;
using Stride.Graphics;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using SilkParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using SilkTextAlignment = Silk.NET.DirectWrite.TextAlignment;
using SilkWordWrapping = Silk.NET.DirectWrite.WordWrapping;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class GlyphMeshTests
{
    private static unsafe List<(VertexPositionNormalTexture[] Vertices, Vector2 Position)> ExtractGlyphs(
        string text, string font, float fontSize, float extrude)
    {
        // Same layout settings as the simple Text3d nodes
        var fmt = Native.CreateTextFormat(font, fontSize);
        var layout = Native.CreateTextLayout(text, fmt, 0.0f, 32.0f);
        fmt->Release();
        layout->SetWordWrapping(SilkWordWrapping.NoWrap);
        layout->SetTextAlignment(SilkTextAlignment.Leading);
        layout->SetParagraphAlignment(SilkParagraphAlignment.Near);
        try
        {
            return GlyphMeshBuilder.ExtractGlyphVertices(layout, extrude);
        }
        finally
        {
            layout->Release();
        }
    }

    private static (Vector2 Min, Vector2 Max) BoundingBox(IEnumerable<VertexPositionNormalTexture> vertices, Vector2 offset = default)
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var v in vertices)
        {
            min.X = Math.Min(min.X, v.Position.X + offset.X);
            min.Y = Math.Min(min.Y, v.Position.Y + offset.Y);
            max.X = Math.Max(max.X, v.Position.X + offset.X);
            max.Y = Math.Max(max.Y, v.Position.Y + offset.Y);
        }
        return (min, max);
    }

    private static void AssertPlacementMatchesWholeMesh(string text, float epsilon)
    {
        var whole = TestData.BuildSimple(text, "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        var glyphs = ExtractGlyphs(text, "Arial", 32, 1f);
        Assert.That(glyphs, Is.Not.Empty);

        var wholeBox = BoundingBox(whole);
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var (vertices, position) in glyphs)
        {
            var box = BoundingBox(vertices, position);
            min = Vector2.Min(min, box.Min);
            max = Vector2.Max(max, box.Max);
        }

        Assert.That(min.X, Is.EqualTo(wholeBox.Min.X).Within(epsilon), "min X");
        Assert.That(min.Y, Is.EqualTo(wholeBox.Min.Y).Within(epsilon), "min Y");
        Assert.That(max.X, Is.EqualTo(wholeBox.Max.X).Within(epsilon), "max X");
        Assert.That(max.Y, Is.EqualTo(wholeBox.Max.Y).Within(epsilon), "max Y");
    }

    [Test]
    public void SingleGlyphMatchesWholeMesh()
    {
        AssertPlacementMatchesWholeMesh("I", epsilon: 0.05f);
    }

    [Test]
    public void LtrPlacementMatchesWholeMesh()
    {
        AssertPlacementMatchesWholeMesh("vvvv", epsilon: 0.1f);
    }

    [Test]
    public void RtlPlacementMatchesWholeMesh()
    {
        AssertPlacementMatchesWholeMesh(TestData.RtlText, epsilon: 0.1f);
    }

    [Test]
    public void SpacesProduceNoMesh()
    {
        var glyphs = ExtractGlyphs("v v", "Arial", 32, 1f);
        Assert.That(glyphs.Count, Is.EqualTo(2));
    }

    [Test]
    public void GlyphsAdvanceAlongX()
    {
        var glyphs = ExtractGlyphs("vvvv", "Arial", 32, 1f);
        Assert.That(glyphs.Count, Is.EqualTo(4));
        for (int i = 1; i < glyphs.Count; i++)
            Assert.That(glyphs[i].Position.X, Is.GreaterThan(glyphs[i - 1].Position.X));
    }

    [Test]
    public void SecondLineIsLower()
    {
        var glyphs = ExtractGlyphs("v\nv", "Arial", 32, 1f);
        Assert.That(glyphs.Count, Is.EqualTo(2));
        Assert.That(glyphs[1].Position.Y, Is.LessThan(glyphs[0].Position.Y));
    }
}
