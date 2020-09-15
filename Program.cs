using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DelaunatorSharp;
using Cognitics.CoordinateSystems;

namespace GMLtoOBJ
{
    class Program
    {
        static string PathToFileFolder;
        static string PathToOutputFolder;
        static int BuildingCount;
        static void Main(string[] args)
        {
            PathToFileFolder = args[0] == null ? "" : args[0];
            var d = Path.GetDirectoryName(PathToFileFolder);
            PathToOutputFolder = args.Length > 1 ? args[1] : File.Exists(PathToFileFolder) ? Path.GetDirectoryName(PathToFileFolder) + "\\output": PathToFileFolder + "\\output";
            if (args[0] == null)
            {
                PrintUsage();
                return;
            }
            try
            {
                Directory.CreateDirectory(PathToOutputFolder);
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                PrintUsage();
                return;
            }
            if(File.Exists(PathToFileFolder))
                OpenFile(PathToFileFolder);
            else if(Directory.Exists(PathToFileFolder))
            {
                DirectoryInfo dinfo = new DirectoryInfo(PathToFileFolder);
                foreach(var file in dinfo.GetFiles("*.gml"))
                {
                    OpenFile(file.FullName);
                }
            }
        }

        static void PrintUsage()
        {
            Console.Out.WriteLine("CityGML to OBJ Converter.");
            Console.Out.WriteLine("GMLtoOBJ [Path to file/folder] [Path to output folder.");
            Console.Out.WriteLine("If no output Folder is specified, the default will create an output folder in the input directory.");
        }

        static void OpenFile(string path)
        {
            List<Building> buildings = new List<Building>();
            if (File.Exists(path))
            {
                var progressBar = new ProgressBar();
                BuildingCount = 0;
                Console.Out.WriteLine("Parsing buildings from " + path);
                XDocument document = XDocument.Load(path);
                foreach (XElement element in document.Root.Elements())
                {
                    if (element.Name.ToString().Contains("cityObjectMember"))
                        ++BuildingCount;
                }
                if (document.Root != null)
                {
                    int progress = 0;
                    foreach (XElement element in document.Root.Elements())
                    {
                        if (element.Name.ToString().Contains("cityObjectMember"))
                        {
                            foreach(XElement child in element.Elements())
                            {
                                if (child.Name.ToString().Contains("building"))
                                {
                                    buildings.Add(Build(child));
                                    ++progress;
                                    progressBar.Report((double)progress / BuildingCount);
                                }
                            }
                        }
                    }
                }
                System.Threading.Thread.Sleep(1000);
                BuildingtoOBJ(buildings, path);
            }
        }

        static void BuildSides(XElement node, ref Building building)
        {
            if (!node.Name.ToString().Contains("LinearRing"))
            {
                foreach (XElement child in node.Nodes())
                    BuildSides(child, ref building);
            }
            else
            {
                foreach (XElement element in node.Nodes())
                {
                    List<double> doubleList = new List<double>();

                    var value = element.Value;
                    var stringValues = value.Split(' ');
                    foreach (string s in stringValues)
                        doubleList.Add(double.Parse(s));
                    if (doubleList[0] == doubleList[doubleList.Count - 3] && doubleList[1] == doubleList[doubleList.Count - 2] && doubleList[2] == doubleList[doubleList.Count - 1])
                    {
                        doubleList.RemoveRange(doubleList.Count - 3, 3);
                    }
                    building.sides.Add(new Polygon(doubleList));
                }
            }
        }

