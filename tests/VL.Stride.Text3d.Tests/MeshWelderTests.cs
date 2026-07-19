// Tests for the optional vertex welding: expanding the welded, indexed mesh through
// its index buffer must reproduce the original triangle soup bit-exactly (lossless),
// while the vertex buffer shrinks substantially.

using System.Reflection;
using NUnit.Framework;
using Stride.Graphics;
using VL.Stride.Text3d.Core;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class MeshWelderTests
{
    private static VertexPositionNormalTexture[] Soup()
        => TestData.BuildSimple("o", "Times New Roman", 64, TextAlignment.Leading, ParagraphAlignment.Near, 4f);

    [Test]
    public void WeldingIsLossless()
    {
        var soup = Soup();
        var (vertices, indices) = MeshWelder.Weld(soup);

        Assert.That(indices.Length, Is.EqualTo(soup.Length), "triangle count must be unchanged");
        for (int i = 0; i < indices.Length; i++)
            Assert.That(vertices[indices[i]], Is.EqualTo(soup[i]), $"expanded vertex {i}");
    }

    [Test]
    public void WeldingReducesVertexCount()
    {
        var soup = Soup();
        var (vertices, _) = MeshWelder.Weld(soup);
        Assert.That(vertices.Length, Is.LessThan(soup.Length * 0.6),
            $"expected substantial reduction, got {soup.Length} -> {vertices.Length}");
    }

    [Test]
    public void WeldedModelProducesEquivalentIndexedMeshData()
    {
        var plainModel = new Text3dModel { Text = "o", Font = "Times New Roman", FontSize = 64, ExtrudeAmount = 4f };
        var weldedModel = new Text3dModel { Text = "o", Font = "Times New Roman", FontSize = 64, ExtrudeAmount = 4f, WeldVertices = true };

        var (plainVertices, plainIndices) = CreateMeshData(plainModel);
        var (weldedVertices, weldedIndices) = CreateMeshData(weldedModel);

        Assert.That(weldedIndices.Length, Is.EqualTo(plainIndices.Length), "index count");
        Assert.That(weldedVertices.Length, Is.LessThan(plainVertices.Length), "vertex count");
        for (int i = 0; i < weldedIndices.Length; i++)
            Assert.That(weldedVertices[weldedIndices[i]], Is.EqualTo(plainVertices[plainIndices[i]]), $"triangle corner {i}");
    }

    private static (VertexPositionNormalTexture[] Vertices, int[] Indices) CreateMeshData(Text3dBase model)
    {
        var method = model.GetType().GetMethod("CreatePrimitiveMeshData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object meshData = method.Invoke(model, null)!;
        var vertices = (VertexPositionNormalTexture[])meshData.GetType().GetProperty("Vertices")!.GetValue(meshData)!;
        var indices = (int[])meshData.GetType().GetProperty("Indices")!.GetValue(meshData)!;
        return (vertices, indices);
    }
}
