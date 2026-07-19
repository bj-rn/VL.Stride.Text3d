// Library-owned public enums replacing the former SharpDX.DirectWrite enum aliases.
// Member names match the old SharpDX names (so re-patched IOBoxes read the same);
// numeric values are the canonical DWRITE_* values and are cast directly to the
// Silk.NET.DirectWrite enums at the interop boundary.
//
// FontFeatureTag (large FourCC enum, used by the Typography text style) is defined in
// FontFeatureTag.cs. ExtrudeOrigin at the end of this file is library-specific (2.2).

namespace VL.Stride.Text3d.Enums;

/// <summary>Alignment of paragraph text along the reading direction axis (DWRITE_TEXT_ALIGNMENT).</summary>
public enum TextAlignment
{
    /// <summary>Text is aligned to the leading edge of the layout box (left for left-to-right text).</summary>
    Leading = 0,
    /// <summary>Text is aligned to the trailing edge of the layout box (right for left-to-right text).</summary>
    Trailing = 1,
    /// <summary>Text is centered within the layout box.</summary>
    Center = 2,
    /// <summary>Text is spread to fill the width of the layout box.</summary>
    Justified = 3,
}

/// <summary>Alignment option of a paragraph relative to the layout box's top and bottom edge (DWRITE_PARAGRAPH_ALIGNMENT).</summary>
public enum ParagraphAlignment
{
    /// <summary>The first line is aligned to the flow's beginning edge (top for top-to-bottom flow).</summary>
    Near = 0,
    /// <summary>The last line is aligned to the flow's ending edge (bottom for top-to-bottom flow).</summary>
    Far = 1,
    /// <summary>Lines are centered between the flow's beginning and ending edge.</summary>
    Center = 2,
}

/// <summary>Word wrapping in multiline paragraphs (DWRITE_WORD_WRAPPING).</summary>
public enum WordWrapping
{
    /// <summary>Words are broken across lines to keep text within the layout box.</summary>
    Wrap = 0,
    /// <summary>Words may overflow the layout box; text only breaks at explicit line breaks.</summary>
    NoWrap = 1,
    /// <summary>Like Wrap, but a word wider than the layout box is broken mid-word.</summary>
    EmergencyBreak = 2,
    /// <summary>Only whole words wrap; a too-wide word overflows instead of breaking.</summary>
    WholeWord = 3,
    /// <summary>Text may wrap after any character.</summary>
    Character = 4,
}

/// <summary>Font stretch as a percentage of normal width (DWRITE_FONT_STRETCH).</summary>
public enum FontStretch
{
    /// <summary>No stretch information is available.</summary>
    Undefined = 0,
    /// <summary>50% of normal width.</summary>
    UltraCondensed = 1,
    /// <summary>62.5% of normal width.</summary>
    ExtraCondensed = 2,
    /// <summary>75% of normal width.</summary>
    Condensed = 3,
    /// <summary>87.5% of normal width.</summary>
    SemiCondensed = 4,
    /// <summary>Normal width (100%).</summary>
    Normal = 5,
    /// <summary>Normal width (100%); alias of Normal.</summary>
    Medium = 5,
    /// <summary>112.5% of normal width.</summary>
    SemiExpanded = 6,
    /// <summary>125% of normal width.</summary>
    Expanded = 7,
    /// <summary>150% of normal width.</summary>
    ExtraExpanded = 8,
    /// <summary>200% of normal width.</summary>
    UltraExpanded = 9,
}

/// <summary>Font slope style: normal, italic or oblique (DWRITE_FONT_STYLE).</summary>
public enum FontStyle
{
    /// <summary>Upright glyphs.</summary>
    Normal = 0,
    /// <summary>Upright glyphs artificially slanted.</summary>
    Oblique = 1,
    /// <summary>The font's true italic glyph forms.</summary>
    Italic = 2,
}

