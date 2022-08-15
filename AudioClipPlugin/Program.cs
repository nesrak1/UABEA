using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
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
                    string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{extension}");

                    string ResourceSource = baseField.Get("m_Resource").Get("m_Source").GetValue().AsString();
                    ulong ResourceOffset = baseField.Get("m_Resource").Get("m_Offset").GetValue().AsUInt64();
                    ulong ResourceSize = baseField.Get("m_Resource").Get("m_Size").GetValue().AsUInt64();

                    byte[] resourceData = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(cont.FileInstance.path), ResourceSource));
                    byte[] resourceCroppedData = new byte[ResourceSize];
                
                    Buffer.BlockCopy(resourceData, (int) ResourceOffset, resourceCroppedData, 0, (int) ResourceSize);
                
                    if (!FsbLoader.TryLoadFsbFromByteArray(resourceCroppedData, out FmodSoundBank bank))
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

                byte[] resourceData = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(cont.FileInstance.path), ResourceSource));
                byte[] resourceCroppedData = new byte[ResourceSize];
                
                Buffer.BlockCopy(resourceData, (int) ResourceOffset, resourceCroppedData, 0, (int) ResourceSize);
                
                if (!FsbLoader.TryLoadFsbFromByteArray(resourceCroppedData, out FmodSoundBank bank))
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
                CompressionFormat.MP3 => "mp3",
                CompressionFormat.AAC => "aac",
                _ => ""
            };
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
