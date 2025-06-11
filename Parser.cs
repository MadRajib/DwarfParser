using System.Text;

namespace DwarfParser
{
    class Parser
    {
        public static Abbreviation ParseAbbreviation(byte[] abbrevData, ref int index, int startIndex)
        {
            var code = LEB128.ReadUnsigned(abbrevData, ref index);

            if (code == 0)
                return null;

            var tag = LEB128.ReadUnsigned(abbrevData, ref index);
            var hasChildren = abbrevData[index];
            index++;
            var abbreviation = new Abbreviation(startIndex, code, (DW_TAG)tag, (DW_CHILDREN)hasChildren);

            while (index < abbrevData.Length)
            {
                UInt32 val = 0;
                var name = LEB128.ReadUnsigned(abbrevData, ref index);
                var form = LEB128.ReadUnsigned(abbrevData, ref index);

                if (form == (UInt32)DW_FORM.DW_FORM_implicit_const)
                    val = (UInt32)LEB128.ReadUnsigned(abbrevData, ref index);

                if (name == 0 && form == 0)
                    break;

                abbreviation.AddAttribute(new Attribute(name, form, val));
            }
            return abbreviation;
        }

        public static CompilationUnit ParseCU(byte[] infoBytes, ref int index, List<Abbreviation> abbrevList)
        {
            if (index >= infoBytes.Length)
                return null;

            var cuID = index;
            var cuh = ParseCUH(infoBytes, ref index, cuID, out int unit_length_bytes_count);
            var cuLength = cuID + unit_length_bytes_count + (int)cuh.Length;

            var abbrevListFiltered = abbrevList.Where(a => a.Offset == cuh.AbbrevOffset).ToList();

            var dieList = new List<DebuggingInformationEntry>();
            while (index < cuLength)
            {
                var die = ParseDIE(infoBytes, ref index, abbrevListFiltered, cuh);
                dieList.Add(die);
            }

            return new CompilationUnit(cuh, dieList);
        }

        static CompilationUnitHeader ParseCUH(byte[] infoBytes, ref int index, int id, out int unit_length_bytes_count)
        {
            UInt64 unit_length;
            bool is64bitDW = false;
            UInt16 version;
            Byte unit_type = 0;
            Byte address_size;
            UInt64 debug_abbrev_offset;

            unit_length = BitConverter.ToUInt32(infoBytes, index);
            index += 4;

            if (unit_length == 0xffffffff)
            {
                is64bitDW = true;
                unit_length = BitConverter.ToUInt64(infoBytes, index);
                index += 8;
                unit_length_bytes_count = 12;
            }
            else
            {
                is64bitDW = false;
                unit_length_bytes_count = 4;
            }

            version = BitConverter.ToUInt16(infoBytes, index);
            index += 2;

            if (version == 5)
            {
                // Unit type present in DW_V5
                unit_type = infoBytes[index++];
                address_size = infoBytes[index++];

                if (is64bitDW)
                {
                    debug_abbrev_offset = BitConverter.ToUInt64(infoBytes, index);
                    index += 8;
                }
                else
                {
                    debug_abbrev_offset = BitConverter.ToUInt32(infoBytes, index);
                    index += 4;
                }

            }
            else
            {
                if (is64bitDW)
                {
                    debug_abbrev_offset = BitConverter.ToUInt64(infoBytes, index);
                    index += 8;
                }
                else
                {
                    debug_abbrev_offset = BitConverter.ToUInt32(infoBytes, index);
                    index += 4;
                }

                address_size = infoBytes[index++];
            }

            return new CompilationUnitHeader(id, unit_length, version, unit_type, debug_abbrev_offset, address_size, is64bitDW);
        }

        public static DebuggingInformationEntry ParseDIE(byte[] infoBytes, ref int index, List<Abbreviation> abbrevList, CompilationUnitHeader CUH)
        {
            var id = index;
            var code = LEB128.ReadUnsigned(infoBytes, ref index);
            if (code == 0)
                return null;

            var abbrev = abbrevList.Find(a => a.Code == code);
            if (abbrev == null)
                return null;

            var die = new DebuggingInformationEntry(id, code, abbrev.Tag, abbrev.HasChildren);
            Console.Write($"Code:{die.Code:X} {index:X}    {abbrev.Tag} addrsize:{CUH.AddrSize}\n");

            foreach (var abbrevAttr in abbrev.AttributeList)
            {

                Attribute attr;

                if (abbrevAttr.Form == DW_FORM.DW_FORM_implicit_const)
                {
                    attr = new Attribute((ulong)abbrevAttr.Name, (ulong)abbrevAttr.Form, abbrevAttr.Const_val);
                    Console.WriteLine($"\t {attr.ToString()} ({attr.Const_val})");
                    die.AddAttribute(attr);
                }
                else
                {
                    byte[] value = GetAttributeValue(infoBytes, ref index, abbrevAttr, CUH);
                    attr = new Attribute(abbrevAttr.Name, abbrevAttr.Form, value);
                    // Console.WriteLine($"\t {attr.ToString()} ({attr.Value:x})");
                    var bytes = attr.Value as byte[];
                    Console.WriteLine($"\t {attr.ToString()} ({(bytes == null ? "null" : string.Join("", bytes.Select(b => b.ToString("X2"))))})");
                    die.AddAttribute(attr);
                }
            }

            return die;
        }

