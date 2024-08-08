using System.Buffers.Binary;

namespace FatePackageManager;

internal static class BinaryReaderExtensions
{
    public static int ReadInt32BigEndian(this BinaryReader @this)
    {
        int value = @this.ReadInt32();

        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        return value;
    }

    public static long ReadInt64BigEndian(this BinaryReader @this)
    {
        long value = @this.ReadInt64();

        if (BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);

        return value;
    }
}
