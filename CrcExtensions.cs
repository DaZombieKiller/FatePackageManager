using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace FatePackageManager;

internal static class CrcExtensions
{
    public static void SetCurrentHash(this Crc32 @this, uint hash)
    {
        ArgumentNullException.ThrowIfNull(@this);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_crc")]
        static extern ref uint GetHashReference(Crc32 @this);

        GetHashReference(@this) = ~hash;
    }
}
