// Library-owned public enums replacing the former SharpDX.DirectWrite enum aliases.
// Member names match the old SharpDX names (so re-patched IOBoxes read the same);
// numeric values are the canonical DWRITE_* values and are cast directly to the
// Silk.NET.DirectWrite enums at the interop boundary.
//
// FontFeatureTag (large FourCC enum, used by the Typography text style) is defined in
// FontFeatureTag.cs.

namespace VL.Stride.Text3d.Enums;

/// <summary>Alignment of paragraph text along the reading direction axis (DWRITE_TEXT_ALIGNMENT).</summary>
public enum TextAlignment
{
    Leading = 0,
    Trailing = 1,
    Center = 2,
    Justified = 3,
}

/// <summary>Alignment option of a paragraph relative to the layout box's top and bottom edge (DWRITE_PARAGRAPH_ALIGNMENT).</summary>
public enum ParagraphAlignment
{
    Near = 0,
    Far = 1,
    Center = 2,
}

/// <summary>Word wrapping in multiline paragraphs (DWRITE_WORD_WRAPPING).</summary>
public enum WordWrapping
{
    Wrap = 0,
    NoWrap = 1,
    EmergencyBreak = 2,
    WholeWord = 3,
    Character = 4,
}

/// <summary>Font stretch as a percentage of normal width (DWRITE_FONT_STRETCH).</summary>
public enum FontStretch
{
    Undefined = 0,
    UltraCondensed = 1,
    ExtraCondensed = 2,
    Condensed = 3,
    SemiCondensed = 4,
    Normal = 5,
    Medium = 5,
    SemiExpanded = 6,
    Expanded = 7,
    ExtraExpanded = 8,
    UltraExpanded = 9,
}

/// <summary>Font slope style: normal, italic or oblique (DWRITE_FONT_STYLE).</summary>
public enum FontStyle
{
    Normal = 0,
    Oblique = 1,
    Italic = 2,
}

/// <summary>Font weight from thin to black (DWRITE_FONT_WEIGHT).</summary>
public enum FontWeight
{
    Thin = 100,
    ExtraLight = 200,
    UltraLight = 200,
    Light = 300,
    SemiLight = 350,
    Normal = 400,
    Regular = 400,
    Medium = 500,
    DemiBold = 600,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    UltraBold = 800,
    Black = 900,
    Heavy = 900,
    ExtraBlack = 950,
    UltraBlack = 950,
}

/// <summary>Method used for line spacing in a text layout (DWRITE_LINE_SPACING_METHOD).</summary>
public enum LineSpacingMethod
{
    Default = 0,
    Uniform = 1,
    Proportional = 2,
}

/// <summary>Direction for how reading progresses (DWRITE_READING_DIRECTION).</summary>
public enum ReadingDirection
{
    LeftToRight = 0,
    RightToLeft = 1,
    TopToBottom = 2,
    BottomToTop = 3,
}

/// <summary>Direction for how lines of text are placed relative to one another (DWRITE_FLOW_DIRECTION).</summary>
public enum FlowDirection
{
    TopToBottom = 0,
    BottomToTop = 1,
    LeftToRight = 2,
    RightToLeft = 3,
}

/// <summary>Text granularity used to trim text overflowing the layout box (DWRITE_TRIMMING_GRANULARITY).</summary>
public enum TrimmingGranularity
{
    None = 0,
    Character = 1,
    Word = 2,
}

/// <summary>How to align glyphs to the margin (DWRITE_OPTICAL_ALIGNMENT).</summary>
public enum OpticalAlignment
{
    None = 0,
    NoSideBearings = 1,
}

/// <summary>Orientation of glyphs when a vertical reading direction is used (DWRITE_VERTICAL_GLYPH_ORIENTATION).</summary>
public enum VerticalGlyphOrientation
{
    Default = 0,
    Stacked = 1,
}
