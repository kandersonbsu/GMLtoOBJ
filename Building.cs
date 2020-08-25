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
        public List<Polygon> sides;
        public Dictionary<Polygon, TriangleNet.Geometry.Polygon> threeDtoTwoD;
        public Building(string id)
        {
            this.id = id;
            sides = new List<Polygon>();
            threeDtoTwoD = new Dictionary<Polygon, TriangleNet.Geometry.Polygon>();
        }

        public void Build(XElement child)
        {
            foreach (XElement node in child.Nodes())
            {
                if (node.Name.ToString().Contains("Solid"))
                {
                    BuildSides(node);
                    continue;
                }
                switch (node.FirstAttribute.Value)
                {
                    case "hash":
                        this.hash = node.Value;
                        break;
                    case "ubid":
                        this.ubid = node.Value;
                        break;
                    case "state":
                        this.state = node.Value;
                        break;
                    case "county":
                        this.county = node.Value;
                        break;
                    case "grid":
                        this.grid = node.Value;
                        break;
                    case "latitude":
                        this.latitude = double.Parse(node.Value);
                        break;
                    case "longitude":
                        this.longitude = double.Parse(node.Value);
                        break;
                    case "area":
                        this.area = double.Parse(node.Value);
                        break;
                    case "height":
                        this.height = double.Parse(node.Value);
                        break;
                    case "height_source":
                        this.height_source = node.Value;
                        break;
                    case "fp_source":
                        this.fp_source = node.Value;
                        break;
                    default:
                        break;
                }
            }
        }

        private void BuildSides(XElement node)
        {
            if (!node.Name.ToString().Contains("LinearRing"))
            {
                foreach (XElement child in node.Nodes())
                    BuildSides(child);
            }
            else
            {
                foreach (XElement element in node.Nodes())
                {
                    List<double> doubleList = new List<double>();
                    
                    var value = element.Value;
                    var stringValues = value.Split(' ');
                    foreach(string s in stringValues)
                        doubleList.Add(double.Parse(s));
                    if(doubleList[0] == doubleList[doubleList.Count - 3] && doubleList[1] == doubleList[doubleList.Count - 2] && doubleList[2] == doubleList[doubleList.Count - 1])
                    {
                        doubleList.RemoveRange(doubleList.Count - 3, 3);
                    }
                    sides.Add(new Polygon(doubleList));
                }
            }
        }
    }
}
