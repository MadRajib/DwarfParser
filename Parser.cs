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
                var name = LEB128.ReadUnsigned(abbrevData, ref index);
                var form = LEB128.ReadUnsigned(abbrevData, ref index);
                if (name == 0 && form == 0)
                    break;
                abbreviation.AddAttribute(new Attribute(name, form));
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

            foreach (var item in abbrevListFiltered)
            {
                Console.WriteLine(item.ToString());   
            }

            var dieList = new List<DebuggingInformationEntry>();
            while (index < cuLength)
            {
                var die = ParseDIE(infoBytes, ref index, abbrevListFiltered, cuID);
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

            return new CompilationUnitHeader(id, unit_length, version, unit_type, debug_abbrev_offset, address_size);
        }

        public static DebuggingInformationEntry ParseDIE(byte[] infoBytes, ref int index, List<Abbreviation> abbrevList, int cuId)
        {
            var id = index;
            var code = LEB128.ReadUnsigned(infoBytes, ref index);
            DebuggingInformationEntry result;
            if (code == 0)
                return null;

            var abbrev = abbrevList.Find(a => a.Code == code);
            if (abbrev == null)
                return null;

            var die = new DebuggingInformationEntry(id, code, abbrev.Tag, abbrev.HasChildren);
            Console.Write($"{index:X}   {abbrev.Tag}\n");
            foreach (var abbrevAttr in abbrev.AttributeList)
            {
                try
                {
                    byte[] value = GetAttributeValue(infoBytes, ref index, abbrevAttr, cuId);
                    Attribute attr = new Attribute(abbrevAttr.Name, abbrevAttr.Form, value);
                    Console.WriteLine($"{attr.ToString()}");
                    die.AddAttribute(attr);
                }
                catch (NotImplementedException ex)
                {
                    Console.WriteLine($"{ex.ToString()}");
                }
            }

            return die;
        }

        // Read attribute value from .debug_info
        public static byte[] GetAttributeValue(byte[] infoBytes, ref int index, Attribute attribute, int cuId)
        {
            Console.WriteLine($" atttribute.Form {attribute.Form}");
            switch (attribute.Form)
            {
                case DW_FORM.Addr:
                    {
                        byte[] addrBytes = new byte[4];
                        Array.Copy(infoBytes, index, addrBytes, 0, 4);
                        index += 4;
                        return addrBytes;

                    }
                case DW_FORM.DwarfBlock2:
                    {
                        var numBytes = BitConverter.ToUInt16(infoBytes, index);
                        index += 2;

                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DwarfBlock4:
                    {
                        var numBytes = BitConverter.ToInt32(infoBytes, index);
                        index += 4;

                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.Data2:
                    {
                        byte[] lenBytes = new byte[2];
                        Array.Copy(infoBytes, index, lenBytes, 0, 2);
                        index += 2;
                        return lenBytes;
                    }
                case DW_FORM.Data4:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;

                    }
                case DW_FORM.Data8:
                    {
                        byte[] lenBytes = new byte[8];
                        Array.Copy(infoBytes, index, lenBytes, 0, 8);
                        index += 8;
                        return lenBytes;

                    }
                case DW_FORM.String:
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
                case DW_FORM.DwarfBlock:
                    {
                        var numBytes = (int)LEB128.ReadUnsigned(infoBytes, ref index);
                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.DwarfBlock1:
                    {
                        var numBytes = (int)infoBytes[index];
                        index++;
                        byte[] blockBytes = new byte[numBytes];
                        Array.Copy(infoBytes, index, blockBytes, 0, numBytes);
                        index += numBytes;
                        return blockBytes;
                    }
                case DW_FORM.Data1:
                    {
                        
                        byte[] lenBytes = new byte[1];
                        Array.Copy(infoBytes, index, lenBytes, 0, 1);
                        index += 1;
                        return lenBytes;
                    }
                case DW_FORM.Flag:
                    {
                        byte[] lenBytes = new byte[1];
                        Array.Copy(infoBytes, index, lenBytes, 0, 1);
                        index += 1;
                        return lenBytes;
                    }
                case DW_FORM.Sdata:
                    return BitConverter.GetBytes(LEB128.ReadSigned(infoBytes, ref index));
                case DW_FORM.Strp:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;
                    }
                case DW_FORM.Udata:
                    return BitConverter.GetBytes(LEB128.ReadUnsigned(infoBytes, ref index));
                case DW_FORM.RefAddr:
                    {
                        byte[] lenBytes = new byte[4];
                        Array.Copy(infoBytes, index, lenBytes, 0, 4);
                        index += 4;
                        return lenBytes;
                    }
                case DW_FORM.Ref1:
                    {
                        var reference = infoBytes[index];
                        index += 1;
                        ulong offset = (ulong)(cuId + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.Ref2:
                    {
                        var reference = BitConverter.ToUInt16(infoBytes, index);
                        index += 2;
                        ulong offset = (ulong)(cuId + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.Ref4:
                    {
                        var reference = BitConverter.ToUInt32(infoBytes, index);
                        index += 4;
                        Console.WriteLine($"offset {reference:x}");
                        ulong offset = (ulong)(cuId + reference);
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.Ref8:
                    {
                        var reference = BitConverter.ToUInt64(infoBytes, index);
                        index += 8;
                        ulong offset = (ulong)cuId + reference;
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.RefUdata:
                    {
                        var reference = LEB128.ReadUnsigned(infoBytes, ref index);
                        return BitConverter.GetBytes((ulong)cuId + reference);
                    }
                case DW_FORM.Indirect:
                    {
                        // var form = LEB128.ReadUnsigned(infoBytes, ref index);
                        // attribute.Form = (DW_FORM)form;
                        // GetAttributeValue(infoBytes, ref index, attribute, cuId);
                    }
                    throw new NotImplementedException("DW_FORM_indirect not yet implemented.");
                // DWARF 4
                case DW_FORM.SecOffset:
                    {
                        //TODO 64bit dwarf or 32 bit dwarf
                        var offset = BitConverter.ToUInt32(infoBytes, index);
                        index += 4;
                        return BitConverter.GetBytes(offset);
                    }
                case DW_FORM.Exprloc:
                    {
                        ulong exprLen = LEB128.ReadUnsigned(infoBytes, ref index);
                        byte[] exprBytes = new byte[exprLen];
                        Array.Copy(infoBytes, index, exprBytes, 0, (int)exprLen);
                        index += (int)exprLen;
                        return exprBytes;
                    }
                case DW_FORM.FlagPresent:
                case DW_FORM.RefSig8:
                    throw new NotImplementedException($"DWARF 4 form {attribute.Form:x} not yet implemented.");
                // DWARF 5
                case DW_FORM.Strx:
                case DW_FORM.Addrx:
                case DW_FORM.RefSup4:
                case DW_FORM.StrpSup:
                case DW_FORM.Data16:
                    throw new NotImplementedException($"DWARF 5 form {attribute.Form:x} not yet implemented.");
                case DW_FORM.LineStrp:
                    {
                        var offset = BitConverter.ToUInt32(infoBytes, index);
                        index += 4;
                        return [0, 0, 0, 0];
                    }
                case DW_FORM.ImplicitConst:
                case DW_FORM.Loclistx:
                case DW_FORM.Rnglistx:
                case DW_FORM.RefSup8:
                case DW_FORM.Strx1:
                case DW_FORM.Strx2:
                case DW_FORM.Strx3:
                case DW_FORM.Strx4:
                    throw new NotImplementedException($"DWARF 5 form {attribute.Form:x} not yet implemented.");
                case DW_FORM.Addrx1:
                case DW_FORM.Addrx2:
                case DW_FORM.Addrx3:
                case DW_FORM.Addrx4:
                    return null;
                // GNU extensions
                case DW_FORM.GnuRefAlt:
                case DW_FORM.GnuStrpAlt:
                    throw new NotImplementedException($"GNU DWARF form {attribute.Form:x} not yet implemented.");

                default:
                    throw new NotImplementedException($"Unknown DW_FORM {attribute.Form:x}");
            }
        }

        // Read string from .debug_str
        public static string StringPtr(List<byte> strData, int index)
        {
            var output = new List<byte>();
            byte character;
            while ((character = strData.ElementAt(index)) > 0)
            {
                output.Add(character);
                index++;
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }
    }


}