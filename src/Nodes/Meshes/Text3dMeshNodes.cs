// The user-facing Text3dMesh nodes (category Stride.Models.Meshes), replacing the
// formerly patched Text3dMesh process definitions. Like Text3d but outputting the
// generated Stride Mesh directly. The obsolete "Text3dMesh (Async)" was dropped.

using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Nodes.Models;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Nodes.Meshes;

/// <summary>Renders a string of text as an extruded 3D mesh.</summary>
[ProcessNode]
public class Text3dMesh : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dModel model = new();
    private Mesh? mesh;
    private int lastHash;

    public Text3dMesh(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    public void Update(out Mesh? output,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        int hash = hashCode.ToHashCode();
        if (hash != lastHash || mesh == null)
        {
            model.Text = text ?? "";
            model.Font = font?.Value ?? "Arial";
            model.FontSize = fontSize;
            model.HorizontalAlignment = textAlignment;
            model.VerticalAlignment = paragraphAlignment;
            model.ExtrudeAmount = extrudeAmount;
            model.ExtrudeOrigin = extrudeOrigin;
            model.FlatteningTolerance = flatteningTolerance;
            model.SmoothingAngle = smoothingAngle;
            var strideModel = Text3dModelBuilder.Build(model, services.Game);
            mesh = strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
            lastHash = hash;
        }
        output = mesh;
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Renders a FontAndParagraph text layout as an extruded 3D mesh.</summary>
[ProcessNode(Name = "Text3dMesh (Advanced)")]
public class Text3dMeshAdvanced : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dAdvancedModel model = new();
    private Mesh? mesh;
    private TextLayoutHandle? lastLayout;
    private int lastHash;

    public Text3dMeshAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    public void Update(out Mesh? output,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            mesh = null;
            lastLayout = null;
        }
        else
        {
            var hashCode = new HashCode();
            hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
            hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
            hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
            int hash = hashCode.ToHashCode();
            if (!ReferenceEquals(layout, lastLayout) || hash != lastHash)
            {
                model.TextLayout = layout;
                model.ExtrudeAmount = extrudeAmount;
                model.ExtrudeOrigin = extrudeOrigin;
                model.FlatteningTolerance = flatteningTolerance;
                model.SmoothingAngle = smoothingAngle;
                var strideModel = Text3dModelBuilder.Build(model, services.Game);
                mesh = strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
                lastLayout = layout;
                lastHash = hash;
            }
        }
        output = mesh;
    }

    public void Dispose() => services.Dispose();
}
