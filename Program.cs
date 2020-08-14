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
                foreach(Polygon p in b.sides)
                {
                    var matrix = PolygonToMatrix(p);
                    int rank = rankOfMatrix(matrix, 3, p.vertices.Count / 3);
                }
            }
        }

        static double[,] PolygonToMatrix(Polygon p)
        {
            double[,] retVal = new double[3, p.vertices.Count / 3];
            int r = 0;
            int c = 0;
            foreach(double d in p.vertices)
            {
                retVal[r, c] = d;
                ++r;
                if(r >= 3)
                {
                    r = 0;
                    ++c;
                }
            }


            return retVal;
        }

        static int rankOfMatrix(double[,] mat, int Row, int Column)
        {

            int rank = Column;

            for (int row = 0; row < rank; row++)
            {
                // Before we visit current row  
                // 'row', we make sure that  
                // mat[row][0],....mat[row][row-1] 
                // are 0.
                // Diagonal element is not zero 
                if (mat[row, row] != 0)
                {
                    for (int col = 0; col < row; col++)
                    {
                        if (col != row)
                        {
                            // This makes all entries  
                            // of current column  
                            // as 0 except entry  
                            // 'mat[row][row]' 
                            double mult =(double)mat[col, row] / mat[row, row];
                            for (int i = 0; i < rank; i++)
                                mat[col, i] -= (int)mult * mat[row, i];
                        }
                    }
                }
                // Diagonal element is already zero.  
                // Two cases arise: 
                // 1) If there is a row below it  
                // with non-zero entry, then swap  
                // this row with that row and process  
                // that row 
                // 2) If all elements in current  
                // column below mat[r][row] are 0,  
                // then remvoe this column by  
                // swapping it with last column and 
                // reducing number of columns by 1. 
                else
                {
                    bool reduce = true;
                    // Find the non-zero element  
                    // in current column  
                    for (int i = row + 1; i < row; i++)
                    {
                        // Swap the row with non-zero  
                        // element with this row. 
                        if (mat[i, row] != 0)
                        {
                            swap(mat, row, i, rank);
                            reduce = false;
                            break;
                        }
                    }
                    // If we did not find any row with  
                    // non-zero element in current  
                    // columnm, then all values in  
                    // this column are 0. 
                    if (reduce)
                    {
                        // Reduce number of columns 
                        rank--;
                        // Copy the last column here 
                        for (int i = 0; i < row; i++)
                            mat[i, row] = mat[i, rank];
                    }
                    // Process this row again 
                    row--;
                }
            }
            return rank;
        }

        // function for exchanging two rows 
        // of a matrix  
        static void swap(double[,] mat,int row1, int row2, int col)
        {
            for (int i = 0; i < col; i++)
            {
                double temp = mat[row1, i];
                mat[row1, i] = mat[row2, i];
                mat[row2, i] = temp;
            }
        }
    }
}
