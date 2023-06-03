using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow
    {
        private void FillGeneralInfo()
        {
            AssetsFile afile = activeFile.file;

            AssetsFileHeader header = afile.Header;
            boxMetadataSize.Text = header.MetadataSize.ToString();
            boxFileSize.Text = header.FileSize.ToString();
            boxFormat.Text = header.Version.ToString();
            boxFirstFileOffset.Text = header.DataOffset.ToString();
            boxEndianness.Text = header.Endianness ? "big endian" : "little endian";

            AssetsFileMetadata meta = afile.Metadata;
            boxEngineVersion.Text = meta.UnityVersion;
            boxPlatform.Text = $"{(BuildTarget)meta.TargetPlatform} ({meta.TargetPlatform})";
            boxTypeTree.Text = meta.TypeTreeEnabled ? "enabled" : "disabled";
        }

        public enum BuildTarget
        {
            StandaloneOSX = 2,
            StandaloneOSXUniversal,
            StandaloneOSXIntel,
            StandaloneWindows,
            WebPlayer,
            WebPlayerStreamed,
            Wii,
            iOS,
            PS3,
            XBOX360,
            StandaloneBroadcom,
            Android,
            StandaloneGLESEmu,
            StandaloneGLES20Emu,
            NaCl,
            StandaloneLinux,
            Flash,
            StandaloneWindows64,
            WebGL,
            WSAPlayer,
            WSAPlayerX64,
            WSAPlayerARM,
            StandaloneLinux64,
            StandaloneLinuxUniversal,
            WP8Player,
            StandaloneOSXIntel64,
            BlackBerry,
            Tizen,
            PSP2,
            PS4,
            PSM,
            XboxOne,
            SamsungTV,
            N3DS,
            WiiU,
            tvOS,
            Switch,
            Lumin,
            Stadia,
            CloudRendering,
            GameCoreXboxSeries,
            GameCoreXboxOne,
            PS5,
            EmbeddedLinux,
            QNX,
            Bratwurst
        }
    }
}
