// Metrics readback nodes (category Stride.Text3d), replacing the formerly patched
// TextLayoutMetrics and LineMetrics definitions. Output pin sets mirror the old
// patches; values are re-read only when the layout changes (or Force Update is set).

using System.Runtime.InteropServices;
using VL.Core.Import;
using VL.Lib.Collections;
using VL.Stride.Text3d.Core;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using IDWriteTextLayout2 = Silk.NET.DirectWrite.IDWriteTextLayout2;
using SilkLineMetrics = Silk.NET.DirectWrite.LineMetrics;
using TextMetrics1 = Silk.NET.DirectWrite.TextMetrics1;

namespace VL.Stride.Text3d.Nodes;

/// <summary>Reads the overall metrics of a FontAndParagraph's text layout.</summary>
[ProcessNode]
public sealed unsafe class TextLayoutMetrics
{
    private static readonly Guid IID_IDWriteTextLayout2 = new("1093c18f-8d5e-43f0-b064-0917311b525e");

    private TextLayoutHandle? lastLayout;
    private int lastVersion = -1;
    private TextMetrics1 metrics;

    /// <param name="left">The left-most point of formatted text relative to the layout box, excluding any glyph overhang.</param>
    /// <param name="top">The top-most point of formatted text relative to the layout box, excluding any glyph overhang.</param>
    /// <param name="width">The width of the formatted text, ignoring trailing whitespace at the end of each line.</param>
    /// <param name="height">The height of the formatted text; for an empty string this is the height of the default font.</param>
    /// <param name="heightIncludingTrailingWhitespace">The height of the formatted text, taking trailing whitespace into account. Differs from Height only for vertical reading directions; for horizontal text the trailing whitespace extends the width instead.</param>
    /// <param name="widthIncludingTrailingWhitespace">The width of the formatted text, taking the trailing whitespace at the end of each line into account. Differs from Width only for horizontal reading directions; for vertical text the trailing whitespace extends the height instead.</param>
    /// <param name="layoutWidth">The initial width given to the layout (Max Width).</param>
    /// <param name="layoutHeight">The initial height given to the layout (Max Height).</param>
    /// <param name="lineCount">Total number of lines.</param>
    /// <param name="maxBidiReorderingDepth">The maximum bidirectional reordering count of any line of text; 1 if there is no bidirectional text or no text at all.</param>
    /// <param name="input">The FontAndParagraph whose text layout is measured.</param>
    /// <param name="forceUpdate">Re-reads the metrics even when the layout did not change.</param>
    public void Update(
        out float left, out float top, out float width, out float height,
        out float heightIncludingTrailingWhitespace, out float widthIncludingTrailingWhitespace,
        out float layoutWidth, out float layoutHeight,
        out int lineCount, out int maxBidiReorderingDepth,
        FontAndParagraph? input = null, bool forceUpdate = false)
    {
        var layout = input?.GetTextLayout();
        int version = input?.GetVersion() ?? -1;
        if (layout != null && (forceUpdate || !ReferenceEquals(layout, lastLayout) || version != lastVersion))
        {
            Guid iid = IID_IDWriteTextLayout2;
            nint l2ptr = 0;
            if (Marshal.QueryInterface((nint)layout.Pointer, ref iid, out l2ptr) == S_OK)
            {
                try
                {
                    // Silk.NET 2.22 does not bind IDWriteTextLayout2::GetMetrics(DWRITE_TEXT_METRICS1*):
                    // its name collides with the inherited GetMetrics and the generator dropped it, so
                    // the struct's GetMetrics dispatches vtable slot 60 (the base method), which never
                    // writes HeightIncludingTrailingWhitespace. The dropped method's slot 71 is unused
                    // in the binding (GetCharacterSpacing = 70, SetVerticalGlyphOrientation = 72);
                    // call it directly. Recheck when upgrading Silk.NET.
                    var l2 = (IDWriteTextLayout2*)l2ptr;
                    TextMetrics1 m = default;
                    ThrowOnFailure(((delegate* unmanaged[Stdcall]<IDWriteTextLayout2*, TextMetrics1*, int>)l2->LpVtbl[71])(l2, &m));
                    metrics = m;
                }
                finally
                {
                    Marshal.Release(l2ptr);
                }
            }
            lastLayout = layout;
            lastVersion = version;
        }
        else if (layout == null)
        {
            metrics = default;
            lastLayout = null;
            lastVersion = -1;
        }

        left = metrics.Left;
        top = metrics.Top;
        width = metrics.Width;
        height = metrics.Height;
        heightIncludingTrailingWhitespace = metrics.HeightIncludingTrailingWhitespace;
        widthIncludingTrailingWhitespace = metrics.WidthIncludingTrailingWhitespace;
        layoutWidth = metrics.LayoutWidth;
        layoutHeight = metrics.LayoutHeight;
        lineCount = (int)metrics.LineCount;
        maxBidiReorderingDepth = (int)metrics.MaxBidiReorderingDepth;
    }
}

