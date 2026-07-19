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
                    // Native IDWriteTextLayout2::GetMetrics writes a DWRITE_TEXT_METRICS1;
                    // the Silk binding types the parameter as the base TextMetrics.
                    TextMetrics1 m = default;
                    ThrowOnFailure(((IDWriteTextLayout2*)l2ptr)->GetMetrics((Silk.NET.DirectWrite.TextMetrics*)&m));
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
