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
            AssetTypeTemplateField template = field.GetTemplateField();
            string align = template.align ? "1" : "0";
            string typeName = template.type;
            string fieldName = template.name;
            bool isArray = template.isArray;

            //string's field isn't aligned but its array is
            if (template.valueType == EnumValueTypes.String)
                align = "1";

            //mainly to handle enum fields not having the int type name
            if (template.valueType != EnumValueTypes.None &&
                template.valueType != EnumValueTypes.Array &&
                template.valueType != EnumValueTypes.ByteArray)
            {
                typeName = CorrectTypeName(template.valueType);
            }

            if (isArray)
            {
                AssetTypeTemplateField sizeTemplate = template.children[0];
                string sizeAlign = sizeTemplate.align ? "1" : "0";
                string sizeTypeName = sizeTemplate.type;
                string sizeFieldName = sizeTemplate.name;

                if (template.valueType == EnumValueTypes.Array)
                {
                    int size = field.GetValue().AsArray().size;
                    sw.WriteLine($"{new string(' ', depth)}{align} {typeName} {fieldName} ({size} items)");
                    sw.WriteLine($"{new string(' ', depth + 1)}{sizeAlign} {sizeTypeName} {sizeFieldName} = {size}");
                    for (int i = 0; i < field.childrenCount; i++)
                    {
                        sw.WriteLine($"{new string(' ', depth + 1)}[{i}]");
                        RecurseTextDump(field.children[i], depth + 2);
                    }
                }
                else if (template.valueType == EnumValueTypes.ByteArray)
                {
                    AssetTypeByteArray byteArray = field.GetValue().AsByteArray();
                    byte[] data = byteArray.data;
                    int size = (int)byteArray.size;

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
                if (field.GetValue() != null)
                {
                    EnumValueTypes evt = field.GetValue().GetValueType();
                    if (evt == EnumValueTypes.String)
                    {
                        //only replace \ with \\ but not " with \" lol
                        //you just have to find the last "
                        string fixedStr = field.GetValue().AsString()
                            .Replace("\\", "\\\\")
                            .Replace("\r", "\\r")
                            .Replace("\n", "\\n");
                        value = $" = \"{fixedStr}\"";
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

        public void DumpJsonAsset(StreamWriter sw, AssetTypeValueField baseField)
        {
            this.sw = sw;
            JToken jBaseField = RecurseJsonDump(baseField, false);
            sw.Write(jBaseField.ToString());
        }

        private JToken RecurseJsonDump(AssetTypeValueField field, bool uabeFlavor)
        {
            AssetTypeTemplateField template = field.GetTemplateField();

            bool isArray = template.isArray;

            if (isArray)
            {
                JArray jArray = new JArray();

                if (template.valueType == EnumValueTypes.Array)
                {
                    for (int i = 0; i < field.childrenCount; i++)
                    {
                        jArray.Add(RecurseJsonDump(field.children[i], uabeFlavor));
                    }
                }
                else if (template.valueType == EnumValueTypes.ByteArray)
                {
                    byte[] byteArrayData = field.GetValue().AsByteArray().data;
                    for (int i = 0; i < byteArrayData.Length; i++)
                    {
                        jArray.Add(byteArrayData[i]);
                    }
                }

                return jArray;
            }
            else
            {
                if (field.GetValue() != null)
                {
                    EnumValueTypes evt = field.GetValue().GetValueType();
                    
                    object value = evt switch
                    {
                        EnumValueTypes.Bool => field.GetValue().AsBool(),
                        EnumValueTypes.Int8 or
                        EnumValueTypes.Int16 or
                        EnumValueTypes.Int32 => field.GetValue().AsInt(),
                        EnumValueTypes.Int64 => field.GetValue().AsInt64(),
                        EnumValueTypes.UInt8 or
                        EnumValueTypes.UInt16 or
                        EnumValueTypes.UInt32 => field.GetValue().AsUInt(),
                        EnumValueTypes.UInt64 => field.GetValue().AsUInt64(),
                        EnumValueTypes.String => field.GetValue().AsString(),
                        EnumValueTypes.Float => field.GetValue().AsFloat(),
                        EnumValueTypes.Double => field.GetValue().AsDouble(),
                        _ => "invalid value"
                    };

                    return (JValue)JToken.FromObject(value);
                }
                else
                {
                    JObject jObject = new JObject();

                    for (int i = 0; i < field.childrenCount; i++)
                    {
                        jObject.Add(field.children[i].GetName(), RecurseJsonDump(field.children[i], uabeFlavor));
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
                aw.bigEndian = false;
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
                aw.bigEndian = false;

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
            bool align = tempField.align;

            if (!tempField.hasValue && !tempField.isArray)
            {
                foreach (AssetTypeTemplateField childTempField in tempField.children)
                {
                    JToken? childToken = token[childTempField.name];

                    if (childToken == null)
                        throw new Exception($"Missing field {childTempField.name} in json.");
                        
                    RecurseJsonImport(childTempField, childToken);
                }

                if (align)
                {
                    aw.Align();
                }
            }
            else
            {
                switch (tempField.valueType)
                {
                    case EnumValueTypes.Bool:
                    {
                        aw.Write((bool)token);
                        break;
                    }
                    case EnumValueTypes.UInt8:
                    {
                        aw.Write((byte)token);
                        break;
                    }
                    case EnumValueTypes.Int8:
                    {
                        aw.Write((sbyte)token);
                        break;
                    }
                    case EnumValueTypes.UInt16:
                    {
                        aw.Write((ushort)token);
                        break;
                    }
                    case EnumValueTypes.Int16:
                    {
                        aw.Write((short)token);
                        break;
                    }
                    case EnumValueTypes.UInt32:
                    {
                        aw.Write((uint)token);
                        break;
                    }
                    case EnumValueTypes.Int32:
                    {
                        aw.Write((int)token);
                        break;
                    }
                    case EnumValueTypes.UInt64:
                    {
                        aw.Write((ulong)token);
                        break;
                    }
                    case EnumValueTypes.Int64:
                    {
                        aw.Write((long)token);
                        break;
                    }
                    case EnumValueTypes.Float:
                    {
                        aw.Write((float)token);
                        break;
                    }
                    case EnumValueTypes.Double:
                    {
                        aw.Write((double)token);
                        break;
                    }
                    case EnumValueTypes.String:
                    {
                        align = true;
                        aw.WriteCountStringInt32((string?)token ?? "");
                        break;
                    }
                    case EnumValueTypes.ByteArray:
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
                if (tempField.isArray && tempField.valueType != EnumValueTypes.ByteArray)
                {
                    //children[0] is size field, children[1] is the data field
                    AssetTypeTemplateField childTempField = tempField.children[1];

                    JArray? tokenArray = (JArray?)token;

                    if (tokenArray == null)
                        throw new Exception($"Field {tempField.name} was not an array in json.");

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

        private string CorrectTypeName(EnumValueTypes valueTypes)
        {
            switch (valueTypes)
            {
                case EnumValueTypes.Bool:
                    return "bool";
                case EnumValueTypes.UInt8:
                    return "UInt8";
                case EnumValueTypes.Int8:
                    return "SInt8";
                case EnumValueTypes.UInt16:
                    return "UInt16";
                case EnumValueTypes.Int16:
                    return "SInt16";
                case EnumValueTypes.UInt32:
                    return "unsigned int";
                case EnumValueTypes.Int32:
                    return "int";
                case EnumValueTypes.UInt64:
                    return "UInt64";
                case EnumValueTypes.Int64:
                    return "SInt64";
                case EnumValueTypes.Float:
                    return "float";
                case EnumValueTypes.Double:
                    return "double";
                case EnumValueTypes.String:
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
    }
}
