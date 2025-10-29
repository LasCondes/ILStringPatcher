using dnlib.DotNet;
using System.Text;

namespace ILStringPatcher.Core
{
    /// <summary>
    /// Detects and extracts the string decoder class from obfuscated assemblies
    /// </summary>
    public class StringDecoderDetector
    {
        private ModuleDefMD _module;
        private TypeDef? _decoderClass;
        private FieldDef? _dataBufferField;
        private FieldDef? _lookupTableField;
        private byte[]? _dataBuffer;
        private string? _lookupTable;

        public StringDecoderDetector(ModuleDefMD module)
        {
            _module = module;
        }

        /// <summary>
        /// Find the string decoder class in the assembly
        /// </summary>
        /// <returns>True if decoder class was found</returns>
        public bool DetectDecoderClass()
        {
            Console.WriteLine("\n=== Detecting String Decoder Class ===");

            // Strategy 1: Look for type with large byte array (>50KB)
            foreach (var type in _module.GetTypes())
            {
                // Skip common system types
                if (type.Namespace == "System" || type.Namespace == "Microsoft")
                    continue;

                FieldDef? byteArrayField = null;
                int byteArraySize = 0;

                // Check for large static byte array
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic && field.FieldType.FullName == "System.Byte[]")
                    {
                        // Try to get the size
                        var cctor = type.FindStaticConstructor();
                        if (cctor?.HasBody == true)
                        {
                            foreach (var instr in cctor.Body.Instructions)
                            {
                                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                                {
                                    var dataField = instr.Operand as FieldDef;
                                    if (dataField?.InitialValue != null && dataField.InitialValue.Length > 50000)
                                    {
                                        byteArrayField = field;
                                        byteArraySize = dataField.InitialValue.Length;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // If we found a large byte array, this is likely the decoder class
                if (byteArrayField != null)
                {
                    // Look for string array field (lookup table alternative format)
                    FieldDef? lookupField = null;
                    foreach (var field in type.Fields)
                    {
                        if (field.IsStatic && field.FieldType.FullName == "System.String[]")
                        {
                            lookupField = field;
                            break;
                        }
                    }

                    _decoderClass = type;
                    _dataBufferField = byteArrayField;
                    _lookupTableField = lookupField;  // May be null, we'll handle it

                    Console.WriteLine($"✓ Found decoder class: {type.FullName}");
                    Console.WriteLine($"  - Data buffer field: {byteArrayField.Name} ({byteArraySize:N0} bytes)");
                    if (lookupField != null)
                    {
                        Console.WriteLine($"  - Lookup table field: {lookupField.Name} (String[])");
                    }
                    else
                    {
                        Console.WriteLine($"  - Lookup table field: Not found (will analyze method IL)");
                    }
                    Console.WriteLine($"  - Methods: {type.Methods.Count}");
                    Console.WriteLine($"  - IsNested: {type.IsNested}");

                    return true;
                }
            }

            Console.WriteLine("✗ String decoder class not found");
            return false;
        }

        /// <summary>
        /// Extract the data buffer and lookup table from the decoder class
        /// </summary>
        /// <returns>True if extraction was successful</returns>
        public bool ExtractDecoderData()
        {
            if (_decoderClass == null || _dataBufferField == null)
            {
                Console.WriteLine("Error: Decoder class not detected");
                return false;
            }

            Console.WriteLine("\n=== Extracting Decoder Data ===");

            // Extract data buffer from field initializer
            _dataBuffer = ExtractByteArray(_dataBufferField);
            if (_dataBuffer == null)
            {
                Console.WriteLine("✗ Failed to extract data buffer");
                return false;
            }
            Console.WriteLine($"✓ Extracted data buffer: {_dataBuffer.Length:N0} bytes");

            // Try to extract lookup table (may be String or String[])
            if (_lookupTableField != null)
            {
                if (_lookupTableField.FieldType.FullName == "System.String")
                {
                    _lookupTable = ExtractString(_lookupTableField);
                    if (_lookupTable != null)
                    {
                        int lineCount = _lookupTable.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                        Console.WriteLine($"✓ Extracted lookup table (String): {lineCount:N0} entries");
                    }
                }
                else if (_lookupTableField.FieldType.FullName == "System.String[]")
                {
                    Console.WriteLine("✓ Lookup table is String[] - will analyze methods directly");
                }
            }
            else
            {
                Console.WriteLine("✓ No lookup table field - will analyze method IL directly");
            }

            return true;
        }

        /// <summary>
        /// Decrypt the data buffer using XOR decryption
        /// </summary>
        public void DecryptDataBuffer()
        {
            if (_dataBuffer == null)
            {
                Console.WriteLine("Error: No data buffer to decrypt");
                return;
            }

            Console.WriteLine("\n=== Decrypting Data Buffer ===");
            Console.Write("Decrypting... ");

            for (int i = 0; i < _dataBuffer.Length; i++)
            {
                byte key = (byte)(i % 256);
                _dataBuffer[i] = (byte)(_dataBuffer[i] ^ key ^ 0xAA);
            }

            Console.WriteLine("✓ Complete");
            Console.WriteLine($"Decrypted {_dataBuffer.Length:N0} bytes");
        }

        /// <summary>
        /// Parse the lookup table and create string mappings
        /// </summary>
        /// <returns>Dictionary mapping method names to decoded strings</returns>
        public Dictionary<string, string> ParseAndDecodeStrings()
        {
            var results = new Dictionary<string, string>();

            if (_dataBuffer == null || _decoderClass == null)
            {
                Console.WriteLine("Error: Missing data for string decoding");
                return results;
            }

            Console.WriteLine("\n=== Decoding Strings ===");

            // If we have a CSV lookup table, use it
            if (_lookupTable != null)
            {
                return DecodeStringsFromLookupTable();
            }

            // Otherwise, analyze method IL to extract offset/length parameters
            return DecodeStringsFromMethodIL();
        }

        /// <summary>
        /// Decode strings using CSV lookup table
        /// </summary>
        private Dictionary<string, string> DecodeStringsFromLookupTable()
        {
            var results = new Dictionary<string, string>();
            var lines = _lookupTable!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;
            int failCount = 0;

            foreach (var line in lines)
            {
                var parts = line.Split(',').Select(p => p.Trim()).ToArray();

                // Skip header or invalid lines
                if (parts.Length < 5 || parts[0] == "StringID")
                    continue;

                try
                {
                    string stringId = parts[0];       // e.g., "A", "B", "C"
                    int offset = int.Parse(parts[3]); // Byte offset in buffer
                    int length = int.Parse(parts[4]); // Number of bytes

                    // Validate bounds
                    if (offset + length > _dataBuffer!.Length)
                    {
                        failCount++;
                        continue;
                    }

                    // Extract and decode string
                    byte[] stringBytes = new byte[length];
                    Array.Copy(_dataBuffer, offset, stringBytes, 0, length);
                    string decodedString = Encoding.UTF8.GetString(stringBytes);

                    // Create method name pattern
                    string methodName = $"_String_{stringId}";
                    results[methodName] = decodedString;

                    successCount++;
                }
                catch
                {
                    failCount++;
                }
            }

            Console.WriteLine($"✓ Successfully decoded: {successCount:N0} strings");
            if (failCount > 0)
            {
                Console.WriteLine($"✗ Failed to decode: {failCount:N0} strings");
            }

            return results;
        }

        /// <summary>
        /// Decode strings by analyzing method IL code
        /// </summary>
        private Dictionary<string, string> DecodeStringsFromMethodIL()
        {
            var results = new Dictionary<string, string>();
            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;

            Console.WriteLine("Analyzing method IL to extract strings...");

            foreach (var method in _decoderClass!.Methods)
            {
                // Skip constructors and methods with parameters
                if (method.Name == ".cctor" || method.Name == ".ctor" || method.Parameters.Count > 0)
                {
                    skippedCount++;
                    continue;
                }

                // Skip if not returning string
                if (method.ReturnType.FullName != "System.String")
                {
                    skippedCount++;
                    continue;
                }

                // Skip if no body
                if (!method.HasBody)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    // Look for pattern: ldc.i4 (index), ldc.i4 (offset), ldc.i4 (length), call (decoder)
                    var instr = method.Body.Instructions;
                    int? index = null, offset = null, length = null;

                    // Find the call instruction first
                    for (int i = 0; i < instr.Count; i++)
                    {
                        var inst = instr[i];

                        // Look for call instruction to core decoder method
                        if (inst.OpCode.Code == dnlib.DotNet.Emit.Code.Call ||
                            inst.OpCode.Code == dnlib.DotNet.Emit.Code.Callvirt)
                        {
                            // The three instructions before call should be: index, offset, length
                            if (i >= 3)
                            {
                                index = ExtractInt32Constant(instr[i - 3]);
                                offset = ExtractInt32Constant(instr[i - 2]);
                                length = ExtractInt32Constant(instr[i - 1]);

                                // Found the pattern, stop searching
                                if (index.HasValue && offset.HasValue && length.HasValue)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    // If we found offset and length, decode the string
                    if (offset.HasValue && length.HasValue && offset.Value >= 0 && length.Value >= 0 &&
                        offset.Value + length.Value <= _dataBuffer!.Length)
                    {
                        byte[] stringBytes = new byte[length.Value];
                        Array.Copy(_dataBuffer, offset.Value, stringBytes, 0, length.Value);
                        string decodedString = Encoding.UTF8.GetString(stringBytes);

                        results[method.Name] = decodedString;
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                catch
                {
                    failCount++;
                }
            }

            Console.WriteLine($"✓ Successfully decoded: {successCount:N0} strings");
            if (failCount > 0)
            {
                Console.WriteLine($"⚠ Failed to decode: {failCount:N0} methods");
            }
            Console.WriteLine($"  Skipped {skippedCount:N0} methods (constructors, parameters, etc.)");

            return results;
        }

        /// <summary>
        /// Get the decoder class type definition
        /// </summary>
        public TypeDef? GetDecoderClass() => _decoderClass;

        /// <summary>
        /// Extract byte array from a static field's initializer
        /// </summary>
        private byte[]? ExtractByteArray(FieldDef field)
        {
            // Find the static constructor (.cctor) that initializes the field
            var cctor = field.DeclaringType.FindStaticConstructor();
            if (cctor == null || !cctor.HasBody)
                return null;

            // Parse IL to find the array initialization
            // For now, we'll look for the InitializeArray pattern
            var instructions = cctor.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];

                // Look for: stsfld (storing to our field)
                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Stsfld)
                {
                    var targetField = instr.Operand as IField;
                    if (targetField?.Name == field.Name)
                    {
                        // Look backwards for ldtoken (loads the data)
                        for (int j = i - 1; j >= 0; j--)
                        {
                            var prevInstr = instructions[j];
                            if (prevInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldtoken)
                            {
                                var dataField = prevInstr.Operand as FieldDef;
                                if (dataField?.InitialValue != null)
                                {
                                    return dataField.InitialValue;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extract string from a static field's initializer
        /// </summary>
        private string? ExtractString(FieldDef field)
        {
            // Find the static constructor (.cctor) that initializes the field
            var cctor = field.DeclaringType.FindStaticConstructor();
            if (cctor == null || !cctor.HasBody)
                return null;

            // Parse IL to find the string initialization
            var instructions = cctor.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];

                // Look for: stsfld (storing to our field)
                if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Stsfld)
                {
                    var targetField = instr.Operand as IField;
                    if (targetField?.Name == field.Name)
                    {
                        // Look backwards for ldstr (loads the string)
                        for (int j = i - 1; j >= 0; j--)
                        {
                            var prevInstr = instructions[j];
                            if (prevInstr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
                            {
                                return prevInstr.Operand as string;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Print sample decoded strings for verification
        /// </summary>
        public void PrintSampleStrings(Dictionary<string, string> strings, int maxSamples = 10)
        {
            Console.WriteLine($"\n=== Sample Decoded Strings (showing first {maxSamples}) ===");

            int count = 0;
            foreach (var kvp in strings.Take(maxSamples))
            {
                string display = kvp.Value.Length > 60
                    ? kvp.Value.Substring(0, 60) + "..."
                    : kvp.Value;

                display = display.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

                Console.WriteLine($"  {kvp.Key}() → \"{display}\"");
                count++;
            }

            if (strings.Count > maxSamples)
            {
                Console.WriteLine($"  ... and {strings.Count - maxSamples} more");
            }
        }

        /// <summary>
        /// Extract int32 constant from IL instruction
        /// </summary>
        private static int? ExtractInt32Constant(dnlib.DotNet.Emit.Instruction instruction)
        {
            var opCode = instruction.OpCode.Code;

            // ldc.i4.0 through ldc.i4.8
            if (opCode >= dnlib.DotNet.Emit.Code.Ldc_I4_0 && opCode <= dnlib.DotNet.Emit.Code.Ldc_I4_8)
            {
                return (int)(opCode - dnlib.DotNet.Emit.Code.Ldc_I4_0);
            }

            // ldc.i4.m1 (load -1)
            if (opCode == dnlib.DotNet.Emit.Code.Ldc_I4_M1)
            {
                return -1;
            }

            // ldc.i4.s (short form - single byte)
            if (opCode == dnlib.DotNet.Emit.Code.Ldc_I4_S)
            {
                return Convert.ToInt32(instruction.Operand);
            }

            // ldc.i4 (full int32)
            if (opCode == dnlib.DotNet.Emit.Code.Ldc_I4)
            {
                return Convert.ToInt32(instruction.Operand);
            }

            return null;
        }
    }
}
