// Assembly-level VL import declarations. Only the namespaces listed here become
// visible in the vvvv node browser; VL.Stride.Text3d.Interop and .Core stay public
// for tests but are never imported.
//
// Longest namespace prefix wins, so Nodes.Models / Nodes.Meshes take precedence over
// the Nodes mapping.

using VL.Core.Import;

[assembly: ImportNamespace("VL.Stride.Text3d.Nodes.Models", Category = "Stride.Models")]
[assembly: ImportNamespace("VL.Stride.Text3d.Nodes.Meshes", Category = "Stride.Models.Meshes")]
[assembly: ImportNamespace("VL.Stride.Text3d.Nodes", Category = "Stride.Text3d")]
[assembly: ImportNamespace("VL.Stride.Text3d.TextStyles", Category = "Stride.Text3d.Advanced.TextStyles")]
[assembly: ImportNamespace("VL.Stride.Text3d.Enums", Category = "Stride.Text3d.Enums")]
