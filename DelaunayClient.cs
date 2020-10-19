using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace GMLtoOBJ
{
    class DelaunayClient
    {
        //! /brief Constructor settings, match the definitions in the DelaunayTriangulation.h header
        public enum Option
        {
            CLIPPING = 0x01,                // Enable Constraint Clipping
            FLATTENING = 0x02,              // Enable Flattening
            INTERPOLATE_EDGES = 0x04,       // Interpolate Intersections between Constraints using current Constraint.
            INTERPOLATE_FACES = 0x08,       // Interpolate new face Vertex points using the current face.
            VISUAL_DEBUG = 0x10,            // Use visual debugging if possible

            DISABLE_EDGE_FLIPS = 0x20,      // Disable edge flipping. This prevents delaunay enforcement, but saves computations.

            // Extended capabilities from the Wrapper
            REMOVE_HOLES = 0x1000,          // Remove the triangles that are inside the holes
            REMOVE_EXTERIOR = 0x2000        // Remove the triangles that are outside the exterior
        };

        //! /brief Error bit flags. Values between 1 and 0x8000 are defined in DelaunayTriangulation.h
        public enum Error
        {
            INVALID_BOUNDARY = 0x01,            //	An invalid boundary was used to construct this DelaunayTriangulation
            CORRUPTION_DETECTED = 0x02,         //	An unknown error has cased the graph to become corrupt
            INVALID_PARAMETER = 0x04            //  Found an invalid parameter
        };


        private UIntPtr clientID_ = UIntPtr.Zero;
        private int error_ = 0;

        public DelaunayClient(double[,] boundary, int resizeIncrement = 100000, double epsilon = 1e-6,
            double areaEpsilon = 3e-5, int maxEdgeFlips = 10000, int settings = (int)Option.CLIPPING)
        {
            clientID_ = NewDelaunayTriangulation(boundary, 0, VectorSize(boundary),
                resizeIncrement, epsilon, areaEpsilon, maxEdgeFlips, settings);
            if (clientID_ == UIntPtr.Zero)
                error_ = (int)Error.INVALID_BOUNDARY;
        }
        ~DelaunayClient()
        {
            Release();
        }

        public void Release()
        {
            UIntPtr copy = clientID_;
            clientID_ = UIntPtr.Zero;
            FreeDelaunayTriangulation(copy);
        }

        public int ErrorCode()
        {
            return error_;
        }

        public void InsertConstrainedLineString(double[,] points)
        {
            error_ = CallInsertConstrainedLineString(clientID_, points, 0, VectorSize(points));
        }

        public void InsertConstrainedPolygon(double[,] points)
        {
            error_ = CallInsertConstrainedPolygon(clientID_, points, 0, VectorSize(points));
        }

        public void GatherTriangles(double[,] polygonLimits, bool excludeHoles, out double[,] vertices, out int[,] triangles)
        {
            // The number of triangles and vertices is known only after CallGatherTriangles();
            CallGatherTriangles(clientID_, polygonLimits, 0, VectorSize(polygonLimits));

            vertices = new double[0, 0];
            int numVertices = CallGetVertices(clientID_, vertices, 0);
            vertices = new double[numVertices, 3];
            CallGetVertices(clientID_, vertices, numVertices);

            triangles = new int[0, 0];
            int numTriangles = CallGetTriangles(clientID_, triangles, 0);
            triangles = new int[numTriangles, 3];
            CallGetTriangles(clientID_, triangles, numTriangles);
            error_ = ErrorDelaunayTriangulation(clientID_);
        }

        //! /brief After GatherTriangles, saves the results of the triangulation to a JSON file
        public void Dump(string fileName)
        {
            error_ = DumpDelaunayTriangulation(clientID_, fileName);
        }



        //! /brief Check the constistency of a vector array, and return its size
        //  /return The number of points in the array, zero if the array is not valid
        private int VectorSize(double[,] values)
        {
            // Ensure this is a Nx3 array
            if (values.Rank != 2 || values.GetLowerBound(0) != 0 || values.GetLowerBound(1) != 0 || values.GetUpperBound(1) != 2)
                return 0;
            return values.GetUpperBound(0) + 1;
        }

        private const string serverDLL_ = "ctltriangulator.dll";
        [DllImport(serverDLL_)]
        private static extern UIntPtr NewDelaunayTriangulation(double[,] boundary, int beginPoint, int endPoint, int resizeIncrement, double epsilon, double areaEpsilon, int maxEdgeFlips, int settings);

        [DllImport(serverDLL_)]
        private static extern UIntPtr FreeDelaunayTriangulation(UIntPtr handle);

        [DllImport(serverDLL_)]
        private static extern int ErrorDelaunayTriangulation(UIntPtr handle);

        [DllImport(serverDLL_)]
        private static extern int CallInsertConstrainedLineString(UIntPtr handle, double[,] constraint, int beginPoint, int endPoint);

        [DllImport(serverDLL_)]
        private static extern int CallInsertConstrainedPolygon(UIntPtr handle, double[,] constraint, int beginPoint, int endPoint);

        [DllImport(serverDLL_)]
        private static extern UIntPtr CallGatherTriangles(UIntPtr handle, double[,] polygonLimits, int beginPoint, int endPoint);

        [DllImport(serverDLL_)]
        private static extern int CallGetTriangles(UIntPtr handle, int[,] triangles, int maxTriangles);

        [DllImport(serverDLL_)]
        private static extern int CallGetVertices(UIntPtr handle, double[,] vertices, int maxVertices);

        [DllImport(serverDLL_)]
        private static extern int DumpDelaunayTriangulation(UIntPtr handle, [MarshalAs(UnmanagedType.LPWStr)] string fileName);

    }
}
