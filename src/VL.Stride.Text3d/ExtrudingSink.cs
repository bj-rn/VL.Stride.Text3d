using SharpDX;
using System;
using System.Collections.Generic;
using SharpDX.Direct2D1;
using Stride.Graphics;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Vector2 = Stride.Core.Mathematics.Vector2;


namespace VL.Stride.Text3d
{
    public unsafe class ExtrudingSink : GeometrySink, TessellationSink
    {
        private List<VertexPositionNormalTexture> vertices;

        public ExtrudingSink(List<VertexPositionNormalTexture> vertices, float height)
        {
            this.vertices = vertices;
            this.m_height = height;
        }

        private struct Vertex2D
        {
            public Vector2 pt;
            public Vector2 norm;
            public Vector2 inter1;
            public Vector2 inter2;
        }

        private List<Vertex2D> m_figureVertices = new List<Vertex2D>();

        private float m_height;

        private Vector2 GetNormal(int i)
        {
            int j = (i + 1) % m_figureVertices.Count;

            Vector2 pti = m_figureVertices[i].pt;
            Vector2 ptj = m_figureVertices[j].pt;
            Vector2 vecij = ptj - pti;

            return Vector2.Normalize(new Vector2(vecij.Y, vecij.X));
        }

        public void AddBeziers(BezierSegment[] beziers)
        {

        }

        public void AddLines(SharpDX.Mathematics.Interop.RawVector2[] pointsRef)
        {
            for (int i = 0; i < pointsRef.Length; i++)
            {
                SharpDX.Mathematics.Interop.RawVector2 rawPoint = pointsRef[i];
                Vertex2D v = new Vertex2D();
                v.pt = *(Vector2*)&rawPoint;

                m_figureVertices.Add(v);

                /*if (m_figureVertices.Count > 0)
                {

                }*/
            }
        }

        public void BeginFigure(SharpDX.Mathematics.Interop.RawVector2 startPoint, FigureBegin figureBegin)
        {
            this.m_figureVertices.Clear();

            Vertex2D v = new Vertex2D()
            {
                pt = *(Vector2*)&startPoint,
                inter1 = Vector2.Zero,
                inter2 = Vector2.Zero,
                norm = Vector2.Zero
            };
            this.m_figureVertices.Add(v);
        }

        public void Close()
        {

        }

        public void EndFigure(FigureEnd figureEnd)
        {
            Vector2 front = m_figureVertices[0].pt;
            Vector2 back = m_figureVertices[m_figureVertices.Count - 1].pt;

            if (front.X == back.X && front.Y == back.Y)
            {
                m_figureVertices.RemoveAt(m_figureVertices.Count - 1);
            }

            if (m_figureVertices.Count > 1)
            {

                //Snap and normals
                for (int i = 0; i < m_figureVertices.Count; i++)
                {
                    Vertex2D v = m_figureVertices[i];
                    v.norm = GetNormal(i);
                    m_figureVertices[i] = v;
                }

                //Interpolate normals
                for (int i = 0; i < m_figureVertices.Count; i++)
                {
                    int h = (i + m_figureVertices.Count - 1) % m_figureVertices.Count;

                    Vector2 n1 = m_figureVertices[h].norm;
                    Vector2 n2 = m_figureVertices[i].norm;

                    Vertex2D v = m_figureVertices[i];

                    if ((n1.X * n2.X + n1.Y * n2.Y) > .5f)
                    {
                        Vector2 sum = m_figureVertices[i].norm + m_figureVertices[h].norm;

                        v.inter1 = Vector2.Normalize(sum);
                        v.inter2 = v.inter1;
                    }
                    else
                    {
                        v.inter1 = m_figureVertices[h].norm;
                        v.inter2 = m_figureVertices[i].norm;
                    }
                    m_figureVertices[i] = v;
                }



                //Output triangles
                Vector2 faketc = Vector2.Zero;

                for (int i = 0; i < m_figureVertices.Count; i++)
                {
                    int j = (i + 1) % m_figureVertices.Count;

                    Vector2 pt = m_figureVertices[i].pt;
                    Vector2 nextPt = m_figureVertices[j].pt;

                    Vector2 ptNorm3 = m_figureVertices[i].inter2;
                    Vector2 nextPtNorm2 = m_figureVertices[j].inter1;

                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(pt.X, pt.Y, m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = faketc });
                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(pt.X, pt.Y, -m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = faketc });
                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(nextPt.X, nextPt.Y, -m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = faketc });
                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(nextPt.X, nextPt.Y, -m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = faketc });
                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(nextPt.X, nextPt.Y, m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = faketc });
                    vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(pt.X, pt.Y, m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = faketc });

                }
            }
        }

        public void SetFillMode(SharpDX.Direct2D1.FillMode fillMode)
        {

        }

        public void SetSegmentFlags(PathSegment vertexFlags)
        {

        }

        public IDisposable Shadow
        {
            get;
            set;
        }

        public void Dispose()
        {
            Shadow.Dispose();
        }

        public void AddTriangles(Triangle[] triangles)
        {

            Vector2 faketc = Vector2.Zero;

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle tri = triangles[i];

                Vector2 d1 = new Vector2(tri.Point2.X - tri.Point1.Y, tri.Point2.Y - tri.Point1.Y);
                Vector2 d2 = new Vector2(tri.Point3.X - tri.Point2.Y, tri.Point3.Y - tri.Point2.Y);

                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point1.X, tri.Point1.Y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = faketc });
                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point2.X, tri.Point2.Y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = faketc });
                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point3.X, tri.Point3.Y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = faketc });
                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point2.X, tri.Point2.Y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = faketc });
                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point1.X, tri.Point1.Y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = faketc });
                vertices.Add(new VertexPositionNormalTexture() { Position = new Vector3(tri.Point3.X, tri.Point3.Y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = faketc });
            }


        }

        public void AddArc(ArcSegment arc)
        {

        }

        public void AddBezier(BezierSegment bezier)
        {

        }

        public void AddLine(SharpDX.Mathematics.Interop.RawVector2 point)
        {

        }

        public void AddQuadraticBezier(QuadraticBezierSegment bezier)
        {

        }

        public void AddQuadraticBeziers(QuadraticBezierSegment[] beziers)
        {

        }


        // https://github.com/sharpdx/SharpDX/blob/master/Source/SharpDX/ComObject.cs#L242=
        // only example of an implementation I could find 
        Result IUnknown.QueryInterface(ref Guid guid, out IntPtr comObject)
        {
            throw new NotImplementedException();
        }

        int IUnknown.AddReference()
        {
            throw new NotImplementedException();
        }

        int IUnknown.Release()
        {
            throw new NotImplementedException();
        }
    }

}
