# ILStringPatcher

A .NET assembly binary patcher that removes string obfuscation by directly modifying IL bytecode.

## What It Does

Converts obfuscated code like this:
```csharp
string msg = DecoderClass.A();
MessageBox.Show(DecoderClass.B());
```

Into readable code like this:
```csharp
string msg = "{{ Region = {0} }}";
MessageBox.Show("Not a valid direction.");
```

**All at the IL bytecode level** - no source code required!

## Features

- 🔓 **String Deobfuscation** - Decrypts and decodes obfuscated strings
- 🔧 **IL Patching** - Replaces method calls with string literals directly in bytecode
- 🚀 **High Performance** - Processes 3,700+ strings in ~2 seconds
- ✅ **Complete Coverage** - Replaces 9,150+ decoder calls across the assembly
- 📊 **Detailed Reporting** - Shows exactly what was patched
- 🔍 **Verification** - Confirms all decoder calls were replaced

## Quick Start

### Build & Run

```bash
cd ILStringPatcher
dotnet build
dotnet run -- -i input.exe -o output.exe
```

### Basic Usage

```bash
# Patch an assembly
dotnet run -- -i BeltStat.exe -o BeltStat_patched.exe

# Dry run (analyze without writing)
dotnet run -- -i BeltStat.exe -o output.exe --dry-run

# Scan mode (detailed analysis)
dotnet run -- -i BeltStat.exe -o output.exe --scan

# Verbose output
dotnet run -- -i BeltStat.exe -o output.exe --verbose
```

## Command Line Options

```
-i, --input     Required. Input .NET assembly path
-o, --output    Required. Output path for patched assembly
-v, --verbose   Enable verbose logging
-d, --dry-run   Analyze without writing changes
-b, --backup    Create backup of input file (default: true)
-s, --scan      Scan and analyze assembly structure
```

## How It Works

### 1. Detection Phase

Scans the assembly to find the string decoder class:
```
✓ Found decoder class
  - Data buffer: 71,589 bytes
  - Decoder methods: 3,768
```

### 2. Extraction Phase

Extracts encrypted string data from IL metadata:
```
✓ Extracted data buffer: 71,589 bytes
```

### 3. Decryption Phase

Decrypts the buffer using XOR cipher:
```
Algorithm: XOR with rotating key
Key = (index % 256) ^ 0xAA
✓ Decrypted 71,589 bytes
```

### 4. Decoding Phase

Analyzes decoder method IL code to extract strings:
```
✓ Successfully decoded: 3,766 strings
```

### 5. Patching Phase

Replaces all `call` instructions with `ldstr` literals:
```
✓ Methods patched: 576
✓ Calls replaced: 9,150
```

## Architecture

```
ILStringPatcher/
├── Core/
│   ├── PELoader.cs                  # Load/write assemblies
│   ├── StringDecoderDetector.cs     # Find & decode strings
│   ├── ILPatcher.cs                 # Patch IL bytecode
│   ├── AssemblyScanner.cs           # Diagnostic scanner
│   ├── TypeInspector.cs             # Type analysis
│   ├── DetailedTypeInspector.cs     # Deep inspection
│   └── MethodILInspector.cs         # IL code analysis
├── Models/
│   └── CommandLineOptions.cs        # CLI arguments
└── Program.cs                        # Main entry point
```

## Requirements

- .NET 9.0 SDK
- macOS, Windows, or Linux
- Target assemblies: .NET Framework 4.0+

## Dependencies

- **dnlib** 4.5.0 - .NET assembly manipulation
- **CommandLineParser** 2.9.1 - CLI argument parsing

## Example Output

```
ILStringPatcher v1.0
=============================

Loading assembly: BeltStat.exe
✓ Loaded: BeltStat, Version=11.3.0.1
  - Types: 391
  - Methods: 10,679

=== Detecting String Decoder Class ===
✓ Found decoder class
  - Data buffer field: 4 (71,589 bytes)
  - Methods: 3,768

=== Decrypting Data Buffer ===
✓ Complete: 71,589 bytes

=== Decoding Strings ===
✓ Successfully decoded: 3,766 strings

=== Sample Decoded Strings ===
  A() → "{{ Region = {0} }}"
  B() → "Not a valid direction."
  C() → "Characters of parameter hex should be in [0-9,A-F,a-f]"

=== Patching IL Code ===
✓ Methods patched: 576
✓ Calls replaced: 9,150
✓ All decoder calls successfully replaced!

Writing assembly to: BeltStat_patched.exe
✓ Assembly written successfully

✓ Operation completed successfully
```

