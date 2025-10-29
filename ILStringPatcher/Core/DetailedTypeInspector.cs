using dnlib.DotNet;
using System.Text;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Detailed inspector for a specific type
    /// </summary>
    public class DetailedTypeInspector
    {
        /// <summary>
        /// Inspect all fields and methods of a type
        /// </summary>
        public static void InspectType(TypeDef type)
        {
            Console.WriteLine($"\n=== Detailed Type Inspection ===");
            Console.WriteLine($"Type: {type.FullName}");
            Console.WriteLine($"IsNested: {type.IsNested}");
            Console.WriteLine($"Fields: {type.Fields.Count}");
            Console.WriteLine($"Methods: {type.Methods.Count}");

            Console.WriteLine("\n--- All Fields ---");
            foreach (var field in type.Fields)
            {
                Console.WriteLine($"  {field.Name}:");
                Console.WriteLine($"    Type: {field.FieldType.FullName}");
                Console.WriteLine($"    IsStatic: {field.IsStatic}");
                Console.WriteLine($"    Access: {field.Access}");

                // Try to extract value
                if (field.IsStatic)
                {
                    var cctor = type.FindStaticConstructor();
                    if (cctor?.HasBody == true)
                    {
                        // Look for initialization
                        for (int i = 0; i < cctor.Body.Instructions.Count; i++)
                        {
                            var instr = cctor.Body.Instructions[i];

                            // Check if this is storing to our field
                            if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Stsfld)
                            {
                                var targetField = instr.Operand as IField;
                                if (targetField?.Name == field.Name)
                                {
                                    // Look backwards for the value
                                    if (i > 0)
                                    {
                                        var prevInstr = cctor.Body.Instructions[i - 1];

                                        // Check for string literal
                                        if (prevInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
                                        {
                                            string value = prevInstr.Operand as string ?? "";
                                            int lineCount = value.Split('\n').Length;
                                            Console.WriteLine($"    Value: String with {value.Length} chars, {lineCount} lines");

                                            if (lineCount > 10)
                                            {
                                                // Show first few lines
                                                var lines = value.Split('\n').Take(5);
                                                Console.WriteLine($"    Preview:");
                                                foreach (var line in lines)
                                                {
                                                    Console.WriteLine($"      {line.Substring(0, Math.Min(80, line.Length))}");
                                                }
                                                Console.WriteLine($"      ... ({lineCount - 5} more lines)");
                                            }
                                        }

                                        // Check for byte array via ldtoken
                                        if (prevInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Newarr)
                                        {
                                            // Look further back for ldtoken
                                            for (int j = i - 1; j >= 0 && j >= i - 10; j--)
                                            {
                                                var checkInstr = cctor.Body.Instructions[j];
                                                if (checkInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                                                {
                                                    var dataField = checkInstr.Operand as FieldDef;
                                                    if (dataField?.InitialValue != null)
                                                    {
                                                        Console.WriteLine($"    Value: Byte array with {dataField.InitialValue.Length:N0} bytes");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("\n--- Sample Methods (first 20) ---");
            foreach (var method in type.Methods.Take(20))
            {
                Console.WriteLine($"  {method.Name}");
                Console.WriteLine($"    Signature: {method.FullName}");
                Console.WriteLine($"    ReturnType: {method.ReturnType.FullName}");
                Console.WriteLine($"    Parameters: {method.Parameters.Count}");
                Console.WriteLine($"    IsStatic: {method.IsStatic}");
            }

            if (type.Methods.Count > 20)
            {
                Console.WriteLine($"  ... and {type.Methods.Count - 20} more methods");
            }
        }

        /// <summary>
        /// Analyze the pattern of method names
        /// </summary>
        public static void AnalyzeMethodPattern(TypeDef type)
        {
            Console.WriteLine($"\n=== Method Name Pattern Analysis ===");

            var methodGroups = type.Methods
                .GroupBy(m => m.Name.Length)
                .OrderBy(g => g.Key);

            foreach (var group in methodGroups.Take(10))
            {
                Console.WriteLine($"  Methods with name length {group.Key}: {group.Count()}");
                foreach (var method in group.Take(5))
                {
                    Console.WriteLine($"    - {method.Name}");
                }
                if (group.Count() > 5)
                {
                    Console.WriteLine($"    ... and {group.Count() - 5} more");
                }
            }

            // Look for _String_ pattern
            var stringMethods = type.Methods.Where(m => m.Name.Contains("String")).ToList();
            if (stringMethods.Count > 0)
            {
                Console.WriteLine($"\n  Methods containing 'String': {stringMethods.Count}");
                foreach (var method in stringMethods.Take(10))
                {
                    Console.WriteLine($"    - {method.Name}");
                }
            }
        }
    }
}
