using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MeshPlugin.MeshClass;

namespace MeshPlugin.MeshTypes
{
    public class BlendShapeData
    {
        public BlendShapeVertex[] vertices;
        public MeshBlendShape[] shapes;
        public MeshBlendShapeChannel[] channels;
        public float[] fullWeights;

        public BlendShapeData(AssetTypeValueField m_Shapes)
        {
            //Section-- vector vertices
            int numVerts = m_Shapes["vertices.Array"].AsArray.size;

            vertices = new BlendShapeVertex[numVerts];
            for (int i = 0; i < numVerts; i++)
            {
                var data = m_Shapes["vertices.Array"][i];
                vertices[i] = new BlendShapeVertex(data);
            }

            //Section-- vector shapes
            int numShapes = m_Shapes["shapes.Array"].AsArray.size;
            shapes = new MeshBlendShape[numShapes];
            for (int i = 0; i < numShapes; i++)
            {
                var data = m_Shapes["shapes.Array"][i];
                shapes[i] = new MeshBlendShape(data);
            }

            //Section-- vector channels
            int numChannels = m_Shapes["channels.Array"].AsArray.size;
            channels = new MeshBlendShapeChannel[numChannels];
            for (int i = 0; i < numChannels; i++)
            {
                var data = m_Shapes["channels.Array"][i];
                channels[i] = new MeshBlendShapeChannel(data);
            }

            int floatCount = m_Shapes["fullWeights.Array"].AsArray.size;
            fullWeights = new float[floatCount];
            for (int i = 0; i < floatCount; i++)
            {
                fullWeights[i] = m_Shapes["fullWeights.Array"][i].AsFloat;
            }
        }
    }
}
