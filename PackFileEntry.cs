using System.Text;

namespace FatePackageManager;

internal abstract class PackFileEntry
{
    private readonly byte[] _utf8Name;

    public PackFile Pack { get; }

    public string FullName { get; }

    public ReadOnlySpan<byte> FullNameUtf8 => _utf8Name;

    public abstract long Length { get; }

    protected PackFileEntry(PackFile pack, string name)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(name);
        Pack = pack;
        FullName = name;
        _utf8Name = Encoding.UTF8.GetBytes(name);
    }

    protected PackFileEntry(PackFile pack, ReadOnlySpan<byte> name)
    {
        ArgumentNullException.ThrowIfNull(pack);
        Pack = pack;
        _utf8Name = name.ToArray();
        FullName = Encoding.UTF8.GetString(_utf8Name);
    }

    public abstract Stream OpenRead();
}
