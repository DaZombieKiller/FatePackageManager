using System.Buffers.Binary;

namespace FatePackageManager;

internal static class StreamExtensions
{
    public static unsafe int ReadInt32BigEndian(this Stream @this)
    {
        int value;
        @this.ReadExactly(new Span<byte>(&value, sizeof(int)));

        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        return value;
    }

    public static unsafe long ReadInt64BigEndian(this Stream @this)
    {
        long value;
        @this.ReadExactly(new Span<byte>(&value, sizeof(long)));

        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        return value;
    }

    public static unsafe void WriteInt32BigEndian(this Stream @this, int value)
    {
        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        @this.Write(new Span<byte>(&value, sizeof(int)));
    }

    public static unsafe void WriteInt64BigEndian(this Stream @this, long value)
    {
        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        @this.Write(new Span<byte>(&value, sizeof(long)));
    }

    public static byte[] ReadBytes(this Stream @this, int count)
    {
        var bytes = new byte[count];
        @this.ReadExactly(bytes);
        return bytes;
    }
}
