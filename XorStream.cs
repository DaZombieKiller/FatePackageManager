namespace FatePackageManager;

internal sealed class XorStream : Stream
{
    private readonly UInt128[] _keys;

    private readonly Stream _stream;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public XorStream(Stream stream, UInt128[] keys)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(keys);
        _keys = keys;
        _stream = stream;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        long pos = _stream.Position;
        int read = _stream.Read(buffer);
        Scrambler.UnScrambleData(_keys, buffer[..read], (int)pos);
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
        throw new NotSupportedException();
    }
}
