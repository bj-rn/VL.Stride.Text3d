/*
Source: https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/Extruder.cs

License:

dx11-vvvv

BSD 3-Clause License

Copyright (c) 2016, Julien Vulliet (mrvux)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; 
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;

using D2DFactory = SharpDX.Direct2D1.Factory;
using D2DGeometry = SharpDX.Direct2D1.Geometry;
using SharpDX.Direct2D1;

using Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace VL.Stride.Text3d
{
    public class Extruder
    {
        private D2DFactory factory;

        public Extruder(D2DFactory factory)
        {
            this.factory = factory;
        }

        const float sc_flatteningTolerance = .1f;

        private D2DGeometry FlattenGeometry(D2DGeometry geometry, float tolerance)
        {
            PathGeometry path = new PathGeometry(this.factory);

            using (GeometrySink sink = path.Open())
            {
                geometry.Simplify(GeometrySimplificationOption.Lines, tolerance, sink);
                sink.Close();
            }
            return path;
        }

        private D2DGeometry OutlineGeometry(D2DGeometry geometry)
        {
            PathGeometry path = new PathGeometry(this.factory);

            using (GeometrySink sink = path.Open())
            {
                geometry.Outline(sink);
                sink.Close();
            }

            return path;
        }


        public void GetVertices(D2DGeometry geometry, List<VertexPositionNormalTexture> vertices, float height = 24.0f)
        {
            vertices.Clear();
            //Empty mesh
            if (geometry == null)
            {
                VertexPositionNormalTexture zero = new VertexPositionNormalTexture();
                vertices.Add(zero);
                vertices.Add(zero);
                vertices.Add(zero);
            }

            using (D2DGeometry flattenedGeometry = this.FlattenGeometry(geometry, sc_flatteningTolerance))
            {
                using (D2DGeometry outlinedGeometry = this.OutlineGeometry(flattenedGeometry))
                {
                    var bounds = outlinedGeometry.GetBounds();
                    //Top and Bottom switched for uv calculation
                    Vector2 min = new Vector2(bounds.Left, bounds.Bottom);
                    Vector2 max = new Vector2(bounds.Right, bounds.Top);
                    
                    using (ExtrudingSink sink = new ExtrudingSink(vertices, height, min, max))
                    {
                        outlinedGeometry.Simplify(GeometrySimplificationOption.Lines, sink);
                        outlinedGeometry.Tessellate(sink);
                    }

                }
            }
        }
    }


}
