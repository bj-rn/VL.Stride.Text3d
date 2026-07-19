// C# reimplementation of the formerly patched FontAndParagraph class: a fluent,
// change-tracked builder of a DirectWrite TextFormat + TextLayout. Consumed by the
// advanced Text3d/Text3dMesh nodes, the TextStyles and the metrics nodes.
//
// The layout is rebuilt lazily in GetTextLayout()/GetTextFormat() when any setting
// (or any applied style) changed — mirroring the Cache regions of the old patch.
// GetTextLayout returns the same handle instance while clean, so consumers can use
// reference identity as their change signal.

using System.Runtime.InteropServices;
using VL.Core.Import;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using SilkFlowDirection = Silk.NET.DirectWrite.FlowDirection;
using SilkLineSpacingMethod = Silk.NET.DirectWrite.LineSpacingMethod;
using SilkOpticalAlignment = Silk.NET.DirectWrite.OpticalAlignment;
using SilkParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using SilkReadingDirection = Silk.NET.DirectWrite.ReadingDirection;
using SilkTextAlignment = Silk.NET.DirectWrite.TextAlignment;
using SilkTrimming = Silk.NET.DirectWrite.Trimming;
using SilkTrimmingGranularity = Silk.NET.DirectWrite.TrimmingGranularity;
using SilkVerticalGlyphOrientation = Silk.NET.DirectWrite.VerticalGlyphOrientation;
using SilkWordWrapping = Silk.NET.DirectWrite.WordWrapping;
using SilkFontWeight = Silk.NET.DirectWrite.FontWeight;
using SilkFontStyle = Silk.NET.DirectWrite.FontStyle;
using SilkFontStretch = Silk.NET.DirectWrite.FontStretch;
using IDWriteInlineObject = Silk.NET.DirectWrite.IDWriteInlineObject;
using IDWriteTextLayout2 = Silk.NET.DirectWrite.IDWriteTextLayout2;
using FlowDirection = VL.Stride.Text3d.Enums.FlowDirection;
using FontStretch = VL.Stride.Text3d.Enums.FontStretch;
using FontStyle = VL.Stride.Text3d.Enums.FontStyle;
using FontWeight = VL.Stride.Text3d.Enums.FontWeight;
using LineSpacingMethod = VL.Stride.Text3d.Enums.LineSpacingMethod;
using OpticalAlignment = VL.Stride.Text3d.Enums.OpticalAlignment;
using ParagraphAlignment = VL.Stride.Text3d.Enums.ParagraphAlignment;
using ReadingDirection = VL.Stride.Text3d.Enums.ReadingDirection;
using TextAlignment = VL.Stride.Text3d.Enums.TextAlignment;
using TrimmingGranularity = VL.Stride.Text3d.Enums.TrimmingGranularity;
using VerticalGlyphOrientation = VL.Stride.Text3d.Enums.VerticalGlyphOrientation;
using WordWrapping = VL.Stride.Text3d.Enums.WordWrapping;

namespace VL.Stride.Text3d.Nodes;

/// <summary>
/// Builds a DirectWrite text layout from font, paragraph and text settings.
/// Feed it into Text3d (Advanced), Text3dMesh (Advanced), TextLayoutMetrics or LineMetrics.
/// </summary>
[ProcessNode(HasStateOutput = true)]
public sealed unsafe class FontAndParagraph : IDisposable
{
    private static readonly Guid IID_IDWriteTextLayout2 = new("1093c18f-8d5e-43f0-b064-0917311b525e");

    // Font (TextFormat) settings
    private string fontFamily = "Arial";
    private FontWeight fontWeight = FontWeight.Normal;
    private FontStyle fontStyle = FontStyle.Normal;
    private FontStretch fontStretch = FontStretch.Normal;
    private float fontSize = 32f;

