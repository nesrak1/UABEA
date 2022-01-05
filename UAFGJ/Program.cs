using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TexturePlugin;
using UABEAvalonia;

namespace UAFGJ
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                DisplayStr("Not enough arguments!");
                return;
            }

            string ab = args[0];
            string png = args[1];

            if (!File.Exists(ab))
            {
                DisplayStr(".ab file not found!");
                return;
            }

            if (!File.Exists(png))
            {
                DisplayStr(".png file not found!");
                return;
            }

            DoStuff(ab, png);
        }

        static private void DisplayStr(string s)
        {
            //DisplayStr(s);
        }

        static private void DoStuff(string ab, string png)
        {
            string ab_real_name = ab;
            string ab_fake_name = ab_real_name + "_temp";
            DisplayStr("Opening file: " + ab);
            DetectedFileType file_type = AssetBundleDetector.DetectFileType(ab);
            DisplayStr("Detected file type: " + file_type.ToString());
            if (file_type != DetectedFileType.BundleFile)
            {
                DisplayStr("Invalid file type: " + file_type.ToString());
                return;
            }

            AssetsManager am = new AssetsManager();

            BundleFileInstance bundleInst = am.LoadBundleFile(ab, true);
            DisplayStr("Loaded bundleInst");

            string assetfile_name = "";
            int cont = 0;
            foreach (var i in bundleInst.file.bundleInf6.dirInf)
            {
                DisplayStr("Found asset file: " + i.name);
                if(!i.name.Contains(".resS"))
                {
                    assetfile_name = i.name;
                }
                cont++;
            }

            if (cont > 2)
            {
                DisplayStr("More than 2 assets file found!");
            }

            DisplayStr("Loading assetInst for " + assetfile_name);
            AssetsFileInstance assetInst = am.LoadAssetsFileFromBundle(bundleInst, 0, true);
            if (assetInst == null)
            {
                DisplayStr("Could not load assetInst for " + assetfile_name);
                return;
            }
            else
            {
                DisplayStr("Loaded assetInst for " + assetfile_name);
            }

            if(!File.Exists("classdata.tpk"))
            {
                DisplayStr("classdata.tpk not found!");
                return;
            }

            am.LoadClassPackage("classdata.tpk");
            if (!assetInst.file.typeTree.hasTypeTree)
            {
                am.LoadClassDatabaseFromPackage(assetInst.file.typeTree.unityVersion);
            }
            DisplayStr("Loaded classdata.tpk");

            AssetTypeValueField atvf = new AssetTypeValueField(); // "baseField"
            AssetFileInfoEx afie = new AssetFileInfoEx();
            int selected = -1;
            string png_noext = png;
            cont = 0;

            // Iterate the files in assetInst
            foreach (var inf in assetInst.table.GetAssetsOfType((int)AssetClassID.Texture2D))
            {
                afie = inf;
                atvf = am.GetTypeInstance(assetInst, afie).GetBaseField();

                var name = atvf.Get("m_Name").GetValue().AsString();
                DisplayStr(name);

                png_noext = Path.GetFileNameWithoutExtension(png);
                png_noext = png_noext.ToLowerInvariant();
                if (name == png_noext)
                {
                    selected = cont;
                    break;
                }
                cont++;
            }

            // Selected "png" to replace not found
            if (selected == -1)
            {
                DisplayStr("Couldn't find equivalent: " + png);
                return;
            }

            // Import textures
            bool ret = ImportTexturesCustom(atvf, png, out AssetTypeValueField id);
            if (id == null || !ret)
            {
                DisplayStr("Could not set image...");
                return;
            }

            DisplayStr("Write output to byte array");
            var newGoBytes = id.WriteToByteArray();
            var repl = new AssetsReplacerFromMemory(0, afie.index, (int)afie.curFileType, 0xffff, newGoBytes);

            if(repl == null || repl.ToString().Length == 0)
            {
                DisplayStr("repl == null");
                return;
            }

            /*
            using (var stream = File.OpenWrite("resources-modified.assets"))
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl }, 0);
            }
            */

            DisplayStr("Write changes to memory");

            //write changes to memory
            byte[] newAssetData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl }, 0);
                newAssetData = stream.ToArray();
            }

            if (newAssetData == null)
            {
                DisplayStr("newAssetData == null");
                return;
            }

            //DisplayStr(Encoding.UTF8.GetString(newAssetData));

            DisplayStr("Do bunRepl");

            //rename this asset name from boring to cool when saving
            var bunRepl = new BundleReplacerFromMemory(assetfile_name, null, true, newAssetData, -1);

            if(bunRepl == null)
            {
                DisplayStr("bunRepl == null");
                return;
            }

            DisplayStr("Do bunWriter");

            var bunWriter = new AssetsFileWriter(File.OpenWrite(ab_fake_name));
            bundleInst.file.Write(bunWriter, new List<BundleReplacer>() { bunRepl });
            bunWriter.Close();
            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();

            if(!File.Exists(ab_fake_name))
            {
                return;
            }

            am = new AssetsManager();
            var bun = am.LoadBundleFile(ab_fake_name);
            using (var stream = File.OpenWrite(ab_real_name))
            using (var writer = new AssetsFileWriter(stream))
            {
                bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZ4);
            }
            am.UnloadAllBundleFiles();

            File.Delete(ab_fake_name);
        }

        private static void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
        }

        private static bool ImportTexturesCustom(AssetTypeValueField atvf, string png, out AssetTypeValueField id)
        {
            // Get the current texture format
            TextureFormat fmt = TextureFormat.RGBA32;

            // Try to import a .png (of the selected textureformat) from selectedFilePath
            // After doing that, save two new variables as width and height of the image
            byte[] encImageBytes = TextureImportExport.ImportPng(png, fmt, out int width, out int height);

            if (encImageBytes == null)
            {
                id = null;
                return false;
            }

            // Tell Unity to load from byte array instead of file
            AssetTypeValueField m_StreamData = atvf.Get("m_StreamData");
            m_StreamData.Get("offset").GetValue().Set(0);
            m_StreamData.Get("size").GetValue().Set(0);
            m_StreamData.Get("path").GetValue().Set("");

            if (!atvf.Get("m_MipCount").IsDummy())
                atvf.Get("m_MipCount").GetValue().Set(1);

            // TextureFormat.RGBA32
            atvf.Get("m_TextureFormat").GetValue().Set((int)fmt);

            // Set to the byte array length
            atvf.Get("m_CompleteImageSize").GetValue().Set(encImageBytes.Length);

            // Set image width
            atvf.Get("m_Width").GetValue().Set(width);

            // Set image height
            atvf.Get("m_Height").GetValue().Set(height);

            // From here: actual writing of the rgba byte array

            // Read the field "image data"
            AssetTypeValueField image_data = atvf.Get("image data");

            // Set type to byte array
            image_data.GetValue().type = EnumValueTypes.ByteArray;
            image_data.templateField.valueType = EnumValueTypes.ByteArray;

            // Create new array with the red data
            AssetTypeByteArray byteArray = new AssetTypeByteArray()
            {
                size = (uint)encImageBytes.Length,
                data = encImageBytes
            };

            // Actually set the byte array
            image_data.GetValue().Set(byteArray);
            id = atvf;
            return true;
        }
    }
}
