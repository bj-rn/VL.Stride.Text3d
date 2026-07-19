# VL.Stride.Text3d

Set of nodes to create and render (extruded) 3d text in VL.Stride.

Since version 2.0 the library is powered by [Silk.NET](https://github.com/dotnet/Silk.NET)
(Direct2D/DirectWrite bindings) instead of the discontinued SharpDX, and all nodes are
implemented in C#.

The extrusion geometry is based on [Extruder.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/Extruder.cs), [ExtrudingSink.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/ExtrudingSink.cs) and [OutlineRenderer.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/OutlineRenderer.cs) that come with [dx11-vvvv](https://github.com/mrvux/dx11-vvvv) by
[Julien Vulliet aka mrvux](https://github.com/mrvux), ported to Silk.NET. His code is licensed under BSD 3, refer to [DX11-vvvv-License.md](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/DX11-vvvv-License.md) for details.

The library itself is released under [MIT license](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/LICENSE).

## Requirements

Version 2.x requires **vvvv gamma 7.4 or later** (net8.0, Stride 4.2). For older vvvv
versions use version 1.0.2 of this package.

## Using the library

In order to use this library with VL you have to install the nuget that is available via nuget.org. For information on how to use nugets with VL, see [Managing Nugets](https://thegraybook.vvvv.org/reference/hde/managing-nugets.html) in the VL documentation. As described there you go to the commandline and then type:

For vvvv gamma 7.4+

    nuget install VL.Stride.Text3d

For 5.0

    nuget install VL.Stride.Text3d -version 1.0.2

For 2021.4

    nuget install VL.Stride.Text3d -version 0.4.0

Try it with vvvv, the visual live-programming environment for .NET
Download: http://visualprogramming.net

## Breaking changes in 2.0

- Requires vvvv gamma ≥ 7.4 (net8.0); the SharpDX dependency is gone.
- All node definitions moved from the VL document into the C# assembly. Existing
  patches will show the Text3d nodes red once — re-pick them via the node browser
  (names, categories and pins are unchanged).
- The enum types (TextAlignment, FontWeight, …) are now defined by the library itself.
  Member names are unchanged, but enum IOBoxes must be re-created once.
- The two advanced overloads are now named `Text3d (Advanced)` and
  `Text3dMesh (Advanced)` (they take a FontAndParagraph).
- `SetBasicFontPorperties` was renamed to `SetBasicFontProperties`.
- Removed: `Text3dMesh (Async)` (was obsolete) and the internal `Services` node.
- The advanced nodes no longer accept a raw SharpDX `TextLayout`; build layouts with
  `FontAndParagraph`.
- Empty text no longer throws — it yields an empty mesh.

---
### License

### [MIT](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/LICENSE)
---
[Support me on Ko-fi](https://ko-fi.com/Q5Q61EQB8X)
