using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;

namespace AudioPlugin
{
    public enum CompressionFormat
    {
        PCM,
        Vorbis,
        ADPCM,
        MP3,
        VAG,
        HEVAG,
        XMA,
        AAC,
        GCADPCM,
        ATRAC9
    }

    public class ExportAudioClipOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Export audio file";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classFile, "AudioClip").classId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }
        
        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            if (selection.Count > 1)
                return await BatchExport(win, workspace, selection);
            else
                return await SingleExport(win, workspace, selection);
        }

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetContainer cont in selection)
                {
                    AssetTypeValueField baseField = workspace.GetBaseField(cont);

                    string name = baseField.Get("m_Name").GetValue().AsString();
                    name = Extensions.ReplaceInvalidPathChars(name);
                    
                    CompressionFormat compressionFormat = (CompressionFormat) baseField.Get("m_CompressionFormat").GetValue().AsInt();
                    string extension = GetExtension(compressionFormat);
                    string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.");

                    string ResourceSource = baseField.Get("m_Resource").Get("m_Source").GetValue().AsString();
                    ulong ResourceOffset = baseField.Get("m_Resource").Get("m_Offset").GetValue().AsUInt64();
                    ulong ResourceSize = baseField.Get("m_Resource").Get("m_Size").GetValue().AsUInt64();

                    byte[] resourceData;
                    if (!GetAudioBytes(cont, ResourceSource, ResourceOffset, ResourceSize, out resourceData))
                    {
                        continue;
                    }

                    if (!FsbLoader.TryLoadFsbFromByteArray(resourceData, out FmodSoundBank bank))
                    {
                        continue;
                    }
                    List<FmodSample> samples = bank.Samples;
                    samples[0].RebuildAsStandardFileFormat(out byte[] sampleData, out string sampleExtension);
                    File.WriteAllBytes(file, sampleData);
                }
                return true;
            }
            return false;
        }
        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            SaveFileDialog sfd = new SaveFileDialog();

            AssetTypeValueField baseField = workspace.GetBaseField(cont);
            string name = baseField.Get("m_Name").GetValue().AsString();
            name = Extensions.ReplaceInvalidPathChars(name);
            
            CompressionFormat compressionFormat = (CompressionFormat) baseField.Get("m_CompressionFormat").GetValue().AsInt();

            sfd.Title = "Save audio file";
            string extension = GetExtension(compressionFormat);
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = $"{extension.ToUpper()} file", Extensions = new List<string>() { extension } }
            };
            sfd.InitialFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{extension}";

            string file = await sfd.ShowAsync(win);

            if (file != null && file != string.Empty)
            {
                string ResourceSource = baseField.Get("m_Resource").Get("m_Source").GetValue().AsString();
                ulong ResourceOffset = baseField.Get("m_Resource").Get("m_Offset").GetValue().AsUInt64();
                ulong ResourceSize = baseField.Get("m_Resource").Get("m_Size").GetValue().AsUInt64();

                byte[] resourceData;
                if (!GetAudioBytes(cont, ResourceSource, ResourceOffset, ResourceSize, out resourceData))
                {
                    return false;
                }
                
                if (!FsbLoader.TryLoadFsbFromByteArray(resourceData, out FmodSoundBank bank))
                {
                    return false;
                }
                List<FmodSample> samples = bank.Samples;
                samples[0].RebuildAsStandardFileFormat(out byte[] sampleData, out string sampleExtension);
                File.WriteAllBytes(file, sampleData);

                return true;
            }
            return false;
        }

        private static string GetExtension(CompressionFormat format)
        {
            return format switch
            {
                CompressionFormat.PCM => "wav",
                CompressionFormat.Vorbis => "ogg",
                CompressionFormat.ADPCM => "wav",
                CompressionFormat.MP3 => "mp3",
                CompressionFormat.VAG => "dat", // proprietary
                CompressionFormat.HEVAG => "dat", // proprietary
                CompressionFormat.XMA => "dat", // proprietary
                CompressionFormat.AAC => "aac",
                CompressionFormat.GCADPCM => "wav", // nintendo adpcm
                CompressionFormat.ATRAC9 => "dat", // proprietary
                _ => ""
            };
        }
        
        private bool GetAudioBytes(AssetContainer cont, string filepath, ulong offset, ulong size, out byte[] audioData)
        {
            if (!string.IsNullOrEmpty(filepath) && cont.FileInstance.parentBundle != null)
            {
                //some versions apparently don't use archive:/
                string searchPath = filepath;
                if (searchPath.StartsWith("archive:/"))
                    searchPath = searchPath.Substring(9);

                searchPath = Path.GetFileName(searchPath);

                AssetBundleFile bundle = cont.FileInstance.parentBundle.file;

                AssetsFileReader reader = bundle.reader;
                AssetBundleDirectoryInfo06[] dirInf = bundle.bundleInf6.dirInf;
                for (int i = 0; i < dirInf.Length; i++)
                {
                    AssetBundleDirectoryInfo06 info = dirInf[i];
                    if (info.name == searchPath)
                    {
                        reader.Position = bundle.bundleHeader6.GetFileDataOffset() + info.offset + (long) offset;
                        audioData = reader.ReadBytes((int)size);
                        return true;
                    }
                }
                audioData = Array.Empty<byte>();
                return false;
            }
            else
            {
                audioData = Array.Empty<byte>();
                return true;
            }
        }

    }

    public class TextAssetPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "AudioClip Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ExportAudioClipOption());
            return info;
        }
    }
}
