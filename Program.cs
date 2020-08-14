using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TriangleNet;

namespace GMLtoOBJ
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
                PrintUsage();
            else
                OpenFile(args[0]);
        }

        static void PrintUsage()
        {
            Console.Out.WriteLine("This is the usage.");
        }

        static void OpenFile(string path)
        {
            List<Building> buildings = new List<Building>();
            if (File.Exists(path))
            {
                XDocument document = XDocument.Load(path);
                if (document.Root != null)
                {
                    foreach (XElement element in document.Root.Elements())
                    {
                        if (element.Name.ToString().Contains("cityObjectMember"))
                        {
                            foreach(XElement child in element.Elements())
                            {
                                if (child.Name.ToString().Contains("building"))
                                {
                                    var attr = child.FirstAttribute.Value;
                                    Building building = new Building(attr);
                                    building.Build(child);
                                    buildings.Add(building);
                                }
                            }
                        }
                    }
                }
                BuildingtoOBJ(buildings);
            }
        }

        static void BuildingtoOBJ(List<Building> buildings)
        {
            foreach(Building b in buildings)
            {

            }
        }
    }
}
