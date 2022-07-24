using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Runtime.InteropServices;

namespace BinderTool.Core.Param
{
    //Largely taken from https://github.com/JKAnderson/SoulsFormats/blob/106e54ac414240b05d4fb5db92863beecbc87dec/SoulsFormats/Formats/PARAM/PARAMDEF/Field.cs
    //for compatibility with https://github.com/soulsmods/Paramdex

    /// <summary>
    /// The type of a field in a param file
    /// </summary>
    public enum ParamFieldType
    {
        /// <summary>
        /// Signed 8-bit (1-byte) integer
        /// </summary>
        s8,

        /// <summary>
        /// Unsigned 8-bit (1-byte) integer
        /// </summary>
        u8,

        /// <summary>
        /// Signed 16-bit (2-byte) integer
        /// </summary>
        s16,

        /// <summary>
        /// Unsigned 16-bit (2-byte) integer
        /// </summary>
        u16,

        /// <summary>
        /// Signed 32-bit (4-byte) integer
        /// </summary>
        s32,

        /// <summary>
        /// Unsigned 32-bit (4-byte) integer
        /// </summary>
        u32,

        /// <summary>
        /// Single precision (32-bit, 4-byte) floating point number
        /// </summary>
        f32,

        /// <summary>
        /// Byte or array of bytes typically used for padding
        /// </summary>
        dummy8,

        /// <summary>
        /// Fixed-width Shift-JIS string
        /// </summary>
        fixstr,

        /// <summary>
        /// Fixed-width UTF-16 string
        /// </summary>
        fixstrW
    }

    public static class ParamFieldTypeExtensions
    {
        public static object Parse(this ParamFieldType ty, string s)
        {
            switch (ty) {
                case ParamFieldType.s8: return sbyte.Parse(s);
                case ParamFieldType.u8: return byte.Parse(s);
                case ParamFieldType.s16: return short.Parse(s);
                case ParamFieldType.u16: return ushort.Parse(s);
                case ParamFieldType.s32: return int.Parse(s);
                case ParamFieldType.u32: return uint.Parse(s);
                case ParamFieldType.f32: return float.Parse(s);
                case ParamFieldType.fixstr: return s;
                case ParamFieldType.fixstrW: return s;
                case ParamFieldType.dummy8: return 0;
            }
            //unreachable
            throw new Exception("Unreachable code");
        }
        public static string DefaultDisplayFormat(this ParamFieldType ty)
        {
            switch (ty) {
                case ParamFieldType.s8:
                case ParamFieldType.u8:
                case ParamFieldType.s16:
                case ParamFieldType.u16:
                case ParamFieldType.s32:
                case ParamFieldType.u32: return "%d";
                case ParamFieldType.f32: return "%f";
                case ParamFieldType.fixstr: 
                case ParamFieldType.fixstrW: return "%s";
                case ParamFieldType.dummy8: return "";
            }
            //unreachable
            throw new Exception("Unreachable code");
        }
    }

    [Flags]
    public enum ParamEditFlags
    {
        /// <summary>
        /// Value is editable and does not wrap
        /// </summary>
        None = 0,

        /// <summary>
        /// Value wraps when incrementing/decrementing past the max/min
        /// </summary>
        Wrap = 1,

        /// <summary>
        /// Value is not editable
        /// </summary>
        Lock = 4
    }
    public class ParamField
    {
        public ParamFieldType FieldType { get; set; }

        public string InternalName { get; set; }

        public int BitSize { get; set; } = -1;

        public int ArrayLength { get; set; } = -1;

        public string DisplayName { get; set; } = null;

        public string Enum { get; set; } = null;

        public string Description { get; set; } = null;

        public string DisplayFormat { get; set; } = null;

        public ParamEditFlags EditFlags { get; set; } = ParamEditFlags.None;

        public float Minimum { get; set; }

        public float Maximum { get; set; }

        public float Increment { get; set; }

        public int SortID { get; set; }

        public object Default { get; set; } = null;

        private ParamFormatDesc _FormatDesc;

        public ParamField(ParamFormatDesc desc)
        {
            _FormatDesc = desc;
        }

