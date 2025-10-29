using dnlib.DotNet;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Detailed inspector for examining specific types
    /// </summary>
    public class TypeInspector
    {
        private ModuleDefMD _module;

        public TypeInspector(ModuleDefMD module)
        {
            _module = module;
        }

        /// <summary>
        /// Inspect a specific type by name pattern
        /// </summary>
        public void InspectTypeByPattern(string pattern)
        {
            Console.WriteLine($"\n=== Inspecting Types Matching: {pattern} ===");

            foreach (var type in _module.GetTypes())
            {
                if (type.FullName.Contains(pattern))
                {
                    InspectType(type);
                }
            }
        }

        /// <summary>
        /// Inspect all compiler-generated types
        /// </summary>
        public void InspectCompilerGeneratedTypes()
        {
            Console.WriteLine("\n=== Inspecting Compiler-Generated Types ===");

            foreach (var type in _module.GetTypes())
            {
                if (type.FullName.Contains("PrivateImplementationDetails"))
                {
                    Console.WriteLine($"\nType: {type.FullName}");
                    Console.WriteLine($"  Namespace: {type.Namespace}");
                    Console.WriteLine($"  IsNested: {type.IsNested}");
                    Console.WriteLine($"  Fields: {type.Fields.Count}");
                    Console.WriteLine($"  Methods: {type.Methods.Count}");
                    Console.WriteLine($"  NestedTypes: {type.NestedTypes.Count}");

                    // Show nested types
                    if (type.NestedTypes.Count > 0)
                    {
                        Console.WriteLine("\n  Nested Types:");
                        foreach (var nested in type.NestedTypes)
                        {
                            Console.WriteLine($"    - {nested.Name}");
                            InspectType(nested, indent: "      ");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get detailed information about a type
        /// </summary>
        public void InspectType(TypeDef type, string indent = "  ")
        {
            Console.WriteLine($"{indent}Type: {type.FullName}");
            Console.WriteLine($"{indent}  IsNested: {type.IsNested}");
            Console.WriteLine($"{indent}  IsPublic: {type.IsPublic}");
            Console.WriteLine($"{indent}  Fields: {type.Fields.Count}");
            Console.WriteLine($"{indent}  Methods: {type.Methods.Count}");

            if (type.Fields.Count > 0)
            {
                Console.WriteLine($"{indent}  Static Fields:");
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic)
                    {
                        Console.WriteLine($"{indent}    - {field.Name}: {field.FieldType.FullName}");

                        // Try to get size for byte arrays
                        if (field.FieldType.FullName == "System.Byte[]")
                        {
                            var cctor = type.FindStaticConstructor();
                            if (cctor?.HasBody == true)
                            {
                                foreach (var instr in cctor.Body.Instructions)
                                {
                                    if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                                    {
                                        var dataField = instr.Operand as FieldDef;
                                        if (dataField?.InitialValue != null)
                                        {
                                            Console.WriteLine($"{indent}      Size: {dataField.InitialValue.Length:N0} bytes");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (type.Methods.Count > 0)
            {
                Console.WriteLine($"{indent}  Methods:");
                foreach (var method in type.Methods.Take(10))
                {
                    Console.WriteLine($"{indent}    - {method.Name}: {method.ReturnType.FullName}");
                }
                if (type.Methods.Count > 10)
                {
                    Console.WriteLine($"{indent}    ... and {type.Methods.Count - 10} more");
                }
            }
        }

        /// <summary>
        /// Find the type containing the large byte array (decoder data)
        /// </summary>
        public TypeDef? FindDecoderTypeBySize(int minSize = 50000)
        {
            Console.WriteLine($"\n=== Searching for Type with Large Byte Array (>{minSize:N0} bytes) ===");

            foreach (var type in _module.GetTypes())
            {
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic && field.FieldType.FullName == "System.Byte[]")
                    {
                        var cctor = type.FindStaticConstructor();
                        if (cctor?.HasBody == true)
                        {
                            foreach (var instr in cctor.Body.Instructions)
                            {
                                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                                {
                                    var dataField = instr.Operand as FieldDef;
                                    if (dataField?.InitialValue != null && dataField.InitialValue.Length > minSize)
                                    {
                                        Console.WriteLine($"✓ Found type: {type.FullName}");
                                        Console.WriteLine($"  Field: {field.Name}");
                                        Console.WriteLine($"  Size: {dataField.InitialValue.Length:N0} bytes");
                                        return type;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("✗ No type found with large byte array");
            return null;
        }
    }
}