        static Building Build(XElement child)
        {
            Building retVal = new Building(child.FirstAttribute.Value);
            string building = "http://www.opengis.net/citygml/building/2.0";
            string gml = "http://www.opengis.net/gml";
            var bldgBoundedBy = XName.Get("boundedBy", building);
            var gmlBoundedby = XName.Get("boundedBy", gml);
            var gmlEnvelope = XName.Get("Envelope", gml);
            var bldgFunction = XName.Get("function", building);
            var bldgMeasuredHeight = XName.Get("measuredHeight", building);
            var nodes = child.Nodes();
            foreach (XElement node in child.Nodes())
            {
                if (node.Name == bldgFunction)
                {
                    retVal.function = int.Parse(node.Value);
                    continue;
                }
                if(node.Name == bldgMeasuredHeight)
                {
                    retVal.measuredHeight = double.Parse(node.Value);
                }
                if (node.Name == gmlBoundedby)
                {
                    var firstChild = node.Element(gmlEnvelope);
                    retVal.needsCoordinateTransform = firstChild.FirstAttribute.Value.Contains("EPSG:4979");
                    var lowerCorner = firstChild.Element(XName.Get("lowerCorner", gml));
                    var upperCorner = firstChild.Element(XName.Get("upperCorner", gml));
                    var lowerCornerArray = lowerCorner.Value.Split(' ');
                    var upperCornerArray = upperCorner.Value.Split(' ');
                    double[] averageArray = new double[3];
                    averageArray[0] = (double.Parse(lowerCornerArray[0]) + double.Parse(upperCornerArray[0])) / 2;
                    averageArray[1] = (double.Parse(lowerCornerArray[1]) + double.Parse(upperCornerArray[1])) / 2;
                    averageArray[2] = (double.Parse(lowerCornerArray[2]) + double.Parse(upperCornerArray[2])) / 2;
                    retVal.centerpoint = averageArray;
                    continue;
                }
                if (node.Name.ToString().Contains("Solid"))
                {
                    BuildSides(node, ref retVal);
                    continue;
                }
                if (node.Name.ToString().Contains("appearance"))
                {
                    ParseAppearance(node);
                    continue;
                }
                if (node.Name == bldgBoundedBy)
                {
                    //Here is the root node to our building geometry. In LOD2, this is either walls, roofs, or floors. This will likely hit three times per building.  
                    LOD2Polygons(node, ref retVal);
                }
                if (node.FirstAttribute == null)
                    continue;
                switch (node.FirstAttribute.Value)
                {
                    case "hash":
                        retVal.hash = node.Value;
                        break;
                    case "ubid":
                        retVal.ubid = node.Value;
                        break;
                    case "state":
                        retVal.state = node.Value;
                        break;
                    case "county":
                        retVal.county = node.Value;
                        break;
                    case "grid":
                        retVal.grid = node.Value;
                        break;
                    case "latitude":
                        retVal.latitude = double.Parse(node.Value);
                        break;
                    case "longitude":
                        retVal.longitude = double.Parse(node.Value);
                        break;
                    case "area":
                        retVal.area = double.Parse(node.Value);
                        break;
                    case "height":
                        retVal.height = double.Parse(node.Value);
                        break;
                    case "height_source":
                        retVal.height_source = node.Value;
                        break;
                    case "fp_source":
                        retVal.fp_source = node.Value;
                        break;
                    default:
                        break;
                }
            }
            return retVal;
        }

        static void LOD2Polygons(XElement element, ref Building building)
        {
            string gmlURL = "http://www.opengis.net/gml";
            string buildingURL = "http://www.opengis.net/citygml/building/2.0";
            //element is bldg:boundedBy
            //Get the wallsurface, groundsurface, or roofsurface node
            var surface = element.Element(XName.Get("WallSurface", buildingURL));
            if (surface == null)
                surface = element.Element(XName.Get("GroundSurface", buildingURL));
            if (surface == null)
                surface = element.Element(XName.Get("RoofSurface", buildingURL));
            if (surface == null)
                return;
            //Get the lod2MultiSurface node
            var lod2MultiSurface = surface.Element(XName.Get("lod2MultiSurface", buildingURL));
            //Get the gml MultiSurface node
            var multisurface = lod2MultiSurface.Element(XName.Get("MultiSurface", gmlURL));
            //Each child of the multisurface node is a polygon
            //Foreach child of multisurface, 
            //create a new polygon, 
            //get the gml:id as the polygon id, 
            //parse through the double list to create the polygon. Exclude the last three points if they match the first three. 
            foreach(XElement child in multisurface.Elements())
            {
                if (child.Name != XName.Get("surfaceMember", gmlURL))
                    continue;
                Polygon polygon = new Polygon();
                var gmlPolygon = child.Element(XName.Get("Polygon", gmlURL));
                //gmlPolygon.Value is the positions
                polygon.gmlID = gmlPolygon.FirstAttribute.Value;
                foreach (var poly in gmlPolygon.Elements())
                {
                    var rawValues = poly.Value.Split(' ');
                    foreach (string s in rawValues)
                        polygon.verts.Add(double.Parse(s));
                    if (polygon.verts[0] == polygon.verts[polygon.verts.Count - 3] && polygon.verts[1] == polygon.verts[polygon.verts.Count - 2] && polygon.verts[2] == polygon.verts[polygon.verts.Count - 1])
                        polygon.verts.RemoveRange(polygon.verts.Count - 3, 3);
                    if (!building.needsCoordinateTransform)
                        NormalizeVerts(building, ref polygon);
                    building.sides.Add(polygon);
                }
            }
        }

