using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public class CommandLineHandler
    {
        public static void PrintHelp()
        {
            Console.WriteLine("UABE AVALONIA");
            Console.WriteLine("WARNING: Command line support VERY EARLY");
            Console.WriteLine("There is a high chance of stuff breaking");
            Console.WriteLine("Use at your own risk");
            Console.WriteLine("  UABEAvalonia batchexport <batch file>");
            Console.WriteLine("  UABEAvalonia batchimport <batch file>");
            Console.WriteLine("  UABEAvalonia applyemip <emip file> <directory>");
            Console.WriteLine("Import/export arguments:");
            Console.WriteLine("  -keepnames writes out to the exact file name in the bundle.");
            Console.WriteLine("      Normally, file names are prepended with the bundle's name.");
            Console.WriteLine("      Note: these names are not compatible with batchimport.");
            Console.WriteLine("  -kd keep .decomp files. When UABEA opens compressed bundles,");
            Console.WriteLine("      they are decompressed into .decomp files. If you want to");
            Console.WriteLine("      decompress bundles, you can use this flag to keep them");
            Console.WriteLine("      without deleting them.");
            Console.WriteLine("  -fd overwrite old .decomp files.");
            Console.WriteLine("  -md decompress into memory. Doesn't write .decomp files.");
            Console.WriteLine("      -kd and -fd won't do anything with this flag set.");
            Console.WriteLine("Batch file example:");
            Console.WriteLine("  +DIR C:/somefolder/bundles/");
            Console.WriteLine("  -FILE C:/somefolder/bundles/specialbundlefile");
        }

        private static string GetMainFileName(string[] args)
        {
            for (int i = 2; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    return args[i];
            }
            return string.Empty;
        }

        private static HashSet<string> GetFlags(string[] args)
        {
            HashSet<string> flags = new HashSet<string>();
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                    flags.Add(args[i]);
            }
            return flags;
        }

        private static HashSet<string> GetAllFilesFromBatchFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            HashSet<string> allPaths = new HashSet<string>();
            foreach (string line in lines)
            {
                if (line.StartsWith("+DIR "))
                {
                    string dirName = line.Substring(5).Trim();
                    string[] files = Directory.GetFiles(dirName, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                        allPaths.Add(file);
                }
                else if (line.StartsWith("-DIR "))
                {
                    string dirName = line.Substring(5).Trim();
                    string[] files = Directory.GetFiles(dirName, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                        allPaths.Remove(file);
                }
                else if (line.StartsWith("+FILE "))
                {
                    string fileName = line.Substring(6).Trim();
                    allPaths.Add(fileName);
                }
                else if (line.StartsWith("-FILE "))
                {
                    string fileName = line.Substring(6).Trim();
                    allPaths.Remove(fileName);
                }
            }
            return allPaths;
        }

        private static AssetBundleFile DecompressBundle(string file, string? decompFile)
        {
            AssetBundleFile bun = new AssetBundleFile();

            Stream fs = File.OpenRead(file);
            AssetsFileReader r = new AssetsFileReader(fs);

            bun.Read(r, true);
            if (bun.bundleHeader6.GetCompressionType() != 0)
            {
                Stream nfs;
                if (decompFile == null)
                    nfs = new MemoryStream();
                else
                    nfs = File.Open(decompFile, FileMode.Create, FileAccess.ReadWrite);

                AssetsFileWriter w = new AssetsFileWriter(nfs);
                bun.Unpack(r, w);

                nfs.Position = 0;
                fs.Close();

                fs = nfs;
                r = new AssetsFileReader(fs);

                bun = new AssetBundleFile();
                bun.Read(r, false);
            }

            return bun;
        }

        private static string GetNextBackup(string affectedFilePath)
        {
            for (int i = 0; i < 10000; i++)
            {
                string bakName = $"{affectedFilePath}.bak{i.ToString().PadLeft(4, '0')}";
                if (!File.Exists(bakName))
                {
                    return bakName;
                }
            }

            Console.WriteLine("Too many backups, exiting for your safety.");
            return null;
        }

        private static void BatchExport(string[] args)
        {
            string mainFileName = GetMainFileName(args);
            HashSet<string> flags = GetFlags(args);
            HashSet<string> files = GetAllFilesFromBatchFile(mainFileName);
            foreach (string file in files)
            {
                string decompFile = $"{file}.decomp";
                string filePath = Path.GetDirectoryName(file);

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    return;
                }
                Console.WriteLine($"Decompressing {file}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                int entryCount = bun.bundleInf6.dirInf.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.bundleInf6.dirInf[i].name;
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);
                    string outName;
                    if (flags.Contains("-keepnames"))
                        outName = Path.Combine(filePath, $"{name}.assets");
                    else
                        outName = Path.Combine(filePath, $"{Path.GetFileName(file)}_{name}.assets");
                    Console.WriteLine($"Exporting {outName}...");
                    File.WriteAllBytes(outName, data);
                }

                bun.Close();

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void BatchImport(string[] args)
        {
            string mainFileName = GetMainFileName(args);
            HashSet<string> flags = GetFlags(args);
            HashSet<string> files = GetAllFilesFromBatchFile(mainFileName);
            foreach (string file in files)
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    return;
                }
                Console.WriteLine($"Decompressing {file} to {decompFile}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                List<BundleReplacer> reps = new List<BundleReplacer>();
                List<Stream> streams = new List<Stream>();

                int entryCount = bun.bundleInf6.dirInf.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.bundleInf6.dirInf[i].name;
                    string matchName = Path.Combine(file, $"{Path.GetFileName(file)}_{name}.assets");

                    if (File.Exists(matchName))
                    {
                        FileStream fs = File.OpenRead(matchName);
                        long length = fs.Length;
                        reps.Add(new BundleReplacerFromStream(matchName, matchName, true, fs, 0, length));
                        streams.Add(fs);
                        Console.WriteLine($"Importing {matchName}...");
                    }
                }

                //I guess uabe always writes to .decomp even if
                //the bundle is already decompressed, that way
                //here it can be used as a temporary file. for
                //now I'll write to memory since having a .decomp
                //file isn't guaranteed here
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    bun.Write(w, reps);
                    data = ms.ToArray();
                }
                Console.WriteLine($"Writing changes to {file}...");

                //uabe doesn't seem to compress here

                foreach (Stream stream in streams)
                    stream.Close();

                bun.Close();
                bun.reader.Close();

                File.WriteAllBytes(file, data);

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void ApplyEmip(string[] args)
        {
            HashSet<string> flags = GetFlags(args);
            string emipFile = args[1];
            string rootDir = args[2];

            if (!File.Exists(emipFile))
            {
                Console.WriteLine($"File {emipFile} does not exist!");
                return;
            }

            InstallerPackageFile instPkg = new InstallerPackageFile();
            FileStream fs = File.OpenRead(emipFile);
            AssetsFileReader r = new AssetsFileReader(fs);
            instPkg.Read(r, true);

            Console.WriteLine($"Installing emip...");
            Console.WriteLine($"{instPkg.modName} by {instPkg.modCreators}");
            Console.WriteLine(instPkg.modDescription);

            foreach (var affectedFile in instPkg.affectedFiles)
            {
                string affectedFileName = Path.GetFileName(affectedFile.path);
                string affectedFilePath = Path.Combine(rootDir, affectedFile.path);

                if (affectedFile.isBundle)
                {
                    string decompFile = $"{affectedFilePath}.decomp";
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    if (flags.Contains("-md"))
                        decompFile = null;

                    Console.WriteLine($"Decompressing {affectedFileName} to {decompFile}...");
                    AssetBundleFile bun = DecompressBundle(affectedFilePath, decompFile);
                    List<BundleReplacer> reps = new List<BundleReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var bunRep = (BundleReplacer)rep;
                        if (bunRep is BundleReplacerFromAssets)
                        {
                            //read in assets files from the bundle for replacers that need them
                            string assetName = bunRep.GetOriginalEntryName();
                            var bunRepInf = BundleHelper.GetDirInfo(bun, assetName);
                            long pos = bun.bundleHeader6.GetFileDataOffset() + bunRepInf.offset;
                            bunRep.Init(bun.reader, pos, bunRepInf.decompressedSize);
                        }
                        reps.Add(bunRep);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.OpenWrite(modFile);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    bun.Write(mw, reps, instPkg.addedTypes); //addedTypes does nothing atm
                    
                    mfs.Close();
                    bun.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);
                    
                    if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                        File.Delete(decompFile);

                    Console.WriteLine($"Done.");
                }
                else //isAssetsFile
                {
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    FileStream afs = File.OpenRead(affectedFilePath);
                    AssetsFileReader ar = new AssetsFileReader(afs);
                    AssetsFile assets = new AssetsFile(ar);
                    List<AssetsReplacer> reps = new List<AssetsReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var assetsReplacer = (AssetsReplacer)rep;
                        reps.Add(assetsReplacer);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.OpenWrite(modFile);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    assets.Write(mw, 0, reps, 0, instPkg.addedTypes);

                    mfs.Close();
                    ar.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    Console.WriteLine($"Done.");
                }
            }

            return;
        }

        public static void CLHMain(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }
            
            string command = args[0];

            if (command == "batchexport")
            {
                BatchExport(args);
            }
            else if (command == "batchimport")
            {
                BatchImport(args);
            }
            else if (command == "applyemip")
            {
                ApplyEmip(args);
            }
        }
    }
}