using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace api.Tests.Shared;

public class LocaleResolverTests
{
    private readonly LocaleResolver _resolver = new();

    [Fact]
    public void Resolve_StoredOverrideWins_OverAcceptLanguage()
    {
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "de-DE";

        var result = _resolver.Resolve(req, "en-US");

        result.ShouldBe("en-US");
    }

    [Fact]
    public void Resolve_AcceptLanguageDeDe_WithNoOverride_ReturnsDeDe()
    {
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "de-DE,de;q=0.9";

        var result = _resolver.Resolve(req, null);

        result.ShouldBe("de-DE");
    }

    [Fact]
    public void Resolve_UnknownAcceptLanguage_WithNoOverride_ReturnsDefault()
    {
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "fr-FR";

        var result = _resolver.Resolve(req, null);

        result.ShouldBe("en-US");
    }

    [Fact]
    public void Resolve_InvalidStoredOverride_FallsBackToAcceptLanguage()
    {
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "de-DE";

        var result = _resolver.Resolve(req, "fr-FR");

        result.ShouldBe("de-DE");
    }

    [Fact]
    public void Resolve_EnUsHigherQualityWeight_ReturnsEnUs()
    {
        // en-US is first and has default q=1.0; de-DE has q=0.5 — en-US must win
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "en-US,de-DE;q=0.5";

        var result = _resolver.Resolve(req, null);

        result.ShouldBe("en-US");
    }

    [Fact]
    public void Resolve_SubstringInAcceptLanguage_DoesNotMatchSupportedLocale()
    {
        // "notde-DE" must NOT substring-match "de-DE"
        var req = new DefaultHttpContext().Request;
        req.Headers["Accept-Language"] = "notde-DE";

        var result = _resolver.Resolve(req, null);

        result.ShouldBe("en-US");
    }
}
