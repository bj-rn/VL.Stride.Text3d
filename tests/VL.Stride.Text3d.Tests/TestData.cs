using Stride.Graphics;
using System.Reflection;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

internal static class TestData
{
    public const string RtlText = "مرحبا";

    /// <summary>Repo root located by walking up from the test assembly to tests/baselines.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string BaselineDir => Path.Combine(RepoRoot, "tests", "baselines");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "baselines")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root (tests/baselines).");
    }

    public static VertexPositionNormalTexture[] BuildSimple(
        string text, string font, int fontSize,
        TextAlignment textAlignment, ParagraphAlignment paragraphAlignment, float extrude)
    {
        var model = new Text3dModel
        {
            Text = text,
            Font = font,
            FontSize = fontSize,
            HorizontalAlignment = textAlignment,
            VerticalAlignment = paragraphAlignment,
            ExtrudeAmount = extrude,
        };
        return CreateMeshVertices(model);
    }

    public static unsafe VertexPositionNormalTexture[] BuildAdvancedUnderlineStrike(
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

    public static VertexPositionNormalTexture[] CreateMeshVertices(Text3dBase model)
    {
        var method = model.GetType().GetMethod("CreatePrimitiveMeshData",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(model.GetType().Name, "CreatePrimitiveMeshData");
        object meshData = method.Invoke(model, null)
            ?? throw new InvalidOperationException("CreatePrimitiveMeshData returned null");
        return (VertexPositionNormalTexture[]?)meshData.GetType().GetProperty("Vertices")!.GetValue(meshData)
            ?? throw new InvalidOperationException("GeometricMeshData.Vertices is null");
    }

    public static float[] ReadBaseline(string caseName)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(BaselineDir, caseName + ".bin"));
        var floats = new float[bytes.Length / sizeof(float)];
        System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
