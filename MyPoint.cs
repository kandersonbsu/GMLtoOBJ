using System;
using System.Collections.Generic;
using System.Text;

namespace GMLtoOBJ
{
    class MyPoint
    {
        public double X;
        public double Y;
        public double Z;

        public MyPoint()
        {
            X = 0.0;
            Y = 0.0;
            Z = 0.0;
        }

        public MyPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return "[" + X + "] [" + Y + "] [" + Z + "]"; 
        }
    }
}