        public static ParamField Deserialize(ParamFormatDesc desc, XmlNode node)
        {
            var ans = new ParamField(desc);
            string def = node.Attributes["Def"].InnerText;
            var typeStr = def.Split(' ')[0];
            switch (typeStr) {
                case "u8": ans.FieldType = ParamFieldType.u8; break;
                case "s8": ans.FieldType = ParamFieldType.s8; break;
                case "u16": ans.FieldType = ParamFieldType.u16; break;
                case "s16": ans.FieldType = ParamFieldType.s16; break;
                case "u32": ans.FieldType = ParamFieldType.u32; break;
                case "s32": ans.FieldType = ParamFieldType.s32; break;
                case "f32": ans.FieldType = ParamFieldType.f32; break;
                case "fixstr": ans.FieldType = ParamFieldType.fixstr; break;
                case "fixstrW": ans.FieldType = ParamFieldType.fixstrW; break;
                case "dummy8": ans.FieldType = ParamFieldType.dummy8; break;
                default: throw new Exception($"Unrecognized param def type {typeStr}");
            }
            var rest = def.Substring(typeStr.Length + 1);
            int restInd = 0;
            for (; restInd < rest.Length && !char.IsWhiteSpace(rest[restInd]) && rest[restInd] != ':' && rest[restInd] != '['; restInd++) {
                ans.InternalName += rest[restInd];
            }
            if (restInd < rest.Length) {
                if (rest[restInd] == ':') {
                    restInd++;
                    var bitfieldStr = "";
                    for (; restInd < rest.Length && char.IsDigit(rest[restInd]); restInd++) {
                        bitfieldStr += rest[restInd];
                    }
                    ans.BitSize = int.Parse(bitfieldStr);
                } else if (rest[restInd] == '[') {
                    restInd++;
                    var arrStr = "";
                    for (; restInd < rest.Length && char.IsDigit(rest[restInd]); restInd++) {
                        arrStr += rest[restInd];
                    }
                    ans.ArrayLength = int.Parse(arrStr);
                    if (rest[restInd++] != ']') throw new Exception("Expected ']' after array length in def string");
                }
            }
            while (restInd < rest.Length && char.IsWhiteSpace(rest[restInd])) restInd++;
            if (restInd < rest.Length && rest[restInd] == '=') {
                restInd++;
                while (restInd < rest.Length && char.IsWhiteSpace(rest[restInd])) restInd++;
                ans.Default = ans.FieldType.Parse(rest.Substring(restInd));
            }
            ans.DisplayName = node.SelectSingleNode("DisplayName")?.InnerText ?? ans.InternalName;
            ans.Enum = node.SelectSingleNode("Enum")?.InnerText;
            ans.Description = node.SelectSingleNode("Description")?.InnerText;
            ans.DisplayFormat = node.SelectSingleNode("DisplayFormat")?.InnerText ?? ans.FieldType.DefaultDisplayFormat();
            ans.EditFlags = 0;
            var editFlagsArr = node.SelectSingleNode("EditFlags")?.InnerText.Split(' ') ?? new string[0];
            foreach (var editFlag in editFlagsArr) {
                switch (editFlag) {
                    case "Wrap": ans.EditFlags |= ParamEditFlags.Wrap; break;
                    case "Lock": ans.EditFlags |= ParamEditFlags.Lock; break;
                    default: throw new Exception($"Unrecognized edit flag {editFlag}");
                }
            }
            ans.Minimum = float.Parse(node.SelectSingleNode("Minimum")?.InnerText ?? "0");
            ans.Maximum = float.Parse(node.SelectSingleNode("Maximum")?.InnerText ?? "0");
            ans.Increment = float.Parse(node.SelectSingleNode("Increment")?.InnerText ?? "0");
            ans.SortID = int.Parse(node.SelectSingleNode("SortID")?.InnerText ?? "0");
            return ans;
        }

