using System;
using System.Collections.Generic;

using SharpDX.DirectWrite;
using DWriteFactory = SharpDX.DirectWrite.Factory;
using D2DFactory = SharpDX.Direct2D1.Factory;

using Stride.Core.Mathematics;

using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

using Stride.Rendering;
using Stride.Rendering.ProceduralModels;

//using VL.Lib.Collections;


namespace VL.Stride.Text3d
{

    public unsafe abstract class Text3dBase: PrimitiveProceduralModelBase
    {

        protected static SharpDX.Direct2D1.Factory d2dFactory;
        protected static SharpDX.DirectWrite.Factory dwFactory;

        protected List<VertexPositionNormalTexture> vertexList;


        protected Text3dBase()
        {
            vertexList = new List<VertexPositionNormalTexture>(1024);

            if (d2dFactory == null)
            {
                d2dFactory = new D2DFactory();
                dwFactory = new DWriteFactory(FactoryType.Shared);
            }
        }

        protected static int[] GetDefaultIndicesArray(int size)
        {
            int[] result = new int[size];
            for (int i = 0; i < size; ++i)
            {
                result[i] = i;
            }
            return result;
        }

        protected VertexDeclaration CreatePos3Norm3Tex2()
        {
            VertexElement pos = new VertexElement("POSITION", PixelFormat.R32G32B32_Float);
            VertexElement normal = new VertexElement("NORMAL", PixelFormat.R32G32B32_Float);
            VertexElement texcoord = new VertexElement("TEXCOORD0", PixelFormat.R32G32_Float);


            VertexElement[] elements = new VertexElement[] { pos, normal, texcoord };

            VertexDeclaration vd = new VertexDeclaration(elements);

            return vd;
        }

       
    }



    public unsafe class Text3d : Text3dBase
    {

        public string Text { get; set; } = "hello world";

        //public VL.Lib.Text.FontList Font { get; set; }

        public string Font { get; set; } = "Arial";

        public int FontSize { get; set; } = 32;

        public SharpDX.DirectWrite.TextAlignment HorizontalAlignment { get; set; } = SharpDX.DirectWrite.TextAlignment.Leading;

        public ParagraphAlignment VerticalAlignment { get; set; } = ParagraphAlignment.Near;

        //public WordWrapping WordWrap { get; set; } = WordWrapping.NoWrap;

        public float ExtrudeAmount { get; set; } = 1.0f;


        public Text3d() : base()
        {
           
        }
        

        protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
        {

            TextFormat fmt = new TextFormat(dwFactory, Font, FontSize);
            TextLayout tl = new TextLayout(dwFactory, Text, fmt, 0.0f, 32.0f)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = HorizontalAlignment,
                ParagraphAlignment = VerticalAlignment
            };

            OutlineRenderer renderer = new OutlineRenderer(d2dFactory);
            Extruder ex = new Extruder(d2dFactory);

            tl.Draw(renderer, 0.0f, 0.0f);

            var outlinedGeometry = renderer.GetGeometry();
            ex.GetVertices(outlinedGeometry, vertexList, ExtrudeAmount);
            outlinedGeometry.Dispose();


            renderer.Dispose();
            fmt.Dispose();
            tl.Dispose();

            var vertices = vertexList.ToArray();

            int[] TmpIndices = GetDefaultIndicesArray(vertices.Length);

            return new GeometricMeshData<VertexPositionNormalTexture>(vertices, TmpIndices, isLeftHanded: false) { Name = "Text3d" };
        }


        [Obsolete("Upddate is deprecated, please use GeometricMeshData instead.")]
        public Mesh Upddate(GraphicsDevice device, GraphicsContext context)
        {

            if (device == null || context == null)
                return null;

            //TextFormat fmt = new TextFormat(dwFactory, (Font as IDynamicEnum).Value, FontSize);

            TextFormat fmt = new TextFormat(dwFactory, Font, FontSize);
            TextLayout tl = new TextLayout(dwFactory, Text, fmt, 0.0f, 32.0f)
            {
                WordWrapping = WordWrapping.NoWrap,
                TextAlignment = HorizontalAlignment,
                ParagraphAlignment = VerticalAlignment
            };

            OutlineRenderer renderer = new OutlineRenderer(d2dFactory);
            Extruder ex = new Extruder(d2dFactory);

            tl.Draw(renderer, 0.0f, 0.0f);

            var outlinedGeometry = renderer.GetGeometry();
            ex.GetVertices(outlinedGeometry, vertexList, ExtrudeAmount);
            outlinedGeometry.Dispose();


            Buffer vbuffer = Buffer.New(device, new BufferDescription()
            {
                BufferFlags = BufferFlags.VertexBuffer, //| BufferFlags.ShaderResource,
                SizeInBytes = vertexList.Count * VertexPositionNormalTexture.Size,
                Usage = GraphicsResourceUsage.Dynamic
            },
            PixelFormat.R32G32B32A32_Float);

            var varray = vertexList.ToArray();

            vbuffer.SetData<VertexPositionNormalTexture>(context.CommandList, varray);

            VertexDeclaration Pos3Norm3Tex2 = CreatePos3Norm3Tex2();
            int vertexcount = vbuffer.SizeInBytes / Pos3Norm3Tex2.VertexStride;

            VertexBufferBinding vbb = new VertexBufferBinding(vbuffer, Pos3Norm3Tex2, vertexcount);
            VertexBufferBinding[] buffers = new VertexBufferBinding[] { vbb };

            MeshDraw md = new MeshDraw();
            md.VertexBuffers = buffers;
            md.DrawCount = vertexcount;
            md.PrimitiveType = PrimitiveType.TriangleList;


            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            for (int i = 0; i < vertexList.Count; i++)
            {
                VertexPositionNormalTexture pn = vertexList[i];

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


    }
}
