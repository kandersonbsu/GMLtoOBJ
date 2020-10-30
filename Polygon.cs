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
        public Dictionary<string, string> vertsToUvs;
        public Dictionary<string, string> vertsToFlattenedVerts;
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

        public void ConvertDelaunay()
        {
            //We have a map of 2d points and 3d points. 
            //We need to add the 3d points to verts in the order of the 2d delaunay verts. 
            //Iterate through the delaunay verts using the delaunay verts as a key into points adding the 3d values in to verts. 
            verts.Clear();
            for(int i = 0; i < delaunayVerts.GetLength(0); ++i)
            {
                string key = string.Format("{0} {1}", delaunayVerts[i, 0], delaunayVerts[i, 1]);
                string value = GetValueFromDictionary(key, vertsToFlattenedVerts);
                //vertsToFlattenedVerts.TryGetValue(key, out value);
                if (value == null)
                    continue;
                var valueArray = value.Split(' ');
                foreach(string s in valueArray)
                {
                    double d = double.Parse(s);
                    verts.Add(d);
                }
            }
            triangles = new int[delaunayTriangles.Length];
            int j = 0;
            for(int i = 0; i < delaunayTriangles.GetLength(0); ++i, j += 3)
            {
                triangles[j] = delaunayTriangles[i, 0];
                triangles[j + 1] = delaunayTriangles[i, 1];
                triangles[j + 2] = delaunayTriangles[i, 2];
            }
            if (uvs.Count < 2)
                return;
            //iterate through the verts. 
            //Stringify the verts. 
            //use the stringify verts as a key into the dictionary. 
            //Parse the uvs into doubles, add them to a list of doubles for the UVs. 
            //Once done, assign the new uv list to the current uv list. 
            List<double> newUVs = new List<double>();
            for(int i = 0; i < verts.Count; i += 3)
            {
                string vertString = string.Format("{0} {1} {2}", verts[i], verts[i + 1], verts[i + 2]);
                string uvString;
                vertsToUvs.TryGetValue(vertString, out uvString);
                if (uvString == null)
                    continue;
                var uvArray = uvString.Split(' ');
                foreach(var uv in uvArray)
                {
                    double d = double.Parse(uv);
                    newUVs.Add(d);
                }
            }
            uvs.Clear();
            uvs = newUVs;
        }

        private string GetValueFromDictionary(string key, Dictionary<string, string> dictionary)
        {
            double diff = 0.001;
            var array = key.Split(' ');
            double d1 = double.Parse(array[0]);
            double d2 = double.Parse(array[1]);
            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                var kvpArray = kvp.Key.Split(' ');
                double val1 = double.Parse(kvpArray[0]);
                double val2 = double.Parse(kvpArray[1]);
                if (Math.Abs(d1 - val1) < diff && Math.Abs(d2 - val2) < diff)
                    return kvp.Value;
            }
            return null;
        }

        public void CreateVertUVDictionary()
        {
            if (uvs.Count < 2)
                return;
            vertsToUvs = new Dictionary<string, string>();
            int j = 0;
            for (int i = 0; i < verts.Count; i += 3, j += 2)
            {
                //stringify the verts
                string vertString = string.Format("{0} {1} {2}", verts[i], verts[i + 1], verts[i + 2]);

                //stringify the uvs
                string uvString = string.Format("{0} {1}", uvs[j], uvs[j + 1]);
                if (vertsToUvs.ContainsKey(vertString))
                    continue;
                vertsToUvs.Add(vertString, uvString);
            }
        }

        public void CreateVertDictionary(double[,] flattenedVerts)
        {
            if (verts.Count < 3)
                return;
            vertsToFlattenedVerts = new Dictionary<string, string>();
            int j = 0;
            for(int i = 0; i < verts.Count; i += 3, ++j)
            {
                string flatVertString = string.Format("{0} {1}", flattenedVerts[j, 0], flattenedVerts[j, 1]);
                string vertString = string.Format("{0} {1} {2}", verts[i], verts[i + 1], verts[i + 2]);
                if (vertsToFlattenedVerts.ContainsKey(flatVertString))
                    continue;
                vertsToFlattenedVerts.Add(flatVertString, vertString);
            }
        }
    }
}
