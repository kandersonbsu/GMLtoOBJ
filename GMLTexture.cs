using System;
using System.Collections.Generic;
using System.Text;

namespace GMLtoOBJ
{
    class GMLTexture
    {
        public string gmlID;
        public string imageURI;
        public string mimeType;
        public string textureType;
        public string wrapMode;
        public double[] borderColor;
        public string targetURI;
        public List<double> textureCoordinates;

        public GMLTexture()
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

        public GMLTexture(string id)
        {
            gmlID = id;
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
