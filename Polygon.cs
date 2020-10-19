using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using DelaunatorSharp;

namespace GMLtoOBJ
{
    class Polygon
    {
        public List<double> bounds;
        public List<double> verts;
        public List<List<double>> interior;
        public List<double> uvs;
        public int[] triangles;
        public int[,] delaunayTriangles;
        public double[,] delaunayVerts;
        public bool isConcave;
        public IPoint[] pointsFlattened;
        public IPoint[] boundsFlattened;
        public string gmlID;
        public string parentID;
        public Polygon(List<double> verts)
        {
            this.verts = verts;
            isConcave = false;
            uvs = new List<double>();
            bounds = new List<double>();
            interior = new List<List<double>>();
        }

        public Polygon()
        {
            verts = new List<double>();
            isConcave = false;
            uvs = new List<double>();
            bounds = new List<double>();
            interior = new List<List<double>>();
        }

        public Polygon(List<double> verts, List<double> bounds)
        {
            this.verts = verts;
            isConcave = false;
            uvs = new List<double>();
            this.bounds = bounds;
            interior = new List<List<double>>();
        }

        public Polygon(List<double> verts, List<double> bounds, List<List<double>> interior)
        {
            this.verts = verts;
            isConcave = false;
            uvs = new List<double>();
            this.bounds = bounds;
            this.interior = interior;
        }

        public void ReverseVerts()
        {
            List<double> reversed = new List<double>();
            for(int i = bounds.Count - 1; i >= 0; i -= 3)
            {
                double x = bounds[i - 2];
                double y = bounds[i - 1];
                double z = bounds[i];
                reversed.Add(x);
                reversed.Add(y);
                reversed.Add(z);
            }
            this.bounds = reversed;
            List<double> uvsReversed = new List<double>();
            for(int i = uvs.Count - 1; i >=0; i -= 2)
            {
                double u = uvs[i - 1];
                double v = uvs[i];
                uvsReversed.Add(u);
                uvsReversed.Add(v);
            }
            this.uvs = uvsReversed;
        }

        public Point[] IPointsAsPoints()
        {
            if (boundsFlattened == null)
                return null;
            Point[] retVal = new Point[boundsFlattened.Length];
            for (int i = 0; i < boundsFlattened.Length; ++i)
                retVal[i] = new Point(boundsFlattened[i].X, boundsFlattened[i].Y);
            return retVal;
        }

        public void ConvertDelaunay()
        {
            verts.Clear();
            for(int i = 0; i < delaunayVerts.GetLength(0); ++i)
            {
                verts.Add(delaunayVerts[i, 0]);
                verts.Add(delaunayVerts[i, 1]);
                verts.Add(delaunayVerts[i, 2]);
            }
            triangles = new int[delaunayTriangles.Length];
            int j = 0;
            for(int i = 0; i < delaunayTriangles.GetLength(0); ++i, j += 3)
            {
                triangles[j] = delaunayTriangles[i, 0];
                triangles[j + 1] = delaunayTriangles[i, 1];
                triangles[j + 2] = delaunayTriangles[i, 2];
            }
        }
    }
}
