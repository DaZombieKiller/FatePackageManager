using System.Buffers;

namespace FatePackageManager;

internal sealed class XorStream : Stream
{
    private readonly bool _leaveOpen;

    private readonly UInt128[] _keys;

    private readonly Stream _stream;

    private readonly int _keyOffset;

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public XorStream(Stream stream, UInt128[] keys, bool leaveOpen)
        : this(stream, keys, keyOffset: 0, leaveOpen)
    {
    }

    public XorStream(Stream stream, UInt128[] keys, int keyOffset, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(keys);
        _keys = keys;
        _stream = stream;
        _keyOffset = keyOffset;
        _leaveOpen = leaveOpen;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long pos = _stream.Position;
        int read = _stream.Read(buffer, offset, count);
        Scrambler.UnScrambleData(_keys, buffer.AsSpan(offset, read), _keyOffset + (int)pos);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        long pos = _stream.Position;
        int read = _stream.Read(buffer);
        Scrambler.UnScrambleData(_keys, buffer[..read], _keyOffset + (int)pos);
        return read;
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        Scrambler.UnScrambleData(_keys, buffer, array, _keyOffset + (int)_stream.Position);
        _stream.Write(array, 0, buffer.Length);
        ArrayPool<byte>.Shared.Return(array);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && !_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
