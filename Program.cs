using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DelaunatorSharp;
using Cognitics.CoordinateSystems;
using System.Numerics;
using NetTopologySuite.IO;
using NetTopologySuite;
using NetTopologySuite.Features;
using System.Threading;

namespace GMLtoOBJ
{
    class Program
    {
        static string PathToFileFolder;
        static string PathToOutputFolder;
        static int BuildingCount;
        static int numThreads = 1;
        static int progress = 0;
        static void Main(string[] args)
        {
            //args[0] == input folder
            PathToFileFolder = args[0] == null ? "" : args[0];
            var d = Path.GetDirectoryName(PathToFileFolder);
            PathToOutputFolder = Path.GetDirectoryName(PathToFileFolder) + "\\output";
            if (args[0] == null)
            {
                PrintUsage();
                return;
            }
            else
            {
                for(int i = 1; i < args.Length; ++i)
                {
                    if(args[i] == "-t")
                    {
                        try
                        {
                            int threads = int.Parse(args[i + 1]);
                            if (threads <= 0)
                                threads = 1;
                            numThreads = threads;
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            PrintUsage();
                        }
                    }
                    if (args[i] == "-o")
                    {
                        PathToOutputFolder = args[i + 1];
                        ++i;
                    }
                }
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
            Console.Out.WriteLine("GMLtoOBJ [Path to file/folder].");
            Console.Out.WriteLine("Additional Options");
            Console.Out.WriteLine("-t [integer] to specify the number of threads to use.");
            Console.Out.WriteLine("-o [path to folder] to specify the output folder.");
            Console.Out.WriteLine("If no output Folder is specified, the default will create an output folder in the input directory.");
            Console.Out.WriteLine("If no threads are specified, it will default to 1.");
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
                string filename = Path.GetFileNameWithoutExtension(path);
                List<Feature> featureList = new List<Feature>();
                foreach(Building b in buildings)
                {
                    Feature feat = new Feature();
                    Dictionary<string, object> atts = new Dictionary<string, object>();
                    string buildingName = "\\" + b.state + "_" + b.county + "_" + b.ubid + ".obj";
                    if (buildingName == "\\__.obj")
                        buildingName = "\\" + b.GetID() + ".obj";
                    string origin = b.centerpoint == null ? b.latitude + " " + b.longitude : b.centerpoint[0] + " " + b.centerpoint[1] + " " + b.centerpoint[2];
                    atts.Add("File Name", buildingName);
                    atts.Add("Origin", origin);
                    feat.Attributes = new AttributesTable(atts);
                    feat.BoundingBox = new NetTopologySuite.Geometries.Envelope();
                    if(b.centerpoint == null)
                    {
                        feat.Geometry = new NetTopologySuite.Geometries.Point(b.latitude, b.longitude);
                    }
                    else
                    {
                        feat.Geometry = new NetTopologySuite.Geometries.Point(b.centerpoint[0], b.centerpoint[1], b.centerpoint[2]);
                    }
                    featureList.Add(feat);
                }
                WriteBuildingsToShapefile(PathToOutputFolder + "\\" + filename, featureList);
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
                        List<double> interior = new List<double>();
                        foreach (string s in rawValues)
                            interior.Add(double.Parse(s));
                        polygon.interior.Add(interior);
                        polygon.verts.AddRange(interior);
                    }
                    else
                    {
                        var rawValues = poly.Value.Split(' ');
                        foreach (string s in rawValues)
                        {
                            double d = double.Parse(s);
                            polygon.bounds.Add(d);
                            polygon.verts.Add(d);
                        }
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
            foreach(var list in polygon.interior)
            {
                for(int i = 0; i < list.Count; ++i)
                {
                    int index = Modulo(i, 3);
                    list[i] = list[i] - centerPoint[index];
                }
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

        static void BuildingtoOBJ(List<Building> buildings, string path)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            Console.WriteLine("");
            Console.WriteLine("Creating OBJ files from Buildings:");
            int iteration = 1;
            var progressBar = new ProgressBar();
            //threaded
            var splitBuildings = SplitBuildingList(buildings, numThreads);
            var threadList = new List<Thread>();
            foreach (List<Building> list in splitBuildings)
                threadList.Add(StartBuildingListThread(list, path));
            while(AllThreadsComplete(threadList))
                progressBar.Report((double)progress / BuildingCount);
            System.Threading.Thread.Sleep(1000);
            Console.WriteLine();
            var timeSpan = timer.Elapsed;
            Console.WriteLine("Elapsed Time: " + timeSpan.ToString(@"m\:ss\.fff"));
        }

        static bool AllThreadsComplete(List<Thread> threads)
        {
            foreach(var thread in threads)
            {
                if (thread.IsAlive)
                    return true;
            }
            return false;
        }

        static List<List<Building>> SplitBuildingList(List<Building> buildings, int numLists)
        {
            var retVal = new List<List<Building>>();
            int chunkSize = (buildings.Count / numLists); //The number of buildings in each list
            for(int i = 0; i < numLists; ++i)
            {
                if (i == numLists - 1)
                    retVal.Add(buildings.GetRange(i * chunkSize, buildings.Count - (chunkSize * i)));
                else
                    retVal.Add(buildings.GetRange(i * chunkSize, chunkSize));
            }

            return retVal;
        }

        static Thread StartBuildingListThread(List<Building> buildings, string path)
        {
            var thread = new Thread(() => WriteBuildingsToOBJ(buildings, path));
            thread.Start();
            return thread;
        }

        static Thread StartBuildingThread(Building building, string path, ref int progress)
        {
            var thread = new Thread(() => WriteBuildingToOBJ(building, path));
            thread.Start();
            return thread;
        }

        static Thread StartPolygonThread(Polygon polygon)
        {
            var thread = new Thread(() => TriangulatePolygon(polygon));
            thread.Start();
            return thread;
        }

        static void WriteBuildingsToOBJ(List<Building> buildings, string path)
        {
            foreach (Building b in buildings)
                WriteBuildingToOBJ(b, path);
        }
        static void WriteBuildingToOBJ(Building building, string path)
        {
            //var thread = Thread.CurrentThread;
            //Console.WriteLine("Starting Thread {0}", thread.ManagedThreadId);
            building.CreateSideUVs();
            if (building.needsCoordinateTransform)
            {
                for (int i = 0; i < building.sides.Count; ++i)
                {
                    Polygon p = building.sides[i];
                    ProjectPolygon(ref p, building.latitude, building.longitude);
                }
            }
            foreach (Polygon polygon in building.sides)
            {
                var xy = TwoDimensionalPolygon(polygon, "xy");
                var xz = TwoDimensionalPolygon(polygon, "xz");
                var yz = TwoDimensionalPolygon(polygon, "yz");

                var xyArea = PolygonArea(xy);
                var xzArea = PolygonArea(xz);
                var yzArea = PolygonArea(yz);

                string components = ((xyArea > xzArea) && (xyArea > yzArea)) ? "xy" : ((xzArea > xyArea) && (xzArea > yzArea)) ? "xz" : "yz";

                polygon.CreateVertUVDictionary();
                double[,] exterior = ListTo2DArray(polygon.bounds, components);
                polygon.CreateVertDictionary(ListTo2DArray(polygon.verts, components));
                double[,] bounds = CalculateBounds(exterior);
                DelaunayClient delaunayClient = new DelaunayClient(bounds, settings: (int)DelaunayClient.Option.REMOVE_EXTERIOR | (int)DelaunayClient.Option.REMOVE_HOLES);
                delaunayClient.InsertConstrainedPolygon(exterior);
                foreach (List<double> interior in polygon.interior)
                {
                    var interiorArray = ListTo2DArray(interior, components);
                    delaunayClient.InsertConstrainedPolygon(interiorArray);
                }
                double[,] empty = new double[0, 0];
                delaunayClient.GatherTriangles(empty, true, out polygon.delaunayVerts, out polygon.delaunayTriangles);
                polygon.ConvertDelaunay();
                delaunayClient.Release();
            }
            string filename = Path.GetFileName(path);
            var filenameNoExtension = filename.Replace(".gml", "");
            string buildingName = "\\" + building.state + "_" + building.county + "_" + building.ubid + ".obj";
            if (buildingName == "\\__.obj")
                buildingName = "\\" + building.GetID() + ".obj";
            string buildingMTL = buildingName.Replace(".obj", ".mtl");
            CreateMTLFile(building.textures, PathToOutputFolder + buildingMTL);
            buildingMTL = buildingMTL.Trim('\\');
            using (StreamWriter sw = File.CreateText(PathToOutputFolder + buildingName))
            {
                int vtOffset = 0;
                sw.WriteLine("Produced by Cognitics");
                sw.WriteLine(DateTime.Now);
                string origin = building.centerpoint == null ? "ORIGIN: " + building.latitude + " " + building.longitude : "ORIGIN: " + building.centerpoint[0] + " " + building.centerpoint[1] + " " + building.centerpoint[2];
                sw.WriteLine(origin);
                sw.WriteLine("mtllib " + buildingMTL);
                sw.WriteLine("");
                int triangleOffset = 1;
                foreach (Polygon p in building.sides)
                {
                    bool vertexTextures = false;
                    sw.WriteLine("# PolygonID " + p.gmlID + " ParentID " + p.parentID);
                    for (int i = 0; i < p.verts.Count - 1; i += 3)
                    {
                        sw.WriteLine("v " + p.verts[i] + " " + p.verts[i + 1] + " " + p.verts[i + 2]);
                    }
                    for (int i = 0; i < p.uvs.Count - 1; i += 2)
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
        }

        static void TriangulatePolygon(Polygon polygon)
        {
            var xy = TwoDimensionalPolygon(polygon, "xy");
            var xz = TwoDimensionalPolygon(polygon, "xz");
            var yz = TwoDimensionalPolygon(polygon, "yz");

            var xyArea = PolygonArea(xy);
            var xzArea = PolygonArea(xz);
            var yzArea = PolygonArea(yz);

            string components = ((xyArea > xzArea) && (xyArea > yzArea)) ? "xy" : ((xzArea > xyArea) && (xzArea > yzArea)) ? "xz" : "yz";

            polygon.CreateVertUVDictionary();
            double[,] exterior = ListTo2DArray(polygon.bounds, components);
            polygon.CreateVertDictionary(ListTo2DArray(polygon.verts, components));
            double[,] bounds = CalculateBounds(exterior);
            DelaunayClient delaunayClient = new DelaunayClient(bounds, settings: (int)DelaunayClient.Option.REMOVE_EXTERIOR | (int)DelaunayClient.Option.REMOVE_HOLES);
            delaunayClient.InsertConstrainedPolygon(exterior);
            foreach (List<double> interior in polygon.interior)
            {
                var interiorArray = ListTo2DArray(interior, components);
                delaunayClient.InsertConstrainedPolygon(interiorArray);
            }
            double[,] empty = new double[0, 0];
            delaunayClient.GatherTriangles(empty, true, out polygon.delaunayVerts, out polygon.delaunayTriangles);
            polygon.ConvertDelaunay();
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
            IPoint[] retVal = new IPoint[p.bounds.Count / 3];
            string comps = components.ToLower();
            if(comps.Contains('x') && comps.Contains('y'))
            {
                for(int i = 0; i < p.bounds.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.bounds[i], p.bounds[i + 1]);
                }
                return retVal;
            }
            if(comps.Contains('x') && comps.Contains('z'))
            {
                for (int i = 0; i < p.bounds.Count - 1; i += 3)
                {
                    retVal[i / 3] = new Point(p.bounds[i], p.bounds[i + 2]);
                }
                return retVal;
            }
            if(comps.Contains('y') && comps.Contains('z'))
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

        static void WriteBuildingsToShapefile(string fileName, List<NetTopologySuite.Features.Feature> buildings)
        {
            if (File.Exists(fileName + ".shp"))
                File.Delete(fileName + ".shp");
            if (File.Exists(fileName + ".dbf"))
                File.Delete(fileName + ".dbf");
            if (File.Exists(fileName + ".dbf"))
                File.Delete(fileName + ".dbf");

            if (buildings.Count == 0)
                return;

            var outGeoFactory = NetTopologySuite.Geometries.GeometryFactory.Default;
            var writer = new ShapefileDataWriter(fileName, outGeoFactory);
            var outDBaseHeader = ShapefileDataWriter.GetHeader(buildings[0], buildings.Count);
            writer.Header = outDBaseHeader;
            writer.Write(buildings);
        }

        static double[,] ListTo2DArray(List<double> points, string components)
        {
            if (points.Count % 3 != 0)
                return null;
            components = components.ToLower();
            List<double> pointsTrimmed = new List<double>();
            if (components == "xy")
            {
                for (int i = 0; i < points.Count - 1; i += 3)
                {
                    pointsTrimmed.Add(points[i]);
                    pointsTrimmed.Add(points[i + 1]);
                }
            }
            else if (components == "xz")
            {
                for (int i = 0; i < points.Count - 1; i += 3)
                {
                    pointsTrimmed.Add(points[i]);
                    pointsTrimmed.Add(points[i + 2]);
                }
            }
            else if (components == "yz")
            {
                for (int i = 0; i < points.Count - 1; i += 3)
                {
                    pointsTrimmed.Add(points[i + 1]);
                    pointsTrimmed.Add(points[i + 2]);
                }
            }
            //All three components into a 2d Array
            else
            {
                double[,] returnVal = new double[points.Count / 3, 3];
                for(int i = 0; i < points.Count - 1; i += 3)
                {
                    returnVal[i / 3, 0] = points[i];
                    returnVal[i / 3, 1] = points[i + 1];
                    returnVal[i / 3, 2] = points[i + 2];
                }
                return returnVal;
            }

            double[,] retVal = new double[pointsTrimmed.Count / 2, 3];
            for(int i = 0; i < pointsTrimmed.Count - 1; i += 2)
            {
                retVal[i / 2, 0] = pointsTrimmed[i];
                retVal[i / 2, 1] = pointsTrimmed[i + 1];
                retVal[i / 2, 2] = 0;
            }
            return retVal;
        }

        static double[,] CalculateBounds(double[,] polygon)
        {
            double x0 = 10000000;
            double x1 = -10000000;
            double y0 = 10000000;
            double y1 = -10000000;
            for (int i = 0; i <= polygon.GetUpperBound(0); i++)
            {
                if (x0 > polygon[i, 0])
                    x0 = polygon[i, 0];
                if (x1 < polygon[i, 0])
                    x1 = polygon[i, 0];
                if (y0 > polygon[i, 1])
                    y0 = polygon[i, 1];
                if (y1 < polygon[i, 1])
                    y1 = polygon[i, 1];
            }
            x0 -= (2 + (x1 - x0) / 10);
            y0 -= (2 + (y1 - y0) / 10);
            x1 += (2 + (x1 - x0) / 10);
            y1 += (2 + (y1 - y0) / 10);
            double[,] boundary = { { x0, y0, 0 }, { x1, y0, 0 }, { x1, y1, 0 }, { x0, y1, 0 } };
            return boundary;
        }
    }
}
