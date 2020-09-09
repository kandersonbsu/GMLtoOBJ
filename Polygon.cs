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
        public int[] triangles;
        public bool isConcave;
        public IPoint[] pointsFlattened;
        public string gmlID;
        public Polygon(List<double> verts)
        {
            this.verts = verts;
            isConcave = false;
        }

        public Polygon()
        {

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
        }
    }
}
