using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Extra.Decompressors.LZ4;
using Avalonia.Platform.Storage;
using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class SearchUtils
    {
        // cheap * search check
        public static bool WildcardMatches(string test, string pattern, bool caseSensitive = true)
        {
            RegexOptions options = 0;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            return Regex.IsMatch(test, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", options);
        }
    }
}
