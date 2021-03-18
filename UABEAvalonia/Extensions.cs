using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class Extensions
    {
        public static AssetExternal GetExtAssetNewData(this AssetsManager _this, AssetsFileInstance relativeTo, int fileId, long pathId,
                                                       MemoryStream data, bool onlyGetInfo = false, bool forceFromCldb = false)
        {
            AssetExternal ext = new AssetExternal();
            if (fileId == 0 && pathId == 0)
            {
                ext.info = null;
                ext.instance = null;
                ext.file = null;
            }
            else if (fileId != 0)
            {
                AssetsFileInstance dep = relativeTo.GetDependency(_this, fileId - 1);
                ext.info = dep.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstanceNewData(dep.file, ext.info, data, forceFromCldb);
                else
                    ext.instance = null;
                ext.file = dep;
            }
            else
            {
                ext.info = relativeTo.table.GetAssetInfo(pathId);
                if (!onlyGetInfo)
                    ext.instance = _this.GetTypeInstanceNewData(relativeTo.file, ext.info, data, forceFromCldb);
                else
                    ext.instance = null;
                ext.file = relativeTo;
            }
            return ext;
        }

        public static AssetTypeInstance GetTypeInstanceNewData(this AssetsManager _this, AssetsFile file, AssetFileInfoEx info,
                                                               MemoryStream data, bool forceFromCldb = false)
        {
            return new AssetTypeInstance(_this.GetTemplateBaseField(file, info, forceFromCldb), new AssetsFileReader(data), 0);
        }
    }
}