    // Paragraph / layout settings
    private TextAlignment textAlignment = TextAlignment.Leading;
    private ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near;
    private WordWrapping wordWrapping = WordWrapping.NoWrap;
    private bool lastLineWrapping = true;
    private LineSpacingMethod lineSpacingMethod = LineSpacingMethod.Default;
    private float lineSpacing;
    private float baseline;
    private ReadingDirection readingDirection = ReadingDirection.LeftToRight;
    private FlowDirection flowDirection = FlowDirection.TopToBottom;
    private TrimmingGranularity trimmingGranularity = TrimmingGranularity.None;
    private string delimiter = "";
    private int delimiterCount;
    private bool ellipsisTrimmingSign;
    private OpticalAlignment opticalAlignment = OpticalAlignment.None;
    private VerticalGlyphOrientation verticalGlyphOrientation = VerticalGlyphOrientation.Default;

    // Text
    private string text = "";
    private float maxWidth;
    private float maxHeight = 32f;

    // Styles
    private IReadOnlyList<ITextStyle>? styles;
    private int appliedStylesSignature;

    private bool dirty = true;
    private int version;
    private TextFormatHandle? formatHandle;
    private TextLayoutHandle? layoutHandle;

    /// <summary>Sets font family, weight, style, stretch and size.</summary>
    /// <param name="font">The name of the font family.</param>
    /// <param name="fontWeight">The weight (thickness of the strokes) of the font.</param>
    /// <param name="fontStyle">The style (normal, italic or oblique) of the font.</param>
    /// <param name="fontStretch">The stretch (width relative to normal) of the font.</param>
    /// <param name="fontSize">The logical size of the font in DIP units (1 DIP = 1/96 inch).</param>
    public void SetBasicFontProperties(FontList? font = null, FontWeight fontWeight = FontWeight.Normal,
        FontStyle fontStyle = FontStyle.Normal, FontStretch fontStretch = FontStretch.Normal,
        float fontSize = 32f)
    {
        Set(ref fontFamily, font?.Value ?? "Arial");
        Set(ref this.fontWeight, fontWeight);
        Set(ref this.fontStyle, fontStyle);
        Set(ref this.fontStretch, fontStretch);
        Set(ref this.fontSize, fontSize);
    }

    /// <summary>Sets the alignment of text along the reading direction axis.</summary>
    /// <param name="textAlignment">The alignment of paragraph text relative to the leading and trailing edge of the layout box.</param>
    public void SetTextAlignment(TextAlignment textAlignment = TextAlignment.Leading)
        => Set(ref this.textAlignment, textAlignment);

    /// <summary>Sets the alignment of the paragraph relative to the layout box.</summary>
    /// <param name="paragraphAlignment">The alignment of the paragraph relative to the top and bottom edge of the layout box.</param>
    public void SetParagraphAlignment(ParagraphAlignment paragraphAlignment = ParagraphAlignment.Near)
        => Set(ref this.paragraphAlignment, paragraphAlignment);

    /// <summary>Sets word wrapping (and whether the last line wraps).</summary>
    /// <param name="wordWrapping">The word wrapping option applied to the whole layout.</param>
    /// <param name="lastLineWrapping">Whether the last word on the last line is wrapped.</param>
    public void SetWordWrapping(WordWrapping wordWrapping = WordWrapping.NoWrap, bool lastLineWrapping = true)
    {
        Set(ref this.wordWrapping, wordWrapping);
        Set(ref this.lastLineWrapping, lastLineWrapping);
    }

    /// <summary>Sets line spacing method, line height and baseline distance.</summary>
    /// <param name="method">How line height is determined (from content, uniform, or proportional).</param>
    /// <param name="lineSpacing">The line height, the distance from one baseline to the next (used by the Uniform method).</param>
    /// <param name="baseline">The distance from the top of the line to its baseline; a reasonable ratio is 80% of the line spacing.</param>
    public void SetLineSpacing(LineSpacingMethod method = LineSpacingMethod.Default,
        float lineSpacing = 0f, float baseline = 0f)
    {
        Set(ref lineSpacingMethod, method);
        Set(ref this.lineSpacing, lineSpacing);
        Set(ref this.baseline, baseline);
    }

