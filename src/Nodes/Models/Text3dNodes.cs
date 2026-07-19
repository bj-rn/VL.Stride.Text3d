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
using VL.Lib.Collections;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using ExtrudeOrigin = VL.Stride.Text3d.Enums.ExtrudeOrigin;
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

    /// <summary>Syncs user-provided extra components onto the entity (never touches ownComponent).</summary>
    public static void SyncComponents(Entity entity, ModelComponent ownComponent,
        ref Spread<EntityComponent>? current, Spread<EntityComponent>? target)
    {
        if (ReferenceEquals(current, target))
            return;

        if (current != null)
        {
            foreach (var component in current)
            {
                if (component != null && !ReferenceEquals(component, ownComponent)
                    && (target == null || !target.Contains(component)))
                    entity.Remove(component);
            }
        }
        if (target != null)
        {
            foreach (var component in target)
            {
                if (component != null && !ReferenceEquals(component, ownComponent)
                    && !entity.Components.Contains(component))
                    entity.Add(component);
            }
        }
        current = target;
    }

    /// <summary>Syncs user-provided child entities under the entity's transform.</summary>
    public static void SyncChildren(Entity entity, ref Spread<Entity>? current, Spread<Entity>? target)
    {
        if (ReferenceEquals(current, target))
            return;

        if (current != null)
        {
            foreach (var child in current)
            {
                if (child != null && (target == null || !target.Contains(child))
                    && child.Transform.Parent == entity.Transform)
                    child.Transform.Parent = null;
            }
        }
        if (target != null)
        {
            foreach (var child in target)
            {
                if (child != null && child.Transform.Parent != entity.Transform)
                    child.Transform.Parent = entity.Transform;
            }
        }
        current = target;
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
    private Spread<EntityComponent>? lastComponents;
    private Spread<Entity>? lastChildren;

    public Text3d(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
        entity = Text3dModelBuilder.CreateEntity("Text3d", out modelComponent);
    }

    /// <param name="output">The entity holding the generated text model.</param>
    /// <param name="text">The string to render.</param>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    /// <param name="transformation">The transformation applied to the entity; when not set the entity keeps its default transform.</param>
    /// <param name="material">The material used to render the model.</param>
    /// <param name="isShadowCaster">Whether the model casts shadows.</param>
    /// <param name="components">Additional components attached to the entity.</param>
    /// <param name="children">Entities parented under this entity's transform.</param>
    /// <param name="name">The name of the entity.</param>
    /// <param name="enabled">Whether the model is rendered.</param>
    public void Update(out Entity output,
        string text = "hello world", FontList? font = null, int fontSize = 32,
        TextAlignment textAlignment = TextAlignment.Leading,
        ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near,
        float extrudeAmount = 1f, ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false,
        Matrix? transformation = null, Material? material = null, bool isShadowCaster = true,
        Spread<EntityComponent>? components = null, Spread<Entity>? children = null,
        string name = "Text3d", bool enabled = true)
    {
        var hashCode = new HashCode();
        hashCode.Add(text); hashCode.Add(font?.Value); hashCode.Add(fontSize);
        hashCode.Add(textAlignment); hashCode.Add(paragraphAlignment);
        hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
        hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
        hashCode.Add(weldVertices); hashCode.Add(material);
        int hash = hashCode.ToHashCode();
        if (hash != lastHash || modelComponent.Model == null)
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
            model.WeldVertices = weldVertices;
            model.MaterialInstance.Material = material;
            modelComponent.Model = Text3dModelBuilder.Build(model, services.Game);
            lastHash = hash;
        }
        Text3dModelBuilder.ApplyTransformation(entity, transformation);
        Text3dModelBuilder.SyncComponents(entity, modelComponent, ref lastComponents, components);
        Text3dModelBuilder.SyncChildren(entity, ref lastChildren, children);
        modelComponent.IsShadowCaster = isShadowCaster;
        modelComponent.Enabled = enabled;
        entity.Name = name;
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
    private int lastHash;
    private Spread<EntityComponent>? lastComponents;
    private Spread<Entity>? lastChildren;

    public Text3dAdvanced(NodeContext nodeContext)
    {
        services = new GameServices(nodeContext);
        entity = Text3dModelBuilder.CreateEntity("Text3d", out modelComponent);
    }

    /// <param name="output">The entity holding the generated text model.</param>
    /// <param name="fontAndParagraph">The FontAndParagraph providing the text layout to render.</param>
    /// <param name="extrudeAmount">The depth of the extrusion along Z.</param>
    /// <param name="extrudeOrigin">Where the extruded mesh sits relative to Z = 0.</param>
    /// <param name="flatteningTolerance">The maximum deviation allowed when flattening the outlines; smaller values yield finer curves and more vertices.</param>
    /// <param name="smoothingAngle">In cycles: side-wall edges sharper than this angle stay hard, flatter ones are shaded smooth.</param>
    /// <param name="weldVertices">Welds identical vertices into an indexed mesh: visually lossless with smaller buffers, but changes the mesh topology (off keeps the plain triangle list).</param>
    /// <param name="transformation">The transformation applied to the entity; when not set the entity keeps its default transform.</param>
    /// <param name="material">The material used to render the model.</param>
    /// <param name="isShadowCaster">Whether the model casts shadows.</param>
    /// <param name="components">Additional components attached to the entity.</param>
    /// <param name="children">Entities parented under this entity's transform.</param>
    /// <param name="name">The name of the entity.</param>
    /// <param name="enabled">Whether the model is rendered.</param>
    public void Update(out Entity output,
        FontAndParagraph? fontAndParagraph = null, float extrudeAmount = 1f,
        ExtrudeOrigin extrudeOrigin = ExtrudeOrigin.Center,
        float flatteningTolerance = Core.Extruder.DefaultFlatteningTolerance,
        float smoothingAngle = Core.Extruder.DefaultSmoothingAngle,
        bool weldVertices = false,
        Matrix? transformation = null, Material? material = null, bool isShadowCaster = true,
        Spread<EntityComponent>? components = null, Spread<Entity>? children = null,
        string name = "Text3d", bool enabled = true)
    {
        var layout = fontAndParagraph?.GetTextLayout();
        if (layout == null)
        {
            modelComponent.Model = null;
            lastLayout = null;
        }
        else
        {
            var hashCode = new HashCode();
            hashCode.Add(layout); hashCode.Add(fontAndParagraph!.GetVersion());
            hashCode.Add(extrudeAmount); hashCode.Add(extrudeOrigin);
            hashCode.Add(flatteningTolerance); hashCode.Add(smoothingAngle);
            hashCode.Add(weldVertices); hashCode.Add(material);
            int hash = hashCode.ToHashCode();
            if (!ReferenceEquals(layout, lastLayout) || hash != lastHash)
            {
                model.TextLayout = layout;
                model.ExtrudeAmount = extrudeAmount;
                model.ExtrudeOrigin = extrudeOrigin;
                model.FlatteningTolerance = flatteningTolerance;
                model.SmoothingAngle = smoothingAngle;
                model.WeldVertices = weldVertices;
                model.MaterialInstance.Material = material;
                modelComponent.Model = Text3dModelBuilder.Build(model, services.Game);
                lastLayout = layout;
                lastHash = hash;
            }
        }
        Text3dModelBuilder.ApplyTransformation(entity, transformation);
        Text3dModelBuilder.SyncComponents(entity, modelComponent, ref lastComponents, components);
        Text3dModelBuilder.SyncChildren(entity, ref lastChildren, children);
        modelComponent.IsShadowCaster = isShadowCaster;
        modelComponent.Enabled = enabled;
        entity.Name = name;
        output = entity;
    }

    public void Dispose() => services.Dispose();
}
