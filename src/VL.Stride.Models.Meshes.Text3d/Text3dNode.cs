using System;
using System.Collections.Generic;

using SharpDX.DirectWrite;
using DWriteFactory = SharpDX.DirectWrite.Factory;
using D2DFactory = SharpDX.Direct2D1.Factory;
using Stride.Rendering;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;
using Stride.Core.Mathematics;
using VL.Lib.Collections;

namespace VL.Stride.Models.Meshes.Text3d
{
    public unsafe class TextMesh
    {

        public string Text { get; set; } = "hello world";

        public VL.Lib.Text.FontList Font { get; set; }

        public int FontSize { get; set; } = 32;

        public SharpDX.DirectWrite.TextAlignment HorizontalAlignment { get; set; } = SharpDX.DirectWrite.TextAlignment.Leading;

        public ParagraphAlignment VerticalAlignment { get; set; } = ParagraphAlignment.Near;

        public WordWrapping WordWrap { get; set; } = WordWrapping.NoWrap;

        public float ExtrudeAmount { get; set; } = 1.0f;


        private static SharpDX.Direct2D1.Factory d2dFactory;
        private static SharpDX.DirectWrite.Factory dwFactory;

        private List<Pos3Norm3VertexSDX> vertexList;


        public TextMesh()
        {
            vertexList = new List<Pos3Norm3VertexSDX>(1024);

            if (d2dFactory == null)
            {
                d2dFactory = new D2DFactory();
                dwFactory = new DWriteFactory(SharpDX.DirectWrite.FactoryType.Shared);
            }
        }

        public Mesh Upddate(GraphicsDevice device, GraphicsContext context)
        {

            if (device == null || context == null)
                return null;

            //TextFormat fmt = new TextFormat(dwFactory, (Font as IDynamicEnum).Value, FontSize);

            TextFormat fmt = new TextFormat(dwFactory, "Arial", FontSize);
            TextLayout tl = new TextLayout(dwFactory, Text, fmt, 0.0f, 32.0f);

            tl.WordWrapping = WordWrap;
            tl.TextAlignment = HorizontalAlignment;
            tl.ParagraphAlignment = VerticalAlignment;

            OutlineRenderer renderer = new OutlineRenderer(d2dFactory);
            Extruder ex = new Extruder(d2dFactory);
           
            tl.Draw(renderer, 0.0f, 0.0f);

            var outlinedGeometry = renderer.GetGeometry();
            ex.GetVertices(outlinedGeometry, vertexList, ExtrudeAmount);
            outlinedGeometry.Dispose();


            Buffer vbuffer = Buffer.New(device, new BufferDescription()
            {
                BufferFlags = BufferFlags.VertexBuffer, //| BufferFlags.ShaderResource,
                SizeInBytes = vertexList.Count * Pos3Norm3VertexSDX.VertexSize,
                Usage = GraphicsResourceUsage.Dynamic
            },
            PixelFormat.R32G32B32A32_Float);

            var varray = vertexList.ToArray();

            vbuffer.SetData<Pos3Norm3VertexSDX>(context.CommandList, varray);

            VertexDeclaration Pos3Norm3Tex2 = CreatePos3Norm3Tex2();
            int vertexcount = vbuffer.SizeInBytes / Pos3Norm3Tex2.VertexStride;

            VertexBufferBinding vbb = new VertexBufferBinding(vbuffer, , vertexcount);
            VertexBufferBinding[] buffers = new VertexBufferBinding[] { vbb };

            MeshDraw md = new MeshDraw();
            md.VertexBuffers = buffers;
            md.DrawCount = vertexcount;
            md.PrimitiveType = PrimitiveType.TriangleList;

           
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            for (int i = 0; i < vertexList.Count; i++)
            {
                Pos3Norm3VertexSDX pn = vertexList[i];

                min.X = pn.Position.X < min.X ? pn.Position.X : min.X;
                min.Y = pn.Position.Y < min.Y ? pn.Position.Y : min.Y;
                min.Z = pn.Position.Z < min.Z ? pn.Position.Z : min.Z;

                max.X = pn.Position.X > max.X ? pn.Position.X : max.X;
                max.Y = pn.Position.Y > max.Y ? pn.Position.Y : max.Y;
                max.Z = pn.Position.Z > max.Z ? pn.Position.Z : max.Z;
            }

            BoundingBox bd = new BoundingBox(min, max);


            Mesh textmesh = new Mesh
            {
                Draw = md,
                BoundingBox = bd
            };


            renderer.Dispose();
            fmt.Dispose();
            tl.Dispose();

            return textmesh;
        }

        private VertexDeclaration CreatePos3Norm3Tex2()
        {
            VertexElement pos = new VertexElement("POSITION", PixelFormat.R32G32B32_Float);
            VertexElement normal = new VertexElement("NORMAL", PixelFormat.R32G32B32_Float);
            VertexElement texcoord = new VertexElement("TEXCOORD0", PixelFormat.R32G32_Float);


            VertexElement[] elements = new VertexElement[] { pos, normal, texcoord };

            VertexDeclaration vd = new VertexDeclaration(elements);

            return vd;
        }

    }
}