        static void NormalizeVerts(Building building, ref Polygon polygon)
        {
            if (building.centerpoint == null || building.centerpoint.Length != 3)
                return;
            var centerPoint = building.centerpoint;
            for(int i = 0; i < polygon.verts.Count; ++i)
            {
                int index = Modulo(i, 3);
                polygon.verts[i] = polygon.verts[i] - centerPoint[index];
            }
        }

        static void LOD2Sides(XElement element, ref Building building)
        {
            string elementName = element.Name.ToString();

            if (!elementName.Contains("MultiSurface"))
            {
                foreach (XElement node in element.Nodes())
                    LOD2Sides(node, ref building);
            }
            foreach(XElement child in element.Nodes())
            {
                if (!child.Name.ToString().Contains("surfaceMember"))
                    continue;
                List<double> verts = new List<double>();
                
            }
        }

        static void ParseAppearance(XElement node)
        {
            string app = "http://www.opengis.net/citygml/appearance/2.0";
            XName appearance = XName.Get("Appearance", app);
            XName surfaceDataMember = XName.Get("surfaceDataMember", app);
            var child = node.Element(appearance);
            var surfaceDataMembers = child.Elements(surfaceDataMember);
            foreach(var sfd in surfaceDataMembers)
            {
                var ParameterizedTexture = XName.Get("ParameterizedTexture", app);
                var textures = sfd.Elements(ParameterizedTexture);
                foreach(var texture in textures)
                {
                    GMLTexture gmlTexture = new GMLTexture(texture.FirstAttribute.Value);
                    CreateTexture(ref gmlTexture, texture);
                }
            }
        }

        static void CreateTexture(ref GMLTexture texture, XElement element)
        {
            string app = "http://www.opengis.net/citygml/appearance/2.0";
            foreach (var child in element.Elements())
            {
                if(child.Name == XName.Get("imageURI", app))
                {
                    texture.imageURI = child.Value;
                    continue;
                }
                if(child.Name == XName.Get("mimeType", app))
                {
                    texture.mimeType = child.Value;
                    continue;
                }
                if(child.Name == XName.Get("textureType", app))
                {
                    texture.textureType = child.Value;
                    continue;
                }
                if(child.Name == XName.Get("wrapMode", app))
                {
                    texture.wrapMode = child.Value;
                    continue;
                }
                if(child.Name == XName.Get("borderColor", app))
                {
                    string value = child.Value;
                    var valArray = value.Split(' ');
                    int index = 0;
                    foreach(string s in valArray)
                    {
                        double d = double.Parse(s);
                        texture.borderColor[index] = d;
                        ++index;
                    }
                    continue;
                }
                if(child.Name == XName.Get("target", app))
                {
                    texture.targetURI = child.FirstAttribute.Value;
                    //We also have to parse out the texture coordinates here. 
                    string value = child.Value;
                    string[] valueArray = value.Split(' ');
                    foreach(string s in valueArray)
                    {
                        double d = double.Parse(s);
                        texture.textureCoordinates.Add(d);
                    }
                    continue;
                }
            }
        }

        static bool IsInPolygon(IPoint[] polygon, IPoint point)
        {
            IPoint p1, p2;
            bool inside = false;
            if (polygon.Length < 3)
                return false;
            var oldPoint = new Point(polygon[polygon.Length - 1].X, polygon[polygon.Length - 1].Y);
            for(int i = 0; i < polygon.Length; ++i)
            {
                var newPoint = new Point(polygon[i].X, polygon[i].Y);

                if(newPoint.X > oldPoint.X)
                {
                    p1 = oldPoint;
                    p2 = newPoint;
                }
                else
                {
                    p1 = newPoint;
                    p2 = oldPoint;
                }
                if((newPoint.X < point.X) == (point.X <= oldPoint.X) && (point.Y - p1.Y) * (p2.X - p1.X) < (p2.Y - p1.Y)*(point.X - p1.X))
                {
                    inside = !inside;
                }
                oldPoint = newPoint;
            }
            return inside;
        }

