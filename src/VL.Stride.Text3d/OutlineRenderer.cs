/*
Source: https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/OutlineRenderer.cs

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

using SharpDX.Direct2D1;

using D2DFactory = SharpDX.Direct2D1.Factory;
using D2DGeometry = SharpDX.Direct2D1.Geometry;
using SharpDX.DirectWrite;
using SharpDX;
using RawMat = SharpDX.Mathematics.Interop.RawMatrix3x2;


namespace VL.Stride.Text3d
{
    public unsafe class OutlineRenderer : TextRendererBase
    {
        private readonly D2DFactory factory;
        private D2DGeometry geometry = null;

        public OutlineRenderer(D2DFactory factory)
        {
            this.factory = factory;
        }

        public override Result DrawGlyphRun(object clientDrawingContext, float baselineOriginX, float baselineOriginY, MeasuringMode measuringMode, GlyphRun glyphRun, GlyphRunDescription glyphRunDescription, SharpDX.ComObject clientDrawingEffect)
        {
            Color4 c = Color4.White;
            if (clientDrawingEffect != null)
            {
                if (clientDrawingEffect is SolidColorBrush)
                {
                    var sb = (SolidColorBrush)clientDrawingEffect;
                    SharpDX.Mathematics.Interop.RawColor4 brushColor = sb.Color;
                    c = *(Color4*)&brushColor;
                }
            }

            if (glyphRun.Indices.Length > 0)
            {
                using (PathGeometry pg = new PathGeometry(this.factory))
                {
                    using (GeometrySink sink = pg.Open())
                    {
                        glyphRun.FontFace.GetGlyphRunOutline(glyphRun.FontSize, glyphRun.Indices, glyphRun.Advances, glyphRun.Offsets, glyphRun.Indices.Length, glyphRun.IsSideways, glyphRun.BidiLevel % 2 == 1, sink as SimplifiedGeometrySink);
                        sink.Close();

                        Matrix3x2 mat = Matrix3x2.Translation(baselineOriginX, baselineOriginY) * Matrix3x2.Scaling(1.0f, -1.0f);
                        TransformedGeometry tg = new TransformedGeometry(this.factory, pg, *(RawMat*)&mat);
                        this.AddGeometry(tg);
                    }
                }
                return Result.Ok;
            }
            else
            {
                return Result.Ok;
            }

        }

        public override Result DrawUnderline(object clientDrawingContext, float baselineOriginX, float baselineOriginY, ref Underline underline, ComObject clientDrawingEffect)
        {
            using (PathGeometry pg = new PathGeometry(this.factory))
            {
                using (GeometrySink sink = pg.Open())
                {
                    Vector2 topLeft = new Vector2(0.0f, underline.Offset);
                    sink.BeginFigure(topLeft, FigureBegin.Filled);
                    topLeft.X += underline.Width;
                    sink.AddLine(topLeft);
                    topLeft.Y += underline.Thickness;
                    sink.AddLine(topLeft);
                    topLeft.X -= underline.Width;
                    sink.AddLine(topLeft);
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();

                    Matrix3x2 mat = Matrix3x2.Translation(baselineOriginX, baselineOriginY) * Matrix3x2.Scaling(1.0f, -1.0f);
                    TransformedGeometry tg = new TransformedGeometry(this.factory, pg, *(RawMat*)&mat);

                    this.AddGeometry(tg);
                    return Result.Ok;
                }
            }
        }

        public override Result DrawStrikethrough(object clientDrawingContext, float baselineOriginX, float baselineOriginY, ref Strikethrough strikethrough, ComObject clientDrawingEffect)
        {
            using (PathGeometry pg = new PathGeometry(this.factory))
            {
                using (GeometrySink sink = pg.Open())
                {
                    Vector2 topLeft = new Vector2(0.0f, strikethrough.Offset);
                    sink.BeginFigure(topLeft, FigureBegin.Filled);
                    topLeft.X += strikethrough.Width;
                    sink.AddLine(topLeft);
                    topLeft.Y += strikethrough.Thickness;
                    sink.AddLine(topLeft);
                    topLeft.X -= strikethrough.Width;
                    sink.AddLine(topLeft);
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();

                    Matrix3x2 mat = Matrix3x2.Translation(baselineOriginX, baselineOriginY) * Matrix3x2.Scaling(1.0f, -1.0f);
                    TransformedGeometry tg = new TransformedGeometry(this.factory, pg, *(RawMat*)&mat);

                    this.AddGeometry(tg);
                    return Result.Ok;
                }
            }
        }

        public override RawMat GetCurrentTransform(object clientDrawingContext)
        {
            return new RawMat()
            {
                M11 = 1.0f,
                M12 = 0.0f,
                M21 = 0.0f,
                M22 = 1.0f,
                M31 = 0.0f,
                M32 = 0.0f
            };
        }

        public override bool IsPixelSnappingDisabled(object clientDrawingContext)
        {
            return true;
        }

        public override float GetPixelsPerDip(object clientDrawingContext)
        {
            return 1.0f;
        }

        public Geometry GetGeometry()
        {
            return this.geometry;
        }

        protected void AddGeometry(D2DGeometry geom)
        {
            if (this.geometry == null)
            {
                this.geometry = geom;
            }
            else
            {
                PathGeometry pg = new PathGeometry(this.factory);

                using (GeometrySink sink = pg.Open())
                {
                    this.geometry.Combine(geom, CombineMode.Union, sink);
                    sink.Close();
                }
                var oldGeom = this.geometry;
                this.geometry = pg;
                oldGeom.Dispose();

            }
        }
    }

}
