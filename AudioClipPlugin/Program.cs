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
using Avalonia.Platform.Storage;

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

            int classId = am.ClassDatabase.FindAssetClassByName("AudioClip").ClassId;

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
            var selectedFolders = await win.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Select export directory"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return false;

            string dir = selectedFolderPaths[0];

            foreach (AssetContainer cont in selection)
            {
                AssetTypeValueField baseField = workspace.GetBaseField(cont);

                string name = baseField["m_Name"].AsString;
                name = PathUtils.ReplaceInvalidPathChars(name);
                    
                CompressionFormat compressionFormat = (CompressionFormat)baseField["m_CompressionFormat"].AsInt;
                string extension = GetExtension(compressionFormat);
                string file = Path.Combine(dir, $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{extension}");

                string ResourceSource = baseField["m_Resource.m_Source"].AsString;
                ulong ResourceOffset = baseField["m_Resource.m_Offset"].AsULong;
                ulong ResourceSize = baseField["m_Resource.m_Size"].AsULong;

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

                if (sampleExtension.ToLowerInvariant() == "wav")
                {
                    // since fmod5sharp gives us malformed wav data, we have to correct it
                    FixWAV(ref sampleData);
                }
                    
                File.WriteAllBytes(file, sampleData);
            }
            return true;
        }

        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField baseField = workspace.GetBaseField(cont);
            string name = baseField["m_Name"].AsString;
            name = PathUtils.ReplaceInvalidPathChars(name);
            
            CompressionFormat compressionFormat = (CompressionFormat) baseField["m_CompressionFormat"].AsInt;

            string extension = GetExtension(compressionFormat);
            var selectedFile = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save audio file",
                FileTypeChoices = new List<FilePickerFileType>()
                {
                    new FilePickerFileType($"{extension.ToUpper()} file (*.{extension})") { Patterns = new List<string>() { "*." + extension } }
                },
                DefaultExtension = extension,
                SuggestedFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}"
            });

            string selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return false;

            string ResourceSource = baseField["m_Resource.m_Source"].AsString;
            ulong ResourceOffset = baseField["m_Resource.m_Offset"].AsULong;
            ulong ResourceSize = baseField["m_Resource.m_Size"].AsULong;

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

            if (sampleExtension.ToLowerInvariant() == "wav")
            {
                // since fmod5sharp gives us malformed wav data, we have to correct it
                FixWAV(ref sampleData);
            }
                
            File.WriteAllBytes(selectedFilePath, sampleData);

            return true;
        }

        private static void FixWAV(ref byte[] wavData)
        {
            int origLength = wavData.Length;
            // remove ExtraParamSize field from fmt subchunk
            for (int i = 36; i < origLength - 2; i++)
            {
                wavData[i] = wavData[i + 2];
            }
            Array.Resize(ref wavData, origLength - 2);
            // write ChunkSize to RIFF chunk
            byte[] riffHeaderChunkSize = BitConverter.GetBytes(wavData.Length - 8);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(riffHeaderChunkSize);
            }
            riffHeaderChunkSize.CopyTo(wavData, 4);
            // write ChunkSize to fmt chunk
            byte[] fmtHeaderChunkSize = BitConverter.GetBytes(16); // it is always 16 for pcm data, which this always
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(fmtHeaderChunkSize);
            }
            fmtHeaderChunkSize.CopyTo(wavData, 16);
            // write ChunkSize to data chunk
            byte[] dataHeaderChunkSize = BitConverter.GetBytes(wavData.Length - 44);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(dataHeaderChunkSize);
            }
            dataHeaderChunkSize.CopyTo(wavData, 40);
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
            if (string.IsNullOrEmpty(filepath))
            {
                audioData = Array.Empty<byte>();
                return false;
            }

            if (cont.FileInstance.parentBundle != null)
            {
                // read from parent bundle archive
                // some versions apparently don't use archive:/
                string searchPath = filepath;
                if (searchPath.StartsWith("archive:/"))
                    searchPath = searchPath.Substring(9);

                searchPath = Path.GetFileName(searchPath);

                AssetBundleFile bundle = cont.FileInstance.parentBundle.file;

                AssetsFileReader reader = bundle.DataReader;
                AssetBundleDirectoryInfo[] dirInf = bundle.BlockAndDirInfo.DirectoryInfos;
                for (int i = 0; i < dirInf.Length; i++)
                {
                    AssetBundleDirectoryInfo info = dirInf[i];
                    if (info.Name == searchPath)
                    {
                        reader.Position = info.Offset + (long)offset;
                        audioData = reader.ReadBytes((int)size);
                        return true;
                    }
                }
            }

            string assetsFileDirectory = Path.GetDirectoryName(cont.FileInstance.path);
            if (cont.FileInstance.parentBundle != null)
            {
                // inside of bundles, the directory contains the bundle path. let's get rid of that.
                assetsFileDirectory = Path.GetDirectoryName(assetsFileDirectory);
            }

            string resourceFilePath = Path.Combine(assetsFileDirectory, filepath);

            if (File.Exists(resourceFilePath))
            {
                // read from file
                AssetsFileReader reader = new AssetsFileReader(resourceFilePath);
                reader.Position = (long)offset;
                audioData = reader.ReadBytes((int)size);
                return true;
            }

            // if that fails, check current directory
            string resourceFileName = Path.Combine(assetsFileDirectory, Path.GetFileName(filepath));
            
            if (File.Exists(resourceFileName))
            {
                // read from file
                AssetsFileReader reader = new AssetsFileReader(resourceFileName);
                reader.Position = (long)offset;
                audioData = reader.ReadBytes((int)size);
                return true;
            }

            audioData = Array.Empty<byte>();
            return false;
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