        static void BuildingtoOBJ(List<Building> buildings, string path)
        {
            Console.WriteLine("");
            Console.WriteLine("Creating OBJ files from Buildings:");
            int iteration = 1;
            int progress = 0;
            var progressBar = new ProgressBar();
            foreach(Building b in buildings)
            {
                if(b.needsCoordinateTransform)
                {
                    for (int i = 0; i < b.sides.Count; ++i)
                    {
                        Polygon p = b.sides[i];
                        ProjectPolygon(ref p, b.latitude, b.longitude);
                    }
                }
                foreach(Polygon p in b.sides)
                {
                    var xy = TwoDimensionalPolygon(p, "xy");
                    var xz = TwoDimensionalPolygon(p, "xz");
                    var yz = TwoDimensionalPolygon(p, "yz");

                    var xyArea = PolygonArea(xy);
                    var xzArea = PolygonArea(xz);
                    var yzArea = PolygonArea(yz);
                    Delaunator d;
                    bool convex;
                    if(xyArea >= xzArea && xyArea >= yzArea)
                    {
                        if (!IsClockwise(xy))
                        {
                            var pverts = p.verts;
                            Array.Reverse(xy);
                            p.ReverseVerts();
                            d = new Delaunator(xy);
                            int[] tris = InvertTriangles(d.Triangles);
                            p.triangles = tris;
                        }
                        else
                            d = new Delaunator(xy);
                        p.pointsFlattened = xy;
                        convex = IsConvex(xy);
                        if (!convex)
                            p.isConcave = true;
                    }
                    else if(xzArea >= xyArea && xzArea >= yzArea)
                    {
                        if (!IsClockwise(xz))
                        {
                            var pverts = p.verts;
                            Array.Reverse(xz);
                            p.ReverseVerts();
                            d = new Delaunator(xz);
                            int[] tris = InvertTriangles(d.Triangles);
                            p.triangles = tris;
                        }
                        else
                            d = new Delaunator(xz);
                        p.pointsFlattened = xz;
                        convex = IsConvex(xz);
                        if (!convex)
                            p.isConcave = true;
                    }
                    else
                    {
                        if (!IsClockwise(yz))
                        {
                            var pverts = p.verts;
                            Array.Reverse(yz);
                            p.ReverseVerts();
                            d = new Delaunator(yz);
                            int[] tris = InvertTriangles(d.Triangles);
                            p.triangles = tris;
                        }
                        else
                            d = new Delaunator(yz);
                        p.pointsFlattened = yz;
                        convex = IsConvex(yz);
                        if (!convex)
                            p.isConcave = true;
                    }
                    if (p.triangles == null)
                        p.triangles = d.Triangles;
                    if (p.isConcave)
                        p.triangles = TriangulatePolygon(p);                                                                                                                                                                                                                                                                                                                                                                                                   
                }
                string filename = Path.GetFileName(path);
                var filenameNoExtension = filename.Replace(".gml", "");
                string buildingName = "\\" + b.state + "_" + b.county + "_" + b.ubid + ".obj";
                if (buildingName == "\\__.obj")
                    buildingName = "\\" + b.GetID() + ".obj";
                ++iteration;
                using (StreamWriter sw = File.CreateText(PathToOutputFolder + buildingName))
                {
                    sw.WriteLine("Produced by Cognitics");
                    sw.WriteLine(DateTime.Now);
                    string origin = b.centerpoint == null ? "ORIGIN: " + b.latitude + " " + b.longitude : "ORIGIN: " + b.centerpoint[0] + " " + b.centerpoint[1] + " " + b.centerpoint[2];
                    sw.WriteLine(origin);
                    sw.WriteLine("");
                    foreach(Polygon p in b.sides)
                    {
                        for (int i = 0; i < p.verts.Count - 1; i += 3)
                        {
                            sw.WriteLine("v " + p.verts[i] + " " + p.verts[i + 1] + " " + p.verts[i + 2]);
                        }
                    }
                    int triangleOffset = 1;
                    foreach(Polygon p in b.sides)
                    {
                        for (int i = 0; i < p.triangles.Length; i += 3)
                        {
                            sw.WriteLine("f " + (p.triangles[i] + triangleOffset) + " " + (p.triangles[i + 1] + triangleOffset) + " " + (p.triangles[i + 2] + triangleOffset));
                        }
                        triangleOffset += p.verts.Count / 3;
                    }
                }
                ++progress;
                progressBar.Report((double)progress / BuildingCount);
            }
            System.Threading.Thread.Sleep(1000);
            Console.WriteLine();
        }

