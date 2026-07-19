// Tests for the 2.2 geometry pins: Extrude Origin, Flattening Tolerance and
// Smoothing Angle. Their defaults (Center / 0.1 / 60°) reproduce the pre-2.2 output
// exactly — guarded by the baseline parity tests.

using NUnit.Framework;
using Stride.Graphics;
using VL.Stride.Text3d.Core;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class GeometryOptionTests
{
    private static VertexPositionNormalTexture[] Build(string text, string font, int size,
        float extrude, ExtrudeOrigin origin = ExtrudeOrigin.Center,
        float tolerance = Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Extruder.DefaultSmoothingAngle)
    {
        var model = new Text3dModel
        {
            Text = text,
            Font = font,
            FontSize = size,
            HorizontalAlignment = TextAlignment.Leading,
            VerticalAlignment = ParagraphAlignment.Near,
            ExtrudeAmount = extrude,
            ExtrudeOrigin = origin,
            FlatteningTolerance = tolerance,
            SmoothingAngle = smoothingAngle,
        };
        return TestData.CreateMeshVertices(model);
    }

    [TestCase(ExtrudeOrigin.Center, -2f, 2f)]
    [TestCase(ExtrudeOrigin.Front, -4f, 0f)]
    [TestCase(ExtrudeOrigin.Back, 0f, 4f)]
    public void ExtrudeOriginShiftsZ(ExtrudeOrigin origin, float expectedMinZ, float expectedMaxZ)
    {
        var vertices = Build("I", "Arial", 64, extrude: 4f, origin: origin);
        float minZ = vertices.Min(v => v.Position.Z);
        float maxZ = vertices.Max(v => v.Position.Z);
        Assert.That(minZ, Is.EqualTo(expectedMinZ).Within(1e-6f));
        Assert.That(maxZ, Is.EqualTo(expectedMaxZ).Within(1e-6f));
    }

    [Test]
    public void FlatteningToleranceControlsDensity()
    {
        // A curvy serif glyph: finer tolerance must produce more vertices.
        var coarse = Build("o", "Times New Roman", 64, extrude: 1f, tolerance: 1.0f);
        var fine = Build("o", "Times New Roman", 64, extrude: 1f, tolerance: 0.02f);
        Assert.That(fine.Length, Is.GreaterThan(coarse.Length));
    }

    [Test]
    public void SmoothingAngleControlsHardEdges()
    {
        // Angle is in cycles (vvvv standard unit). Rectangle glyph: at 0 nothing is
        // smoothed (corner normals stay per-edge); at 0.5 (=180°) adjacent edge normals
        // are always averaged. Positions must be identical, only side-wall normals may
        // differ.
        var hard = Build("I", "Arial", 64, extrude: 4f, smoothingAngle: 0f);
        var smooth = Build("I", "Arial", 64, extrude: 4f, smoothingAngle: 0.5f);

        Assert.That(smooth.Length, Is.EqualTo(hard.Length));
        bool anyNormalDiffers = false;
        for (int i = 0; i < hard.Length; i++)
        {
            Assert.That(smooth[i].Position, Is.EqualTo(hard[i].Position));
            if (hard[i].Normal.Z == 0f && smooth[i].Normal != hard[i].Normal)
                anyNormalDiffers = true;
        }
        Assert.That(anyNormalDiffers, Is.True, "expected corner normals to change between 0° and 180° smoothing");
    }
}
