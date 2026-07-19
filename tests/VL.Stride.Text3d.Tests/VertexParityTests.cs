// Regression tests: the implementation must reproduce the committed baseline fixtures.
// History: the fixtures were captured in Phase 0 from the SharpDX 1.0.2 build
// (tests/Baseline.Console) and proved the 2.0 Silk.NET port bit-exact; for 2.1 they
// were deliberately regenerated from the fixed implementation (corrected side-wall
// normals — positions, UVs and cap normals guarded unchanged during regeneration).
//
// To regenerate deliberately: set REGENERATE_BASELINES=1 and run the suite once.

using System.Text.Json.Nodes;
using NUnit.Framework;
using Stride.Graphics;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class VertexParityTests
{
    private const float Epsilon = 1e-5f;

    private static bool RegenerateBaselines =>
        Environment.GetEnvironmentVariable("REGENERATE_BASELINES") == "1";

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
    public void MatchesBaseline(string caseName, Func<VertexPositionNormalTexture[]> run)
    {
        if (RegenerateBaselines)
        {
            Regenerate(caseName, run());
            return;
        }

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

    /// <summary>
    /// Guards the 2.1 side-wall normal fix: for a convex single-contour glyph (Arial's
    /// capital I is a plain rectangular bar) every side-wall normal must point away
    /// from the glyph's center. With the pre-2.1 formula normalize(vec.Y, vec.X) this
    /// fails; hole contours are covered implicitly because D2D winds them opposite,
    /// which flips the formula's result accordingly.
    /// </summary>
    [Test]
    public void SideWallNormalsPointOutward()
    {
        var vertices = TestData.BuildSimple("I", "Arial", 64, TextAlignment.Leading, ParagraphAlignment.Near, 4f);
        var sideWall = vertices.Where(v => v.Normal.Z == 0f).ToArray();
        Assert.That(sideWall, Is.Not.Empty);

        float cx = sideWall.Average(v => v.Position.X);
        float cy = sideWall.Average(v => v.Position.Y);
        foreach (var v in sideWall)
        {
            float dot = v.Normal.X * (v.Position.X - cx) + v.Normal.Y * (v.Position.Y - cy);
            Assert.That(dot, Is.GreaterThan(0f),
                $"side-wall normal ({v.Normal.X}, {v.Normal.Y}) at ({v.Position.X}, {v.Position.Y}) points inward");
        }
    }

    /// <summary>
    /// Deliberate fixture regeneration (REGENERATE_BASELINES=1). Guards that only
    /// side-wall normal X/Y components change relative to the previous fixture —
    /// positions, UVs, cap normals and the normal Z component must stay identical.
    /// </summary>
    private static void Regenerate(string caseName, VertexPositionNormalTexture[] vertices)
    {
        string binPath = Path.Combine(TestData.BaselineDir, caseName + ".bin");
        string jsonPath = Path.Combine(TestData.BaselineDir, caseName + ".json");

        var payload = new byte[vertices.Length * 8 * sizeof(float)];
        var floats = new float[vertices.Length * 8];
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            floats[i * 8 + 0] = v.Position.X; floats[i * 8 + 1] = v.Position.Y; floats[i * 8 + 2] = v.Position.Z;
            floats[i * 8 + 3] = v.Normal.X; floats[i * 8 + 4] = v.Normal.Y; floats[i * 8 + 5] = v.Normal.Z;
            floats[i * 8 + 6] = v.TextureCoordinate.X; floats[i * 8 + 7] = v.TextureCoordinate.Y;
        }
        System.Buffer.BlockCopy(floats, 0, payload, 0, payload.Length);

        if (File.Exists(binPath))
        {
            float[] old = TestData.ReadBaseline(caseName);
            Assert.That(vertices.Length, Is.EqualTo(old.Length / 8), "vertex count must not change on regeneration");
            for (int i = 0; i < vertices.Length; i++)
            {
                for (int c = 0; c < 8; c++)
                {
                    bool isSideWallNormalXY = (c == 3 || c == 4) && old[i * 8 + 5] == 0f;
                    if (!isSideWallNormalXY)
                        Assert.That(floats[i * 8 + c], Is.EqualTo(old[i * 8 + c]),
                            $"component {c} of vertex {i} changed — only side-wall normal X/Y may differ");
                }
            }
        }

        File.WriteAllBytes(binPath, payload);

        var sidecar = JsonNode.Parse(File.ReadAllText(jsonPath))!;
        sidecar["vertexCount"] = vertices.Length;
        sidecar["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
        sidecar["source"] = "VL.Stride.Text3d 2.1.0 (Silk.NET, corrected side-wall normals); originally captured from 1.0.2 (SharpDX)";
        File.WriteAllText(jsonPath, sidecar.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        Assert.Pass($"regenerated {caseName} ({vertices.Length} vertices)");
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
