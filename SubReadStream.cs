namespace FatePackageManager;

internal sealed class SubReadStream : Stream
{
    private readonly long _start;

    private readonly long _end;

    private readonly Stream _baseStream;

    private long _position;

    private bool _disposed;

    /// <summary>Initializes a new <see cref="SubReadStream"/> instance.</summary>
    public SubReadStream(Stream stream, long start, long maxLength)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable.", nameof(stream));

        ArgumentOutOfRangeException.ThrowIfLessThan(start, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 0);
        _start = start;
        _position = start;
        _end = start + maxLength;
        _baseStream = stream;
    }

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _end - _start;
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _position - _start;
        }

        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!CanSeek)
                throw new NotSupportedException();

            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            _baseStream.Position = _position = _start + value;
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => _baseStream.CanRead && !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => _baseStream.CanSeek && !_disposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotReadable();

        if (_position >= _end)
            return 0;

        if (_baseStream.Position != _position)
            _baseStream.Position = _position;

        if (_position + count > _end)
            count = (int)(_end - _position);

        int read = _baseStream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotReadable();

        if (_position >= _end)
            return 0;

        if (_baseStream.Position != _position)
            _baseStream.Position = _position;

        if (_position > _end - buffer.Length)
            buffer = buffer[..(int)(_end - _position)];

        int read = _baseStream.Read(buffer);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public unsafe override int ReadByte()
    {
        byte b = default;
        return Read(new Span<byte>(&b, 1)) == 1 ? b : -1;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotReadable();

        async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_position >= _end)
                return 0;

            if (_baseStream.Position != _position)
                _baseStream.Position = _position;

            if (_position > _end - buffer.Length)
                buffer = buffer[..(int)(_end - _position)];

            int read = await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += read;
            return read;
        }

        return ReadAsyncCore(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        switch (origin)
        {
        case SeekOrigin.Current:
            offset += _position - _start;
            break;
        case SeekOrigin.End:
            offset += _end - _start;
            break;
        }

        return Position = offset;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    /// <summary>Throws <see cref="NotSupportedException"/> if the stream is not readable.</summary>
    private void ThrowIfNotReadable()
    {
        if (!CanRead)
        {
            throw new NotSupportedException();
        }
    }
}
