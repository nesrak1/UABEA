using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class PathUtils
    {
        // https://stackoverflow.com/a/23182807
        public static string ReplaceInvalidPathChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string GetFilePathWithoutExtension(string path)
        {
            string? directoryName = Path.GetDirectoryName(path);
            if (directoryName != null)
            {
                return Path.Combine(directoryName, Path.GetFileNameWithoutExtension(path));
            }

            return string.Empty;
        }

        public static string GetAssetsFileDirectory(AssetsFileInstance fileInst)
        {
            if (fileInst.parentBundle != null)
            {
                string dir = Path.GetDirectoryName(fileInst.parentBundle.path)!;

                // addressables
                string? upDir = Path.GetDirectoryName(dir);
                string? upDir2 = Path.GetDirectoryName(upDir ?? string.Empty);
                if (upDir != null && upDir2 != null)
                {
                    if (Path.GetFileName(upDir) == "aa" && Path.GetFileName(upDir2) == "StreamingAssets")
                    {
                        dir = Path.GetDirectoryName(upDir2)!;
                    }
                }

                return dir;
            }
            else
            {
                string dir = Path.GetDirectoryName(fileInst.path)!;
                if (fileInst.name == "unity default resources" || fileInst.name == "unity_builtin_extra")
                {
                    dir = Path.GetDirectoryName(dir)!;
                }
                return dir;
            }
        }
    }
}
