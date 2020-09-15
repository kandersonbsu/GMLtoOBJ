using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace GMLtoOBJ
{
    class X3DMaterial : ISurfaceDataMember
    {
        public string gmlID;
        public string name;
        public double ambientIntensity;
        public Vector3 diffuseColor;
        public Vector3 emissiveColor;
        public Vector3 specularColor;
        public double shininess;
        public double transparency;
        public string target;

        public X3DMaterial()
        {
            gmlID = "";
            name = "";
            ambientIntensity = 0.0;
            diffuseColor = Vector3.Zero;
            emissiveColor = Vector3.Zero;
            specularColor = Vector3.Zero;
            shininess = 0.0;
            transparency = 0.0;
            target = "";
        }

        public X3DMaterial(string ID)
        {
            gmlID = ID;
            name = "";
            ambientIntensity = 0.0;
            diffuseColor = Vector3.Zero;
            emissiveColor = Vector3.Zero;
            specularColor = Vector3.Zero;
            shininess = 0.0;
            transparency = 0.0;
            target = "";
        }
    }
}