        public object Read(BinaryReader r, ref int bitOffset, ref uint bitValue)
        {
            if (BitSize != -1) {
                if (bitOffset == -1) {
                    bitOffset = 0;
                    switch (FieldType) {
                        case ParamFieldType.u8:
                        case ParamFieldType.dummy8: bitValue = r.ReadByte(); break;
                        case ParamFieldType.u16: bitValue = r.ReadUInt16(); break;
                        case ParamFieldType.u32: bitValue = r.ReadUInt32(); break;
                        default: throw new Exception($"Invalid field type for bit field: {FieldType}");
                    }
                }
                var ans = bitValue << (32 - BitSize - bitOffset) >> (32 - BitSize);
                bitOffset += BitSize;
                switch (FieldType) {
                    case ParamFieldType.u8:
                    case ParamFieldType.dummy8: return (byte)ans;
                    case ParamFieldType.u16: return (ushort)ans;
                    default: return ans;
                }
            }
            bitOffset = -1;
            switch (FieldType) {
                case ParamFieldType.s8: return r.ReadSByte();
                case ParamFieldType.u8: return r.ReadByte();
                case ParamFieldType.s16: return r.ReadInt16();
                case ParamFieldType.u16: return r.ReadUInt16();
                case ParamFieldType.s32: return r.ReadInt32();
                case ParamFieldType.u32: return r.ReadUInt32();
                case ParamFieldType.f32: return r.ReadSingle();
                case ParamFieldType.fixstr: return Encoding.ASCII.GetString(r.ReadBytes(ArrayLength));
                case ParamFieldType.fixstrW: return Encoding.Unicode.GetString(r.ReadBytes(ArrayLength * 2));
                case ParamFieldType.dummy8: return 0;
            }
            throw new Exception("Unreachable code");
        }

        /// <summary>
        /// Formats a value received from <code>Read</code> as a string
        /// </summary>
        public string Format(object value)
        {
            var fstr = DisplayFormat;
            var ans = new StringBuilder(100);
            if (value is sbyte b) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, b);
            else if(value is short s) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, s);
            else if (value is int i) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, i);
            else if (value is byte ub) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, ub);
            else if (value is ushort us) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, us);
            else if (value is uint ui) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, ui);
            else if (value is float f) _snwprintf_s(ans, (IntPtr)100, (IntPtr)32, fstr, f);
            else throw new Exception($"Could not format; unexpected value type for {value}");
            return ans.ToString();
        }

        //we have to use C's printf because the game uses C-style format specifiers internally
        //and converting those to C# format specifiers is error-prone
        [DllImport("msvcrt.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _snwprintf_s([MarshalAs(UnmanagedType.LPWStr)] StringBuilder str, IntPtr bufferSize, IntPtr length, String format, int p);

        [DllImport("msvcrt.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _snwprintf_s([MarshalAs(UnmanagedType.LPWStr)] StringBuilder str, IntPtr bufferSize, IntPtr length, String format, uint p);

        [DllImport("msvcrt.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _snwprintf_s([MarshalAs(UnmanagedType.LPWStr)] StringBuilder str, IntPtr bufferSize, IntPtr length, String format, double p);
    }

    public class ParamFormatDesc
    {
        public short DataVersion { get; set; }

        public string ParamType { get; set; }
        
        public bool BigEndian { get; set; }

        public bool Unicode { get; set; }

        public short FormatVersion { get; set; }

        public List<ParamField> Fields { get; set; }

        public static ParamFormatDesc Deserialize(string path)
        {
            var xml = new XmlDocument();
            xml.Load(path);
            return Deserialize(xml);
        }

        public static ParamFormatDesc Deserialize(XmlDocument xml)
        {
            var ans = new ParamFormatDesc();
            XmlNode root = xml.SelectSingleNode("PARAMDEF");
            ans.ParamType = root.SelectSingleNode("ParamType").InnerText;
            ans.DataVersion = short.Parse(root.SelectSingleNode("DataVersion")?.InnerText ?? "0");
            ans.BigEndian = bool.Parse(root.SelectSingleNode("BigEndian").InnerText);
            ans.Unicode = bool.Parse(root.SelectSingleNode("Unicode").InnerText);
            ans.FormatVersion = short.Parse((root.SelectSingleNode("FormatVersion") ?? root.SelectSingleNode("Version")).InnerText);
            ans.Fields = new List<ParamField>();
            foreach (XmlNode node in root.SelectNodes("Fields/Field")) {
                ans.Fields.Add(ParamField.Deserialize(ans, node));
            }
            return ans;
        }
    }
}
