using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class InstallerPackageFile
    {
        public string magic;
        public bool includesCldb;
        public string modName;
        public string modCreators;
        public string modDescription;
        public ClassDatabaseFile addedTypes;
        public List<InstallerPackageAssetsDesc> affectedFiles;

        public bool Read(AssetsFileReader reader, bool prefReplacersInMemory = false)
        {
            reader.BigEndian = false;

            magic = reader.ReadStringLength(4);
            if (magic != "EMIP")
                return false;

            includesCldb = reader.ReadByte() != 0;

            modName = reader.ReadCountStringInt16();
            modCreators = reader.ReadCountStringInt16();
            modDescription = reader.ReadCountStringInt16();

            if (includesCldb)
            {
                addedTypes = new ClassDatabaseFile();
                addedTypes.Read(reader);
                ////get past the data since the reader goes back to the beginning
                //reader.Position = 0x16 + addedTypes.Header.CompressedSize;
            }
            else
            {
                addedTypes = null;
            }

            int affectedFilesCount = reader.ReadInt32();
            affectedFiles = new List<InstallerPackageAssetsDesc>();
            for (int i = 0; i < affectedFilesCount; i++)
            {
                List<object> replacers = new List<object>();
                InstallerPackageAssetsDesc desc = new InstallerPackageAssetsDesc()
                {
                    isBundle = reader.ReadByte() != 0,
                    path = reader.ReadCountStringInt16()
                };
                int replacerCount = reader.ReadInt32();
                for (int j = 0; j < replacerCount; j++)
                {
                    object repObj = ParseReplacer(reader, prefReplacersInMemory);
                    if (repObj is AssetsReplacer repAsset)
                    {
                        replacers.Add(repAsset);
                    }
                    else if (repObj is BundleReplacer repBundle)
                    {
                        replacers.Add(repBundle);
                    }
                }
                desc.replacers = replacers;
                affectedFiles.Add(desc);
            }

            return true;
        }
        public void Write(AssetsFileWriter writer)
        {
            writer.BigEndian = false;

            writer.Write(Encoding.ASCII.GetBytes(magic));
            
            writer.Write(includesCldb);

            writer.WriteCountStringInt16(modName);
            writer.WriteCountStringInt16(modCreators);
            writer.WriteCountStringInt16(modDescription);

            if (includesCldb)
            {
                addedTypes.Write(writer, ClassFileCompressionType.Uncompressed);
                //writer.Position = 0x16 + addedTypes.Header.CompressedSize;
            }

            writer.Write(affectedFiles.Count);
            for (int i = 0; i < affectedFiles.Count; i++)
            {
                InstallerPackageAssetsDesc desc = affectedFiles[i];
                writer.Write(desc.isBundle);
                writer.WriteCountStringInt16(desc.path);
                
                writer.Write(desc.replacers.Count);
                for (int j = 0; j < desc.replacers.Count; j++)
                {
                    object repObj = desc.replacers[j];
                    if (repObj is AssetsReplacer repAsset)
                    {
                        repAsset.WriteReplacer(writer);
                    }
                    else if (repObj is BundleReplacer repBundle)
                    {
                        repBundle.WriteReplacer(writer);
                    }
                }
            }
        }

        private static object ParseReplacer(AssetsFileReader reader, bool prefReplacersInMemory)
        {
            short replacerType = reader.ReadInt16();
            byte fileType = reader.ReadByte();
            if (fileType == 0) //BundleReplacer
            {
                string oldName = reader.ReadCountStringInt16();
                string newName = reader.ReadCountStringInt16();
                bool hasSerializedData = reader.ReadByte() != 0; //guess
                long replacerCount = reader.ReadInt64();
                List<AssetsReplacer> replacers = new List<AssetsReplacer>();
                for (int i = 0; i < replacerCount; i++)
                {
                    AssetsReplacer assetReplacer = (AssetsReplacer)ParseReplacer(reader, prefReplacersInMemory);
                    replacers.Add(assetReplacer);
                }

                if (replacerType == 4) //BundleReplacerFromAssets
                {
                    //we have to null the assetsfile here and call init later
                    BundleReplacer replacer = new BundleReplacerFromAssets(oldName, newName, null, replacers, 0);
                    return replacer;
                }
            }
            else if (fileType == 1) //AssetsReplacer
            {
                byte unknown01 = reader.ReadByte(); //always 1
                int fileId = reader.ReadInt32();
                long pathId = reader.ReadInt64();
                int classId = reader.ReadInt32();
                ushort monoScriptIndex = reader.ReadUInt16();

                List<AssetPPtr> preloadDependencies = new List<AssetPPtr>();
                int preloadDependencyCount = reader.ReadInt32();
                for (int i = 0; i < preloadDependencyCount; i++)
                {
                    AssetPPtr pptr = new AssetPPtr(reader.ReadInt32(), reader.ReadInt64());
                    preloadDependencies.Add(pptr);
                }

                if (replacerType == 0) //remover
                {
                    AssetsReplacer replacer = new AssetsRemover(pathId);
                    if (preloadDependencyCount != 0)
                        replacer.SetPreloadDependencies(preloadDependencies);

                    return replacer;
                }
                else if (replacerType == 2) //adder/replacer?
                {
                    Hash128? propertiesHash = null;
                    Hash128? scriptHash = null;
                    ClassDatabaseFile? classData = null;
                    AssetsReplacer replacer;

                    bool flag1 = reader.ReadByte() != 0; //no idea, couldn't get it to be 1
                    if (flag1)
                    {
                        throw new NotSupportedException("you just found a file with the mysterious flag1 set, send the file to nes");
                    }

                    bool flag2 = reader.ReadByte() != 0; //has properties hash
                    if (flag2)
                    {
                        propertiesHash = new Hash128(reader);
                    }

                    bool flag3 = reader.ReadByte() != 0; //has script hash
                    if (flag3)
                    {
                        scriptHash = new Hash128(reader);
                    }

                    bool flag4 = reader.ReadByte() != 0; //has cldb
                    if (flag4)
                    {
                        classData = new ClassDatabaseFile();
                        classData.Read(reader);
                    }

                    long bufLength = reader.ReadInt64();
                    if (prefReplacersInMemory)
                    {
                        byte[] buf = reader.ReadBytes((int)bufLength);
                        replacer = new AssetsReplacerFromMemory(pathId, classId, monoScriptIndex, buf);
                    }
                    else
                    {
                        replacer = new AssetsReplacerFromStream(pathId, classId, monoScriptIndex, reader.BaseStream, reader.Position, bufLength);
                        reader.Position += bufLength;
                    }

                    if (propertiesHash != null)
                        replacer.SetPropertiesHash(propertiesHash.Value);
                    if (scriptHash != null)
                        replacer.SetScriptIDHash(scriptHash.Value);
                    if (scriptHash != null)
                        replacer.SetTypeInfo(classData, null, false); //idk what the last two are supposed to do
                    if (preloadDependencyCount != 0)
                        replacer.SetPreloadDependencies(preloadDependencies);

                    return replacer;
                }
            }
            return null;
        }
    }

    public class InstallerPackageAssetsDesc
    {
        public bool isBundle;
        public string path;
        public List<object> replacers;
    }
}
