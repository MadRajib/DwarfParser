using System.Reflection.PortableExecutable;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.VisualBasic;

namespace DwarfParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var elfPath = "";

            switch (args.Length)
            {
                case 1:
                    elfPath = args[0];
                    break;
                default:
                    Console.WriteLine("Invalid number of parameters!");
                    Console.WriteLine("Usage: DwarfParser.exe <My_elf_file.elf> [Symbol]");
                    Environment.Exit(1);
                    break;
            }

            var elfFile = ELFSharp.ELF.ELFReader.Load(elfPath);

            DebugStrOff debugStrOff = new DebugStrOff(elfFile);
            
            DebugStr debugStr = new DebugStr(elfFile);
            var off = DebugStrOff.readOffsetFrom(0x0a);
            Console.WriteLine($"{DebugStr.readStrFrom(off)}");

            var abbrevList = ExtractAbbrevList(elfFile);
            var cuList = ExtractCuList(elfFile, abbrevList);

            foreach (var cu in cuList)
            {
                DebuggingInformationEntry root_die = cu.DieList[0];

                if (root_die.HasChildren == DW_CHILDREN.No)
                    continue;
                
                foreach (var die in root_die.Children)
                {
                    if (die.Tag == DW_TAG.DW_TAG_variable)
                    {
                        var attr = die.AttributeList.Find(a => a.Name == DW_AT.DW_AT_name);
                        if (attr == null)
                            continue;
                        var offs = BitConverter.ToUInt64(attr.Value);
                        Console.WriteLine($"{attr.ToString()} name: {DebugStr.readStrFrom(offs)}");
                        
                    }
                }
            }

        }


        static void print_childrens(DebuggingInformationEntry root_die, int tab_count)
        {
            for (int i = 0; i < tab_count; i++)
                Console.Write("\t");
            Console.Write($"{root_die.printString(tab_count)}");

            foreach (var die in root_die.Children)
            {
                if (die.Tag == DW_TAG.DW_TAG_variable)
                    print_childrens(die, ++tab_count);
            }
            
        }

        static List<Abbreviation> ExtractAbbrevList(IELF elfFile)
        {
            var abbrevList = new List<Abbreviation>();
            var abbrevBytes = elfFile.Sections.Where(s => s.Name == ".debug_abbrev").First().GetContents();

            int index = 0;
            while (index < abbrevBytes.Length)
            {
                var startIndex = index;
                Abbreviation abbrev;
                while ((abbrev = Parser.ParseAbbreviation(abbrevBytes, ref index, startIndex)) != null)
                {
                    abbrevList.Add(abbrev);
                }
            }

            return abbrevList;

        }

        static List<CompilationUnit> ExtractCuList(IELF elfFile, List<Abbreviation> abbrevList)
        {
            var cuListFlat = new List<CompilationUnit>();
            var cuList = new List<CompilationUnit>();
            var index = 0;

            var infoBytes = elfFile.Sections.Where(s => s.Name == ".debug_info").First().GetContents();

            CompilationUnit cu;
            while ((cu = Parser.ParseCU(infoBytes, ref index, abbrevList)) != null)
                cuListFlat.Add(cu);

            foreach (var c in cuListFlat)
            {
                index = 0;
                var dieList = InflateDieListRecursive(c.DieList, ref index);
                var inflatedCu = new CompilationUnit(c.Cuh, dieList);
                cuList.Add(inflatedCu);
            }

            return cuList;
        }

        // Group children to parent DIEs
        static List<DebuggingInformationEntry> InflateDieListRecursive(List<DebuggingInformationEntry> dieList, ref int index)
        {
            var output = new List<DebuggingInformationEntry>();
            while (index < dieList.Count)
            {
                var die = dieList.ElementAt(index);
                index++;
                if (die == null)
                    break;
                if (die.HasChildren == DW_CHILDREN.Yes)
                {
                    var childDieList = InflateDieListRecursive(dieList, ref index);
                    die.AddDieList(childDieList);
                }
                output.Add(die);
            }
            return output;
        }

    }
}