/// <summary>Font weight from thin to black (DWRITE_FONT_WEIGHT).</summary>
public enum FontWeight
{
    /// <summary>Thin (100).</summary>
    Thin = 100,
    /// <summary>Extra light (200).</summary>
    ExtraLight = 200,
    /// <summary>Ultra light (200); alias of ExtraLight.</summary>
    UltraLight = 200,
    /// <summary>Light (300).</summary>
    Light = 300,
    /// <summary>Semi light (350).</summary>
    SemiLight = 350,
    /// <summary>Normal (400).</summary>
    Normal = 400,
    /// <summary>Regular (400); alias of Normal.</summary>
    Regular = 400,
    /// <summary>Medium (500).</summary>
    Medium = 500,
    /// <summary>Demi bold (600).</summary>
    DemiBold = 600,
    /// <summary>Semi bold (600); alias of DemiBold.</summary>
    SemiBold = 600,
    /// <summary>Bold (700).</summary>
    Bold = 700,
    /// <summary>Extra bold (800).</summary>
    ExtraBold = 800,
    /// <summary>Ultra bold (800); alias of ExtraBold.</summary>
    UltraBold = 800,
    /// <summary>Black (900).</summary>
    Black = 900,
    /// <summary>Heavy (900); alias of Black.</summary>
    Heavy = 900,
    /// <summary>Extra black (950).</summary>
    ExtraBlack = 950,
    /// <summary>Ultra black (950); alias of ExtraBlack.</summary>
    UltraBlack = 950,
}

/// <summary>Method used for line spacing in a text layout (DWRITE_LINE_SPACING_METHOD).</summary>
public enum LineSpacingMethod
{
    /// <summary>Line spacing depends solely on the content.</summary>
    Default = 0,
    /// <summary>Uniform line spacing regardless of content; Line Spacing and Baseline are used.</summary>
    Uniform = 1,
    /// <summary>Line spacing and baseline distance proportional to the content's computed values.</summary>
    Proportional = 2,
}

/// <summary>Direction for how reading progresses (DWRITE_READING_DIRECTION).</summary>
public enum ReadingDirection
{
    /// <summary>Reading progresses left to right.</summary>
    LeftToRight = 0,
    /// <summary>Reading progresses right to left.</summary>
    RightToLeft = 1,
    /// <summary>Reading progresses top to bottom.</summary>
    TopToBottom = 2,
    /// <summary>Reading progresses bottom to top.</summary>
    BottomToTop = 3,
}

/// <summary>Direction for how lines of text are placed relative to one another (DWRITE_FLOW_DIRECTION).</summary>
public enum FlowDirection
{
    /// <summary>Lines flow top to bottom.</summary>
    TopToBottom = 0,
    /// <summary>Lines flow bottom to top.</summary>
    BottomToTop = 1,
    /// <summary>Lines flow left to right.</summary>
    LeftToRight = 2,
    /// <summary>Lines flow right to left.</summary>
    RightToLeft = 3,
}

/// <summary>Text granularity used to trim text overflowing the layout box (DWRITE_TRIMMING_GRANULARITY).</summary>
public enum TrimmingGranularity
{
    /// <summary>No trimming; text overflows the layout box.</summary>
    None = 0,
    /// <summary>Text is trimmed at a character boundary.</summary>
    Character = 1,
    /// <summary>Text is trimmed at a word boundary.</summary>
    Word = 2,
}

/// <summary>How to align glyphs to the margin (DWRITE_OPTICAL_ALIGNMENT).</summary>
public enum OpticalAlignment
{
    /// <summary>Default alignment respecting the glyphs' side bearings.</summary>
    None = 0,
    /// <summary>Glyph edges align optically to the margins, ignoring side bearings.</summary>
    NoSideBearings = 1,
}

/// <summary>Orientation of glyphs when a vertical reading direction is used (DWRITE_VERTICAL_GLYPH_ORIENTATION).</summary>
public enum VerticalGlyphOrientation
{
    /// <summary>Latin script rotates 90° clockwise in vertical flow; ideographs stay upright.</summary>
    Default = 0,
    /// <summary>All glyphs stay upright (stacked) in vertical flow.</summary>
    Stacked = 1,
}

/// <summary>Where the extruded mesh sits relative to Z = 0 (library-specific, since 2.2).</summary>
public enum ExtrudeOrigin
{
    /// <summary>The extrusion is centered on Z = 0 (from +amount/2 to -amount/2).</summary>
    Center = 0,
    /// <summary>The front face sits at Z = 0; the mesh extends backwards (negative Z).</summary>
    Front = 1,
    /// <summary>The back face sits at Z = 0; the mesh extends forwards (positive Z).</summary>
    Back = 2,
}
