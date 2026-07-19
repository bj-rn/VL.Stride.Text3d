/*
Source: https://github.com/mrvux/dx11-vvvv/blob/master/Nodes/VVVV.DX11.Nodes.Text3d/ExtrudingSink.cs
Ported from SharpDX to Silk.NET 2.22.0 (managed ID2D1SimplifiedGeometrySink +
ID2D1TessellationSink via .NET 8 source-generated COM); geometry math is 1:1 with the
original.

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

// Ported per the agreed bug policy (verified bit-exact against the SharpDX build in
// Phase 1 for 2.0), with one deliberate 2.1 change:
//  - Since 2.1 the side-wall normal is the true outward edge normal
//    normalize(vec.Y, -vec.X) — the original's normalize(vec.Y, vec.X) mirrored the
//    normal across the diagonal, so extruded side walls shaded incorrectly. The sign
//    is verified by the SideWallNormalsPointOutward test (rectangular glyph "I") and
//    holds for hole contours too, since D2D's Outline() winds them opposite to outer
//    contours which flips the formula's result accordingly. Vertex positions, UVs and
//    cap normals are unchanged (guarded during baseline regeneration).
//  - PORT-NOTE: normal smoothing threshold dot > 0.5 as original.
// The dead d1/d2 locals of the original AddTriangles (which contained X/Y typos and were
// never used) are omitted — no behavior change.

using Silk.NET.Direct2D;
using Stride.Graphics;
using VL.Stride.Text3d.Interop;
using Vector2 = Stride.Core.Mathematics.Vector2;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace VL.Stride.Text3d.Core;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public sealed unsafe partial class ExtrudingSink : ID2D1SimplifiedGeometrySinkCallback, ID2D1TessellationSinkCallback
{
    private readonly List<VertexPositionNormalTexture> vertices;
    private readonly float zFront;
    private readonly float zBack;
    private readonly float smoothingThreshold;
    private readonly Vector2 min;
    private readonly Vector2 max;
    private readonly Enums.SideUVMapping sideUVMapping;
    private readonly float textureScale;

    private struct Vertex2D
    {
        public Vector2 pt;
        public Vector2 norm;
        public Vector2 uv;
        public Vector2 inter1;
        public Vector2 inter2;
    }

    private readonly List<Vertex2D> m_figureVertices = new();

    /// <param name="zFront">Z of the front face (was +height/2 in the original).</param>
    /// <param name="zBack">Z of the back face (was -height/2 in the original).</param>
    /// <param name="smoothingThreshold">Adjacent edge normals are averaged when their dot
    /// product exceeds this (cos of the smoothing angle; the original hard-coded 0.5).</param>
    /// <param name="sideUVMapping">How the side walls are UV-mapped; ContourDepthTiled also
    /// switches the caps to absolute density.</param>
    /// <param name="textureScale">Surface distance covered by one texture repeat (ContourDepthTiled only).</param>
    public ExtrudingSink(List<VertexPositionNormalTexture> vertices, float zFront, float zBack,
        float smoothingThreshold, Vector2 min, Vector2 max,
        Enums.SideUVMapping sideUVMapping, float textureScale)
    {
        this.vertices = vertices;
        this.zFront = zFront;
        this.zBack = zBack;
        this.smoothingThreshold = smoothingThreshold;
        this.min = min;
        this.max = max;
        this.sideUVMapping = sideUVMapping;
        this.textureScale = textureScale;
    }

    private Vector2 GetNormal(int i)
    {
        int j = (i + 1) % m_figureVertices.Count;

        Vector2 pti = m_figureVertices[i].pt;
        Vector2 ptj = m_figureVertices[j].pt;
        Vector2 vecij = ptj - pti;

        // True outward edge normal (2.1 fix, see file header).
        return Vector2.Normalize(new Vector2(vecij.Y, -vecij.X));
    }

    private Vector2 GetUV(int i)
    {
        return (m_figureVertices[i].pt - min) / (max - min);
    }

    private Vector2 GetUV(float x, float y)
    {
        // ContourDepthTiled switches the caps to absolute density so walls and caps tile
        // uniformly; min.Y is the top of the silhouette (the bounds are Y-swapped), so V
        // grows downward like the planar projection.
        if (sideUVMapping == Enums.SideUVMapping.ContourDepthTiled)
            return new Vector2((x - min.X) / textureScale, (min.Y - y) / textureScale);

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

    public void BeginFigure(Point2F startPoint, int figureBegin)
    {
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

                if ((n1.X * n2.X + n1.Y * n2.Y) > smoothingThreshold)
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

            // Cumulative arc length for the contour UV modes; arc[n] is the perimeter
            // including the closing edge back to the first point.
            float[]? arc = null;
            if (sideUVMapping != Enums.SideUVMapping.Silhouette)
            {
                int n = m_figureVertices.Count;
                arc = new float[n + 1];
                for (int i = 1; i <= n; i++)
                    arc[i] = arc[i - 1] + (m_figureVertices[i % n].pt - m_figureVertices[i - 1].pt).Length();
                if (arc[n] <= 0f)
                    arc[n] = 1f;
            }

            float vFront = 0f;
            float vBack = sideUVMapping == Enums.SideUVMapping.ContourDepthTiled
                ? Math.Abs(zFront - zBack) / textureScale
                : 1f;

            // Output side-wall triangles
            for (int i = 0; i < m_figureVertices.Count; i++)
            {
                int j = (i + 1) % m_figureVertices.Count;

                Vector2 pt = m_figureVertices[i].pt;
                Vector2 nextPt = m_figureVertices[j].pt;

                Vector2 ptNorm3 = m_figureVertices[i].inter2;
                Vector2 nextPtNorm2 = m_figureVertices[j].inter1;

                Vector2 uvFrontI, uvBackI, uvFrontJ, uvBackJ;
                if (sideUVMapping == Enums.SideUVMapping.Silhouette)
                {
                    // Planar silhouette projection, constant along the depth (original behavior)
                    uvFrontI = uvBackI = m_figureVertices[i].uv;
                    uvFrontJ = uvBackJ = m_figureVertices[j].uv;
                }
                else
                {
                    // U along the contour; the closing edge ends at arc[n] (the perimeter),
                    // not at 0, which duplicates the seam vertices instead of interpolating
                    // backwards through the whole texture.
                    float divisor = sideUVMapping == Enums.SideUVMapping.ContourDepth
                        ? arc![m_figureVertices.Count]
                        : textureScale;
                    float uI = arc![i] / divisor;
                    float uJ = arc[i + 1] / divisor;
                    uvFrontI = new Vector2(uI, vFront);
                    uvBackI = new Vector2(uI, vBack);
                    uvFrontJ = new Vector2(uJ, vFront);
                    uvBackJ = new Vector2(uJ, vBack);
                }

                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, zFront), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uvFrontI });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, zBack), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = uvBackJ });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, zBack), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uvBackI });

                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, zBack), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = uvBackJ });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(pt.X, pt.Y, zFront), Normal = new Vector3(ptNorm3.X, ptNorm3.Y, 0.0f), TextureCoordinate = uvFrontI });
                vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(nextPt.X, nextPt.Y, zFront), Normal = new Vector3(nextPtNorm2.X, nextPtNorm2.Y, 0.0f), TextureCoordinate = uvFrontJ });
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

            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p1x, p1y, zFront), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p1x, p1y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p3x, p3y, zFront), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p3x, p3y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p2x, p2y, zFront), Normal = new Vector3(0.0f, 0.0f, 1.0f), TextureCoordinate = GetUV(p2x, p2y) });

            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p2x, p2y, zBack), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p2x, p2y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p3x, p3y, zBack), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p3x, p3y) });
            vertices.Add(new VertexPositionNormalTexture { Position = new Vector3(p1x, p1y, zBack), Normal = new Vector3(0.0f, 0.0f, -1.0f), TextureCoordinate = GetUV(p1x, p1y) });
        }
    }
}
