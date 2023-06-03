using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class FileUtils
    {
        private static readonly string[] BYTE_SIZE_SUFFIXES = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        public static string GetFormattedByteSize(long size)
        {
            int log = (int)Math.Log(size, 1024);
            double div = log == 0 ? 1 : Math.Pow(1024, log);
            double num = size / div;
            return $"{num:f2}{BYTE_SIZE_SUFFIXES[log]}";
        }

        public static List<string> GetFilesInDirectory(string path, List<string> extensions)
        {
            List<string> files = new List<string>();
            foreach (string extension in extensions)
            {
                files.AddRange(Directory.GetFiles(path, "*." + extension));
            }
            return files;
        }
    }
}
