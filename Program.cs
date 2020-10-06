using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DelaunatorSharp;
using Cognitics.CoordinateSystems;
using System.Numerics;

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
            int i = 0;
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
                    retVal.textures.AddRange(ParseAppearance(node));
                    continue;
                }
                if (node.Name == bldgBoundedBy)
                {
                    ++i;
                    if (i != 274)
                        continue;
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

            //This is temporary in order to just get the roof
            if (surface.Name == XName.Get("WallSurface", buildingURL) || surface.Name == XName.Get("GroundSurface", buildingURL))
               return;
            //Get the lod2MultiSurface node
            var lod2MultiSurface = surface.Element(XName.Get("lod2MultiSurface", buildingURL));
            //Get the gml MultiSurface node
            var multisurface = lod2MultiSurface.Element(XName.Get("MultiSurface", gmlURL));
            var parentSurfaceID = multisurface.FirstAttribute.Value;
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
                polygon.parentID = parentSurfaceID;
                var gmlPolygon = child.Element(XName.Get("Polygon", gmlURL));
                //gmlPolygon.Value is the positions
                polygon.gmlID = gmlPolygon.FirstAttribute.Value;
                foreach (var poly in gmlPolygon.Elements())
                {
                    if (poly.Name == XName.Get("interior", gmlURL))
                    {
                        var rawValues = poly.Value.Split(' ');
                        foreach (string s in rawValues)
                            polygon.verts.Add(double.Parse(s));
                        if (polygon.verts[0] == polygon.verts[polygon.verts.Count - 3] && polygon.verts[1] == polygon.verts[polygon.verts.Count - 2] && polygon.verts[2] == polygon.verts[polygon.verts.Count - 1])
                            polygon.verts.RemoveRange(polygon.verts.Count - 3, 3);
                    }
                    else
                    {
                        var rawValues = poly.Value.Split(' ');
                        foreach (string s in rawValues)
                        {
                            double d = double.Parse(s);
                            polygon.verts.Add(d);
                            polygon.bounds.Add(d);
                        }
                        if (polygon.verts[0] == polygon.verts[polygon.verts.Count - 3] && polygon.verts[1] == polygon.verts[polygon.verts.Count - 2] && polygon.verts[2] == polygon.verts[polygon.verts.Count - 1])
                            polygon.verts.RemoveRange(polygon.verts.Count - 3, 3);
                        if (polygon.bounds[0] == polygon.bounds[polygon.bounds.Count - 3] && polygon.bounds[1] == polygon.bounds[polygon.bounds.Count - 2] && polygon.bounds[2] == polygon.bounds[polygon.bounds.Count - 1])
                            polygon.bounds.RemoveRange(polygon.bounds.Count - 3, 3);
                    }

                }
                if (!building.needsCoordinateTransform)
                    NormalizeVerts(building, ref polygon);
                building.sides.Add(polygon);
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
            for(int i = 0; i < polygon.bounds.Count; ++i)
            {
                int index = Modulo(i, 3);
                polygon.bounds[i] = polygon.bounds[i] - centerPoint[index];
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

        static List<ISurfaceDataMember> ParseAppearance(XElement node)
        {
            List<ISurfaceDataMember> retVal = new List<ISurfaceDataMember>();
            string app = "http://www.opengis.net/citygml/appearance/2.0";
            XName appearance = XName.Get("Appearance", app);
            XName surfaceDataMember = XName.Get("surfaceDataMember", app);
            var child = node.Element(appearance);
            var surfaceDataMembers = child.Elements(surfaceDataMember);
            foreach (var sfd in surfaceDataMembers)
            {
                var ParameterizedTexture = XName.Get("ParameterizedTexture", app);
                var X3DMaterial = XName.Get("X3DMaterial", app);
                var textures = sfd.Elements(ParameterizedTexture);
                var materials = sfd.Elements(X3DMaterial);
                foreach (var texture in textures)
                {
                    ParameterizedTexture gmlTexture = new ParameterizedTexture(texture.FirstAttribute.Value);
                    CreateParamaterizedTexture(ref gmlTexture, texture);
                    retVal.Add(gmlTexture);
                }
                foreach(var material in materials)
                {
                    X3DMaterial mat = new X3DMaterial(material.FirstAttribute.Value);
                    CreateX3DMaterial(ref mat, material);
                    retVal.Add(mat);
                }
            }
            return retVal;
        }

        static void CreateParamaterizedTexture(ref ParameterizedTexture texture, XElement element)
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
                    string target = child.FirstAttribute.Value.Substring(1, child.FirstAttribute.Value.Length - 1);
                    texture.targetURI = target;
                    //We also have to parse out the texture coordinates here. 
                    var textCoordList = XName.Get("TexCoordList", app);
                    var tclNode = child.Elements(textCoordList);
                    var textureCoordinates = tclNode.Elements(XName.Get("textureCoordinates", app));
                    foreach(var t in textureCoordinates)
                    {
                        string value = t.Value;
                        string[] valueArray = value.Split(' ');
                        foreach (string s in valueArray)
                        {
                            double d = double.Parse(s);
                            texture.textureCoordinates.Add(d);
                        }
                        if (texture.textureCoordinates[0] == texture.textureCoordinates[texture.textureCoordinates.Count - 2] && texture.textureCoordinates[1] == texture.textureCoordinates[texture.textureCoordinates.Count - 1])
                            texture.textureCoordinates.RemoveRange(texture.textureCoordinates.Count - 2, 2);
                        continue;
                    }
                }
            }
        }

        static void CreateX3DMaterial(ref X3DMaterial material, XElement element)
        {
            string gmlString = "http://www.opengis.net/gml";
            string appString = "http://www.opengis.net/citygml/appearance/2.0";
            material.gmlID = element.FirstAttribute.Value;
            foreach (var child in element.Elements())
            {
                if(child.Name == XName.Get("name", gmlString))
                {
                    material.name = child.Value;
                    continue;
                }
                if(child.Name == XName.Get("ambientIntensity", appString))
                {
                    material.ambientIntensity = double.Parse(child.Value);
                    try
                    {
                        material.ambientIntensity = double.Parse(child.Value);
                    }catch(Exception e)
                    {
                        material.ambientIntensity = 0.2; //0.2 is the default ambient intensity value
                    }
                    continue;
                }
                if(child.Name == XName.Get("diffuseColor", appString))
                {
                    var vals = child.Value.Split(' ');
                    material.diffuseColor = ParseColorValue(vals);
                    continue;
                }
                if(child.Name == XName.Get("emissiveColor", appString))
                {
                    var vals = child.Value.Split(' ');
                    material.emissiveColor = ParseColorValue(vals);
                    continue;
                }
                if (child.Name == XName.Get("specularColor", appString))
                {
                    var vals = child.Value.Split(' ');
                    material.specularColor = ParseColorValue(vals);
                    continue;
                }
                if (child.Name == XName.Get("shininess", appString))
                {
                    material.shininess = double.Parse(child.Value);
                    continue;
                }
                if (child.Name == XName.Get("transparency", appString))
                {
                    material.transparency = double.Parse(child.Value);
                    continue;
                }
                if (child.Name == XName.Get("target", appString))
                {
                    string target = child.Value;
                    target = target.Substring(1, target.Length - 1);
                    material.target = target;
                    continue;
                }
            }
        }

        /// <summary>
        /// New IsInPolygon code
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        // Given three colinear points p, q, r,  
        // the function checks if point q lies 
        // on line segment 'pr' 
        static bool onSegment(Point p, Point q, Point r)
        {
            if (q.X <= Math.Max(p.X, r.X) &&
                q.X >= Math.Min(p.X, r.X) &&
                q.Y <= Math.Max(p.Y, r.Y) &&
                q.Y >= Math.Min(p.Y, r.Y))
            {
                return true;
            }
            return false;
        }

        // To find orientation of ordered triplet (p, q, r). 
        // The function returns following values 
        // 0 --> p, q and r are colinear 
        // 1 --> Clockwise 
        // 2 --> Counterclockwise 
        static int orientation(Point p, Point q, Point r)
        {
            double val = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);

            if (val == 0)
            {
                return 0; // colinear 
            }
            return (val > 0) ? 1 : 2; // clock or counterclock wise 
        }

        // The function that returns true if  
        // line segment 'p1q1' and 'p2q2' intersect. 
        static bool doIntersect(Point p1, Point q1,
                                Point p2, Point q2)
        {
            // Find the four orientations needed for  
            // general and special cases 
            int o1 = orientation(p1, q1, p2);
            int o2 = orientation(p1, q1, q2);
            int o3 = orientation(p2, q2, p1);
            int o4 = orientation(p2, q2, q1);

            // General case 
            if (o1 != o2 && o3 != o4)
            {
                return true;
            }

            // Special Cases 
            // p1, q1 and p2 are colinear and 
            // p2 lies on segment p1q1 
            if (o1 == 0 && onSegment(p1, p2, q1))
            {
                return true;
            }

            // p1, q1 and p2 are colinear and 
            // q2 lies on segment p1q1 
            if (o2 == 0 && onSegment(p1, q2, q1))
            {
                return true;
            }

            // p2, q2 and p1 are colinear and 
            // p1 lies on segment p2q2 
            if (o3 == 0 && onSegment(p2, p1, q2))
            {
                return true;
            }

            // p2, q2 and q1 are colinear and 
            // q1 lies on segment p2q2 
            if (o4 == 0 && onSegment(p2, q1, q2))
            {
                return true;
            }

            // Doesn't fall in any of the above cases 
            return false;
        }

        // Returns true if the point p lies  
        // inside the polygon[] with n vertices 
        static bool isInside(Point[] polygon, Point p)
        {
            int n = polygon.Length;
            // There must be at least 3 vertices in polygon[] 
            if (n < 3)
            {
                return false;
            }

            // Create a point for line segment from p to infinite 
            Point extreme = new Point(10000, p.Y);

            // Count intersections of the above line  
            // with sides of polygon 
            int count = 0, i = 0;
            do
            {
                int next = (i + 1) % n;

                // Check if the line segment from 'p' to  
                // 'extreme' intersects with the line  
                // segment from 'polygon[i]' to 'polygon[next]' 
                if (doIntersect(polygon[i],
                                polygon[next], p, extreme))
                {
                    // If the point 'p' is colinear with line  
                    // segment 'i-next', then check if it lies  
                    // on segment. If it lies, return true, otherwise false 
                    if (orientation(polygon[i], p, polygon[next]) == 0)
                    {
                        return onSegment(polygon[i], p,
                                         polygon[next]);
                    }
                    count++;
                }
                i = next;
            } while (i != 0);

            // Return true if count is odd, false otherwise 
            return (count % 2 == 1); // Same as (count%2 == 1) 
        }


        //End of new IsInPolygon code
        static Vector3 ParseColorValue(string[] values)
        {
            if (values.Length != 3)
                return Vector3.Zero;
            Vector3 retVal = new Vector3();
            retVal.X = (float)double.Parse(values[0]);
            retVal.Y = (float)double.Parse(values[1]);
            retVal.Z = (float)double.Parse(values[2]);
            return retVal;
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
                b.CreateSideUVs();
                if (b.needsCoordinateTransform)
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
                        p.boundsFlattened = TwoDimensionalBounds(p, "xy");
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
                        p.boundsFlattened = TwoDimensionalBounds(p, "xz");
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
                        p.boundsFlattened = TwoDimensionalBounds(p, "yz");
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
                string buildingMTL = buildingName.Replace(".obj", ".mtl");
                CreateMTLFile(b.textures, PathToOutputFolder + buildingMTL);
                buildingMTL = buildingMTL.Trim('\\');
                using (StreamWriter sw = File.CreateText(PathToOutputFolder + buildingName))
                {
                    int vtOffset = 0;
                    sw.WriteLine("Produced by Cognitics");
                    sw.WriteLine(DateTime.Now);
                    string origin = b.centerpoint == null ? "ORIGIN: " + b.latitude + " " + b.longitude : "ORIGIN: " + b.centerpoint[0] + " " + b.centerpoint[1] + " " + b.centerpoint[2];
                    sw.WriteLine(origin);
                    sw.WriteLine("mtllib " + buildingMTL);
                    sw.WriteLine("");
                    int triangleOffset = 1;
                    foreach (Polygon p in b.sides)
                    {
                        bool vertexTextures = false;
                        sw.WriteLine("# PolygonID " + p.gmlID + " ParentID " + p.parentID);
                        for (int i = 0; i < p.verts.Count - 1; i += 3)
                        {
                            sw.WriteLine("v " + p.verts[i] + " " + p.verts[i + 1] + " " + p.verts[i + 2]);
                        }
                        for(int i = 0; i < p.uvs.Count - 1; i += 2)
                        {
                            vertexTextures = true;
                            sw.WriteLine("vt " + p.uvs[i] + " " + p.uvs[i + 1]);
                        }
                        if (!vertexTextures)
                            vtOffset -= (p.verts.Count / 3);
                        if (vertexTextures)
                        {
                            sw.WriteLine("usemtl " + p.gmlID);
                            for (int i = 0; i < p.triangles.Length; i += 3)
                            {
                                int firstV = p.triangles[i] + triangleOffset;
                                int secondV = p.triangles[i + 1] + triangleOffset;
                                int thirdV = p.triangles[i + 2] + triangleOffset;

                                int firstVT = p.triangles[i] + triangleOffset + vtOffset;
                                int secondVT = p.triangles[i + 1] + triangleOffset + vtOffset;
                                int thirdVT = p.triangles[i + 2] + triangleOffset + vtOffset;
                                sw.WriteLine("f " + firstV + "/" + firstVT + " " + secondV + "/" + secondVT + " " + thirdV + "/" + thirdVT);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < p.triangles.Length; i += 3)
                            {
                                sw.WriteLine("f " + (p.triangles[i] + triangleOffset) + " " + (p.triangles[i + 1] + triangleOffset) + " " + (p.triangles[i + 2] + triangleOffset));
                            }
                        }
                        triangleOffset += p.verts.Count / 3;
                        sw.WriteLine();
                    }
                }
                ++progress;
                progressBar.Report((double)progress / BuildingCount);
            }
            System.Threading.Thread.Sleep(1000);
            Console.WriteLine();
        }

        static void CreateMTLFile(List<ISurfaceDataMember> textures, string path)
        {
            using (StreamWriter sw = File.CreateText(path))
            {
                foreach(var tex in textures)
                {
                    if (tex.GetType() == typeof(X3DMaterial))
                        continue;
                    ParameterizedTexture texture = (ParameterizedTexture)tex;
                    sw.WriteLine("newmtl " + texture.targetURI);
                    sw.WriteLine("illum 0"); //Possible placeholder
                    sw.WriteLine("Ka 1.0 1.0 1.0");
                    sw.WriteLine("Kd 1.0 1.0 1.0");
                    sw.WriteLine("Ks 0.0 0.0 0.0");
                    sw.WriteLine("map_Kd " + texture.imageURI);
                    sw.WriteLine();
                }
            }
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
                if (p.X > 18 && p.X < 19.0)
                    Console.Write("");
                    /*
                    if(IsInPolygon(polygon.boundsFlattened, p))
                    {
                        prunedTriangles.Add(triangles[i]);
                        prunedTriangles.Add(triangles[i + 1]);
                        prunedTriangles.Add(triangles[i + 2]);
                    }*/
                //if (isInside(polygon.IPointsAsPoints(), p))
                //{
                //    prunedTriangles.Add(triangles[i]);
                //    prunedTriangles.Add(triangles[i + 1]);
                //    prunedTriangles.Add(triangles[i + 2]);
                //}
                prunedTriangles.Add(triangles[i]);
                prunedTriangles.Add(triangles[i + 1]);
                prunedTriangles.Add(triangles[i + 2]);
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
        static IPoint[] TwoDimensionalBounds(Polygon p, string components)
        {
            if (components.Length != 2)
                return null;
            IPoint[] retVal = new IPoint[p.bounds.Count / 3];
            string comps = components.ToLower();
            if (comps.Contains('x') && comps.Contains('y'))
            {
                for (int i = 0; i < p.bounds.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.bounds[i], p.bounds[i + 1]);
                }
                return retVal;
            }
            if (comps.Contains('x') && comps.Contains('z'))
            {
                for (int i = 0; i < p.bounds.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.bounds[i], p.bounds[i + 2]);
                }
                return retVal;
            }
            if (comps.Contains('y') && comps.Contains('z'))
            {
                for (int i = 0; i < p.bounds.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.bounds[i + 1], p.bounds[i + 2]);
                }
                return retVal;
            }
            else
                return null;
        }
    }
}
