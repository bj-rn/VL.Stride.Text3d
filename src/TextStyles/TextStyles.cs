// The ten per-range text style nodes (category Stride.Text3d.Advanced.TextStyles),
// replacing the formerly patched ITextStyle implementations. Each node caches one
// style instance and returns it as ITextStyle for FontAndParagraph.ApplyStyles.

using System.Runtime.InteropServices;
using VL.Core.Import;
using VL.Lib.Text;
using VL.Stride.Text3d.Core;
using VL.Stride.Text3d.Interop;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using SilkFontFeature = Silk.NET.DirectWrite.FontFeature;
using SilkFontFeatureTag = Silk.NET.DirectWrite.FontFeatureTag;
using SilkFontStretch = Silk.NET.DirectWrite.FontStretch;
using SilkFontStyle = Silk.NET.DirectWrite.FontStyle;
using SilkFontWeight = Silk.NET.DirectWrite.FontWeight;
using IDWriteTextLayout1 = Silk.NET.DirectWrite.IDWriteTextLayout1;
using IDWriteTypography = Silk.NET.DirectWrite.IDWriteTypography;
using FontFeatureTagEnum = VL.Stride.Text3d.Enums.FontFeatureTag;
using FontStretchEnum = VL.Stride.Text3d.Enums.FontStretch;
using FontStyleEnum = VL.Stride.Text3d.Enums.FontStyle;
using FontWeightEnum = VL.Stride.Text3d.Enums.FontWeight;

namespace VL.Stride.Text3d.TextStyles;

internal static class LayoutInterop
{
    private static readonly Guid IID_IDWriteTextLayout1 = new("9064d822-80a7-465c-a986-df65f78b8feb");

    /// <summary>QIs to IDWriteTextLayout1; caller must Marshal.Release the returned pointer.</summary>
    public static unsafe IDWriteTextLayout1* GetLayout1(TextLayoutHandle layout)
        => (IDWriteTextLayout1*)layout.QueryInterface(IID_IDWriteTextLayout1);
}

/// <summary>Sets the font family for a text range.</summary>
[ProcessNode]
public class FontFamily
{
    private sealed class Style : TextStyleBase
    {
        public string Family = "Arial";
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
        {
            fixed (char* pFamily = Family)
                ThrowOnFailure(textLayout.Pointer->SetFontFamilyName(pFamily, Range));
        }
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0, FontList? font = null, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        var family = font?.Value ?? "Arial";
        if (family != style.Family) { style.Family = family; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Sets the font size for a text range.</summary>
[ProcessNode]
public class FontSize
{
    private sealed class Style : TextStyleBase
    {
        public float Size = 32f;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetFontSize(Size, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0, float fontSize = 32f, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (fontSize != style.Size) { style.Size = fontSize; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Sets the font stretch for a text range.</summary>
[ProcessNode]
public class FontStretch
{
    private sealed class Style : TextStyleBase
    {
        public FontStretchEnum Stretch = FontStretchEnum.Normal;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetFontStretch((SilkFontStretch)Stretch, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0,
        FontStretchEnum fontStretch = FontStretchEnum.Normal, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (fontStretch != style.Stretch) { style.Stretch = fontStretch; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Sets the font style (normal, italic, oblique) for a text range.</summary>
[ProcessNode]
public class FontStyle
{
    private sealed class Style : TextStyleBase
    {
        public FontStyleEnum Value = FontStyleEnum.Normal;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetFontStyle((SilkFontStyle)Value, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0,
        FontStyleEnum fontStyle = FontStyleEnum.Normal, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (fontStyle != style.Value) { style.Value = fontStyle; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Sets the font weight for a text range.</summary>
[ProcessNode]
public class FontWeight
{
    private sealed class Style : TextStyleBase
    {
        public FontWeightEnum Weight = FontWeightEnum.Normal;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetFontWeight((SilkFontWeight)Weight, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0,
        FontWeightEnum fontWeight = FontWeightEnum.Bold, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (fontWeight != style.Weight) { style.Weight = fontWeight; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Strikes through a text range.</summary>
[ProcessNode]
public class StrikeThrough
{
    private sealed class Style : TextStyleBase
    {
        public bool Value = true;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetStrikethrough(Value, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0, bool strikeThrough = true, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (strikeThrough != style.Value) { style.Value = strikeThrough; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Underlines a text range.</summary>
[ProcessNode]
public class Underline
{
    private sealed class Style : TextStyleBase
    {
        public bool Value = true;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
            => ThrowOnFailure(textLayout.Pointer->SetUnderline(Value, Range));
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0, bool underline = true, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (underline != style.Value) { style.Value = underline; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Enables pair kerning for a text range.</summary>
[ProcessNode]
public class PairKerning
{
    private sealed class Style : TextStyleBase
    {
        public bool Value = true;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
        {
            var layout1 = LayoutInterop.GetLayout1(textLayout);
            try { ThrowOnFailure(layout1->SetPairKerning(Value, Range)); }
            finally { Marshal.Release((nint)layout1); }
        }
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0, bool pairKerning = true, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (pairKerning != style.Value) { style.Value = pairKerning; style.BumpVersion(); }
        return style;
    }
}

/// <summary>Sets leading/trailing character spacing and minimum advance width for a text range.</summary>
[ProcessNode]
public class CharacterSpacing
{
    private sealed class Style : TextStyleBase
    {
        public float Leading;
        public float Trailing;
        public float MinimumAdvanceWidth;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
        {
            var layout1 = LayoutInterop.GetLayout1(textLayout);
            try { ThrowOnFailure(layout1->SetCharacterSpacing(Leading, Trailing, MinimumAdvanceWidth, Range)); }
            finally { Marshal.Release((nint)layout1); }
        }
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0,
        float leadingSpacing = 0f, float trailingSpacing = 0f, float minimumAdvanceWidth = 0f, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (leadingSpacing != style.Leading || trailingSpacing != style.Trailing
            || minimumAdvanceWidth != style.MinimumAdvanceWidth)
        {
            style.Leading = leadingSpacing;
            style.Trailing = trailingSpacing;
            style.MinimumAdvanceWidth = minimumAdvanceWidth;
            style.BumpVersion();
        }
        return style;
    }
}

/// <summary>Applies an OpenType typography feature to a text range.</summary>
[ProcessNode]
public class Typography
{
    private sealed class Style : TextStyleBase
    {
        public FontFeatureTagEnum Tag = FontFeatureTagEnum.Default;
        public int Parameter = 1;
        protected override unsafe void ApplyCore(TextLayoutHandle textLayout)
        {
            IDWriteTypography* typography = null;
            ThrowOnFailure(Native.DWriteFactory->CreateTypography(&typography));
            try
            {
                ThrowOnFailure(typography->AddFontFeature(new SilkFontFeature
                {
                    NameTag = (SilkFontFeatureTag)Tag,
                    Parameter = (uint)Math.Max(0, Parameter),
                }));
                ThrowOnFailure(textLayout.Pointer->SetTypography(typography, Range));
            }
            finally
            {
                typography->Release();
            }
        }
    }

    private readonly Style style = new();

    public ITextStyle Update(int startPosition = 0, int length = 0,
        FontFeatureTagEnum fontFeature = FontFeatureTagEnum.Default, int parameter = 1, bool enabled = true)
    {
        style.SetCommon(startPosition, length, enabled);
        if (fontFeature != style.Tag || parameter != style.Parameter)
        {
            style.Tag = fontFeature;
            style.Parameter = parameter;
            style.BumpVersion();
        }
        return style;
    }
}
