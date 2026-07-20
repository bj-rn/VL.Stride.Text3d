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

## Physics for 3d text

Together with [VL.Stride.BepuPhysics](https://github.com/bj-rn/VL.Stride.BepuPhysics)
(0.12.0 or newer) typed text becomes a physics collider without stalling the frame:
enable `Compute Points` on `Text3dMeshes (Async)` and connect its `Point Groups`
output to `HullsFromPointGroups (Async)`, then `ConvexHullCollider` into a `Static` or
`Body` on the text entity. Every glyph gets its own convex hull, baked on a background
thread from the same data as the rendered meshes, so meshes and colliders can never
desync; `In Progress` reports background activity, the last completed result stays
active meanwhile. The packages connect purely at the patch level (plain
`Spread<Vector3>` point data), neither depends on the other's assembly. See the
`help/HowTo Physical 3d Text.vl` patch shipped with VL.Stride.BepuPhysics for the
complete pipeline: spheres raining onto typed text.

Notes:
- Hulls are convex per glyph, so counters (the holes in e, o, a, ...) are filled in,
  by design.
- `Extrude Amount` must be non zero: coplanar points cannot form a hull volume and
  yield no collider.
- The whole-mesh nodes (`Text3dMesh (Async)` variants) offer a `Points` output for a
  single hull around all of the text via `HullFromPoints (Async)`.

What runs where:

| Work | Thread |
|---|---|
| Text geometry extraction | background (existing async nodes) |
| Point dedup + glyph translation | background (same bake as the meshes) |
| Convex hull computation | background (VL.Stride.BepuPhysics) |
| GPU mesh buffers for rendering | main (graphics device) |
| Engine hull build at collider attach | main, unavoidable; trivial with reduced hull points |

For measured attach costs (with 42 glyphs: 0.15 ms for one reduced whole-text hull,
5.8 ms for 42 per-glyph hulls, vs 4.3 ms for a raw whole-text hull and 14.9 ms for
raw per-glyph hulls) see the "Async hull baking" section of the VL.Stride.BepuPhysics
README.

## Changes in 2.4

- New optional `Compute Points` pin on the four async mesh nodes: when enabled, the
  background computation also extracts the distinct vertex positions as collider
  points, made for convex hull baking with the async hull nodes of
  [VL.Stride.BepuPhysics](https://github.com/bj-rn/VL.Stride.BepuPhysics). The
  whole-mesh nodes gain a `Points` output (mesh-local space), the per-glyph nodes a
  `Point Groups` output (one group per glyph, in text-local space, ready for
  `HullsFromPointGroups (Async)`). Off by default, the outputs are then empty and the
  extra pass is skipped entirely. Note that toggling the pin recomputes in the
  background and also refreshes the mesh outputs on adoption.

## Changes in 2.2

- A sample triplanar material shader ships with the package: connect the
  `TriplanarColor` ShaderFX node to a ComputeColor input of a material (for example the
  Diffuse slot) to texture caps and side walls seamlessly without any UVs. Best for
  tileable surface textures; for placed textures use the `Side UV Mapping` modes below.
- New `Side UV Mapping` pin on all mesh/model producing nodes (default Silhouette, the
  previous behavior): ContourDepth unwraps the side walls, running U once around each
  contour and V along the extrusion depth; ContourDepthTiled tiles absolute surface
  distances by the new `Texture Scale` pin on walls and caps alike for uniform texel
  density (use wrapping texture addressing).
- New `Weld Vertices` pin on all mesh/model producing nodes (default off): welds
  identical vertices into an indexed mesh, visually lossless with roughly 2 to 3 times
  smaller vertex buffers. Off by default because it changes the mesh topology, which
  per-face techniques relying on the plain triangle list may depend on.
- Async variants of all mesh nodes (`Text3dMesh (Async)`, `Text3dMesh (Advanced Async)`,
  `Text3dMeshes (Async)` and `Text3dMeshes (Advanced Async)`): geometry is computed on a
  background thread (an `In Progress` output reports activity), the last completed
  result stays available meanwhile, and rapid input changes are coalesced.
- New nodes `Text3dMeshes` and `Text3dMeshes (Advanced)`: output one mesh per glyph
  plus per-glyph transforms for typography animation. Each mesh's pivot sits on the
  baseline at the glyph's pen position. Spaces produce no mesh, ligatures may merge
  characters, and underline/strikethrough decorations are not included.
- New pins on the Text3d/Text3dMesh nodes (defaults reproduce the previous behavior
  exactly):
  - `Extrude Origin` (Center/Front/Back): where the mesh sits relative to Z = 0.
  - `Flattening Tolerance` (default 0.1): curve quality of the outlines; smaller
    values give finer curves and more vertices.
  - `Smoothing Angle` (in cycles, default 1/6 = 60°): side-wall edges sharper than
    this stay hard, flatter ones are shaded smooth.
- The entity nodes regained the `Name` pin.
- All enum members now carry tooltips.

## Changes in 2.1

- Side-wall lighting corrected: the extrusion's side walls now use the true outward
  surface normal (a bug inherited from the original dx11-vvvv code mirrored it, so
  extruded surfaces shaded incorrectly). Silhouette, front/back caps and UVs are
  unchanged; only the shading of the extruded sides differs.

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

### Upgrading a 1.x patch

Nodes from this library show up red once; re-pick each via the node browser (double
click the red node). Pin names were kept, so links reconnect automatically. The
mapping:

| 1.x node | 2.x replacement | Note |
|---|---|---|
| Text3d / Text3dMesh (simple) | same names | `FontSize` pin is now `Font Size`, re-draw that link |
| Text3d / Text3dMesh (FontAndParagraph overload) | `Text3d (Advanced)` / `Text3dMesh (Advanced)` | renamed |
| Text3dMesh (Async) | removed | was obsolete; 2.2 added new async variants |
| FontAndParagraph and its Set* operations | same names | `SetBasicFontPorperties` is now `SetBasicFontProperties` |
| TextStyles (FontFamily, FontSize, FontStretch, FontStyle, FontWeight, StrikeThrough, Underline, PairKerning, CharacterSpacing, Typography) | same names and category | |
| TextLayoutMetrics / LineMetrics | same names | same output pins |
| Enum types (TextAlignment, FontWeight, WordWrapping, …) | same names and member names | new type identity, re-create enum IOBoxes and re-enter their values |
| Services (Internal) | removed | nodes obtain the Game themselves |

## Maintenance notes

The 2.x package is version-coupled to what vvvv bundles:

| this library | vvvv gamma | Silk.NET | Stride |
|---|---|---|---|
| 2.x | 7.4 | 2.22.0 (exact pin) | 4.2.1.2487 |

When vvvv updates its bundled Silk.NET or Stride, this package needs a matching
release: bump `Silk.NET.Direct2D` (exact `[x.y.z]` pin) and the `Stride.*` versions in
`src/VL.Stride.Text3d.csproj` plus the nuspec dependency, rebuild, and run the test
suite. Two things to preserve when touching the interop:

- Never use Silk.NET's managed-`String` overloads for DirectWrite calls; they marshal
  the WCHAR parameters with the wrong encoding (silent font fallback, intermittent
  E_INVALIDARG). Always pass pinned UTF-16 `char*` (see `src/Interop/Native.cs`).
- The vertex-output regression fixtures in `tests/baselines` are font/machine-dependent
  (DirectWrite output varies with installed font versions). After moving to a new
  machine, regenerate them once with `REGENERATE_BASELINES=1 dotnet test`.
- `src/Core/BackgroundComputation.cs` (the poll based helper behind the async nodes)
  is intentionally duplicated in
  [VL.Stride.BepuPhysics](https://github.com/bj-rn/VL.Stride.BepuPhysics) as
  `src/Internal/BackgroundComputation.cs`: only namespace and visibility differ.
  Duplicating 75 stable lines beats a cross package dependency (or a shared micro
  nuget) that would couple the two packages' releases. When changing the helper,
  change it in both repos; each carries the same semantics tests
  (`BackgroundComputationTests`), so a divergence shows up in whichever suite was not
  updated.

### Possible future direction

The library is Windows-only because text shaping, outline extraction and tessellation
run on Direct2D/DirectWrite. Should vvvv/Stride ever leave Windows, the whole native
layer could be replaced by a cross-platform stack: text shaping and glyph outlines via
HarfBuzz (HarfBuzzSharp) or Typography.OpenFont, boolean outline merging and
tessellation via LibTessDotNet. The extrusion and UV logic in `src/Core` is independent
of Direct2D input and would carry over; the regression fixtures would then also become
machine-independent. This is a large effort with no user-visible gain today and is
recorded here only as a direction, not a plan.

---
### License

### [MIT](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/LICENSE)
