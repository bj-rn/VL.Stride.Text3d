// Headless tests of the C# node surface that doesn't need a graphics device:
// FontAndParagraph building/caching, text styles, and metrics readback.

using NUnit.Framework;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Nodes;
using FontWeightEnum = VL.Stride.Text3d.Enums.FontWeight;
using WordWrapping = VL.Stride.Text3d.Enums.WordWrapping;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class FontAndParagraphTests
{
    [Test]
    public void BuildsLayoutAndCachesUntilDirty()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("hello");
        Assert.That(fap.GetIsDirty(), Is.True);

        var layout1 = fap.GetTextLayout();
        Assert.That(layout1, Is.Not.Null);
        Assert.That(fap.GetIsDirty(), Is.False);

        // Unchanged settings -> same handle instance (reference identity = change signal)
        fap.SetText("hello");
        Assert.That(fap.GetTextLayout(), Is.SameAs(layout1));

        // Changed settings -> rebuilt handle, bumped version
        int versionBefore = fap.GetVersion();
        fap.SetText("world");
        Assert.That(fap.GetIsDirty(), Is.True);
        var layout2 = fap.GetTextLayout();
        Assert.That(layout2, Is.Not.SameAs(layout1));
        Assert.That(fap.GetVersion(), Is.GreaterThan(versionBefore));
    }

    [Test]
    public void AppliedStyleChangeTriggersRebuild()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("hello world");

        var weightNode = new TextStyles.FontWeight();
        var style = weightNode.Update(startPosition: 0, length: 5, fontWeight: FontWeightEnum.Bold);
        fap.ApplyStyles(new[] { style });

        var layout1 = fap.GetTextLayout();

        // Same style values -> no rebuild
        weightNode.Update(startPosition: 0, length: 5, fontWeight: FontWeightEnum.Bold);
        Assert.That(fap.GetTextLayout(), Is.SameAs(layout1));

        // Changed style value -> rebuild
        weightNode.Update(startPosition: 0, length: 5, fontWeight: FontWeightEnum.Black);
        Assert.That(fap.GetTextLayout(), Is.Not.SameAs(layout1));
    }

    [Test]
    public void AllStyleNodesApplyWithoutError()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("hello world styles");

        var styles = new ITextStyle[]
        {
            new TextStyles.FontFamily().Update(0, 5),
            new TextStyles.FontSize().Update(0, 5, 48f),
            new TextStyles.FontStretch().Update(2, 4),
            new TextStyles.FontStyle().Update(2, 4, Enums.FontStyle.Italic),
            new TextStyles.FontWeight().Update(6, 5, FontWeightEnum.Bold),
            new TextStyles.StrikeThrough().Update(6, 5),
            new TextStyles.Underline().Update(0, 11),
            new TextStyles.PairKerning().Update(0, 18),
            new TextStyles.CharacterSpacing().Update(0, 18, 2f, 1f, 0f),
            new TextStyles.Typography().Update(0, 18, Enums.FontFeatureTag.StandardLigatures, 1),
        };
        fap.ApplyStyles(styles);

        Assert.That(() => fap.GetTextLayout(), Throws.Nothing);
    }

    [Test]
    public void MetricsNodesReadValues()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("line one\nline two");
        fap.SetWordWrapping(WordWrapping.NoWrap);

        var layoutMetrics = new TextLayoutMetrics();
        layoutMetrics.Update(out _, out _, out float width, out float height,
            out _, out _, out _, out _, out int lineCount, out _, fap);
        Assert.That(width, Is.GreaterThan(0));
        Assert.That(height, Is.GreaterThan(0));
        Assert.That(lineCount, Is.EqualTo(2));

        var lineMetrics = new LineMetrics();
        lineMetrics.Update(out var baseline, out var lineHeight, out _,
            out var length, out _, out _, fap);
        Assert.That(baseline.Count, Is.EqualTo(2));
        Assert.That(lineHeight.Count, Is.EqualTo(2));
        Assert.That(length.Count, Is.EqualTo(2));
        Assert.That(baseline[0], Is.GreaterThan(0));
    }

    // Regression: Silk.NET does not bind IDWriteTextLayout2::GetMetrics(TextMetrics1*),
    // so the pin read 0 until the node started calling the vtable slot directly.
    // Per the DWRITE_TEXT_METRICS1 docs the field is "pertinent for vertical text":
    // for horizontal reading directions trailing whitespace extends the width and the
    // two heights coincide; for vertical reading directions lines flow as columns and
    // the trailing whitespace extends the height.
    [Test]
    public void HeightIncludingTrailingWhitespaceIsRead()
    {
        using var horizontal = new FontAndParagraph();
        horizontal.SetText("hello   \nworld   ");

        var layoutMetrics = new TextLayoutMetrics();
        layoutMetrics.Update(out _, out _, out float width, out float height,
            out float heightIncl, out float widthIncl, out _, out _, out _, out _, horizontal);

        Assert.That(heightIncl, Is.GreaterThan(0));
        Assert.That(heightIncl, Is.EqualTo(height).Within(0.001f));
        Assert.That(widthIncl, Is.GreaterThan(width));

        using var vertical = new FontAndParagraph();
        vertical.SetText("hello   \nworld   ");
        vertical.SetReadingDirection(Enums.ReadingDirection.TopToBottom);
        vertical.SetFlowDirection(Enums.FlowDirection.LeftToRight);

        layoutMetrics.Update(out _, out _, out float verticalWidth, out float verticalHeight,
            out float verticalHeightIncl, out float verticalWidthIncl, out _, out _, out _, out _, vertical);

        Assert.That(verticalHeightIncl, Is.GreaterThan(verticalHeight));
        // ...and the mirror image: vertically the trailing whitespace no longer
        // extends the width.
        Assert.That(verticalWidthIncl, Is.EqualTo(verticalWidth).Within(0.001f));
    }

    // Regression: DirectWrite rejects a reading/flow direction pair that is parallel,
    // but only lazily on Draw/GetMetrics (DWRITE_E_FLOWDIRECTIONCONFLICTS), so every
    // consumer threw when one of the two pins was changed on its own. The layout now
    // derives a perpendicular fallback flow from the authoritative reading direction.
    [Test]
    public void ParallelReadingAndFlowDirectionsAreResolved()
    {
        var layoutMetrics = new TextLayoutMetrics();

        foreach (var reading in new[] { Enums.ReadingDirection.TopToBottom, Enums.ReadingDirection.BottomToTop })
        {
            using var fap = new FontAndParagraph();
            fap.SetText("hello world");
            fap.SetReadingDirection(reading); // flow stays at its now-parallel default TopToBottom

            Assert.That(() => layoutMetrics.Update(out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, fap),
                Throws.Nothing, reading.ToString());
            layoutMetrics.Update(out _, out _, out float width, out float height,
                out _, out _, out _, out _, out _, out _, fap);
            // glyphs stack vertically, so the layout really is vertical
            Assert.That(height, Is.GreaterThan(width), reading.ToString());
        }

        foreach (var flow in new[] { Enums.FlowDirection.LeftToRight, Enums.FlowDirection.RightToLeft })
        {
            using var fap = new FontAndParagraph();
            fap.SetText("hello world");
            fap.SetFlowDirection(flow); // reading stays at its now-parallel default LeftToRight

            Assert.That(() => layoutMetrics.Update(out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, fap),
                Throws.Nothing, flow.ToString());
        }
    }

    // Regression: DirectWrite draws vertical layouts only through IDWriteTextRenderer1;
    // with the plain IDWriteTextRenderer the outline renderers previously failed with
    // DWRITE_E_TEXTRENDERERINCOMPATIBLE (0x8898500A).
    [Test]
    public void VerticalLayoutsExtractMeshes()
    {
        foreach (var reading in new[] { Enums.ReadingDirection.TopToBottom, Enums.ReadingDirection.BottomToTop })
        {
            using var fap = new FontAndParagraph();
            fap.SetText("hello world");
            fap.SetReadingDirection(reading);

            var model = new Text3dAdvancedModel { TextLayout = fap.GetTextLayout(), ExtrudeAmount = 1f };
            var vertices = TestData.CreateMeshVertices(model);

            Assert.That(vertices, Is.Not.Empty, reading.ToString());
            // glyphs stack along Y, so the mesh must be taller than wide
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in vertices)
            {
                minX = Math.Min(minX, v.Position.X);
                maxX = Math.Max(maxX, v.Position.X);
                minY = Math.Min(minY, v.Position.Y);
                maxY = Math.Max(maxY, v.Position.Y);
            }
            Assert.That(maxY - minY, Is.GreaterThan(maxX - minX), reading.ToString());
        }
    }

    [Test]
    public void VerticalLayoutWithDecorationsExtracts()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("hello world");
        fap.SetReadingDirection(Enums.ReadingDirection.TopToBottom);
        fap.ApplyStyles(new ITextStyle[]
        {
            new TextStyles.Underline().Update(0, 5),
            new TextStyles.StrikeThrough().Update(6, 5),
        });

        var model = new Text3dAdvancedModel { TextLayout = fap.GetTextLayout(), ExtrudeAmount = 1f };
        Assert.That(TestData.CreateMeshVertices(model), Is.Not.Empty);
    }

    [Test]
    public unsafe void VerticalLayoutsExtractGlyphMeshes()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("abc");
        fap.SetReadingDirection(Enums.ReadingDirection.TopToBottom);

        var glyphs = GlyphMeshBuilder.ExtractGlyphVertices(fap.GetTextLayout()!.Pointer, extrudeAmount: 1f);

        Assert.That(glyphs.Count, Is.EqualTo(3));
        // pen positions progress along the vertical baseline, not the horizontal one
        var xs = glyphs.Select(g => g.Position.X).ToArray();
        var ys = glyphs.Select(g => g.Position.Y).ToArray();
        Assert.That(ys.Max() - ys.Min(), Is.GreaterThan(xs.Max() - xs.Min()));
    }

    [Test]
    public void TrimmingWithEllipsisSignBuilds()
    {
        using var fap = new FontAndParagraph();
        fap.SetText("some quite long text that will be trimmed");
        fap.SetMaxWidth(50f);
        fap.SetWordWrapping(WordWrapping.NoWrap);
        fap.SetTrimming(Enums.TrimmingGranularity.Character, "", 0, ellipsisTrimmingSign: true);

        Assert.That(() => fap.GetTextLayout(), Throws.Nothing);
    }
}
