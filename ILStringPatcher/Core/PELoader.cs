using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Handles loading and writing .NET assemblies using dnlib
    /// </summary>
    public class PELoader
    {
        private ModuleDefMD? _module;
        private string? _loadedPath;

        /// <summary>
        /// Load a .NET assembly from disk
        /// </summary>
        /// <param name="path">Path to the .exe or .dll file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadAssembly(string path)
        {
            try
            {
                Console.WriteLine($"Loading assembly: {path}");

                if (!File.Exists(path))
                {
                    Console.WriteLine($"Error: File not found: {path}");
                    return false;
                }

                // Load the module using dnlib
                _module = ModuleDefMD.Load(path);
                _loadedPath = path;

                Console.WriteLine($"✓ Loaded: {_module.Assembly.FullName}");
                Console.WriteLine($"  - Target Framework: {_module.RuntimeVersion}");
                Console.WriteLine($"  - Entry Point: {_module.EntryPoint?.FullName ?? "None"}");
                Console.WriteLine($"  - Types: {_module.Types.Count}");
                Console.WriteLine($"  - Methods: {CountMethods()}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading assembly: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Write the modified assembly to disk
        /// </summary>
        /// <param name="outputPath">Path where to save the modified assembly</param>
        /// <returns>True if written successfully</returns>
        public bool WriteAssembly(string outputPath)
        {
            if (_module == null)
            {
                Console.WriteLine("Error: No module loaded");
                return false;
            }

            try
            {
                Console.WriteLine($"Writing assembly to: {outputPath}");

                // Configure writer options
                var options = new ModuleWriterOptions(_module)
                {
                    // Preserve original metadata tokens where possible
                    MetadataOptions = { Flags = MetadataFlags.PreserveAll }
                };

                // Write the module
                _module.Write(outputPath, options);

                Console.WriteLine($"✓ Assembly written successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing assembly: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the loaded module for manipulation
        /// </summary>
        public ModuleDefMD? GetModule() => _module;

        /// <summary>
        /// Count total methods in the assembly
        /// </summary>
        private int CountMethods()
        {
            if (_module == null) return 0;

            int count = 0;
            foreach (var type in _module.Types)
            {
                count += type.Methods.Count;
            }
            return count;
        }

        /// <summary>
        /// Get summary information about the loaded assembly
        /// </summary>
        public void PrintSummary()
        {
            if (_module == null)
            {
                Console.WriteLine("No assembly loaded");
                return;
            }

            Console.WriteLine("\n=== Assembly Summary ===");
            Console.WriteLine($"Name: {_module.Assembly.FullName}");
            Console.WriteLine($"Path: {_loadedPath}");
            Console.WriteLine($"Runtime: {_module.RuntimeVersion}");
            Console.WriteLine($"Architecture: {_module.Machine}");
            Console.WriteLine($"Types: {_module.Types.Count}");
            Console.WriteLine($"Methods: {CountMethods()}");
            Console.WriteLine($"Resources: {_module.Resources.Count}");
            Console.WriteLine($"Entry Point: {_module.EntryPoint?.FullName ?? "None"}");
            Console.WriteLine("=======================\n");
        }
    }
}
