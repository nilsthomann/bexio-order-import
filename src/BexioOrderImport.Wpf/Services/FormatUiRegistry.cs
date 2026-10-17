using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace BexioOrderImport.Wpf.Services;

/// <summary>
/// The UI providers of the registered formats. Every format needs exactly one.
/// </summary>
public sealed class FormatUiRegistry
{
    private readonly Dictionary<string, IFormatUiProvider> _providers;

    public FormatUiRegistry(IFormatRegistry formats, IEnumerable<IFormatUiProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(providers);

        _providers = new Dictionary<string, IFormatUiProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.FormatId, provider))
            {
                throw new InvalidOperationException($"Format id '{provider.FormatId}' has more than one UI provider.");
            }
        }

        var missing = formats.Formats.FirstOrDefault(f => !_providers.ContainsKey(f.FormatId));
        if (missing != null)
        {
            throw new InvalidOperationException($"Format '{missing.FormatId}' has no UI provider.");
        }
    }

    public IFormatUiProvider? Find(string? formatId)
        => formatId != null && _providers.TryGetValue(formatId, out var provider) ? provider : null;
}

public static class FormatServiceCollectionExtensions
{
    /// <summary>
    /// Registers an import format with its UI provider.
    /// </summary>
    public static IServiceCollection AddFormat<TAdapter, TUiProvider>(this IServiceCollection services)
        where TAdapter : class, IFormatAdapter
        where TUiProvider : class, IFormatUiProvider
    {
        services.AddSingleton<IFormatAdapter, TAdapter>();
        services.AddSingleton<IFormatUiProvider, TUiProvider>();
        return services;
    }
}
