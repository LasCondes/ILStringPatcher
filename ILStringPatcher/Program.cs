using CommandLine;
using ILStringPatcher.Models;
using ILStringPatcher.Core;

namespace ILStringPatcher
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("ILStringPatcher v1.0");
            Console.WriteLine("====================\n");

            return Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(
                    options => RunPatcher(options),
                    errors => 1
                );
        }

        static int RunPatcher(CommandLineOptions options)
        {
            try
            {
                // Validate input file
                if (!File.Exists(options.InputPath))
                {
                    Console.WriteLine($"Error: Input file not found: {options.InputPath}");
                    return 1;
                }

                // Create backup if requested
                if (options.CreateBackup && !options.DryRun)
                {
                    string backupPath = options.InputPath + ".backup";
                    Console.WriteLine($"Creating backup: {backupPath}");
                    File.Copy(options.InputPath, backupPath, overwrite: true);
                    Console.WriteLine("✓ Backup created\n");
                }

                // Load the assembly
                var loader = new PELoader();
                if (!loader.LoadAssembly(options.InputPath))
                {
                    return 1;
                }

                Console.WriteLine();
                loader.PrintSummary();

                var module = loader.GetModule();
                if (module == null)
                {
                    Console.WriteLine("Error: Failed to get module");
                    return 1;
                }

                // If scan mode, run diagnostics and exit
                if (options.ScanMode)
                {
                    var scanner = new AssemblyScanner(module);
                    scanner.ScanForObfuscatedTypes();
                    scanner.ScanForByteArrayFields();
                    scanner.ScanForStringFields();
                    scanner.ScanForDecoderMethods();

                    var inspector = new TypeInspector(module);
                    inspector.InspectCompilerGeneratedTypes();
                    var decoderType = inspector.FindDecoderTypeBySize(50000);

                    if (decoderType != null)
                    {
                        DetailedTypeInspector.InspectType(decoderType);
                        DetailedTypeInspector.AnalyzeMethodPattern(decoderType);
                        MethodILInspector.AnalyzeDecoderMethods(decoderType, count: 5);
                    }

                    Console.WriteLine("\n✓ Scan complete");
                    return 0;
                }

                // Detect and extract string decoder
                var detector = new StringDecoderDetector(module);

                if (!detector.DetectDecoderClass())
                {
                    Console.WriteLine("\nWarning: No string decoder class found in assembly");
                    Console.WriteLine("         This assembly may not be obfuscated, or uses a different obfuscation scheme");
                }
                else
                {
                    if (!detector.ExtractDecoderData())
                    {
                        Console.WriteLine("\nError: Failed to extract decoder data");
                        return 1;
                    }

                    detector.DecryptDataBuffer();

                    var decodedStrings = detector.ParseAndDecodeStrings();

                    if (decodedStrings.Count > 0)
                    {
                        detector.PrintSampleStrings(decodedStrings);
                    }

                    Console.WriteLine($"\n✓ Total strings decoded: {decodedStrings.Count:N0}");

                    // Patch IL code to replace decoder calls with string literals
                    var decoderClassDef = detector.GetDecoderClass();
                    if (decoderClassDef != null)
                    {
                        var patcher = new ILPatcher(module, decoderClassDef, decodedStrings);
                        patcher.PatchAllMethods();

                        // Verify patching
                        int remainingCalls = patcher.CountRemainingDecoderCalls();
                        if (remainingCalls > 0)
                        {
                            Console.WriteLine($"\n⚠ Warning: {remainingCalls:N0} decoder calls still remain");
                        }
                        else
                        {
                            Console.WriteLine($"\n✓ All decoder calls successfully replaced!");
                        }

                        // Show sample of patched methods
                        patcher.PrintPatchedMethodsSample(10);
                    }
                }

                // Write output (if not dry-run)
                if (!options.DryRun)
                {
                    // For now, just write the unmodified assembly to verify the pipeline works
                    Console.WriteLine($"\nWriting to: {options.OutputPath}");
                    if (!loader.WriteAssembly(options.OutputPath))
                    {
                        return 1;
                    }
                    Console.WriteLine($"✓ Successfully wrote assembly to {options.OutputPath}");
                }
                else
                {
                    Console.WriteLine("\n[DRY RUN] - No output written");
                }

                Console.WriteLine("\n✓ Operation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFatal error: {ex.Message}");
                if (options.Verbose)
                {
                    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                }
                return 1;
            }
        }
    }
}
