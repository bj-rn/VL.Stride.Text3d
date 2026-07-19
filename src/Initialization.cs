// Assembly-level VL import declarations. Only the namespaces listed here become
// visible in the vvvv node browser; VL.Stride.Text3d.Interop and .Core stay public
// for tests but are never imported.
//
// Longest namespace prefix wins, so Nodes.Models / Nodes.Meshes take precedence over
// the Nodes mapping.
//
// The category strings replicate the 1.x in-document categories verbatim (verified
// against the LastCategoryFullName references in the help patches), so nodes appear
// exactly where existing users look for them.

using VL.Core.Import;

[assembly: ImportNamespace("VL.Stride.Text3d.Nodes.Models", Category = "VL.Stride.Models")]
[assembly: ImportNamespace("VL.Stride.Text3d.Nodes.Meshes", Category = "VL.Stride.Models.Meshes")]
[assembly: ImportNamespace("VL.Stride.Text3d.Nodes", Category = "VL.Stride.Text3d")]
[assembly: ImportNamespace("VL.Stride.Text3d.TextStyles", Category = "VL.Stride.Text3d.TextStyles")]
[assembly: ImportNamespace("VL.Stride.Text3d.Enums", Category = "VL.Stride.Text3d.Enums")]
