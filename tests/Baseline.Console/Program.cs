// Phase 0 baseline capture: dumps vertex output of the legacy SharpDX-based
// VL.Stride.Text3d 1.0.2 build to ../baselines as regression fixtures.
//
//   dotnet run -- capture   (default) write <case>.bin / <case>.json fixtures
//   dotnet run -- verify    recompute all cases and byte-compare against fixtures
//
// Fixture format: <case>.bin is a little-endian float32 stream, 8 floats per vertex
// (pos.xyz, normal.xyz, uv.xy). <case>.json records inputs, vertex count and sha256.

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SharpDX.DirectWrite;
using Stride.Graphics;
using VL.Stride.Text3d;
using TextAlignment = SharpDX.DirectWrite.TextAlignment;

const string RtlText = "مرحبا"; // "marhaba"

string baselineDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "baselines"));
bool verify = args.Length > 0 && args[0].Equals("verify", StringComparison.OrdinalIgnoreCase);
Directory.CreateDirectory(baselineDir);

var cases = new List<(string Name, Dictionary<string, object?> Inputs, Func<VertexPositionNormalTexture[]> Run)>
{
    Simple("simple-default", "vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f),
    Simple("multiline-center-far", "line1\nline2", "Arial", 32, TextAlignment.Center, ParagraphAlignment.Far, 1f),
    Simple("times-size8", "vvvv", "Times New Roman", 8, TextAlignment.Leading, ParagraphAlignment.Near, 1f),
    Simple("times-size128", "vvvv", "Times New Roman", 128, TextAlignment.Leading, ParagraphAlignment.Near, 1f),
    Simple("extrude0", "vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 0f),
    Simple("extrude24", "vvvv", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 24f),
    Simple("rtl", RtlText, "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f),
    Advanced("advanced-underline-strike", "Hello World", "Arial", 32f, 1f),
    Simple("empty", "", "Arial", 32, TextAlignment.Leading, ParagraphAlignment.Near, 1f),
};

int failures = 0;
foreach (var (name, inputs, run) in cases)
{
    string binPath = Path.Combine(baselineDir, name + ".bin");
    string jsonPath = Path.Combine(baselineDir, name + ".json");
    try
    {
        var vertices = run();
        byte[] payload = Serialize(vertices);
        string sha = Convert.ToHexString(SHA256.HashData(payload));

        if (verify)
        {
            byte[] expected = File.ReadAllBytes(binPath);
            bool ok = expected.AsSpan().SequenceEqual(payload);
            Console.WriteLine($"{name,-28} {(ok ? "OK" : "MISMATCH")}  ({vertices.Length} vertices)");
            if (!ok) failures++;
        }
        else
        {
            File.WriteAllBytes(binPath, payload);
            WriteSidecar(jsonPath, name, inputs, vertices.Length, sha, exception: null);
            Console.WriteLine($"{name,-28} captured  ({vertices.Length} vertices, sha256 {sha[..12]}...)");
        }
    }
    catch (Exception e)
    {
        // The legacy build is known to throw on empty text (NRE in Extruder.FlattenGeometry).
        var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
        if (verify)
        {
            var sidecar = JsonDocument.Parse(File.ReadAllText(jsonPath));
            string? expectedEx = sidecar.RootElement.GetProperty("exception").GetString();
            bool ok = expectedEx == inner.GetType().FullName;
            Console.WriteLine($"{name,-28} {(ok ? "OK" : "MISMATCH")}  (throws {inner.GetType().Name})");
            if (!ok) failures++;
        }
        else
        {
            WriteSidecar(jsonPath, name, inputs, 0, null, inner.GetType().FullName);
            if (File.Exists(binPath)) File.Delete(binPath);
            Console.WriteLine($"{name,-28} captured  (throws {inner.GetType().Name})");
        }
    }
}

Console.WriteLine(verify
    ? failures == 0 ? "VERIFY PASSED" : $"VERIFY FAILED ({failures} case(s))"
    : $"Fixtures written to {baselineDir}");
return failures == 0 ? 0 : 1;

static (string, Dictionary<string, object?>, Func<VertexPositionNormalTexture[]>) Simple(
    string name, string text, string font, int fontSize,
    TextAlignment hAlign, ParagraphAlignment vAlign, float extrude)
{
    var inputs = new Dictionary<string, object?>
    {
        ["node"] = "Text3d", ["text"] = text, ["font"] = font, ["fontSize"] = fontSize,
        ["textAlignment"] = hAlign.ToString(), ["paragraphAlignment"] = vAlign.ToString(),
        ["extrudeAmount"] = extrude,
    };
    return (name, inputs, () =>
    {
        var node = new Text3d
        {
            Text = text, Font = font, FontSize = fontSize,
            HorizontalAlignment = hAlign, VerticalAlignment = vAlign,
            ExtrudeAmount = extrude,
        };
        return CreateMeshVertices(node);
    });
}

static (string, Dictionary<string, object?>, Func<VertexPositionNormalTexture[]>) Advanced(
    string name, string text, string font, float fontSize, float extrude)
{
    var inputs = new Dictionary<string, object?>
    {
        ["node"] = "Text3dAdvanced", ["text"] = text, ["font"] = font, ["fontSize"] = fontSize,
        ["underlineRange"] = "0..5", ["strikethroughRange"] = "6..5", ["extrudeAmount"] = extrude,
    };
    return (name, inputs, () =>
    {
        using var dwFactory = new SharpDX.DirectWrite.Factory(FactoryType.Shared);
        using var fmt = new TextFormat(dwFactory, font, fontSize);
        using var tl = new TextLayout(dwFactory, text, fmt, 0.0f, 32.0f)
        {
            WordWrapping = WordWrapping.NoWrap,
        };
        tl.SetUnderline(true, new TextRange(0, 5));
        tl.SetStrikethrough(true, new TextRange(6, 5));
        var node = new Text3dAdvanced { TextLayout = tl, ExtrudeAmount = extrude };
        return CreateMeshVertices(node);
    });
}

static VertexPositionNormalTexture[] CreateMeshVertices(Text3dBase node)
{
    // CreatePrimitiveMeshData is protected in PrimitiveProceduralModelBase.
    var method = node.GetType().GetMethod("CreatePrimitiveMeshData",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(node.GetType().Name, "CreatePrimitiveMeshData");
    object meshData = method.Invoke(node, null)
        ?? throw new InvalidOperationException("CreatePrimitiveMeshData returned null");
    var vertices = (VertexPositionNormalTexture[]?)meshData.GetType().GetProperty("Vertices")!.GetValue(meshData)
        ?? throw new InvalidOperationException("GeometricMeshData.Vertices is null");
    return vertices;
}

static byte[] Serialize(VertexPositionNormalTexture[] vertices)
{
    using var ms = new MemoryStream(vertices.Length * 8 * sizeof(float));
    using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        foreach (var v in vertices)
        {
            w.Write(v.Position.X); w.Write(v.Position.Y); w.Write(v.Position.Z);
            w.Write(v.Normal.X); w.Write(v.Normal.Y); w.Write(v.Normal.Z);
            w.Write(v.TextureCoordinate.X); w.Write(v.TextureCoordinate.Y);
        }
    }
    return ms.ToArray();
}

static void WriteSidecar(string path, string name, Dictionary<string, object?> inputs,
    int vertexCount, string? sha256, string? exception)
{
    var doc = new Dictionary<string, object?>
    {
        ["case"] = name, ["inputs"] = inputs, ["vertexCount"] = vertexCount,
        ["sha256"] = sha256, ["exception"] = exception,
        ["source"] = "VL.Stride.Text3d 1.0.2 (SharpDX 4.2.0), legacy/VL.Stride.Text3d.dll",
    };
    File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
}
