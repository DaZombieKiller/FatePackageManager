using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace FatePackageManager;

internal sealed class PackFile : IDisposable
{
    private const int HeaderSize = 0x38;

    private const int EntrySize = 0x20;

    private readonly Stream _stream;

    private readonly List<PackFileEntry> _entries = [];

    private readonly long _dataStartPos;

    private readonly UInt128[] _keys;

    public ReadOnlyCollection<PackFileEntry> Entries { get; }

    public PackFile(Stream stream, ReadOnlySpan<UInt128> keys)
    {
        _keys = keys.ToArray();
        _stream = stream;
        using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
        reader.ReadInt32BigEndian(); // magic
        reader.ReadInt32BigEndian(); // version?
        var fileCount = reader.ReadInt64BigEndian();
        _dataStartPos = reader.ReadInt64BigEndian();
        ReadEntries(fileCount);
        Entries = _entries.AsReadOnly();
    }

    private void ReadEntries(long fileCount)
    {
        using var stream = new SubReadStream(_stream, HeaderSize, _dataStartPos - HeaderSize);
        using var reader = new BinaryReader(new XorStream(stream, _keys));

        // Read the names buffer first
        stream.Position = EntrySize * fileCount;
        var namesBuffer = Inflate(reader.ReadBytes((int)(stream.Length - stream.Position)));

        // Read the file entries
        stream.Position = 0;

        for (int i = 0; i < fileCount; i++)
        {
            var nameOffset = reader.ReadInt64BigEndian();
            var dataOffset = reader.ReadInt64BigEndian();
            var dataLength = reader.ReadInt64BigEndian();
            var fullLength = reader.ReadInt64BigEndian();
            var entryName = GetTerminatedString(namesBuffer.AsSpan((int)nameOffset));
            _entries.Add(new PackFileEntry(this, entryName, dataOffset, dataLength, fullLength));
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    private static string GetTerminatedString(ReadOnlySpan<byte> buffer)
    {
        int length = buffer.IndexOf((byte)0);

        if (length == -1)
            return Encoding.UTF8.GetString(buffer);

        return Encoding.UTF8.GetString(buffer[..length]);
    }

    private static unsafe byte[] Inflate(ReadOnlySpan<byte> buffer)
    {
        using var ms = new MemoryStream();

        fixed (byte* pointer = buffer)
        {
            using var ps = new UnmanagedMemoryStream(pointer, buffer.Length);
            using var ds = new ZLibStream(ps, CompressionMode.Decompress);
            ds.CopyTo(ms);
        }

        return ms.ToArray();
    }

    internal Stream OpenEntry(PackFileEntry entry)
    {
        return new XorStream(new SubReadStream(_stream, _dataStartPos + entry.Offset, entry.Length), _keys);
    }
}
