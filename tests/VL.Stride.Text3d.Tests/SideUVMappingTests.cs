// Tests for the Side UV Mapping modes. The default (Silhouette) is covered by the
// baseline parity tests; these cover the two contour modes on the rectangular Arial "I"
// (extrude 4). Side-wall vertices are emitted before the caps, so they form a
// contiguous prefix of the vertex list, in groups of 6 per contour edge.

using NUnit.Framework;
using Stride.Graphics;
using VL.Stride.Text3d.Core;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using SideUVMapping = VL.Stride.Text3d.Enums.SideUVMapping;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class SideUVMappingTests
{
    private const float Extrude = 4f;

    private static VertexPositionNormalTexture[] Build(SideUVMapping mapping, float textureScale = 32f)
    {
        var model = new Text3dModel
        {
            Text = "I",
            Font = "Arial",
            FontSize = 64,
            HorizontalAlignment = TextAlignment.Leading,
            VerticalAlignment = ParagraphAlignment.Near,
            ExtrudeAmount = Extrude,
            SideUVMapping = mapping,
            TextureScale = textureScale,
        };
        return TestData.CreateMeshVertices(model);
    }

    private static (VertexPositionNormalTexture[] Side, VertexPositionNormalTexture[] Caps) Split(
        VertexPositionNormalTexture[] vertices)
    {
        int sideCount = 0;
        while (sideCount < vertices.Length && vertices[sideCount].Normal.Z == 0f)
            sideCount++;
        return (vertices[..sideCount], vertices[sideCount..]);
    }

    [Test]
    public void ContourDepthMapsVAlongDepthAndUAroundContour()
    {
        var (side, caps) = Split(Build(SideUVMapping.ContourDepth));
        Assert.That(side, Is.Not.Empty);
        Assert.That(caps, Is.Not.Empty);

        foreach (var v in side)
        {
            // V: 0 on the front face, 1 on the back face
            Assert.That(v.TextureCoordinate.Y, Is.EqualTo(v.Position.Z > 0 ? 0f : 1f), "V along depth");
            Assert.That(v.TextureCoordinate.X, Is.InRange(0f, 1f), "U within one repeat");
        }

        // U increases along the contour within every wall quad (6 vertices per edge:
        // corners 0 and 4 carry the edge start U, corner 5 the edge end U)
        Assert.That(side.Length % 6, Is.EqualTo(0));
        float maxU = 0f;
        for (int q = 0; q < side.Length; q += 6)
        {
            float uStart = side[q].TextureCoordinate.X;
            float uEnd = side[q + 5].TextureCoordinate.X;
            Assert.That(uEnd, Is.GreaterThan(uStart), $"quad at {q}");
            maxU = Math.Max(maxU, uEnd);
        }
        Assert.That(maxU, Is.EqualTo(1f), "the closing edge ends at the seam (U = 1)");

        // Caps keep the planar 0..1 projection in this mode
        foreach (var v in caps)
        {
            Assert.That(v.TextureCoordinate.X, Is.InRange(0f, 1f));
            Assert.That(v.TextureCoordinate.Y, Is.InRange(0f, 1f));
        }
    }

    [Test]
    public void ContourDepthTiledUsesAbsoluteDensity()
    {
        const float scale = 8f;
        var (side, caps) = Split(Build(SideUVMapping.ContourDepthTiled, scale));
        Assert.That(side, Is.Not.Empty);
        Assert.That(caps, Is.Not.Empty);

        foreach (var v in side)
        {
            // V: 0 on the front face, extrude/scale on the back face
            Assert.That(v.TextureCoordinate.Y, Is.EqualTo(v.Position.Z > 0 ? 0f : Extrude / scale), "V in repeats");
        }

        // The rectangle's perimeter is far larger than the scale, so U spans several repeats
        Assert.That(side.Max(v => v.TextureCoordinate.X), Is.GreaterThan(1f));

        // Caps are tiled at the same density (glyph height greatly exceeds the scale)
        Assert.That(caps.Max(v => v.TextureCoordinate.Y), Is.GreaterThan(1f));
        Assert.That(caps.Min(v => v.TextureCoordinate.Y), Is.GreaterThanOrEqualTo(0f));
    }
}
