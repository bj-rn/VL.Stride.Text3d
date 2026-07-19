// Phase 1 spike runner: rebuilds the Phase 0 test matrix through the Silk.NET pipeline
// and compares vertex output against tests/baselines fixtures.
//
//   dotnet run -- compare   (default) per-case count + epsilon comparison
//   dotnet run -- leak      1000 iterations of the default case, working-set report

using System.Text.Json;
using Spike;
using Stride.Graphics;
using TextAlignment = Silk.NET.DirectWrite.TextAlignment;
using ParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;

const string RtlText = "مرحبا";
const float Epsilon = 1e-5f;

string baselineDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "baselines"));
bool leakMode = args.Length > 0 && args[0].Equals("leak", StringComparison.OrdinalIgnoreCase);

if (leakMode)
{
    RunLeakTest();
    return 0;
}

if (args.Length > 0 && args[0].Equals("diag", StringComparison.OrdinalIgnoreCase))
{
    // Bounds diagnostic: compare position ranges, baseline vs port, for one case.
    float[] b = ReadFloats(Path.Combine(baselineDir, "simple-default.bin"));
    PrintBounds("baseline simple-default", Enumerable.Range(0, b.Length / 8)
        .Select(i => (b[i * 8], b[i * 8 + 1], b[i * 8 + 2])));
    var mine = SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
    PrintBounds("port     simple-default", mine.Select(v => (v.Position.X, v.Position.Y, v.Position.Z)));
    return 0;

    static void PrintBounds(string label, IEnumerable<(float X, float Y, float Z)> pts)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        int n = 0;
        foreach (var p in pts) { n++; minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        Console.WriteLine($"{label}: {n} verts, X [{minX:G9} .. {maxX:G9}], Y [{minY:G9} .. {maxY:G9}]");
    }
}

var cases = new (string Name, Func<List<VertexPositionNormalTexture>> Run)[]
{
    ("simple-default", () => SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("multiline-center-far", () => SilkText3dPipeline.BuildSimple("line1\nline2", "Arial", 32, TextAlignment.Center, ParagraphAlignment.Far, 1f)),
    ("times-size8", () => SilkText3dPipeline.BuildSimple("vvvv", "Times New Roman", 8, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("times-size128", () => SilkText3dPipeline.BuildSimple("vvvv", "Times New Roman", 128, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("extrude0", () => SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 0f)),
    ("extrude24", () => SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 24f)),
    ("rtl", () => SilkText3dPipeline.BuildSimple(RtlText, "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("advanced-underline-strike", () => SilkText3dPipeline.BuildAdvancedUnderlineStrike("Hello World", "Arial", 32, 1f)),
    ("empty", () => SilkText3dPipeline.BuildSimple("", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
};

int failures = 0;
Span<float> actual = stackalloc float[8];
foreach (var (name, run) in cases)
{
    try
    {
        var vertices = run();
        string binPath = Path.Combine(baselineDir, name + ".bin");

        if (!File.Exists(binPath))
        {
            // Baseline recorded an exception for this case (empty text). The port
            // intentionally returns a degenerate 3-vertex zero mesh instead.
            var sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(baselineDir, name + ".json")));
            string? baselineEx = sidecar.RootElement.GetProperty("exception").GetString();
            bool degenerate = vertices.Count == 3 && vertices.All(v =>
                v.Position == default && v.Normal == default && v.TextureCoordinate == default);
            Console.WriteLine($"{name,-28} {(degenerate ? "OK" : "FAIL")}  (baseline threw {baselineEx}; port returns degenerate mesh — intentional change)");
            if (!degenerate) failures++;
            continue;
        }

        float[] expected = ReadFloats(binPath);
        int expectedCount = expected.Length / 8;

        if (vertices.Count != expectedCount)
        {
            Console.WriteLine($"{name,-28} FAIL  vertex count {vertices.Count} != baseline {expectedCount}");
            failures++;
            continue;
        }

        float maxDiff = 0f;
        int diffIndex = -1;
        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            actual[0] = v.Position.X; actual[1] = v.Position.Y; actual[2] = v.Position.Z;
            actual[3] = v.Normal.X; actual[4] = v.Normal.Y; actual[5] = v.Normal.Z;
            actual[6] = v.TextureCoordinate.X; actual[7] = v.TextureCoordinate.Y;
            for (int c = 0; c < 8; c++)
            {
                float d = Math.Abs(actual[c] - expected[i * 8 + c]);
                if (d > maxDiff) { maxDiff = d; diffIndex = i; }
            }
        }

        bool ok = maxDiff <= Epsilon;
        Console.WriteLine($"{name,-28} {(ok ? "OK" : "FAIL")}  ({vertices.Count} vertices, max component diff {maxDiff:E2}{(diffIndex >= 0 ? $" @v{diffIndex}" : "")})");
        if (!ok) failures++;
    }
    catch (Exception e)
    {
        Console.WriteLine($"{name,-28} FAIL  threw {e.GetType().Name}: {e.Message}");
        failures++;
    }
}

Console.WriteLine(failures == 0 ? "COMPARE PASSED" : $"COMPARE FAILED ({failures} case(s))");
return failures == 0 ? 0 : 1;

static float[] ReadFloats(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    var floats = new float[bytes.Length / sizeof(float)];
    System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
    return floats;
}

static void RunLeakTest()
{
    // Warm-up
    for (int i = 0; i < 10; i++)
        SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long start = Environment.WorkingSet;
    Console.WriteLine($"working set after warm-up: {start / 1024 / 1024.0:F1} MB");

    // First iterations grow the heap and native glyph/geometry caches; the leak
    // criterion is steady-state growth AFTER that settling window.
    long settled = 0;
    for (int i = 1; i <= 3000; i++)
    {
        SilkText3dPipeline.BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
        if (i % 500 == 0)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long ws = Environment.WorkingSet;
            if (i == 500) settled = ws;
            Console.WriteLine($"after {i,4} iterations: {ws / 1024 / 1024.0:F1} MB");
        }
    }

    long end = Environment.WorkingSet;
    double growth = (end - settled) / 1024.0 / 1024.0;
    Console.WriteLine($"steady-state growth (iterations 500..3000): {growth:F1} MB {(growth < 2 ? "(OK — no leak)" : "(SUSPICIOUS)")}");
}
