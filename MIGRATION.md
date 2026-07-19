# Help patch migration guide (1.x → 2.0)

The two documents in `help/` still reference the node definitions that lived inside the
old `VL.Stride.Text3d.vl` plus the SharpDX enum aliases. Since 2.0 all nodes come from
the C# assembly, every one of those references shows up red and must be re-picked once
in the vvvv editor. This is deliberate: hand-editing the `NodeReference` XML for
C#-imported symbols is error-prone — the editor rewrites them authoritatively when you
re-pick a node.

## Procedure (per help file)

1. Launch vvvv 7.4 with this repository as a package repository:

       vvvv.exe --package-repositories "D:\_dev\_vl-libs" -o "<path to help patch>" --log

2. For every red node: double-click it, find the replacement per the table below,
   re-pick it. **Pin names are unchanged** (one exception: the old `FontSize` pin on the
   simple Text3d/Text3dMesh nodes is now `Font Size` per vvvv naming convention — that
   one link must be re-drawn), so links reconnect automatically.
3. Re-enter the values of enum IOBoxes (the enum *types* changed identity; the member
   names are the same, so read the old value off the red IOBox first).
4. Let the editor update the document dependencies on save (the old pinned
   VL.CoreLib/VL.Stride 2022.5 entries disappear; SharpDX entries must be gone).
5. Verify: no red/pink anywhere, patch renders, then save.

## Node mapping

| Old (in-document definition) | New (C# assembly) | Notes |
|---|---|---|
| Text3d (VL.Stride.Models, simple) | Text3d (VL.Stride.Models) | unchanged name/pins |
| Text3d (VL.Stride.Models, FontAndParagraph overload) | **Text3d (Advanced)** (VL.Stride.Models) | renamed |
| Text3dMesh (VL.Stride.Models.Meshes, simple) | Text3dMesh (VL.Stride.Models.Meshes) | unchanged |
| Text3dMesh (FontAndParagraph overload) | **Text3dMesh (Advanced)** | renamed |
| Text3dMesh (Async) | — | removed (was obsolete) |
| FontAndParagraph (VL.Stride.Text3d) + all Set* ops | FontAndParagraph + same ops | `SetBasicFontPorperties` → **SetBasicFontProperties** |
| TextStyles: FontFamily, FontSize, FontStretch, FontStyle, FontWeight, StrikeThrough, Underline, PairKerning, CharacterSpacing, Typography (VL.Stride.Text3d.TextStyles) | same names, same category | now ProcessNodes returning ITextStyle |
| TextLayoutMetrics / LineMetrics (VL.Stride.Text3d) | same names, same category | same output pins |
| Enums: WordWrapping, TextAlignment, ParagraphAlignment, FontStretch, FontStyle, FontWeight, LineSpacingMethod, ReadingDirection, FlowDirection, FontFeatureTag, OpticalAlignment, VerticalGlyphOrientation (VL.Stride.Text3d.Enums) | same names, same category | new type identity → re-create IOBoxes |
| Services (Internal) | — | removed (nodes get the Game via NodeContext) |

## Status

The help patches were re-linked and the compile tests in
`tests/VL.Stride.Text3d.Tests/VlDocumentTests.cs` are enabled and green.