/// <summary>Reads the per-line metrics of a FontAndParagraph's text layout.</summary>
[ProcessNode]
public sealed unsafe class LineMetrics
{
    private TextLayoutHandle? lastLayout;
    private int lastVersion = -1;

    private Spread<float> baseline = Spread<float>.Empty;
    private Spread<float> height = Spread<float>.Empty;
    private Spread<bool> isTrimmed = Spread<bool>.Empty;
    private Spread<int> length = Spread<int>.Empty;
    private Spread<int> newlineLength = Spread<int>.Empty;
    private Spread<int> trailingWhitespaceLength = Spread<int>.Empty;

    /// <param name="baseline">Per line: the distance from the top of the text line to its baseline.</param>
    /// <param name="height">Per line: the height of the text line.</param>
    /// <param name="isTrimmed">Per line: whether the line is trimmed.</param>
    /// <param name="length">Per line: the number of text positions, including trailing whitespace and newline characters.</param>
    /// <param name="newlineLength">Per line: the number of characters in the newline sequence at its end; 0 if the line was wrapped or is the end of the text.</param>
    /// <param name="trailingWhitespaceLength">Per line: the number of whitespace positions at its end (newline sequences count as whitespace).</param>
    /// <param name="input">The FontAndParagraph whose text layout is measured.</param>
    /// <param name="forceUpdate">Re-reads the metrics even when the layout did not change.</param>
    public void Update(
        out Spread<float> baseline, out Spread<float> height, out Spread<bool> isTrimmed,
        out Spread<int> length, out Spread<int> newlineLength, out Spread<int> trailingWhitespaceLength,
        FontAndParagraph? input = null, bool forceUpdate = false)
    {
        var layout = input?.GetTextLayout();
        int version = input?.GetVersion() ?? -1;
        if (layout != null && (forceUpdate || !ReferenceEquals(layout, lastLayout) || version != lastVersion))
        {
            ReadLineMetrics(layout);
            lastLayout = layout;
            lastVersion = version;
        }
        else if (layout == null && lastLayout != null)
        {
            this.baseline = Spread<float>.Empty;
            this.height = Spread<float>.Empty;
            this.isTrimmed = Spread<bool>.Empty;
            this.length = Spread<int>.Empty;
            this.newlineLength = Spread<int>.Empty;
            this.trailingWhitespaceLength = Spread<int>.Empty;
            lastLayout = null;
            lastVersion = -1;
        }

        baseline = this.baseline;
        height = this.height;
        isTrimmed = this.isTrimmed;
        length = this.length;
        newlineLength = this.newlineLength;
        trailingWhitespaceLength = this.trailingWhitespaceLength;
    }

    private void ReadLineMetrics(TextLayoutHandle layout)
    {
        uint count = 0;
        // First call intentionally undersized to obtain the actual line count.
        layout.Pointer->GetLineMetrics((SilkLineMetrics*)null, 0, &count);
        if (count == 0)
        {
            baseline = Spread<float>.Empty;
            height = Spread<float>.Empty;
            isTrimmed = Spread<bool>.Empty;
            length = Spread<int>.Empty;
            newlineLength = Spread<int>.Empty;
            trailingWhitespaceLength = Spread<int>.Empty;
            return;
        }

        var lines = new SilkLineMetrics[count];
        fixed (SilkLineMetrics* pLines = lines)
            ThrowOnFailure(layout.Pointer->GetLineMetrics(pLines, count, &count));

        var baselineB = new SpreadBuilder<float>((int)count);
        var heightB = new SpreadBuilder<float>((int)count);
        var isTrimmedB = new SpreadBuilder<bool>((int)count);
        var lengthB = new SpreadBuilder<int>((int)count);
        var newlineLengthB = new SpreadBuilder<int>((int)count);
        var trailingB = new SpreadBuilder<int>((int)count);

        for (uint i = 0; i < count; i++)
        {
            ref var line = ref lines[i];
            baselineB.Add(line.Baseline);
            heightB.Add(line.Height);
            isTrimmedB.Add(line.IsTrimmed);
            lengthB.Add((int)line.Length);
            newlineLengthB.Add((int)line.NewlineLength);
            trailingB.Add((int)line.TrailingWhitespaceLength);
        }

        baseline = baselineB.ToSpread();
        height = heightB.ToSpread();
        isTrimmed = isTrimmedB.ToSpread();
        length = lengthB.ToSpread();
        newlineLength = newlineLengthB.ToSpread();
        trailingWhitespaceLength = trailingB.ToSpread();
    }
}
