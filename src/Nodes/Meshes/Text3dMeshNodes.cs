// The user-facing Text3dMesh nodes (category Stride.Models.Meshes), replacing the
// formerly patched Text3dMesh process definitions. Like Text3d but outputting the
// generated Stride Mesh directly. The obsolete "Text3dMesh (Async)" was dropped.

using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Nodes.Models;
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
        float extrudeAmount = 1f)
    {
        int hash = HashCode.Combine(text, font?.Value, fontSize, textAlignment, paragraphAlignment, extrudeAmount);
        if (hash != lastHash || mesh == null)
        {
            model.Text = text ?? "";
            model.Font = font?.Value ?? "Arial";
            model.FontSize = fontSize;
            model.HorizontalAlignment = textAlignment;
            model.VerticalAlignment = paragraphAlignment;
            model.ExtrudeAmount = extrudeAmount;
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
    private int lastVersion = -1;
    private float lastExtrude = float.NaN;

    public Text3dMeshAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
    }

    public void Update(out Mesh? output,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        int version = fontAndParagraph?.GetVersion() ?? -1;
        if (layout == null)
        {
            mesh = null;
            lastLayout = null;
            lastVersion = -1;
        }
        else if (!ReferenceEquals(layout, lastLayout) || version != lastVersion || extrudeAmount != lastExtrude)
        {
            model.TextLayout = layout;
            model.ExtrudeAmount = extrudeAmount;
            var strideModel = Text3dModelBuilder.Build(model, services.Game);
            mesh = strideModel.Meshes.Count > 0 ? strideModel.Meshes[0] : null;
            lastLayout = layout;
            lastVersion = version;
            lastExtrude = extrudeAmount;
        }
        output = mesh;
    }

    public void Dispose() => services.Dispose();
}
