using dnlib.DotNet;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Utility for scanning and analyzing assembly contents
    /// </summary>
    public class AssemblyScanner
    {
        private ModuleDefMD _module;

        public AssemblyScanner(ModuleDefMD module)
        {
            _module = module;
        }

        /// <summary>
        /// Find all static byte array fields in the assembly
        /// </summary>
        public void ScanForByteArrayFields()
        {
            Console.WriteLine("\n=== Scanning for Static Byte Array Fields ===");

            int foundCount = 0;

            foreach (var type in _module.GetTypes())
            {
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic && field.FieldType.FullName == "System.Byte[]")
                    {
                        foundCount++;
                        Console.WriteLine($"  Type: {type.FullName}");
                        Console.WriteLine($"    Field: {field.Name}");
                        Console.WriteLine($"    Type: {field.FieldType.FullName}");

                        // Try to estimate size if possible
                        var cctor = type.FindStaticConstructor();
                        if (cctor?.HasBody == true)
                        {
                            var instructions = cctor.Body.Instructions;
                            foreach (var instr in instructions)
                            {
                                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                                {
                                    var dataField = instr.Operand as FieldDef;
                                    if (dataField?.InitialValue != null)
                                    {
                                        Console.WriteLine($"    Size: {dataField.InitialValue.Length:N0} bytes");
                                    }
                                }
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }

            if (foundCount == 0)
            {
                Console.WriteLine("  No static byte array fields found");
            }
            else
            {
                Console.WriteLine($"Total: {foundCount} static byte array fields found");
            }
        }

        /// <summary>
        /// Find all static string fields in the assembly
        /// </summary>
        public void ScanForStringFields()
        {
            Console.WriteLine("\n=== Scanning for Static String Fields ===");

            int foundCount = 0;

            foreach (var type in _module.GetTypes())
            {
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic && field.FieldType.FullName == "System.String")
                    {
                        foundCount++;
                        Console.WriteLine($"  Type: {type.FullName}");
                        Console.WriteLine($"    Field: {field.Name}");

                        // Try to extract the string value
                        var cctor = type.FindStaticConstructor();
                        if (cctor?.HasBody == true)
                        {
                            var instructions = cctor.Body.Instructions;
                            for (int i = 0; i < instructions.Count; i++)
                            {
                                var instr = instructions[i];
                                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Stsfld)
                                {
                                    var targetField = instr.Operand as IField;
                                    if (targetField?.Name == field.Name && i > 0)
                                    {
                                        var prevInstr = instructions[i - 1];
                                        if (prevInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
                                        {
                                            string value = prevInstr.Operand as string ?? "";
                                            string preview = value.Length > 100
                                                ? value.Substring(0, 100) + "..."
                                                : value;
                                            int lineCount = value.Split('\n').Length;
                                            Console.WriteLine($"    Length: {value.Length} chars, {lineCount} lines");
                                            Console.WriteLine($"    Preview: {preview.Replace("\r", "\\r").Replace("\n", "\\n")}");
                                        }
                                    }
                                }
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }

            if (foundCount == 0)
            {
                Console.WriteLine("  No static string fields found");
            }
            else
            {
                Console.WriteLine($"Total: {foundCount} static string fields found");
            }
        }

        /// <summary>
        /// Find types with suspicious obfuscation patterns
        /// </summary>
        public void ScanForObfuscatedTypes()
        {
            Console.WriteLine("\n=== Scanning for Obfuscated Types ===");

            var suspiciousTypes = new List<TypeDef>();

            foreach (var type in _module.GetTypes())
            {
                // Look for types with GUIDs in names (common obfuscation pattern)
                if (type.Name.Contains("_002D") || type.Name.Length > 50)
                {
                    suspiciousTypes.Add(type);
                }
            }

            if (suspiciousTypes.Count == 0)
            {
                Console.WriteLine("  No obviously obfuscated types found");
            }
            else
            {
                Console.WriteLine($"Found {suspiciousTypes.Count} types with obfuscated names:");
                foreach (var type in suspiciousTypes.Take(10))
                {
                    Console.WriteLine($"  - {type.FullName}");
                    Console.WriteLine($"    Fields: {type.Fields.Count}, Methods: {type.Methods.Count}");
                }

                if (suspiciousTypes.Count > 10)
                {
                    Console.WriteLine($"  ... and {suspiciousTypes.Count - 10} more");
                }
            }
        }

        /// <summary>
        /// Look for string decoder methods
        /// </summary>
        public void ScanForDecoderMethods()
        {
            Console.WriteLine("\n=== Scanning for String Decoder Methods ===");

            int foundCount = 0;

            foreach (var type in _module.GetTypes())
            {
                foreach (var method in type.Methods)
                {
                    // Look for methods that start with _String_ or similar patterns
                    if (method.Name.StartsWith("_String_") ||
                        method.Name.Contains("String") && method.IsStatic &&
                        method.ReturnType.FullName == "System.String" &&
                        method.Parameters.Count == 0)
                    {
                        foundCount++;
                        if (foundCount <= 10)
                        {
                            Console.WriteLine($"  Type: {type.Name}");
                            Console.WriteLine($"    Method: {method.FullName}");
                        }
                    }
                }
            }

            if (foundCount == 0)
            {
                Console.WriteLine("  No string decoder methods found");
            }
            else
            {
                Console.WriteLine($"\nTotal: {foundCount} potential decoder methods found");
            }
        }
    }
}
