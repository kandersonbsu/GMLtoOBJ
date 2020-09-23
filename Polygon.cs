using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using DelaunatorSharp;

namespace GMLtoOBJ
{
    class Polygon
    {
        public List<double> verts;
        public List<double> uvs;
        public int[] triangles;
        public bool isConcave;
        public IPoint[] pointsFlattened;
        public string gmlID;
        public string parentID;
        public Polygon(List<double> verts)
        {
            this.verts = verts;
            isConcave = false;
            uvs = new List<double>();
        }

        public Polygon()
        {
            verts = new List<double>();
            uvs = new List<double>();
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
    }
}
