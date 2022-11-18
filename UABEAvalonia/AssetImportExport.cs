using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class AssetImportExport
    {
        private StreamWriter sw;
        private StreamReader sr;
        private AssetsFileWriter aw;

        public void DumpRawAsset(FileStream wfs, AssetsFileReader reader, long position, uint size)
        {
            Stream assetFs = reader.BaseStream;
            assetFs.Position = position;
            byte[] buf = new byte[4096];
            int bytesLeft = (int)size;
            while (bytesLeft > 0)
            {
                int readSize = assetFs.Read(buf, 0, Math.Min(bytesLeft, buf.Length));
                wfs.Write(buf, 0, readSize);
                bytesLeft -= readSize;
            }
        }

        public void DumpTextAsset(StreamWriter sw, AssetTypeValueField baseField)
        {
            this.sw = sw;
            RecurseTextDump(baseField, 0);
        }

        private void RecurseTextDump(AssetTypeValueField field, int depth)
        {
            AssetTypeTemplateField template = field.TemplateField;
            string align = template.IsAligned ? "1" : "0";
            string typeName = template.Type;
            string fieldName = template.Name;
            bool isArray = template.IsArray;

            //string's field isn't aligned but its array is
            if (template.ValueType == AssetValueType.String)
                align = "1";

            //mainly to handle enum fields not having the int type name
            if (template.ValueType != AssetValueType.None &&
                template.ValueType != AssetValueType.Array &&
                template.ValueType != AssetValueType.ByteArray &&
                !isArray)
            {
                typeName = CorrectTypeName(template.ValueType);
            }

            if (isArray)
            {
                AssetTypeTemplateField sizeTemplate = template.Children[0];
                string sizeAlign = sizeTemplate.IsAligned ? "1" : "0";
                string sizeTypeName = sizeTemplate.Type;
                string sizeFieldName = sizeTemplate.Name;

                if (template.ValueType != AssetValueType.ByteArray)
                {
                    int size = field.AsArray.size;
                    sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)");
                    sw.WriteLine($"{new string(' ', depth + 1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}");
                    for (int i = 0; i < field.Children.Count; i++)
                    {
                        sw.WriteLine($"{new string(' ', depth + 1)}[{i}]");
                        RecurseTextDump(field.Children[i], depth + 2);
                    }
                }
                else
                {
                    byte[] data = field.AsByteArray;
                    int size = data.Length;

                    sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)");
                    sw.WriteLine($"{new string(' ', depth + 1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}");
                    for (int i = 0; i < size; i++)
                    {
                        sw.WriteLine($"{new string(' ', depth + 1)}[{i}]");
                        sw.WriteLine($"{new string(' ', depth + 2)}0 UInt8 data = {data[i]}");
                    }
                }
            }
            else
            {
                string value = "";
                if (field.Value != null)
                {
                    AssetValueType evt = field.Value.ValueType;
                    if (evt == AssetValueType.String)
                    {
                        //only replace \ with \\ but not " with \" lol
                        //you just have to find the last "
                        string fixedStr = field.AsString
                            .Replace("\\", "\\\\")
                            .Replace("\r", "\\r")
                            .Replace("\n", "\\n");
                        value = $" = \"{fixedStr}\"";
                    }
                    else if (1 <= (int)evt && (int)evt <= 12)
                    {
                        value = $" = {field.AsString}";
                    }
                }
                sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName}{value}");

                for (int i = 0; i < field.Children.Count; i++)
                {
                    RecurseTextDump(field.Children[i], depth + 1);
                }
            }
        }

        public void DumpJsonAsset(StreamWriter sw, AssetTypeValueField baseField)
        {
            this.sw = sw;
            JToken jBaseField = RecurseJsonDump(baseField, false);
            sw.Write(jBaseField.ToString());
        }

        private JToken RecurseJsonDump(AssetTypeValueField field, bool uabeFlavor)
        {
            AssetTypeTemplateField template = field.TemplateField;

            bool isArray = template.IsArray;

            if (isArray)
            {
                JArray jArray = new JArray();

                if (template.ValueType != AssetValueType.ByteArray)
                {
                    for (int i = 0; i < field.Children.Count; i++)
                    {
                        jArray.Add(RecurseJsonDump(field.Children[i], uabeFlavor));
                    }
                }
                else
                {
                    byte[] byteArrayData = field.AsByteArray;
                    for (int i = 0; i < byteArrayData.Length; i++)
                    {
                        jArray.Add(byteArrayData[i]);
                    }
                }

                return jArray;
            }
            else
            {
                if (field.Value != null)
                {
                    AssetValueType evt = field.Value.ValueType;
                    
                    object value = evt switch
                    {
                        AssetValueType.Bool => field.AsBool,
                        AssetValueType.Int8 or
                        AssetValueType.Int16 or
                        AssetValueType.Int32 => field.AsInt,
                        AssetValueType.Int64 => field.AsLong,
                        AssetValueType.UInt8 or
                        AssetValueType.UInt16 or
                        AssetValueType.UInt32 => field.AsUInt,
                        AssetValueType.UInt64 => field.AsULong,
                        AssetValueType.String => field.AsString,
                        AssetValueType.Float => field.AsFloat,
                        AssetValueType.Double => field.AsDouble,
                        _ => "invalid value"
                    };

                    return (JValue)JToken.FromObject(value);
                }
                else
                {
                    JObject jObject = new JObject();

                    for (int i = 0; i < field.Children.Count; i++)
                    {
                        jObject.Add(field.Children[i].FieldName, RecurseJsonDump(field.Children[i], uabeFlavor));
                    }

                    return jObject;
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

        public byte[]? ImportTextAsset(StreamReader sr, out string? exceptionMessage)
        {
            this.sr = sr;
            using (MemoryStream ms = new MemoryStream())
            {
                aw = new AssetsFileWriter(ms);
                aw.BigEndian = false;
                try
                {
                    ImportTextAssetLoop();
                    exceptionMessage = null;
                }
                catch (Exception ex)
                {
                    exceptionMessage = ex.Message;
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
                        valueStrFix = UnescapeDumpString(valueStrFix);
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

        public byte[]? ImportJsonAsset(AssetTypeTemplateField tempField, StreamReader sr, out string? exceptionMessage)
        {
            this.sr = sr;
            using (MemoryStream ms = new MemoryStream())
            {
                aw = new AssetsFileWriter(ms);
                aw.BigEndian = false;

                try
                {
                    string jsonText = sr.ReadToEnd();
                    JToken token = JToken.Parse(jsonText);

                    RecurseJsonImport(tempField, token);
                    exceptionMessage = null;
                }
                catch (Exception ex)
                {
                    exceptionMessage = ex.Message;
                    return null;
                }
                return ms.ToArray();
            }
        }

        private void RecurseJsonImport(AssetTypeTemplateField tempField, JToken token)
        {
            bool align = tempField.IsAligned;

            if (!tempField.HasValue && !tempField.IsArray)
            {
                foreach (AssetTypeTemplateField childTempField in tempField.Children)
                {
                    JToken? childToken = token[childTempField.Name];

                    if (childToken == null)
                        throw new Exception($"Missing field {childTempField.Name} in json.");
                        
                    RecurseJsonImport(childTempField, childToken);
                }

                if (align)
                {
                    aw.Align();
                }
            }
            else
            {
                switch (tempField.ValueType)
                {
                    case AssetValueType.Bool:
                    {
                        aw.Write((bool)token);
                        break;
                    }
                    case AssetValueType.UInt8:
                    {
                        aw.Write((byte)token);
                        break;
                    }
                    case AssetValueType.Int8:
                    {
                        aw.Write((sbyte)token);
                        break;
                    }
                    case AssetValueType.UInt16:
                    {
                        aw.Write((ushort)token);
                        break;
                    }
                    case AssetValueType.Int16:
                    {
                        aw.Write((short)token);
                        break;
                    }
                    case AssetValueType.UInt32:
                    {
                        aw.Write((uint)token);
                        break;
                    }
                    case AssetValueType.Int32:
                    {
                        aw.Write((int)token);
                        break;
                    }
                    case AssetValueType.UInt64:
                    {
                        aw.Write((ulong)token);
                        break;
                    }
                    case AssetValueType.Int64:
                    {
                        aw.Write((long)token);
                        break;
                    }
                    case AssetValueType.Float:
                    {
                        aw.Write((float)token);
                        break;
                    }
                    case AssetValueType.Double:
                    {
                        aw.Write((double)token);
                        break;
                    }
                    case AssetValueType.String:
                    {
                        align = true;
                        aw.WriteCountStringInt32((string?)token ?? "");
                        break;
                    }
                    case AssetValueType.ByteArray:
                    {
                        JArray byteArrayJArray = ((JArray?)token) ?? new JArray();
                        byte[] byteArrayData = new byte[byteArrayJArray.Count];
                        for (int i = 0; i < byteArrayJArray.Count; i++)
                        {
                            byteArrayData[i] = (byte)byteArrayJArray[i];
                        }
                        aw.Write(byteArrayData.Length);
                        aw.Write(byteArrayData);
                        break;
                    }
                }

                //have to do this because of bug in MonoDeserializer
                if (tempField.IsArray && tempField.ValueType != AssetValueType.ByteArray)
                {
                    //children[0] is size field, children[1] is the data field
                    AssetTypeTemplateField childTempField = tempField.Children[1];

                    JArray? tokenArray = (JArray?)token;

                    if (tokenArray == null)
                        throw new Exception($"Field {tempField.Name} was not an array in json.");

                    aw.Write(tokenArray.Count);
                    foreach (JToken childToken in tokenArray.Children())
                    {
                        RecurseJsonImport(childTempField, childToken);
                    }
                }

                if (align)
                {
                    aw.Align();
                }
            }
        }

        private bool StartsWithSpace(string str, string value)
        {
            return str.StartsWith(value + " ");
        }

        private string UnescapeDumpString(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length);
            bool escaping = false;
            foreach (char c in str)
            {
                if (!escaping && c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (escaping)
                {
                    if (c == '\\')
                        sb.Append('\\');
                    else if (c == 'r')
                        sb.Append('\r');
                    else if (c == 'n')
                        sb.Append('\n');
                    else
                        sb.Append(c);

                    escaping = false;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private string CorrectTypeName(AssetValueType valueTypes)
        {
            switch (valueTypes)
            {
                case AssetValueType.Bool:
                    return "bool";
                case AssetValueType.UInt8:
                    return "UInt8";
                case AssetValueType.Int8:
                    return "SInt8";
                case AssetValueType.UInt16:
                    return "UInt16";
                case AssetValueType.Int16:
                    return "SInt16";
                case AssetValueType.UInt32:
                    return "unsigned int";
                case AssetValueType.Int32:
                    return "int";
                case AssetValueType.UInt64:
                    return "UInt64";
                case AssetValueType.Int64:
                    return "SInt64";
                case AssetValueType.Float:
                    return "float";
                case AssetValueType.Double:
                    return "double";
                case AssetValueType.String:
                    return "string";
            }
            return "UnknownBaseType";
        }

        public static AssetsReplacer CreateAssetReplacer(AssetContainer cont, byte[] data)
        {
            return new AssetsReplacerFromMemory(0, cont.PathId, (int)cont.ClassId, cont.MonoId, data);
        }

        public static BundleReplacer CreateBundleReplacer(string name, bool isSerialized, byte[] data)
        {
            return new BundleReplacerFromMemory(name, name, isSerialized, data, -1);
        }

        public static BundleReplacer CreateBundleReplacer(string name, bool isSerialized, Stream stream, long start, long size)
        {
            return new BundleReplacerFromStream(name, name, isSerialized, stream, start, size);
        }

        public static BundleReplacer CreateBundleRemover(string name, bool isSerialized, int bundleListIndex = -1)
        {
            return new BundleRemover(name, isSerialized, bundleListIndex);
        }
    }
}
