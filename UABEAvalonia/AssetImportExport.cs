using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UABEAvalonia
{
    public class AssetImportExport
    {
        private StreamWriter sw;
        private StreamReader sr;
        private AssetsFileWriter aw;

        public void DumpRawAsset(FileStream fs, AssetsFile file, AssetFileInfoEx info)
        {
            Stream assetFs = file.readerPar;
            assetFs.Position = info.absoluteFilePos;
            byte[] buf = new byte[4096];
            int bytesLeft = (int)info.curFileSize;
            while (bytesLeft > 0)
            {
                int size = assetFs.Read(buf, 0, buf.Length);
                fs.Write(buf, 0, size);
                bytesLeft -= size;
            }
        }

        public void DumpTextAsset(StreamWriter sw, AssetTypeValueField baseField)
        {
            this.sw = sw;
            RecurseTextDump(baseField, 0);
        }

        public void DumpXmlAsset(string path, AssetTypeValueField baseField)
        {
            var doc = new XmlDocument();
            var result = DumpXmlNode(doc, baseField);
            doc.AppendChild(result);
            doc.Save(path);
        }

        /**
         * this method 's logic fully copy from dump text asset
         */
        private XmlNode DumpXmlNode(XmlDocument doc, AssetTypeValueField field) {
            AssetTypeTemplateField template = field.GetTemplateField();
            string align = template.align ? true.ToString() : false.ToString();
            string typeName = template.type;
            string fieldName = template.name;
            bool isArray = template.isArray;

            if (template.valueType == EnumValueTypes.String)
                align = true.ToString();
            string nodeName = field.GetValue() != null ? field.GetValue().GetValueType().ToString() : "object";
            var e = doc.CreateElement(isArray ? "array" : nodeName);
            e.SetAttribute("align", align);
            if (field.GetValue() == null) { 
                e.SetAttribute("typeName", typeName);
            }
            e.SetAttribute("fieldName", fieldName);
            if (isArray)
            {
                AssetTypeTemplateField sizeTemplate = template.children[0];
                string sizeAlign = sizeTemplate.align ? true.ToString() : false.ToString();
                string sizeTypeName = sizeTemplate.type;
                string sizeFieldName = sizeTemplate.name;
                int size = field.GetValue().AsArray().size;
                e.SetAttribute("size", size.ToString());
                e.SetAttribute("sizeAlign", sizeAlign);
                e.SetAttribute("sizeTypeName", sizeTypeName);
                e.SetAttribute("sizeFieldName", sizeFieldName);
                for (int i = 0; i < field.childrenCount; i++)
                {
                    var result = DumpXmlNode(doc, field.children[i]);
                    e.AppendChild(result);
                }
            }
            else
            {
                string value = "";
                if (field.GetValue() != null)
                {
                    EnumValueTypes evt = field.GetValue().GetValueType();
                    if (evt == EnumValueTypes.String)
                    {
                        value = field.GetValue().AsString().Replace("\\", "\\\\");
                    }
                    else if (1 <= (int)evt && (int)evt <= 12)
                    {
                        value = field.GetValue().AsString();
                    }
                    var text = doc.CreateTextNode(value);
                    e.AppendChild(text);
                }
                for (int i = 0; i < field.childrenCount; i++)
                {
                    var result = DumpXmlNode(doc, field.children[i]);
                    e.AppendChild(result);
                }
            }
            return e;
        }

        private void RecurseTextDump(AssetTypeValueField field, int depth)
        {
            AssetTypeTemplateField template = field.GetTemplateField();
            string align = template.align ? "1" : "0";
            string typeName = template.type;
            string fieldName = template.name;
            bool isArray = template.isArray;

            //string's field isn't aligned but its array is
            if (template.valueType == EnumValueTypes.String)
                align = "1";

            if (isArray)
            {
                AssetTypeTemplateField sizeTemplate = template.children[0];
                string sizeAlign = sizeTemplate.align ? "1" : "0";
                string sizeTypeName = sizeTemplate.type;
                string sizeFieldName = sizeTemplate.name;
                int size = field.GetValue().AsArray().size;
                sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)");
                sw.WriteLine($"{new string(' ', depth+1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}");
                for (int i = 0; i < field.childrenCount; i++)
                {
                    sw.WriteLine($"{new string(' ', depth+1)}[{i}]");
                    RecurseTextDump(field.children[i], depth + 2);
                }
            }
            else
            {
                string value = "";
                if (field.GetValue() != null)
                {
                    EnumValueTypes evt = field.GetValue().GetValueType();
                    if (evt == EnumValueTypes.String)
                    {
                        //only replace \ with \\ but not " with \" lol
                        //you just have to find the last "
                        value = $" = \"{field.GetValue().AsString().Replace("\\", "\\\\")}\"";
                    }
                    else if (1 <= (int)evt && (int)evt <= 12)
                    {
                        value = $" = {field.GetValue().AsString()}";
                    }
                }
                sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName}{value}");

                for (int i = 0; i < field.childrenCount; i++)
                {
                    RecurseTextDump(field.children[i], depth + 1);
                }
            }
        }

        public byte[] ImportRawAsset(FileStream fs)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public byte[]? ImportTextAsset(StreamReader sr)
        {
            this.sr = sr;
            using (MemoryStream ms = new MemoryStream())
            {
                aw = new AssetsFileWriter(ms);
                aw.bigEndian = false;
                try
                {
                    ImportTextAssetLoop();
                }
                catch
                {
                    return null;
                }
                return ms.ToArray();
            }
        }

        private void ImportTextAssetLoop()
        {
            Stack<bool> alignStack = new Stack<bool>();
            while (true)
            {
                string? line = sr.ReadLine();


                if (line == null)
                    return;

                int thisDepth = 0;
                while (line[thisDepth] == ' ')
                    thisDepth++;

                if (line[thisDepth] == '[') //array index, ignore
                    continue;

                if (thisDepth < alignStack.Count)
                {
                    while (thisDepth < alignStack.Count)
                    {
                        if (alignStack.Pop())
                            aw.Align();
                    }
                }

                bool align = line.Substring(thisDepth, 1) == "1";
                int typeName = thisDepth + 2;
                int eqSign = line.IndexOf('=');
                string valueStr = line.Substring(eqSign + 1).Trim();

                if (eqSign != -1)
                {
                    string check = line.Substring(typeName);
                    //this list may be incomplete
                    if (StartsWithSpace(check, "bool"))
                    {
                        aw.Write(bool.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt8"))
                    {
                        aw.Write(byte.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt8"))
                    {
                        aw.Write(sbyte.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt16"))
                    {
                        aw.Write(ushort.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt16"))
                    {
                        aw.Write(short.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "unsigned int"))
                    {
                        aw.Write(uint.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "int"))
                    {
                        aw.Write(int.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt64"))
                    {
                        aw.Write(ulong.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt64"))
                    {
                        aw.Write(long.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "float"))
                    {
                        aw.Write(float.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "double"))
                    {
                        aw.Write(double.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "string"))
                    {
                        int firstQuote = valueStr.IndexOf('"');
                        int lastQuote = valueStr.LastIndexOf('"');
                        string valueStrFix = valueStr.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        aw.WriteCountStringInt32(valueStrFix);
                    }

                    if (align)
                    {
                        aw.Align();
                    }
                }
                else
                {
                    alignStack.Push(align);
                }
            }
        }

        private bool StartsWithSpace(string str, string value)
        {
            return str.StartsWith(value + " ");
        }

        public static AssetsReplacer CreateAssetReplacer(AssetsFile file, AssetFileInfoEx info, byte[] data)
        {
            return new AssetsReplacerFromMemory(0, info.index, (int)info.curFileType, AssetHelper.GetScriptIndex(file, info), data);
        }

        public static BundleReplacer CreateBundleReplacer(string name, bool isSerialized, byte[] data)
        {
            return new BundleReplacerFromMemory(name, name, isSerialized, data, -1);
        }

        /**
         * @return is anything wrote
         */
        public static bool writeData(AssetsFileWriter asset, string name, string value)
        {
            bool writed = true;
            switch (name.ToLower())
            {
                case "bool":
                    asset.Write(bool.Parse(value));
                    break;
                case "uint8":
                    asset.Write(byte.Parse(value));
                    break;
                case "sint8":
                    asset.Write(sbyte.Parse(value));
                    break;
                case "uint16":
                    asset.Write(ushort.Parse(value));
                    break;
                case "sint16":
                    asset.Write(short.Parse(value));
                    break;
                case "unsigned int":
                    asset.Write(uint.Parse(value));
                    break;
                case "int32":
                case "int":
                    asset.Write(int.Parse(value));
                    break;
                case "uint64":
                    asset.Write(ulong.Parse(value));
                    break;
                case "int64":
                case "sint64":
                    asset.Write(long.Parse(value));
                    break;
                case "float":
                    asset.Write(float.Parse(value));
                    break;
                case "double":
                    asset.Write(double.Parse(value));
                    break;
                case "string":
                    asset.WriteCountStringInt32(value);
                    break;
                default:
                    writed = false;
                    break;
            }
            return writed;
        }

        /**
         * all logic is rewrite from ImportTextAssetLoop
         * xml is more readable than text, and they can be easily modify by other program or function
         */
        public static byte[]? ImportXml(string path)
        {
            try
            {
                MemoryStream mainStream = new MemoryStream();
                AssetsFileWriter aw = new AssetsFileWriter(mainStream);
                aw.bigEndian = false;
                using (XmlReader reader = XmlReader.Create(File.OpenRead(path)))
                {
                    string? lastValue = null;
                    /**
                     * store every node 's align requirement info;
                     */
                    Stack<bool> alignStack = new Stack<bool>();
                    /**
                     * every array data 's MemoryStream, when reach end of array they will append on mainStream;
                     */
                    Stack<Stream> streams = new Stack<Stream>();
                    /**
                     * count every object/array node's direct sub child
                     */
                    Stack<int> objectCount = new Stack<int>();
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                lastValue = null;
                                bool result;
                                bool.TryParse(reader.GetAttribute("align") ?? "false", out result);
                                alignStack.Push(result);
                                if (reader.Name.ToLower().Equals("array"))
                                {
                                    streams.Push(new MemoryStream());
                                }
                                if (reader.Name.ToLower().Equals("array") || reader.Name.ToLower().Equals("object"))
                                {
                                    if (objectCount.Count > 0)
                                    {
                                        objectCount.Push(objectCount.Pop() + 1);
                                    }
                                    objectCount.Push(0);
                                }
                                break;
                            case XmlNodeType.EndElement:
                                bool align = alignStack.Pop();
                                int itemCount = 0;
                                bool isArray = reader.Name.ToLower().Equals("array");
                                bool isObject = reader.Name.ToLower().Equals("object");
                                if (isArray || isObject)
                                {
                                    itemCount = objectCount.Pop();
                                }
                                if (isArray)
                                {
                                    Stream topStream = streams.Pop();
                                    var writer = new AssetsFileWriter(topStream);
                                    writer.bigEndian = false;
                                    writer.Write(itemCount);
                                    streams.TryPeek(out Stream? next);
                                    topStream.Position = 0;
                                    topStream.CopyTo(next ?? mainStream);
                                }
                                var writeResult = false;
                                if (streams.Count > 0)
                                {
                                    var writer = new AssetsFileWriter(streams.Peek());
                                    writer.bigEndian = false;
                                    writeResult = writeData(writer, reader.Name, lastValue ?? "");
                                }
                                else
                                {
                                    writeResult = writeData(aw, reader.Name, lastValue ?? "");
                                }
                                if (!writeResult && !reader.Name.ToLower().Equals("array") && !reader.Name.ToLower().Equals("object"))
                                {
                                    throw new Exception($"error in writing {reader.Name} {reader.Value}");
                                }
                                /** 
                                 * align data if need
                                 */
                                if (align)
                                {
                                    streams.TryPeek(out Stream? next);
                                    var topStream = next ?? mainStream;
                                    long currentSize = mainStream.Length;
                                    /** 
                                     * count length
                                     */
                                    foreach (var item in streams.ToArray())
                                    {
                                        currentSize += item.Length;
                                    }
                                    long alignByteCount = 4 - (currentSize % 4);
                                    if (alignByteCount < 4 && alignByteCount > 0)
                                    {
                                        topStream.Write(new byte[4], 0, (int)alignByteCount);
                                    }
                                }
                                break;
                            case XmlNodeType.Text:
                                lastValue = reader.Value;
                                break;
                        }
                    }
                }
                return mainStream.ToArray();
            }
            catch (Exception e) {
                return null;            
            }
        }
    }
}
