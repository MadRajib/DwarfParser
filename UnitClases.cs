using System.Text;

namespace DwarfParser
{

    class Attribute
    {
        public DW_AT Name { get; }
        public DW_FORM Form { get; }
        public byte[] Value { get; }

        public Attribute(ulong name, ulong form)
        {
            Name = (DW_AT)name;
            Form = (DW_FORM)form;
        }

        public Attribute(DW_AT name, DW_FORM form, byte[] value)
        {
            Name = name;
            Form = form;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Name} [{Form}] ({BitConverter.ToString(Value)})";
        }
    }

    class CompilationUnit
    {
        public CompilationUnitHeader Cuh { get; }
        public List<DebuggingInformationEntry> DieList { get; }

        public CompilationUnit(CompilationUnitHeader cuh, List<DebuggingInformationEntry> dieList)
        {
            Cuh = cuh;
            DieList = dieList;
        }

        public string GetName(List<byte> strData)
        {
            return DieList.First().GetName(strData);
        }

        public List<DebuggingInformationEntry> GetChildren()
        {
            List<DebuggingInformationEntry> output;

            // Determine if inflated our not
            if (DieList.Count == 1)
                output = DieList.First().Children;
            else
                output = DieList;

            return output;
        }
    }

    class CompilationUnitHeader
    {
        public int Id { get; }
        public ulong Length { get; } // Byte length, not including this field
        public ushort Version; // DWARF version
        public byte Unit_type;
        public ulong AbbrevOffset { get; } // Offset into .debug_abbrev
        byte PtrSize; // Size in bytes of an address


        public CompilationUnitHeader(int id, ulong length, ushort version, byte unit_type, ulong offset, byte size)
        {
            Id = id;
            Length = length;
            Version = version;
            Unit_type = unit_type;
            AbbrevOffset = offset;
            PtrSize = size;
        }
    }

    class DebuggingInformationEntry
    {
        public int Id { get; }
        public ulong Code { get; }
        public DW_TAG Tag { get; }
        public DW_CHILDREN HasChildren { get; }
        public List<Attribute> AttributeList { get; }
        public List<DebuggingInformationEntry> Children { get; }

        public DebuggingInformationEntry(int id, ulong code, DW_TAG tag, DW_CHILDREN hasChildren)
        {
            Id = id;
            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
            Children = new List<DebuggingInformationEntry>();
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }

        public void AddDieList(List<DebuggingInformationEntry> dieList)
        {
            Children.AddRange(dieList);
        }

        public String GetName(List<byte> strData)
        {
            string output = null;
            var attr = AttributeList.Find(a => a.Name == DW_AT.Name);

            if (attr != null)
            {
                switch (attr.Form)
                {
                    case DW_FORM.String:
                        output = Encoding.ASCII.GetString(attr.Value);
                        break;
                    case DW_FORM.Strp:
                        var strp = BitConverter.ToInt32(attr.Value, 0);
                        // output = StringPtr(strData, strp);
                        break;
                    default:
                        throw new NotImplementedException();
                        break;
                }
            }

            return output;
        }

        public int GetTypeId()
        {
            int output = 0;
            var typeId = AttributeList.Find(a => a.Name == DW_AT.Type);

            if (typeId != null)
                output = BitConverter.ToInt32(typeId.Value, 0);

            return output;
        }
    }

    class Abbreviation
    {
        public ulong Offset { get; }
        public ulong Code { get; }
        public UInt32 Const_val { get;} // Only used for implicit const
        public DW_TAG Tag { get; }
        public DW_CHILDREN HasChildren { get; }
        public List<Attribute> AttributeList { get; }

        public Abbreviation(int start, ulong code, DW_TAG tag, DW_CHILDREN hasChildren, UInt32 val = 0)
        {
            Offset = (ulong)start;
            Code = code;
            Tag = tag;
            Const_val = val;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }

        public override string ToString()
        {
            return $"Code: {Code}, Tag: {Tag}, HasChildren: {HasChildren}";
        }

    }
}