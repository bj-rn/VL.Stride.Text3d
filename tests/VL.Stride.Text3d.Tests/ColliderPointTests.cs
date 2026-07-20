// Tests for the collider point extraction behind the async mesh nodes' Compute Points
// option: distinct vertex positions (whole mesh) and per-glyph groups translated to
// text-local space, both built with caller-owned reusable scratch collections.

using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Lib.Collections;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using GlyphList = System.Collections.Generic.List<(Stride.Graphics.VertexPositionNormalTexture[] Vertices, Stride.Core.Mathematics.Vector2 Position)>;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using SilkParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using SilkTextAlignment = Silk.NET.DirectWrite.TextAlignment;
using SilkWordWrapping = Silk.NET.DirectWrite.WordWrapping;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class ColliderPointTests
{
    private static unsafe GlyphList ExtractGlyphs(string text, float extrude = 1f)
    {
        // Same layout settings as the simple Text3d nodes
        var fmt = Native.CreateTextFormat("Arial", 32f);
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

    private static Spread<Vector3> Distinct(VertexPositionNormalTexture[] vertices)
        => ColliderPoints.DistinctPositions(vertices, new HashSet<Vector3>(), new SpreadBuilder<Vector3>());

    private static Spread<Spread<Vector3>> DistinctPerGlyph(GlyphList glyphs)
        => ColliderPoints.DistinctPositionsPerGlyph(glyphs, new HashSet<Vector3>(),
            new SpreadBuilder<Vector3>(), new SpreadBuilder<Spread<Vector3>>());

    [Test]
    public void PointsEqualDistinctVertexPositions()
    {
        var vertices = TestData.BuildSimple("abc", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        var points = Distinct(vertices);

        var expected = new HashSet<Vector3>();
        foreach (var v in vertices)
            expected.Add(v.Position);

        Assert.That(points, Is.EquivalentTo(expected));
    }

    [Test]
    public void DedupReducesCounts()
    {
        // A plain triangle list repeats shared corner positions many times.
        var vertices = TestData.BuildSimple("abc", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        var points = Distinct(vertices);

        Assert.That(points.Count, Is.GreaterThan(0));
        Assert.That(points.Count, Is.LessThan(vertices.Length));
    }

    [Test]
    public void GroupCountMatchesGlyphCount()
    {
        var glyphs = ExtractGlyphs("abc");
        var groups = DistinctPerGlyph(glyphs);

        Assert.That(glyphs.Count, Is.EqualTo(3));
        Assert.That(groups.Count, Is.EqualTo(glyphs.Count));
    }

    [Test]
    public void SpacesYieldNoGroup()
    {
        // Spaces produce no glyph mesh, so they contribute no point group either.
        var glyphs = ExtractGlyphs("a b");
        var groups = DistinctPerGlyph(glyphs);

        Assert.That(glyphs.Count, Is.EqualTo(2));
        Assert.That(groups.Count, Is.EqualTo(2));
    }

    [Test]
    public void GroupsAreGlyphLocalDistinctPositionsPlusTranslation()
    {
        var glyphs = ExtractGlyphs("abc");
        var groups = DistinctPerGlyph(glyphs);

        for (var i = 0; i < glyphs.Count; i++)
        {
            var (vertices, position) = glyphs[i];
            var translation = new Vector3(position.X, position.Y, 0f);
            var expected = new HashSet<Vector3>();
            foreach (var v in vertices)
                expected.Add(v.Position + translation);

            Assert.That(groups[i], Is.EquivalentTo(expected), $"glyph {i}");
        }
    }

    [Test]
    public void RtlAndMultilineTextsProduceGroupsPerGlyph()
    {
        foreach (var text in new[] { TestData.RtlText, "line1\nline2" })
        {
            var glyphs = ExtractGlyphs(text);
            var groups = DistinctPerGlyph(glyphs);

            Assert.That(glyphs.Count, Is.GreaterThan(0), text);
            Assert.That(groups.Count, Is.EqualTo(glyphs.Count), text);
            foreach (var group in groups)
                Assert.That(group.Count, Is.GreaterThan(3), text);
        }
    }

    [Test]
    public void EmptyInputsYieldEmptyOutputs()
    {
        Assert.That(Distinct(Array.Empty<VertexPositionNormalTexture>()), Is.Empty);
        Assert.That(DistinctPerGlyph(new GlyphList()), Is.Empty);
    }

    [Test]
    public void ScratchReuseProducesIdenticalResultsAcrossBakes()
    {
        // The nodes reuse one scratch set/builder across bakes (max one bake in flight);
        // ToSpread() must never alias the builder's buffer, so earlier results stay
        // intact after Clear() and refill.
        var seen = new HashSet<Vector3>();
        var pointBuilder = new SpreadBuilder<Vector3>();
        var groupBuilder = new SpreadBuilder<Spread<Vector3>>();

        var verticesA = TestData.BuildSimple("abc", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        var verticesB = TestData.BuildSimple("XYZW", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);

        var first = ColliderPoints.DistinctPositions(verticesA, seen, pointBuilder);
        var firstSnapshot = first.ToArray();
        var second = ColliderPoints.DistinctPositions(verticesB, seen, pointBuilder);

        Assert.That(first.ToArray(), Is.EqualTo(firstSnapshot), "first result mutated by scratch reuse");
        Assert.That(second, Is.EquivalentTo(Distinct(verticesB)));
        Assert.That(first, Is.EquivalentTo(Distinct(verticesA)));

        var glyphs = ExtractGlyphs("ab");
        var groupsFirst = ColliderPoints.DistinctPositionsPerGlyph(glyphs, seen, pointBuilder, groupBuilder);
        var groupsFirstCounts = new List<int>();
        foreach (var g in groupsFirst)
            groupsFirstCounts.Add(g.Count);
        var glyphs2 = ExtractGlyphs("cdef");
        var groupsSecond = ColliderPoints.DistinctPositionsPerGlyph(glyphs2, seen, pointBuilder, groupBuilder);

        Assert.That(groupsFirst.Count, Is.EqualTo(2));
        Assert.That(groupsSecond.Count, Is.EqualTo(4));
        for (var i = 0; i < groupsFirst.Count; i++)
            Assert.That(groupsFirst[i].Count, Is.EqualTo(groupsFirstCounts[i]), "group mutated by scratch reuse");
    }
}
