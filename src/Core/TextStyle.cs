// ITextStyle and the shared range/enabled/versioning base for the TextStyles nodes.
// Lives in the unimported Core namespace; the user-facing style nodes are in
// VL.Stride.Text3d.TextStyles and return ITextStyle instances produced from these.

using TextRange = Silk.NET.DirectWrite.TextRange;

namespace VL.Stride.Text3d.Core;

/// <summary>A styling operation applied to a text range of a layout (see the TextStyles category).</summary>
public interface ITextStyle
{
    /// <summary>Increments whenever any setting of the style changes.</summary>
    int Version { get; }

    /// <summary>Applies the style to the given layout (no-op when disabled).</summary>
    void Apply(TextLayoutHandle textLayout);
}

public abstract class TextStyleBase : ITextStyle
{
    private int startPosition;
    private int length;
    private bool enabled = true;

    public int Version { get; private set; }

    protected bool Enabled => enabled;

    protected TextRange Range => new()
    {
        StartPosition = (uint)Math.Max(0, startPosition),
        Length = (uint)Math.Max(0, length),
    };

    /// <summary>Marks the style changed; called by the owning style node when a value changes.</summary>
    public void BumpVersion() => Version++;

    public void SetCommon(int startPosition, int length, bool enabled)
    {
        if (startPosition != this.startPosition || length != this.length || enabled != this.enabled)
        {
            this.startPosition = startPosition;
            this.length = length;
            this.enabled = enabled;
            BumpVersion();
        }
    }

    public void Apply(TextLayoutHandle textLayout)
    {
        if (enabled)
            ApplyCore(textLayout);
    }

    protected abstract void ApplyCore(TextLayoutHandle textLayout);
}
