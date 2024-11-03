using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshPlugin.MeshTypes
{
    public class m_BindPose
    {
        public List<float> e = new List<float>();
        public m_BindPose(AssetTypeValueField data)
        {
            e.Add(data["e00"].AsFloat);
            e.Add(data["e01"].AsFloat);
            e.Add(data["e02"].AsFloat);
            e.Add(data["e03"].AsFloat);
            e.Add(data["e10"].AsFloat);
            e.Add(data["e11"].AsFloat);
            e.Add(data["e12"].AsFloat);
            e.Add(data["e13"].AsFloat);
            e.Add(data["e20"].AsFloat);
            e.Add(data["e21"].AsFloat);
            e.Add(data["e22"].AsFloat);
            e.Add(data["e23"].AsFloat);
            e.Add(data["e30"].AsFloat);
            e.Add(data["e31"].AsFloat);
            e.Add(data["e32"].AsFloat);
            e.Add(data["e33"].AsFloat);
        }
    }
}
