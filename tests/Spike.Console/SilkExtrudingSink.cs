/*
Port of ExtrudingSink.cs (SharpDX) to Silk.NET 2.22.0 / managed COM callbacks.
Original source: https://github.com/mrvux/dx11-vvvv (BSD 3-Clause, Julien Vulliet) —
full license header retained in src/ during Phase 2.

Geometry math is ported VERBATIM per the agreed bug policy, including:
 - PORT-NOTE: side-wall normal formula normalize(vec.Y, vec.X) is NOT the true outward
   normal (would be (vec.Y, -vec.X)); kept 1:1 for baseline parity, fix planned for 2.1.
 - PORT-NOTE: normal smoothing threshold dot > 0.5 as original.
The dead d1/d2 locals of the original AddTriangles (which contained X/Y typos and were
never used) are omitted — no behavior change.
*/

using Silk.NET.Direct2D;
using Spike.Interop;
using Stride.Graphics;
using Vector2 = Stride.Core.Mathematics.Vector2;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace Spike;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
internal sealed unsafe partial class SilkExtrudingSink : ID2D1SimplifiedGeometrySinkCallback, ID2D1TessellationSinkCallback
{
    private readonly List<VertexPositionNormalTexture> vertices;
    private readonly float m_height;
    private readonly Vector2 min;
    private readonly Vector2 max;

    private struct Vertex2D
    {
        public Vector2 pt;
        public Vector2 norm;
        public Vector2 uv;
        public Vector2 inter1;
        public Vector2 inter2;
    }

    private readonly List<Vertex2D> m_figureVertices = new();

    public SilkExtrudingSink(List<VertexPositionNormalTexture> vertices, float height, Vector2 min, Vector2 max)
    {
        this.vertices = vertices;
        this.m_height = height;
        this.min = min;
        this.max = max;
    }

    private Vector2 GetNormal(int i)
    {
        int j = (i + 1) % m_figureVertices.Count;

        Vector2 pti = m_figureVertices[i].pt;
        Vector2 ptj = m_figureVertices[j].pt;
        Vector2 vecij = ptj - pti;

        // PORT-NOTE: kept 1:1 — see file header.
        return Vector2.Normalize(new Vector2(vecij.Y, vecij.X));
    }

    private Vector2 GetUV(int i)
    {
        return (m_figureVertices[i].pt - min) / (max - min);
    }

    private Vector2 GetUV(float x, float y)
    {
        var uv = new Vector2(x, y);
        return (uv - min) / (max - min);
    }

    // ---- ID2D1SimplifiedGeometrySink ----

    public void SetFillMode(int fillMode)
    {
    }

    public void SetSegmentFlags(int vertexFlags)
    {
    }

    internal static bool LogSinkCalls = Environment.GetEnvironmentVariable("SPIKE_LOG_SINK") == "1";
    private static int logBudget = 12;

    public void BeginFigure(Point2F startPoint, int figureBegin)
    {
        if (LogSinkCalls && logBudget > 0)
        {
            logBudget--;
            Console.WriteLine($"  [sink] BeginFigure ({startPoint.X:G9}, {startPoint.Y:G9}) figureBegin={figureBegin}");
        }
        m_figureVertices.Clear();
        m_figureVertices.Add(new Vertex2D
        {
            pt = new Vector2(startPoint.X, startPoint.Y),
            inter1 = Vector2.Zero,
            inter2 = Vector2.Zero,
            norm = Vector2.Zero,
            uv = Vector2.Zero,
        });
    }

    public void AddLines(Point2F* points, uint pointsCount)
    {
        if (LogSinkCalls && logBudget > 0)
        {
            logBudget--;
            Console.WriteLine($"  [sink] AddLines count={pointsCount} first=({points[0].X:G9}, {points[0].Y:G9})");
        }
        for (uint i = 0; i < pointsCount; i++)
        {
            m_figureVertices.Add(new Vertex2D { pt = new Vector2(points[i].X, points[i].Y) });
        }
    }

    public void AddBeziers(void* beziers, uint beziersCount)
    {
        // Geometry is pre-flattened to lines before reaching this sink (as original).
    }

    public void EndFigure(int figureEnd)
    {
        Vector2 front = m_figureVertices[0].pt;
        Vector2 back = m_figureVertices[m_figureVertices.Count - 1].pt;

        if (front.X == back.X && front.Y == back.Y)
        {
            m_figureVertices.RemoveAt(m_figureVertices.Count - 1);
        }

        if (m_figureVertices.Count > 1)
        {
            // Snap and normals
            for (int i = 0; i < m_figureVertices.Count; i++)
            {
                Vertex2D v = m_figureVertices[i];
                v.norm = GetNormal(i);
                v.uv = GetUV(i);
                m_figureVertices[i] = v;
            }

            // Interpolate normals
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

            // Output side-wall triangles
            for (int i = 0; i < m_figureVertices.Count; i++)
            {
                int j = (i + 1) % m_figureVertices.Count;

                Vector2 pt = m_figureVertices[i].pt;
                Vector2 nextPt = m_figureVertices[j].pt;

                Vector2 ptNorm3 = m_figureVertices[i].inter2;
                Vector2 nextPtNorm2 = m_figureVertices[j].inter1;

                Vector2 uv = m_figureVertices[i].uv;
                Vector2 nextUV = m_figureVertices[j].uv;

                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uv });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, -m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = nextUV });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, -m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uv });

                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, -m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = nextUV });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, m_height / 2), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uv });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, m_height / 2), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = nextUV });
            }
        }
    }

    public int Close() => ComCallbackHelper.S_OK;

    // ---- ID2D1TessellationSink ----

    public void AddTriangles(Triangle* triangles, uint trianglesCount)
    {
        for (uint i = 0; i < trianglesCount; i++)
        {
            Triangle tri = triangles[i];

            float p1x = tri.Point1.X, p1y = tri.Point1.Y;
            float p2x = tri.Point2.X, p2y = tri.Point2.Y;
            float p3x = tri.Point3.X, p3y = tri.Point3.Y;

            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p1x, p1y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p1x, p1y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p3x, p3y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p3x, p3y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p2x, p2y, m_height / 2), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p2x, p2y) });

            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p2x, p2y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p2x, p2y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p3x, p3y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p3x, p3y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p1x, p1y, -m_height / 2), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p1x, p1y) });
        }
    }
}
