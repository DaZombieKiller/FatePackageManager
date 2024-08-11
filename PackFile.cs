using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;

namespace FatePackageManager;

internal sealed class PackFile : IDisposable
{
    private const int HeaderSize = 0x38;

    private const int EntrySize = 0x20;

    private readonly Stream? _stream;

    private readonly List<PackFileEntry> _entries = [];

    private readonly long _dataStartPos;

    private readonly UInt128[] _keys;

    public ReadOnlyCollection<PackFileEntry> Entries { get; }

    public PackFile(Stream stream, ReadOnlySpan<UInt128> keys)
        : this(keys)
    {
        _stream = stream;
        stream.ReadInt32BigEndian(); // magic
        stream.ReadInt32BigEndian(); // version?
        var fileCount = stream.ReadInt64BigEndian();
        _dataStartPos = stream.ReadInt64BigEndian();
        ReadEntries(fileCount);
    }

    public PackFile(ReadOnlySpan<UInt128> keys)
    {
        _keys = keys.ToArray();
        Entries = _entries.AsReadOnly();
    }

    public void Write(string destination)
    {
        var sw = Stopwatch.StartNew();

        // Generate name buffer
        byte[] nameBuffer;
        var nameOffset = new int[_entries.Count];
        var dataOffset = new long[_entries.Count];

        // Sort entries
        _entries.Sort(EntrySortComparer.Instance);

        using (var ms = new MemoryStream())
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                nameOffset[i] = (int)ms.Position;
                ms.Write(_entries[i].FullNameUtf8);
                ms.WriteByte(0);
            }

            // Align to 16 bytes
            while ((ms.Position & 0x0F) != 0)
                ms.WriteByte(0);

            ms.TryGetBuffer(out var buffer);
            nameBuffer = Deflate(buffer, CompressionLevel.Optimal);
        }

        // Determine maximal size of the archive
        long headerSize = HeaderSize + EntrySize * _entries.Count + nameBuffer.Length;
        long totalSize = headerSize;

        for (int i = 0; i < _entries.Count; i++)
            totalSize += _entries[i].Length;

        // Generate
        using (var stream = File.Create(destination))
        {
            stream.SetLength(totalSize);

            // Write header
            stream.Write("FPD\x00"u8);
            stream.WriteInt32BigEndian(2);
            stream.WriteInt64BigEndian(_entries.Count);
            stream.WriteInt64BigEndian(headerSize);

            for (int i = 0; i < 4; i++)
                stream.WriteInt64BigEndian(0);

            using var xor = new XorStream(stream, _keys, -HeaderSize, leaveOpen: true);

            // Write entries
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0)
                    dataOffset[i] = dataOffset[i - 1] + _entries[i - 1].Length;

                xor.WriteInt64BigEndian(nameOffset[i]);
                xor.WriteInt64BigEndian(dataOffset[i]);
                xor.WriteInt64BigEndian(_entries[i].Length);
                xor.WriteInt64BigEndian(0);
            }

            // Name buffer
            xor.Write(nameBuffer);
        }

        // Write data
        using (var mmf = MemoryMappedFile.CreateFromFile(destination, FileMode.Open))
        using (var mmv = mmf.CreateViewAccessor())
        {
            unsafe
            {
                byte* pointer = null;
                mmv.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

                try
                {
                    Parallel.For(0, _entries.Count, i =>
                    {
                        using var data = _entries[i].OpenRead();
                        using var umms = new UnmanagedMemoryStream(pointer + headerSize + dataOffset[i], _entries[i].Length, _entries[i].Length, FileAccess.Write);
                        data.CopyTo(new XorStream(umms, _keys, leaveOpen: true));
                    });
                }
                finally
                {
                    mmv.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }

            mmv.Flush();
        }

        Console.WriteLine($"Packed in {sw.ElapsedMilliseconds}ms");
    }

    public void AddEntry(FileInfo file, string name)
    {
        _entries.Add(new ExternalPackEntry(this, name, file, compress: false));
    }

    private void ReadEntries(long fileCount)
    {
        using var stream = new XorStream(new SubReadStream(_stream!, HeaderSize, _dataStartPos - HeaderSize), _keys, leaveOpen: false);

        // Read the names buffer first
        stream.Position = EntrySize * fileCount;
        var namesBuffer = Inflate(stream.ReadBytes((int)(stream.Length - stream.Position)));

        // Read the file entries
        stream.Position = 0;

        for (int i = 0; i < fileCount; i++)
        {
            var nameOffset = stream.ReadInt64BigEndian();
            var dataOffset = stream.ReadInt64BigEndian();
            var dataLength = stream.ReadInt64BigEndian();
            var fullLength = stream.ReadInt64BigEndian();
            var entryName = GetTerminatedString(namesBuffer.AsSpan((int)nameOffset));
            _entries.Add(new InternalPackEntry(this, entryName, dataOffset, dataLength, fullLength));
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }

    private static ReadOnlySpan<byte> GetTerminatedString(ReadOnlySpan<byte> buffer)
    {
        int length = buffer.IndexOf((byte)0);

        if (length == -1)
            return buffer;

        return buffer[..length];
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

    private static unsafe byte[] Deflate(ReadOnlySpan<byte> buffer, CompressionLevel compressionLevel)
    {
        using var ms = new MemoryStream();

        fixed (byte* pointer = buffer)
        {
            using var ps = new UnmanagedMemoryStream(pointer, buffer.Length);
            using var ds = new ZLibStream(ms, compressionLevel);
            ps.CopyTo(ds);
        }

        return ms.ToArray();
    }

    private sealed class ExternalPackEntry(PackFile pack, string name, FileInfo file, bool compress) : PackFileEntry(pack, name)
    {
        public FileInfo File { get; } = file;

        public bool Compress { get; } = compress;

        public override long Length => File.Length;

        public override Stream OpenRead()
        {
            return File.OpenRead();
        }
    }

    private sealed class InternalPackEntry(PackFile pack, ReadOnlySpan<byte> name, long offset, long length, long uncompressedLength) : PackFileEntry(pack, name)
    {
        public long Offset { get; } = offset;

        public override long Length { get; } = length;

        public long UncompressedLength { get; } = uncompressedLength;

        public override Stream OpenRead()
        {
            Stream stream = new XorStream(new SubReadStream(Pack._stream!, Pack._dataStartPos + Offset, Length), Pack._keys, leaveOpen: false);

            if (UncompressedLength != 0)
                stream = new ZLibStream(stream, CompressionMode.Decompress);

            return stream;
        }
    }

    private sealed class EntrySortComparer : IComparer<PackFileEntry>
    {
        public static EntrySortComparer Instance { get; } = new();

        public int Compare(PackFileEntry? a, PackFileEntry? b)
        {
            return a!.FullNameUtf8.SequenceCompareTo(b!.FullNameUtf8);
        }
    }
}
