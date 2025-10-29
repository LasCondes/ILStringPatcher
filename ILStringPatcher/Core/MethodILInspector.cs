using dnlib.DotNet;
using System.Text;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Inspector for analyzing IL code of specific methods
    /// </summary>
    public class MethodILInspector
    {
        /// <summary>
        /// Print the IL code of a specific method
        /// </summary>
        public static void PrintMethodIL(MethodDef method)
        {
            Console.WriteLine($"\n=== IL Code for Method: {method.FullName} ===");
            Console.WriteLine($"ReturnType: {method.ReturnType.FullName}");
            Console.WriteLine($"Parameters: {method.Parameters.Count}");
            Console.WriteLine($"HasBody: {method.HasBody}");

            if (!method.HasBody)
            {
                Console.WriteLine("(No method body)");
                return;
            }

            Console.WriteLine("\nInstructions:");
            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                string operandStr = "";

                if (instr.Operand != null)
                {
                    if (instr.Operand is int intOp)
                    {
                        operandStr = $" {intOp}";
                    }
                    else if (instr.Operand is IMethod methodOp)
                    {
                        operandStr = $" {methodOp.DeclaringType?.Name}::{methodOp.Name}";
                    }
                    else if (instr.Operand is IField fieldOp)
                    {
                        operandStr = $" {fieldOp.DeclaringType?.Name}::{fieldOp.Name}";
                    }
                    else
                    {
                        operandStr = $" {instr.Operand}";
                    }
                }

                Console.WriteLine($"  IL_{i:D4}: {instr.OpCode.Name}{operandStr}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Analyze several decoder methods to find patterns
        /// </summary>
        public static void AnalyzeDecoderMethods(TypeDef decoderClass, int count = 5)
        {
            Console.WriteLine($"\n=== Analyzing First {count} Decoder Methods ===");

            int analyzed = 0;
            foreach (var method in decoderClass.Methods)
            {
                // Skip constructors and methods with parameters
                if (method.Name == ".cctor" || method.Name == ".ctor" || method.Parameters.Count > 0)
                    continue;

                // Skip if not returning string
                if (method.ReturnType.FullName != "System.String")
                    continue;

                PrintMethodIL(method);

                analyzed++;
                if (analyzed >= count)
                    break;
            }
        }
    }
}