        static int[] InvertTriangles(int[] triangles)
        {
            int[] retval = new int[triangles.Length];
            for(int i = 0; i < triangles.Length; i += 3)
            {
                retval[i] = triangles[i + 2];
                retval[i + 1] = triangles[i + 1];
                retval[i + 2] = triangles[i];
            }
            return retval;
        }

        static bool IsClockwise(IPoint[] points)
        {
            double sum = 0.0;
            for(int i = 0; i < points.Length; ++i)
            {
                IPoint i1 = points[i];
                IPoint i2 = points[Modulo(i + 1, points.Length)];
                sum += (i2.X - i1.X) * (i2.Y + i1.Y);
            }
            return sum > 0.0;
        }

        static int[] TriangulatePolygon(Polygon polygon)
        {
            int[] triangles = polygon.triangles;
            List<int> prunedTriangles = new List<int>();
            for(int i = 0; i < triangles.Length; i += 3)
            {
                //find the mid point, if this triangle is in the polygon, add these to the prunedTriangle list. 
                var firstTri = polygon.pointsFlattened[triangles[i]];
                var secondTri = polygon.pointsFlattened[triangles[i + 1]];
                var thirdTri = polygon.pointsFlattened[triangles[i + 2]];

                var xAverage = (firstTri.X + secondTri.X + thirdTri.X) / 3;
                var yAverage = (firstTri.Y + secondTri.Y + thirdTri.Y) / 3;

                Point p = new Point(xAverage, yAverage);
                if(IsInPolygon(polygon.pointsFlattened, p))
                {
                    prunedTriangles.Add(triangles[i]);
                    prunedTriangles.Add(triangles[i + 1]);
                    prunedTriangles.Add(triangles[i + 2]);
                }
            }
            return prunedTriangles.ToArray();
        }

        static bool IsConvex(IPoint[] points)
        {
            if (points.Length < 4)
                return true;
            bool sign = false;
            for(int i = 0; i < points.Length; ++i)
            {
                double dx1 = points[(i + 2) % points.Length].X - points[(i + 1) % points.Length].X;
                double dy1 = points[(i + 2) % points.Length].Y - points[(i + 1) % points.Length].Y;
                double dx2 = points[i].X - points[(i + 1) % points.Length].X;
                double dy2 = points[i].Y - points[(i + 1) % points.Length].Y;
                double crossproduct = dx1 * dy2 - dy1 * dx2;
                if (i == 0)
                    sign = crossproduct > 0;
                else if (sign != (crossproduct > 0))
                    return false;
            }
            return true;
        }

        static int Modulo(int a, int b)
        {
            int mod = a % b;
            return mod < 0 ? mod + b : mod;
        }

        static void ProjectPolygon(ref Polygon polygon, double lattitude, double longitude)
        {
            GeographicCoordinates coords = new GeographicCoordinates(lattitude, longitude);
            FlatEarthProjection fep = new FlatEarthProjection(coords);
            for(int i = 0; i < polygon.verts.Count - 1; i += 3)
            {
                double x, y;
                fep.TransformToCartesian(polygon.verts[i + 1], polygon.verts[i], out x, out y);
                polygon.verts[i] = y;
                polygon.verts[i + 1] = x;
            }
        }

        static double PolygonArea(IPoint[] polygon)
        {
            double retVal = 0.0;
            int i, j;

            for(i = 0; i < polygon.Length; ++i)
            {
                j = (i + 1) % polygon.Length;
                retVal += polygon[i].X * polygon[j].Y;
                retVal -= polygon[i].Y * polygon[j].X;
            }
            retVal /= 2;

            return (retVal < 0) ? -retVal : retVal;
        }

        static IPoint[] TwoDimensionalPolygon(Polygon p, string components)
        {
            if (components.Length != 2)
                return null;
            IPoint[] retVal = new IPoint[p.verts.Count / 3];
            string comps = components.ToLower();
            if(comps.Contains('x') && comps.Contains('y'))
            {
                for(int i = 0; i < p.verts.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.verts[i], p.verts[i + 1]);
                }
                return retVal;
            }
            if(comps.Contains('x') && comps.Contains('z'))
            {
                for (int i = 0; i < p.verts.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.verts[i], p.verts[i + 2]);
                }
                return retVal;
            }
            if(comps.Contains('y') && comps.Contains('z'))
            {
                for (int i = 0; i < p.verts.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.verts[i + 1], p.verts[i + 2]);
                }
                return retVal;
            }
            else
                return null;
        }
    }
}
