using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

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
            var strData = elfFile.Sections.Where(s => s.Name == ".debug_str").First().GetContents();

            var abbrevList = ExtractAbbrevList(elfFile);
            var cuList = ExtractCuList(elfFile, abbrevList);

            foreach (var cu in cuList)
            {
                var index = 0;
                var root_die = cu.DieList.First();
                if (root_die == null) continue;
                if (root_die.HasChildren == 0) continue;

                foreach (var die in root_die.Children)
                {
                    if (die != null && die.Tag == DW_TAG.DW_TAG_variable)
                    {
                        Console.WriteLine($"Gobal var name: {die.GetName(strData)} {die.Tag} {die.Code:x} ");
                        var attr = die.AttributeList.Find(a => a.Name == DW_AT.DW_AT_location);
                        if (attr == null) continue;

                    }
                }

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