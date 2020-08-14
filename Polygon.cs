using System;
using System.Collections.Generic;
using System.Text;

namespace GMLtoOBJ
{
    class Polygon
    {
        public List<double> vertices;

        public Polygon(List<double> verts)
        {
            vertices = verts;
        }
    }
}
