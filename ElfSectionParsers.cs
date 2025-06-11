using ELFSharp.ELF;
using System.Text;

namespace DwarfParser
{
    public class DebugStr
    {
        private byte[] dataBytes;
        public DebugStr(IELF elfFile)
        {
            dataBytes = elfFile.Sections.Where(s => s.Name == ".debug_str").First().GetContents();
        }

        public String readStrFrom(UInt64 offset)
        {
            var output = new List<byte>();
            byte character;
            while ((character = dataBytes[offset]) > 0)
            {
                output.Add(character);
                offset++;
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }
    }

    public class DebugStrOff
    {
        private byte[] dataBytes;
        private ulong index = 0;
        private byte unit_length_bytes_count;
        private bool is64bitDW = false;
        UInt16 version;
        UInt64 unit_length;
        UInt16 padding = 0;
        byte addr_size = 0;
        public DebugStrOff(IELF elfFile)
        {
            dataBytes = elfFile.Sections.Where(s => s.Name == ".debug_str_offsets").First().GetContents();
            parseHeader();
        }

        private void parseHeader()
        {
            unit_length = BitConverter.ToUInt32(dataBytes, (int)index);
            index += 4;

            if (unit_length == 0xffffffff)
            {
                is64bitDW = true;
                unit_length = BitConverter.ToUInt64(dataBytes, (int)index);
                index += 8;
                unit_length_bytes_count = 12;
                addr_size = 8;
            }
            else
            {
                is64bitDW = false;
                unit_length_bytes_count = 4;
                addr_size = 4;
            }

            version = BitConverter.ToUInt16(dataBytes, (int)index);
            index += 2;

            padding = BitConverter.ToUInt16(dataBytes, (int)index);
            index += 2;
        }

        public UInt64 readOffsetFrom(UInt64 offset)
        {
            UInt64 off = index + (offset * addr_size);


            byte[] rawOffsetBytes = new byte[addr_size];
            Array.Copy(dataBytes, (int)off, rawOffsetBytes, 0, addr_size);

            if (is64bitDW)
                return BitConverter.ToUInt64(rawOffsetBytes, 0);
            else
                return BitConverter.ToUInt32(rawOffsetBytes, 0);
        }

        public override string ToString()
        {
            return $" unit_len {unit_length} ver {version}  padd: {padding} base :{index:x}";
        }
    }

}