        // Read attribute value from .debug_info
        public static byte[] GetAttributeValue(byte[] infoBytes, ref int index, Attribute attribute, CompilationUnitHeader CUH)
        {
            if (CUH.AddrSize != 4 && CUH.AddrSize != 8)
            {
                Console.WriteLine("Invalid address size");
                Environment.Exit(0);
            }
            // Console.WriteLine($" atttribute.Form {attribute.Form}");
            switch (attribute.Form)
            {
                case DW_FORM.DW_FORM_addr:
                    {
                        byte[] addrBytes = new byte[CUH.AddrSize];
                        Array.Copy(infoBytes, index, addrBytes, 0, CUH.AddrSize);
                        index += CUH.AddrSize;
                        return addrBytes;
                    }
                case DW_FORM.DW_FORM_block2:
                    {
                        var numBytes = BitConverter.ToUInt16(infoBytes, index);
                        index += 2;

                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DW_FORM_block4:
                    {
                        var numBytes = BitConverter.ToInt32(infoBytes, index);
                        index += 4;

                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DW_FORM_data2:
                    {
                        byte[] lenBytes = new byte[2];
                        Array.Copy(infoBytes, index, lenBytes, 0, 2);
                        index += 2;
                        return lenBytes;
                    }
                case DW_FORM.DW_FORM_data4:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;

                    }
                case DW_FORM.DW_FORM_data8:
                    {
                        byte[] lenBytes = new byte[8];
                        Array.Copy(infoBytes, index, lenBytes, 0, 8);
                        index += 8;
                        return lenBytes;

                    }
                case DW_FORM.DW_FORM_string:
                    {
                        var str = new List<byte>();
                        while (index < infoBytes.Length)
                        {
                            var data = infoBytes[index];
                            index++;
                            if (data == 0)
                                break;
                            str.Add(data);
                        }
                        return str.ToArray();
                    }
                case DW_FORM.DW_FORM_block:
                    {
                        var numBytes = (int)LEB128.ReadUnsigned(infoBytes, ref index);
                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DW_FORM_block1:
                    {
                        var numBytes = (int)infoBytes[index];
                        index++;
                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DW_FORM_data1:
                    {

                        byte[] lenBytes = new byte[1];
                        Array.Copy(infoBytes, index, lenBytes, 0, 1);
                        index += 1;
                        return lenBytes;
                    }
                case DW_FORM.DW_FORM_flag:
                    {
                        byte[] lenBytes = new byte[1];
                        Array.Copy(infoBytes, index, lenBytes, 0, 1);
                        index += 1;
                        return lenBytes;
                    }
                case DW_FORM.DW_FORM_sdata:
                    return BitConverter.GetBytes(LEB128.ReadSigned(infoBytes, ref index));
                case DW_FORM.DW_FORM_strp:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;
                    }
                case DW_FORM.DW_FORM_udata:
                    return BitConverter.GetBytes(LEB128.ReadUnsigned(infoBytes, ref index));
                case DW_FORM.DW_FORM_ref_addr:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;
                    }
                case DW_FORM.DW_FORM_ref1:
                    {
                        var reference = infoBytes[index];
                        index += 1;
                        short offset = (short)(CUH.Id + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.DW_FORM_ref2:
                    {
                        var reference = BitConverter.ToUInt16(infoBytes, index);
                        index += 2;
                        UInt16 offset = (UInt16)(CUH.Id + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.DW_FORM_ref4:
                    {
                        var reference = BitConverter.ToUInt32(infoBytes, index);
                        index += 4;
                        UInt32 offset = (UInt32)(CUH.Id + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.DW_FORM_ref8:
                    {
                        var reference = BitConverter.ToUInt64(infoBytes, index);
                        index += 8;
                        UInt64 offset = (UInt64)CUH.Id + reference;
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.DW_FORM_ref_udata:
                    {
                        var reference = LEB128.ReadUnsigned(infoBytes, ref index);
                        return BitConverter.GetBytes((ulong)CUH.Id + reference);
                    }
                case DW_FORM.DW_FORM_indirect:
                    {
                        // var form = LEB128.ReadUnsigned(infoBytes, ref index);
                        // attribute.Form = (DW_FORM)form;
                        // GetAttributeValue(infoBytes, ref index, attribute, cuId);
                    }
                    throw new NotImplementedException("DW_FORM_indirect not yet implemented.");
                // DWARF 4
                case DW_FORM.DW_FORM_sec_offset:
                    {
                        var addr_size = CUH.Is64BitDwarf ? 8 : 4;
                        byte[] offBytes = new byte[addr_size];

                        Array.Copy(infoBytes, index, offBytes, 0, addr_size);

                        index += addr_size;
                        return offBytes;
                    }
                case DW_FORM.DW_FORM_exprloc:
                    {
                        ulong exprLen = LEB128.ReadUnsigned(infoBytes, ref index);
                        byte[] exprBytes = new byte[exprLen];
                        Array.Copy(infoBytes, index, exprBytes, 0, (int)exprLen);
                        index += (int)exprLen;
                        return exprBytes;
                    }
                case DW_FORM.DW_FORM_flag_present:
                    return [1];

                // DWARF 5
                case DW_FORM.DW_FORM_strx:
                    {
                        LEB128.ReadUnsigned(infoBytes, ref index);
                        return [0];
                    }
                case DW_FORM.DW_FORM_addrx:
                    {
                        LEB128.ReadUnsigned(infoBytes, ref index);
                        return [0];
                    }
                case DW_FORM.DW_FORM_ref_sup4:
                case DW_FORM.DW_FORM_strp_sup:
                case DW_FORM.DW_FORM_data16:
                    throw new NotImplementedException($"DWARF 5 form {attribute.Form:x} not yet implemented.");
                case DW_FORM.DW_FORM_line_strp:
                    {
                        var offset = BitConverter.ToUInt32(infoBytes, index);
                        index += 4;
                        return [0, 0, 0, 0];
                    }
                case DW_FORM.DW_FORM_ref_sig8:
                    throw new NotImplementedException($"DWARF 4 form {attribute.Form:x} not yet implemented.");
                case DW_FORM.DW_FORM_implicit_const:
                    return null;
                case DW_FORM.DW_FORM_loclistx:
                case DW_FORM.DW_FORM_rnglistx:
                case DW_FORM.DW_FORM_ref_sup8:
                case DW_FORM.DW_FORM_strx1:
                    {
                        UInt64 offx = infoBytes[index];
                        offx = DebugStrOff.readOffsetFrom(offx);
                        index++;
                        return BitConverter.GetBytes(offx);
                    }
                case DW_FORM.DW_FORM_strx2:
                    {
                        const byte sz = 2;
                        byte[] offBytes = new byte[sz];
                        index += sz;
                        Array.Copy(infoBytes, index, offBytes, 0, sz);

                        UInt64 offx = BitConverter.ToUInt16(offBytes);
                        offx = DebugStrOff.readOffsetFrom(offx);

                        return BitConverter.GetBytes(offx);
                    }
                case DW_FORM.DW_FORM_strx3:
                    {
                        const byte sz = 4;
                        byte[] offBytes = new byte[sz];
                        index += sz;
                        Array.Copy(infoBytes, index, offBytes, 0, sz);

                        UInt64 offx = BitConverter.ToUInt32(offBytes);
                        offx = DebugStrOff.readOffsetFrom(offx);
                        index++;
                        return BitConverter.GetBytes(offx);
                    }
                case DW_FORM.DW_FORM_strx4:
                    {
                        const byte sz = 8;
                        byte[] offBytes = new byte[sz];
                        index += sz;
                        Array.Copy(infoBytes, index, offBytes, 0, sz);

                        UInt64 offx = BitConverter.ToUInt64(offBytes);
                        offx = DebugStrOff.readOffsetFrom(offx);
                        index++;
                        return BitConverter.GetBytes(offx);
                    }
                case DW_FORM.DW_FORM_addrx1:
                case DW_FORM.DW_FORM_addrx2:
                case DW_FORM.DW_FORM_addrx3:
                case DW_FORM.DW_FORM_addrx4:
                    return null;
                // GNU extensions
                case DW_FORM.DW_FORM_GNU_addr_index:
                case DW_FORM.DW_FORM_GNU_str_index:
                case DW_FORM.DW_FORM_GNU_ref_alt:
                case DW_FORM.DW_FORM_GNU_strp_alt:
                    throw new NotImplementedException($"GNU DWARF form {attribute.Form:x} not yet implemented.");
                default:
                    throw new NotImplementedException($"Unknown DW_FORM {attribute.Form:x}");
            }
        }

        // Read string from .debug_str
        public static string StringPtr(byte[] strBytes, int index)
        {
            var output = new List<byte>();
            byte character;
            while ((character = strBytes[index]) > 0)
            {
                output.Add(character);
                index++;
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }
    }


}