## Technical Details

### IL Transformation

**Before:**
```il
IL_0000: call String DecoderClass::A()
```

**After:**
```il
IL_0000: ldstr "{{ Region = {0} }}"
```

### Supported Obfuscation Pattern

This patcher targets a specific obfuscation pattern:
- Strings encrypted with XOR cipher (rotating key)
- Stored in single large byte array (50KB+)
- Accessed via generated decoder methods
- Each method loads offset/length and calls core decoder

### Performance

Measured on M1 MacBook Pro:
- **Load**: <0.1s (20 MB/s)
- **Decrypt**: <0.1s (700 KB/s)
- **Decode**: 0.5s (7,500 strings/s)
- **Patch**: 1.0s (10,000 methods/s)
- **Write**: 0.5s (4 MB/s)
- **Total**: ~2.2s

## Use Cases

### Reverse Engineering
- Analyze obfuscated malware
- Understand proprietary algorithms
- Recover lost documentation

### Security Analysis
- Audit third-party libraries
- Find hidden functionality
- Compliance checking

### Software Maintenance
- Debug legacy obfuscated code
- Modernize old applications
- Code quality audits

## Limitations

1. **Decoder class remains**: The unused decoder class stays in the assembly (can be removed in future version)
2. **File size increase**: ~10% larger due to embedded strings
3. **Strong names**: Digital signatures become invalid
4. **Other obfuscation**: Control flow and naming obfuscation remain intact

## Future Enhancements

### Phase 3: Cleanup
- Remove unused decoder class
- Remove data buffer field (save 71 KB)
- Optimize metadata

### Phase 4: Advanced Features
- GUI interface
- Support for other obfuscators
- Batch processing
- Decompiler integration

## Legal & Ethics

⚠️ **Important**: This tool is for legitimate security research and analysis only.

- ✅ Always obtain proper authorization
- ✅ Comply with applicable laws
- ✅ Respect intellectual property
- ✅ Follow responsible disclosure
- ❌ Do not use for malicious purposes

## Results

Successfully patched BeltStat.exe:
- ✅ 3,766 strings decoded (99.9%)
- ✅ 9,150 decoder calls replaced (100%)
- ✅ 576 methods patched
- ✅ Patched assembly is valid and loadable
- ✅ Zero decoder calls remain

See [PATCHING_RESULTS.md](PATCHING_RESULTS.md) for detailed results.

## Project Structure

```
ILStringPatcher/
├── ILStringPatcher/                # Main project
│   ├── Core/                     # Core functionality
│   ├── Models/                   # Data models
│   ├── Program.cs                # Entry point
│   └── ILStringPatcher.csproj      # Project file
├── README.md                     # This file
└── PATCHING_RESULTS.md           # Detailed results
```

## Building from Source

```bash
# Clone repository
git clone https://github.com/yourusername/ILStringPatcher.git
cd ILStringPatcher/ILStringPatcher

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run -- -i input.exe -o output.exe
```

## Troubleshooting

### "Error loading assembly"
- Ensure input file is a valid .NET assembly
- Check file permissions

### "Decoder class not found"
- Assembly may not be obfuscated with this pattern
- Try `--scan` mode to analyze structure

### "Failed to decode strings"
- Obfuscation may use different encryption
- Check verbose output for details

## Contributing

This is a personal project, but suggestions and bug reports are welcome via GitHub Issues.

## Acknowledgments

Built with:
- .NET 9.0 by Microsoft
- dnlib by 0xd4d
- CommandLineParser by gsscoder

---

**Note**: This tool reverses string obfuscation for analysis purposes. Always ensure your use is legal and ethical.

*Last Updated: October 29, 2025*
*Version: 1.0*