    /// <summary>Sets the direction reading progresses in.</summary>
    /// <param name="readingDirection">The text reading direction (for example right-to-left) set for the paragraph.</param>
    public void SetReadingDirection(ReadingDirection readingDirection = ReadingDirection.LeftToRight)
        => Set(ref this.readingDirection, readingDirection);

    /// <summary>Sets the direction lines are placed in.</summary>
    /// <param name="flowDirection">The direction in which lines of text are placed relative to one another.</param>
    public void SetFlowDirection(FlowDirection flowDirection = FlowDirection.TopToBottom)
        => Set(ref this.flowDirection, flowDirection);

    /// <summary>Sets the text of the layout.</summary>
    /// <param name="text">The string the text layout is created from.</param>
    public void SetText(string text = "")
        => Set(ref this.text, text ?? "");

    /// <summary>Sets the layout box width (0 = unconstrained).</summary>
    /// <param name="maxWidth">The maximum width of the layout box, in DIP units; alignment, wrapping and trimming are computed against it.</param>
    public void SetMaxWidth(float maxWidth = 0f)
        => Set(ref this.maxWidth, maxWidth);

    /// <summary>Sets the layout box height.</summary>
    /// <param name="maxHeight">The maximum height of the layout box, in DIP units; paragraph alignment is computed against it.</param>
    public void SetMaxHeight(float maxHeight = 32f)
        => Set(ref this.maxHeight, maxHeight);

    /// <summary>Sets trimming for text overflowing the layout box.</summary>
    /// <param name="granularity">The text granularity (character or word) at which trimming applies.</param>
    /// <param name="delimiter">A character that signals the beginning of the portion of text to preserve; most useful for path ellipsis, where the delimiter would be a slash. Empty for none.</param>
    /// <param name="delimiterCount">How many occurrences of the delimiter to step back from the end.</param>
    /// <param name="ellipsisTrimmingSign">Whether an ellipsis (…) sign is shown at the trimming position.</param>
    public void SetTrimming(TrimmingGranularity granularity = TrimmingGranularity.None,
        string delimiter = "", int delimiterCount = 0, bool ellipsisTrimmingSign = false)
    {
        Set(ref trimmingGranularity, granularity);
        Set(ref this.delimiter, delimiter ?? "");
        Set(ref this.delimiterCount, delimiterCount);
        Set(ref this.ellipsisTrimmingSign, ellipsisTrimmingSign);
    }

    /// <summary>Sets how glyphs align to the margins.</summary>
    /// <param name="opticalAlignment">How glyphs align to the margins: default, or ignoring the glyphs' side bearings so their edges align optically.</param>
    public void SetOpticalAlignment(OpticalAlignment opticalAlignment = OpticalAlignment.None)
        => Set(ref this.opticalAlignment, opticalAlignment);

    /// <summary>Sets the orientation of glyphs when a vertical reading direction is used.</summary>
    /// <param name="verticalGlyphOrientation">The preferred orientation of glyphs in a vertical reading direction: rotated per script defaults, or all upright (stacked).</param>
    public void SetVerticalGlyphOrientation(VerticalGlyphOrientation verticalGlyphOrientation = VerticalGlyphOrientation.Default)
        => Set(ref this.verticalGlyphOrientation, verticalGlyphOrientation);

    /// <summary>Applies per-range text styles (see the TextStyles category).</summary>
    /// <param name="styles">The text styles applied to the layout, in order; later styles override earlier ones on overlapping ranges.</param>
    public void ApplyStyles(IReadOnlyList<ITextStyle>? styles)
    {
        this.styles = styles;
    }

    /// <summary>True when the next GetTextLayout/GetTextFormat call will rebuild the layout.</summary>
    public bool GetIsDirty() => dirty || StylesSignature() != appliedStylesSignature;

    /// <summary>Increments whenever the layout is rebuilt (change signal for consumers).</summary>
    public int GetVersion() => version;

    /// <summary>The built text layout.</summary>
    public TextLayoutHandle GetTextLayout()
    {
        EnsureBuilt();
        return layoutHandle!;
    }

