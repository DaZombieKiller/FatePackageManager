using FatePackageManager;

if (args.Length < 1)
{
    Console.WriteLine("fpm <input.dat> [out dir]");
    return;
}

// Compute scrambled XOR keys
var keys = Scrambler.GetScrambledKeys(Scrambler.BaseKeys, Scrambler.PackFileSalt);

// Load package
using var package = new PackFile(File.OpenRead(args[0]), keys);

// Output dir
var outputPath = Environment.CurrentDirectory;

if (args.Length > 1)
    outputPath = args[1];

// Extract files
foreach (var entry in package.Entries)
{
    Console.WriteLine(entry.FullName);
    var filePath = Path.Combine(outputPath, entry.FullName);

    if (Path.GetDirectoryName(filePath) is { } directory)
        Directory.CreateDirectory(directory);

    using var stream = entry.Open();
    using var output = File.Create(filePath);
    stream.CopyTo(output);
}
