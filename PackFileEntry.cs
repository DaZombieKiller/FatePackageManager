using System.IO.Compression;

namespace FatePackageManager;

internal sealed class PackFileEntry
{
    internal long Offset { get; }

    internal long Length { get; }

    internal long UncompressedLength { get; }

    public PackFile Pack { get; }

    public string FullName { get; }

    internal PackFileEntry(PackFile pack, string name, long offset, long length, long uncompressedLength)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(name);
        Pack = pack;
        FullName = name;
        Offset = offset;
        Length = length;
        UncompressedLength = uncompressedLength;
    }

    public Stream Open()
    {
        if (UncompressedLength == 0)
            return Pack.OpenEntry(this);

        return new ZLibStream(Pack.OpenEntry(this), CompressionMode.Decompress);
    }
}
