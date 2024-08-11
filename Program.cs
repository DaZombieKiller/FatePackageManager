using FatePackageManager;

if (args.Length < 1)
{
    Console.WriteLine("fpm <in.dat> [out dir]");
    Console.WriteLine("fpm pack <in dir> <out.dat>");
    return;
}

// Compute scrambled XOR keys
var keys = Scrambler.GetScrambledKeys(Scrambler.BaseKeys, Scrambler.PackFileSalt);

if (args[0] == "pack" && args.Length == 3)
{
    var pack = new PackFile(keys);

    foreach (string path in Directory.GetFiles(args[1], "*.*", SearchOption.AllDirectories))
        pack.AddEntry(new FileInfo(path), Path.GetRelativePath(args[1], path).Replace('\\', '/'));

    pack.Write(args[2]);
    return;
}

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

    using var stream = entry.OpenRead();
    using var output = File.Create(filePath);
    stream.CopyTo(output);
}
