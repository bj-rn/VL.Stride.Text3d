# VL.Stride.Text3d

Set of nodes to create and render (extruded) 3d text in VL.Stride.

The nodes make use of [Extruder.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/Extruder.cs), [ExtrudingSink.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/ExtrudingSink.cs) and [OutlineRenderer.cs](https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/OutlineRenderer.cs) that come with [dx11-vvvv](https://github.com/mrvux/dx11-vvvv) by 
[Julien Vuliet aka mrvux](https://github.com/mrvux). I made only some minor changes. His code is licensed under BSD 3, refer to [DX11-vvvv-License.md](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/DX11-vvvv-License.md) for details.

The library itself is released under [MIT license](https://github.com/bj-rn/VL.Stride.Text3d/blob/master/LICENSE).

## Using the library
In order to use this library with VL you have to install the nuget that is available via nuget.org. For information on how to use nugets with VL, see [Managing Nugets](https://thegraybook.vvvv.org/reference/libraries/dependencies.html#manage-nugets) in the VL documentation. As described there you go to the commandline and then type:

    nuget install VL.Stride.Text3d


Try it with vvvv, the visual live-programming environment for .NET  
Download: http://visualprogramming.net
