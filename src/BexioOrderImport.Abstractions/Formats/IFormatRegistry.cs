namespace BexioOrderImport.Abstractions.Formats;

/// <summary>
/// The import formats registered at startup.
/// </summary>
public interface IFormatRegistry
{
    IReadOnlyList<IFormatAdapter> Formats { get; }

    IFormatAdapter? Find(string? formatId);
}

public sealed class FormatRegistry : IFormatRegistry
{
    public FormatRegistry(IEnumerable<IFormatAdapter> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        Formats = [.. formats];

        var duplicate = Formats.GroupBy(f => f.FormatId, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Format id '{duplicate.Key}' is registered more than once.");
        }
    }

    public IReadOnlyList<IFormatAdapter> Formats { get; }

    public IFormatAdapter? Find(string? formatId)
        => Formats.FirstOrDefault(f => string.Equals(f.FormatId, formatId, StringComparison.OrdinalIgnoreCase));
}
