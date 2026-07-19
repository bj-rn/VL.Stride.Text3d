// Parity harness: rebuilds the Phase 0 test matrix through the library's Core classes
// (src/, Silk.NET port) and compares vertex output against tests/baselines fixtures.
//
//   dotnet run -- compare   (default) per-case count + epsilon comparison
//   dotnet run -- leak      3000 iterations, steady-state working-set report

using System.Reflection;
using System.Text.Json;
using Stride.Graphics;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;

const string RtlText = "مرحبا";
const float Epsilon = 1e-5f;

string baselineDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "baselines"));
bool leakMode = args.Length > 0 && args[0].Equals("leak", StringComparison.OrdinalIgnoreCase);

if (leakMode)
{
    RunLeakTest();
    return 0;
}

var cases = new (string Name, Func<VertexPositionNormalTexture[]> Run)[]
{
    ("simple-default", () => BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("multiline-center-far", () => BuildSimple("line1\nline2", "Arial", 32, TextAlignment.Center, ParagraphAlignment.Far, 1f)),
    ("times-size8", () => BuildSimple("vvvv", "Times New Roman", 8, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("times-size128", () => BuildSimple("vvvv", "Times New Roman", 128, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("extrude0", () => BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 0f)),
    ("extrude24", () => BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 24f)),
    ("rtl", () => BuildSimple(RtlText, "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
    ("advanced-underline-strike", () => BuildAdvancedUnderlineStrike("Hello World", "Arial", 32, 1f)),
    ("empty", () => BuildSimple("", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f)),
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
            bool degenerate = vertices.Length == 3 && vertices.All(v =>
                v.Position == default && v.Normal == default && v.TextureCoordinate == default);
            Console.WriteLine($"{name,-28} {(degenerate ? "OK" : "FAIL")}  (baseline threw {baselineEx}; port returns degenerate mesh — intentional change)");
            if (!degenerate) failures++;
            continue;
        }

        float[] expected = ReadFloats(binPath);
        int expectedCount = expected.Length / 8;

        if (vertices.Length != expectedCount)
        {
            Console.WriteLine($"{name,-28} FAIL  vertex count {vertices.Length} != baseline {expectedCount}");
            failures++;
            continue;
        }

        float maxDiff = 0f;
        int diffIndex = -1;
        for (int i = 0; i < vertices.Length; i++)
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
        Console.WriteLine($"{name,-28} {(ok ? "OK" : "FAIL")}  ({vertices.Length} vertices, max component diff {maxDiff:E2}{(diffIndex >= 0 ? $" @v{diffIndex}" : "")})");
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

static VertexPositionNormalTexture[] BuildSimple(
    string text, string font, int fontSize,
    TextAlignment textAlignment, ParagraphAlignment paragraphAlignment, float extrude)
{
    var model = new Text3dModel
    {
        Text = text, Font = font, FontSize = fontSize,
        HorizontalAlignment = textAlignment, VerticalAlignment = paragraphAlignment,
        ExtrudeAmount = extrude,
    };
    return CreateMeshVertices(model);
}

static unsafe VertexPositionNormalTexture[] BuildAdvancedUnderlineStrike(
    string text, string font, float fontSize, float extrude)
{
    var fmt = Native.CreateTextFormat(font, fontSize);
    var layout = Native.CreateTextLayout(text, fmt, 0.0f, 32.0f);
    fmt->Release();

    layout->SetWordWrapping(Silk.NET.DirectWrite.WordWrapping.NoWrap);
    layout->SetUnderline(true, new Silk.NET.DirectWrite.TextRange { StartPosition = 0, Length = 5 });
    layout->SetStrikethrough(true, new Silk.NET.DirectWrite.TextRange { StartPosition = 6, Length = 5 });

    using var handle = TextLayoutHandle.FromPointer((nint)layout, addRef: false);
    var model = new Text3dAdvancedModel { TextLayout = handle, ExtrudeAmount = extrude };
    return CreateMeshVertices(model);
}

static VertexPositionNormalTexture[] CreateMeshVertices(Text3dBase model)
{
    // CreatePrimitiveMeshData is protected in PrimitiveProceduralModelBase.
    var method = model.GetType().GetMethod("CreatePrimitiveMeshData",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(model.GetType().Name, "CreatePrimitiveMeshData");
    object meshData = method.Invoke(model, null)
        ?? throw new InvalidOperationException("CreatePrimitiveMeshData returned null");
    var vertices = (VertexPositionNormalTexture[]?)meshData.GetType().GetProperty("Vertices")!.GetValue(meshData)
        ?? throw new InvalidOperationException("GeometricMeshData.Vertices is null");
    return vertices;
}

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
        BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    Console.WriteLine($"working set after warm-up: {Environment.WorkingSet / 1024 / 1024.0:F1} MB");

    // First iterations grow the heap and native glyph/geometry caches; the leak
    // criterion is steady-state growth AFTER that settling window.
    long settled = 0;
    for (int i = 1; i <= 3000; i++)
    {
        BuildSimple("vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f);
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
