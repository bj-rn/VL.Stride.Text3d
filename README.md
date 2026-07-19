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

## Changes in 2.2

- New `Weld Vertices` pin on all mesh/model producing nodes (default off): welds
  identical vertices into an indexed mesh — visually lossless with roughly 2–3× smaller
  vertex buffers. Off by default because it changes the mesh topology, which per-face
  techniques relying on the plain triangle list may depend on.
- Async variants of all mesh nodes — `Text3dMesh (Async)`, `Text3dMesh (Advanced Async)`,
  `Text3dMeshes (Async)` and `Text3dMeshes (Advanced Async)`: geometry is computed on a
  background thread (an `In Progress` output reports activity), the last completed
  result stays available meanwhile, and rapid input changes are coalesced.
- New nodes `Text3dMeshes` and `Text3dMeshes (Advanced)`: output one mesh per glyph
  plus per-glyph transforms for typography animation. Each mesh's pivot sits on the
  baseline at the glyph's pen position. Spaces produce no mesh, ligatures may merge
  characters, and underline/strikethrough decorations are not included.
- New pins on the Text3d/Text3dMesh nodes (defaults reproduce the previous behavior
  exactly):
  - `Extrude Origin` (Center/Front/Back) — where the mesh sits relative to Z = 0.
  - `Flattening Tolerance` (default 0.1) — curve quality of the outlines; smaller
    values give finer curves and more vertices.
  - `Smoothing Angle` (in cycles, default 1/6 = 60°) — side-wall edges sharper than
    this stay hard, flatter ones are shaded smooth.
- The entity nodes regained the `Name` pin.
- All enum members now carry tooltips.

## Changes in 2.1

- Side-wall lighting corrected: the extrusion's side walls now use the true outward
  surface normal (a bug inherited from the original dx11-vvvv code mirrored it, so
  extruded surfaces shaded incorrectly). Silhouette, front/back caps and UVs are
  unchanged — only the shading of the extruded sides differs.

## Breaking changes in 2.0

- Requires vvvv gamma ≥ 7.4 (net8.0); the SharpDX dependency is gone.
- All node definitions moved from the VL document into the C# assembly. Existing
  patches will show the Text3d nodes red once, re-pick them via the node browser
  (names, categories and pins are unchanged).
- The enum types (TextAlignment, FontWeight, …) are now defined by the library itself.
  Member names are unchanged, but enum IOBoxes must be re-created once.
- The two advanced overloads are now named `Text3d (Advanced)` and
  `Text3dMesh (Advanced)` (they take a FontAndParagraph).
- `SetBasicFontPorperties` was renamed to `SetBasicFontProperties`.
- Removed: `Text3dMesh (Async)` (was obsolete) and the internal `Services` node.
- The advanced nodes no longer accept a raw SharpDX `TextLayout`; build layouts with
  `FontAndParagraph`.
- Empty text no longer throws, it yields an empty mesh.

## Maintenance notes

The 2.x package is version-coupled to what vvvv bundles:

| this library | vvvv gamma | Silk.NET | Stride |
|---|---|---|---|
| 2.x | 7.4 | 2.22.0 (exact pin) | 4.2.1.2487 |

When vvvv updates its bundled Silk.NET or Stride, this package needs a matching
release: bump `Silk.NET.Direct2D` (exact `[x.y.z]` pin) and the `Stride.*` versions in
`src/VL.Stride.Text3d.csproj` plus the nuspec dependency, rebuild, and run the test
suite. Two things to preserve when touching the interop:

- Never use Silk.NET's managed-`String` overloads for DirectWrite calls — they marshal
  the WCHAR parameters with the wrong encoding (silent font fallback, intermittent
  E_INVALIDARG). Always pass pinned UTF-16 `char*` (see `src/Interop/Native.cs`).
- The vertex-output regression fixtures in `tests/baselines` are font/machine-dependent
  (DirectWrite output varies with installed font versions). After moving to a new
  machine, regenerate them once with `REGENERATE_BASELINES=1 dotnet test`.

---
### License

### [MIT](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/LICENSE)
