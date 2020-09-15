using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Numerics;

namespace GMLtoOBJ
{
    class Building
    {
        private string id;
        public string hash;
        public string ubid;
        public string state;
        public string county;
        public string grid;
        public double latitude;
        public double longitude;
        public double area;
        public double height;
        public string height_source;
        public string fp_source;
        public double measuredHeight;
        public double[] centerpoint;
        public bool needsCoordinateTransform;
        public List<Polygon> sides;
        public int function;
        public Dictionary<Polygon, TriangleNet.Geometry.Polygon> threeDtoTwoD;
        public Building(string id)
        {
            needsCoordinateTransform = true;
            this.id = id;
            sides = new List<Polygon>();
            threeDtoTwoD = new Dictionary<Polygon, TriangleNet.Geometry.Polygon>();
        }

        public string GetID()
        {
            return id;
        }
    }
}
