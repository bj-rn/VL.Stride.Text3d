using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using InputElement = SharpDX.Direct3D11.InputElement;

namespace VL.Stride.Models.Meshes.Text3d
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Pos3Norm3VertexSDX
    {
        public Vector3 Position;
        public Vector3 Normals;
        public Vector2 TextureCoords;

        private static InputElement[] layout;

        public static InputElement[] Layout
        {
            get
            {
                if (layout == null)
                {
                    layout = new InputElement[]
                    {
                        new InputElement("POSITION",0,SharpDX.DXGI.Format.R32G32B32_Float,0, 0),
                        new InputElement("NORMAL",0,SharpDX.DXGI.Format.R32G32B32_Float,12,0),
                        new InputElement("TEXCOORD0",0,SharpDX.DXGI.Format.R32G32_Float,24,0),
                    };
                }
                return layout;
            }
        }

        public static int VertexSize
        {
            get { return Marshal.SizeOf(typeof(Pos3Norm3VertexSDX)); }
        }
    }
}