    /// <summary>The built text format.</summary>
    public TextFormatHandle GetTextFormat()
    {
        EnsureBuilt();
        return formatHandle!;
    }

    public void Dispose()
    {
        layoutHandle?.Dispose();
        layoutHandle = null;
        formatHandle?.Dispose();
        formatHandle = null;
    }

    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            dirty = true;
        }
    }

    private int StylesSignature()
    {
        if (styles == null || styles.Count == 0)
            return 0;
        var hash = new HashCode();
        hash.Add(styles.Count);
        for (int i = 0; i < styles.Count; i++)
        {
            var s = styles[i];
            hash.Add(s?.GetHashCode() ?? 0);
            hash.Add(s?.Version ?? 0);
        }
        return hash.ToHashCode();
    }

    private void EnsureBuilt()
    {
        int stylesSignature = StylesSignature();
        if (!dirty && stylesSignature == appliedStylesSignature && layoutHandle != null)
            return;

        layoutHandle?.Dispose();
        layoutHandle = null;
        formatHandle?.Dispose();
        formatHandle = null;

        var fmt = Native.CreateTextFormat(fontFamily, fontSize,
            (SilkFontWeight)fontWeight, (SilkFontStyle)fontStyle, (SilkFontStretch)fontStretch);
        var layout = Native.CreateTextLayout(text, fmt, maxWidth, maxHeight);

        // Layout-level settings (the IDWriteTextFormat setters are part of the layout vtable)
        ThrowOnFailure(layout->SetTextAlignment((SilkTextAlignment)textAlignment));
        ThrowOnFailure(layout->SetParagraphAlignment((SilkParagraphAlignment)paragraphAlignment));
        ThrowOnFailure(layout->SetWordWrapping((SilkWordWrapping)wordWrapping));
        ThrowOnFailure(layout->SetReadingDirection((SilkReadingDirection)readingDirection));
        ThrowOnFailure(layout->SetFlowDirection((SilkFlowDirection)flowDirection));
        ThrowOnFailure(layout->SetLineSpacing((SilkLineSpacingMethod)lineSpacingMethod, lineSpacing, baseline));

        // Trimming (optionally with an ellipsis sign built from the format)
        var trimming = new SilkTrimming
        {
            Granularity = (SilkTrimmingGranularity)trimmingGranularity,
            Delimiter = delimiter.Length > 0 ? (uint)char.ConvertToUtf32(delimiter, 0) : 0u,
            DelimiterCount = (uint)Math.Max(0, delimiterCount),
        };
        IDWriteInlineObject* sign = null;
        if (ellipsisTrimmingSign)
            ThrowOnFailure(Native.DWriteFactory->CreateEllipsisTrimmingSign(fmt, &sign));
        ThrowOnFailure(layout->SetTrimming(&trimming, sign));
        if (sign != null)
            sign->Release();

        // IDWriteTextLayout2-level settings (Windows 8.1+)
        Guid iid2 = IID_IDWriteTextLayout2;
        if (Marshal.QueryInterface((nint)layout, ref iid2, out nint l2ptr) == S_OK)
        {
            var l2 = (IDWriteTextLayout2*)l2ptr;
            ThrowOnFailure(l2->SetLastLineWrapping(lastLineWrapping));
            ThrowOnFailure(l2->SetOpticalAlignment((SilkOpticalAlignment)opticalAlignment));
            ThrowOnFailure(l2->SetVerticalGlyphOrientation((SilkVerticalGlyphOrientation)verticalGlyphOrientation));
            Marshal.Release(l2ptr);
        }

        formatHandle = TextFormatHandle.FromPointer((nint)fmt, addRef: false);
        layoutHandle = TextLayoutHandle.FromPointer((nint)layout, addRef: false);

        // Per-range styles last, so they override the paragraph-wide settings
        if (styles != null)
        {
            for (int i = 0; i < styles.Count; i++)
                styles[i]?.Apply(layoutHandle);
        }

        appliedStylesSignature = stylesSignature;
        dirty = false;
        version++;
    }
}
