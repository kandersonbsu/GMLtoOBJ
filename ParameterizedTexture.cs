using System;
using System.Collections.Generic;
using System.Text;

namespace GMLtoOBJ
{
    class ParameterizedTexture : ISurfaceDataMember
    {
        public string gmlID;
        public string imageURI;
        public string mimeType;
        public string textureType;
        public string wrapMode;
        public double[] borderColor;
        public string targetURI;
        public List<double> textureCoordinates;

        public ParameterizedTexture()
        {
            gmlID = "";
            imageURI = "";
            mimeType = "";
            textureType = "";
            wrapMode = "";
            borderColor = new double[4];
            targetURI = "";
            textureCoordinates = new List<double>();
        }

        public ParameterizedTexture(string ID)
        {
            gmlID = ID;
            imageURI = "";
            mimeType = "";
            textureType = "";
            wrapMode = "";
            borderColor = new double[4];
            targetURI = "";
            textureCoordinates = new List<double>();
        }
    }
}
