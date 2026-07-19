// Regression tests: the Silk.NET port must reproduce the committed SharpDX 1.0.2
// baseline fixtures (captured in Phase 0 by tests/Baseline.Console).

using NUnit.Framework;
using Stride.Graphics;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class VertexParityTests
{
    private const float Epsilon = 1e-5f;

    public static IEnumerable<TestCaseData> Cases()
    {
        yield return Case("simple-default", () => TestData.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f));
        yield return Case("multiline-center-far", () => TestData.BuildSimple("line1\nline2", "Arial", 32, TextAlignment.Center, ParagraphAlignment.Far, 1f));
        yield return Case("times-size8", () => TestData.BuildSimple("vvvv", "Times New Roman", 8, TextAlignment.Leading, ParagraphAlignment.Near, 1f));
        yield return Case("times-size128", () => TestData.BuildSimple("vvvv", "Times New Roman", 128, TextAlignment.Leading, ParagraphAlignment.Near, 1f));
        yield return Case("extrude0", () => TestData.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 0f));
        yield return Case("extrude24", () => TestData.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 24f));
        yield return Case("rtl", () => TestData.BuildSimple(TestData.RtlText, "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f));
        yield return Case("advanced-underline-strike", () => TestData.BuildAdvancedUnderlineStrike("Hello World", "Arial", 32, 1f));

        static TestCaseData Case(string name, Func<VertexPositionNormalTexture[]> run)
            => new TestCaseData(name, run) { TestName = name };
    }

    [TestCaseSource(nameof(Cases))]
    public void MatchesSharpDXBaseline(string caseName, Func<VertexPositionNormalTexture[]> run)
    {
        float[] expected = TestData.ReadBaseline(caseName);
        var vertices = run();

        Assert.That(vertices.Length, Is.EqualTo(expected.Length / 8), "vertex count");

        float maxDiff = 0f;
        Span<float> actual = stackalloc float[8];
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            actual[0] = v.Position.X; actual[1] = v.Position.Y; actual[2] = v.Position.Z;
            actual[3] = v.Normal.X; actual[4] = v.Normal.Y; actual[5] = v.Normal.Z;
            actual[6] = v.TextureCoordinate.X; actual[7] = v.TextureCoordinate.Y;
            for (int c = 0; c < 8; c++)
                maxDiff = Math.Max(maxDiff, Math.Abs(actual[c] - expected[i * 8 + c]));
        }

        Assert.That(maxDiff, Is.LessThanOrEqualTo(Epsilon), "max vertex component difference");
    }

    [Test]
    public void EmptyTextYieldsDegenerateMesh()
    {
        // Intentional 2.0 behavior change: the 1.0.2 build threw NullReferenceException here.
        var vertices = TestData.BuildSimple("", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        Assert.That(vertices.Length, Is.EqualTo(3));
        Assert.That(vertices, Is.All.Matches<VertexPositionNormalTexture>(v =>
            v.Position == default && v.Normal == default && v.TextureCoordinate == default));
    }

    [Test]
    public void RepeatedGenerationIsStable()
    {
        var first = TestData.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        for (int i = 0; i < 50; i++)
        {
            var again = TestData.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
            Assert.That(again.Length, Is.EqualTo(first.Length));
        }
    }
}
