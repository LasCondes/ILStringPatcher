using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Text;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Patches IL code to replace decoder method calls with string literals
    /// </summary>
    public class ILPatcher
    {
        private ModuleDefMD _module;
        private TypeDef _decoderClass;
        private Dictionary<string, string> _decodedStrings;
        private int _methodsPatched = 0;
        private int _callsReplaced = 0;

        public ILPatcher(ModuleDefMD module, TypeDef decoderClass, Dictionary<string, string> decodedStrings)
        {
            _module = module;
            _decoderClass = decoderClass;
            _decodedStrings = decodedStrings;
        }

        /// <summary>
        /// Patch all methods in the assembly
        /// </summary>
        public void PatchAllMethods()
        {
            Console.WriteLine("\n=== Patching IL Code ===");
            Console.WriteLine("Scanning all methods for decoder calls...");

            _methodsPatched = 0;
            _callsReplaced = 0;

            foreach (var type in _module.GetTypes())
            {
                // Skip the decoder class itself
                if (type == _decoderClass)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (method.HasBody)
                    {
                        bool modified = PatchMethod(method);
                        if (modified)
                        {
                            _methodsPatched++;
                        }
                    }
                }
            }

            Console.WriteLine($"✓ Patching complete");
            Console.WriteLine($"  Methods patched: {_methodsPatched:N0}");
            Console.WriteLine($"  Calls replaced: {_callsReplaced:N0}");
        }

        /// <summary>
        /// Patch a single method's IL code
        /// </summary>
        private bool PatchMethod(MethodDef method)
        {
            bool modified = false;
            var instructions = method.Body.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];

                // Look for call instructions
                if (instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt)
                {
                    var calledMethod = instr.Operand as IMethod;
                    if (calledMethod == null)
                        continue;

                    // Check if this is a call to our decoder class
                    if (calledMethod.DeclaringType?.FullName == _decoderClass.FullName)
                    {
                        string methodName = calledMethod.Name;

                        // Check if we have a decoded string for this method
                        if (_decodedStrings.TryGetValue(methodName, out string? decodedString))
                        {
                            // Replace call with ldstr instruction
                            instr.OpCode = OpCodes.Ldstr;
                            instr.Operand = decodedString;

                            modified = true;
                            _callsReplaced++;
                        }
                    }
                }
            }

            return modified;
        }

        /// <summary>
        /// Get patching statistics
        /// </summary>
        public (int methodsPatched, int callsReplaced) GetStatistics()
        {
            return (_methodsPatched, _callsReplaced);
        }

        /// <summary>
        /// Verify the patching by checking for remaining decoder calls
        /// </summary>
        public int CountRemainingDecoderCalls()
        {
            int remainingCalls = 0;

            foreach (var type in _module.GetTypes())
            {
                if (type == _decoderClass)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (method.HasBody)
                    {
                        foreach (var instr in method.Body.Instructions)
                        {
                            if (instr.OpCode.Code == Code.Call || instr.OpCode.Code == Code.Callvirt)
                            {
                                var calledMethod = instr.Operand as IMethod;
                                if (calledMethod?.DeclaringType?.FullName == _decoderClass.FullName)
                                {
                                    remainingCalls++;
                                }
                            }
                        }
                    }
                }
            }

            return remainingCalls;
        }

        /// <summary>
        /// Print sample of patched methods
        /// </summary>
        public void PrintPatchedMethodsSample(int maxSamples = 10)
        {
            Console.WriteLine($"\n=== Sample Patched Methods (first {maxSamples}) ===");

            int count = 0;
            foreach (var type in _module.GetTypes())
            {
                if (type == _decoderClass)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (method.HasBody)
                    {
                        bool hasDecoderCalls = false;
                        var replacements = new List<string>();

                        foreach (var instr in method.Body.Instructions)
                        {
                            if (instr.OpCode.Code == Code.Ldstr)
                            {
                                string value = instr.Operand as string ?? "";
                                if (_decodedStrings.ContainsValue(value))
                                {
                                    hasDecoderCalls = true;
                                    string preview = value.Length > 40 ? value.Substring(0, 40) + "..." : value;
                                    preview = preview.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
                                    replacements.Add(preview);
                                }
                            }
                        }

                        if (hasDecoderCalls)
                        {
                            Console.WriteLine($"  {type.Name}.{method.Name}");
                            foreach (var replacement in replacements.Take(3))
                            {
                                Console.WriteLine($"    → \"{replacement}\"");
                            }
                            if (replacements.Count > 3)
                            {
                                Console.WriteLine($"    ... and {replacements.Count - 3} more");
                            }

                            count++;
                            if (count >= maxSamples)
                                return;
                        }
                    }
                }
            }

            if (count == 0)
            {
                Console.WriteLine("  (No patched methods found in sample)");
            }
        }
    }
}
