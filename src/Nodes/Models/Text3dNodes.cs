// The user-facing Text3d entity nodes (category Stride.Models), replacing the formerly
// patched Text3d process definitions. Simple variant takes text/font pins; the
// (Advanced) variant takes a FontAndParagraph. Both output a cached Entity holding a
// ModelComponent whose Model is regenerated only when inputs change.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Lib.Basics.Resources;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using StrideModel = Stride.Rendering.Model;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;

namespace VL.Stride.Text3d.Nodes.Models;

internal sealed class GameServices : IDisposable
{
    private readonly NodeContext nodeContext;
    private IResourceHandle<Game>? gameHandle;

    public GameServices(NodeContext nodeContext) => this.nodeContext = nodeContext;

    public Game Game
    {
        get
        {
            gameHandle ??= ((IResourceProvider<Game>?)nodeContext.AppHost.Services.GetService(typeof(IResourceProvider<Game>)))?.GetHandle()
                ?? throw new InvalidOperationException("No Stride Game available — VL.Stride must be loaded.");
            return gameHandle.Resource;
        }
    }

    public void Dispose() => gameHandle?.Dispose();
}

internal static class Text3dModelBuilder
{
    /// <summary>Generates a Stride Model from the procedural text model.</summary>
    public static StrideModel Build(Text3dBase proceduralModel, Game game)
    {
        var model = new StrideModel();
        proceduralModel.Generate(game.Services, model);
        return model;
    }

    public static Entity CreateEntity(string name, out ModelComponent modelComponent)
    {
        var entity = new Entity(name);
        modelComponent = new ModelComponent();
        entity.Add(modelComponent);
        return entity;
    }

    /// <summary>Applies the optional Transformation matrix to the entity's transform.</summary>
    public static void ApplyTransformation(Entity entity, Matrix? transformation)
    {
        if (transformation is { } matrix)
        {
            entity.Transform.UseTRS = false;
            entity.Transform.LocalMatrix = matrix;
        }
        else
        {
            entity.Transform.UseTRS = true;
        }
    }
}

/// <summary>Renders a string of text as an extruded 3D model entity.</summary>
[ProcessNode]
public class Text3d : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dModel model = new();
    private readonly Entity entity;
    private readonly ModelComponent modelComponent;
    private int lastHash;

    public Text3d(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
        entity = Text3dModelBuilder.CreateEntity("Text3d", out modelComponent);
    }

    public void Update(out Entity output,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f,
        Matrix? transformation = null, Material? material = null, bool isShadowCaster = true)
    {
        int hash = HashCode.Combine(text, font?.Value, fontSize, textAlignment, paragraphAlignment, extrudeAmount, material);
        if (hash != lastHash || modelComponent.Model == null)
        {
            model.Text = text ?? "";
            model.Font = font?.Value ?? "Arial";
            model.FontSize = fontSize;
            model.HorizontalAlignment = textAlignment;
            model.VerticalAlignment = paragraphAlignment;
            model.ExtrudeAmount = extrudeAmount;
            model.MaterialInstance.Material = material;
            modelComponent.Model = Text3dModelBuilder.Build(model, services.Game);
            lastHash = hash;
        }
        Text3dModelBuilder.ApplyTransformation(entity, transformation);
        modelComponent.IsShadowCaster = isShadowCaster;
        output = entity;
    }

    public void Dispose() => services.Dispose();
}

/// <summary>Renders a FontAndParagraph text layout as an extruded 3D model entity.</summary>
[ProcessNode(Name = "Text3d (Advanced)")]
public class Text3dAdvanced : IDisposable
{
    private readonly GameServices services;
    private readonly Text3dAdvancedModel model = new();
    private readonly Entity entity;
    private readonly ModelComponent modelComponent;
    private TextLayoutHandle? lastLayout;
    private int lastVersion = -1;
    private float lastExtrude = float.NaN;
    private Material? lastMaterial;

    public Text3dAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
        entity = Text3dModelBuilder.CreateEntity("Text3d", out modelComponent);
    }

    public void Update(out Entity output,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        Matrix? transformation = null, Material? material = null, bool isShadowCaster = true)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        int version = fontAndParagraph?.GetVersion() ?? -1;
        if (layout == null)
        {
            modelComponent.Model = null;
            lastLayout = null;
            lastVersion = -1;
        }
        else if (!ReferenceEquals(layout, lastLayout) || version != lastVersion
            || extrudeAmount != lastExtrude || !ReferenceEquals(material, lastMaterial))
        {
            model.TextLayout = layout;
            model.ExtrudeAmount = extrudeAmount;
            model.MaterialInstance.Material = material;
            modelComponent.Model = Text3dModelBuilder.Build(model, services.Game);
            lastLayout = layout;
            lastVersion = version;
            lastExtrude = extrudeAmount;
            lastMaterial = material;
        }
        Text3dModelBuilder.ApplyTransformation(entity, transformation);
        modelComponent.IsShadowCaster = isShadowCaster;
        output = entity;
    }

    public void Dispose() => services.Dispose();
}
