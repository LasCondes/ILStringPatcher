using CommandLine;

namespace ILStringPatcher.Models
{
    /// <summary>
    /// Command-line options for the binary patcher
    /// </summary>
    public class CommandLineOptions
    {
        [Option('i', "input", Required = true, HelpText = "Input .NET assembly path (e.g., BeltStat.exe)")]
        public string InputPath { get; set; } = string.Empty;

        [Option('o', "output", Required = true, HelpText = "Output path for patched assembly")]
        public string OutputPath { get; set; } = string.Empty;

        [Option('v', "verbose", Required = false, Default = false, HelpText = "Enable verbose logging")]
        public bool Verbose { get; set; }

        [Option('d', "dry-run", Required = false, Default = false, HelpText = "Analyze without writing changes")]
        public bool DryRun { get; set; }

        [Option('b', "backup", Required = false, Default = true, HelpText = "Create backup of input file")]
        public bool CreateBackup { get; set; }

        [Option('s', "scan", Required = false, Default = false, HelpText = "Scan and analyze assembly structure")]
        public bool ScanMode { get; set; }
    }
}
