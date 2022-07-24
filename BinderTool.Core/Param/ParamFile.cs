using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BinderTool.Core.Param
{
    public class ParamFile
    {
        public string StructName { get; set; }
        public int Version { get; set; }
        public int EntrySize { get; set; }
        public ParamEntry[] Entries { get; set; }
        public short Type1 { get; set; }
        public short Type2 { get; set; }

        private Dictionary<long, string> namesCache = null;

        public static ParamFile ReadParamFile(Stream inputStream)
        {
            ParamFile paramFile = new ParamFile();
            paramFile.Read(inputStream);
            return paramFile;
        }

        public void Read(Stream inputStream)
        {
            BinaryReader reader = new BinaryReader(inputStream, Encoding.ASCII, true);
            int fileSize = reader.ReadInt32(); //0x0
            short unknown1 = reader.ReadInt16(); // 0 0x4
            short type1 = reader.ReadInt16(); // 0-2 0x6
            short type2 = reader.ReadInt16(); // 0-10 0x8
            short count = reader.ReadInt16(); //0xa
            int unknown4 = reader.ReadInt32(); //0xc
            int fileSize2 = reader.ReadInt32(); //seems to be 64-bit 0x10
            int unknown6 = reader.ReadInt32(); //0x14
            int unknown7 = reader.ReadInt32(); //0x18
            int unknown8 = reader.ReadInt32(); //0x1c
            int unknown9 = reader.ReadInt32(); //0x20
            int unknown10 = reader.ReadInt32(); //0x24
            int unknown13 = reader.ReadInt32(); //0x28
            int version = reader.ReadInt32(); //0x2c
            int dataOffset = reader.ReadInt32();
            int unknown14 = reader.ReadInt32();
            int unknown15 = reader.ReadInt32();
            int unknown16 = reader.ReadInt32();

            fileSize = System.Math.Min(fileSize, fileSize2);

            ParamEntry[] entries = new ParamEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new ParamEntry();
                entries[i].Read(reader);
            }

            const int headerSize = 64;
            const int dictionarySize = 24;

            int entrySize = (fileSize - headerSize - count * dictionarySize) / count;
            for (int i = 0; i < count; i++)
            {
                entries[i].Data = reader.ReadBytes(entrySize);
            }

            string paramName = reader.ReadNullTerminatedString();

            StructName = paramName;
            Version = version;
            Type1 = type1;
            Type2 = type2;
            EntrySize = entrySize;
            Entries = entries;
        }

        /// <summary>
        /// Gets an entry's name from the name .txt file if there is one
        /// </summary>
        /// <param name="filename">
        /// The name of the .param file to match to a name .txt file. 
        /// May include preceding directories if convenient.
        /// </param>
        /// <param name="paramDefPath">The path to the param def folder</param>
        /// <returns>A string with the entry's name if one exists, or <code>null</code>otherwise</returns>
        public string GetName(long id, GameVersion game, string filename, string paramDefPath)
        {
            if (namesCache == null) {
                var txtName = Path.GetFileNameWithoutExtension(filename) + ".txt";
                string gameFolder;
                switch (game) {
                    case GameVersion.DarkSouls2: gameFolder = "DS2S"; break;
                    case GameVersion.DarkSouls3: gameFolder = "DS3"; break;
                    case GameVersion.Bloodborne: gameFolder = "BB"; break;
                    case GameVersion.Sekiro: gameFolder = "SDT"; break;
                    case GameVersion.EldenRing: gameFolder = "ER"; break;
                    default: return null;
                }
                var txtPath = Path.Combine(paramDefPath, gameFolder, "Names", txtName);
                namesCache = new Dictionary<long, string>();
                if (!File.Exists(txtPath)) return null;
                var lines = File.ReadAllLines(txtPath);
                foreach (var line in lines) {
                    int spInd = 0;
                    while (char.IsDigit(line[spInd])) spInd++;
                    var idStr = line.Substring(0, spInd);
                    var val = line.Substring(spInd + 1);
                    namesCache[long.Parse(idStr)] = val;
                }
            }
            return namesCache.ContainsKey(id) ? namesCache[id] : null;
        }
    
        public List<(long, List<object>)> Parse(ParamFormatDesc def)
        {
            var ans = new List<(long, List<object>)>();
            foreach (var entry in Entries) {
                var curr = new List<object>();
                var reader = new BinaryReader(new MemoryStream(entry.Data));
                int bitOffset = -1;
                uint bitValue = 0;
                foreach (var f in def.Fields) {
                    curr.Add(f.Read(reader, ref bitOffset, ref bitValue));
                }
                ans.Add((entry.Id, curr));
            }
            return ans;
        }

        public ParamFormatDesc TryGetDef(GameVersion game, string paramDefPath)
        {
            string gameFolder;
            switch (game) {
                case GameVersion.DarkSouls2: gameFolder = "DS2S"; break;
                case GameVersion.DarkSouls3: gameFolder = "DS3"; break;
                case GameVersion.Bloodborne: gameFolder = "BB"; break;
                case GameVersion.Sekiro: gameFolder = "SDT"; break;
                case GameVersion.EldenRing: gameFolder = "ER"; break;
                default: return null;
            }
            var path = Path.Combine(paramDefPath, gameFolder, "Defs", StructName + ".xml");
            if (!File.Exists(path)) return null;
            return ParamFormatDesc.Deserialize(path);
        }
    }
}