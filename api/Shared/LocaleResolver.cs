using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace EnergyTracker.Api.Shared;

public sealed class LocaleResolver
{
    private static readonly string[] SupportedLocales = ["de-DE", "en-US"];
    private const string DefaultLocale = "en-US";

    public string Resolve(HttpRequest request, string? storedOverride)
    {
        if (storedOverride is not null && SupportedLocales.Contains(storedOverride))
            return storedOverride;

        // Parse Accept-Language tokens in quality-weight order; match exact locale tags only
        var candidates = request.Headers.AcceptLanguage
            .SelectMany(h => (h ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(token =>
            {
                var semi = token.IndexOf(';');
                if (semi < 0) return (lang: token, q: 1.0);
                var lang = token[..semi].Trim();
                var qPart = token[(semi + 1)..].Trim();
                var q = qPart.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(qPart[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var qv)
                    ? qv : 1.0;
                return (lang, q);
            })
            .OrderByDescending(t => t.q);

        foreach (var (lang, _) in candidates)
        {
            var match = Array.Find(SupportedLocales, s => s.Equals(lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return DefaultLocale;
    }
}
