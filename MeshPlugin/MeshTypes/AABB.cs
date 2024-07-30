using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshPlugin.TypeStructs;

namespace MeshPlugin.MeshTypes
{
    public class AABB
    {
        public Vector3 m_Center;
        public Vector3 m_Extent;

        public AABB(AssetTypeValueField AABB)
        {
            var m_Center = AABB["m_Center"];
            this.m_Center.X = m_Center["x"].AsFloat;
            this.m_Center.Y = m_Center["y"].AsFloat;
            this.m_Center.Z = m_Center["z"].AsFloat;

            var m_Extent = AABB["m_Extent"];
            this.m_Extent.X = m_Extent["x"].AsFloat;
            this.m_Extent.Y = m_Extent["y"].AsFloat;
            this.m_Extent.Z = m_Extent["z"].AsFloat;
        }
    }
}
