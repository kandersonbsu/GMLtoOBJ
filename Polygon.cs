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
        public List<double> uvs;
        public int[] triangles;
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
        }

        public Polygon()
        {
            verts = new List<double>();
            isConcave = false;
            uvs = new List<double>();
            bounds = new List<double>();
        }

        public Polygon(List<double> verts, List<double> bounds)
        {
            this.verts = verts;
            isConcave = false;
            uvs = new List<double>();
            this.bounds = bounds;
        }

        public void ReverseVerts()
        {
            List<double> reversed = new List<double>();
            for(int i = verts.Count - 1; i >= 0; i -= 3)
            {
                double x = verts[i - 2];
                double y = verts[i - 1];
                double z = verts[i];
                reversed.Add(x);
                reversed.Add(y);
                reversed.Add(z);
            }
            this.verts = reversed;
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
    }
}
