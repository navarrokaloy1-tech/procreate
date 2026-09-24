using Microsoft.Extensions.Caching.Memory;

namespace ProCreateApi.Services.Sso;

/// <summary>What was pending when the browser left for the provider.</summary>
/// <param name="Nonce">Tied to the ID token so it cannot be replayed.</param>
/// <param name="CodeVerifier">The PKCE secret, kept off the wire until the exchange.</param>
/// <param name="Audience">"staff" or "patient" — which sign-in started this.</param>
public record SsoRequest(string Nonce, string CodeVerifier, string Audience);

/// <summary>
/// Short-lived server-side state for a sign-in round trip.
///
/// Two things pass through here. The pending request, because the PKCE
/// verifier must not leave the server; and the finished session, because the
/// provider returns the browser by redirect and a session token in a URL
/// would be left behind in history and logs. The browser carries a one-time
/// ticket instead and swaps it for the token over a normal POST.
/// </summary>
public class SsoHandoff
{
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);

    // Long enough for a redirect and one page load, short enough that a
    // ticket left in a browser's history is worthless by the time it is read.
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;

    public SsoHandoff(IMemoryCache cache) => _cache = cache;

    public string StashRequest(SsoRequest request)
    {
        var state = OidcClient.NewSecret();
        _cache.Set(RequestKey(state), request, RequestLifetime);
        return state;
    }

    /// <summary>Single use: a replayed state finds nothing.</summary>
    public SsoRequest? TakeRequest(string? state)
    {
        if (string.IsNullOrEmpty(state)) return null;

        var key = RequestKey(state);
        if (!_cache.TryGetValue(key, out SsoRequest? request)) return null;

        _cache.Remove(key);
        return request;
    }

    public string StashSession(object session)
    {
        var ticket = OidcClient.NewSecret();
        _cache.Set(TicketKey(ticket), session, TicketLifetime);
        return ticket;
    }

    /// <summary>Single use: the ticket is spent the first time it is redeemed.</summary>
    public object? TakeSession(string? ticket)
    {
        if (string.IsNullOrEmpty(ticket)) return null;

        var key = TicketKey(ticket);
        if (!_cache.TryGetValue(key, out object? session)) return null;

        _cache.Remove(key);
        return session;
    }

    private static string RequestKey(string state) => "sso:request:" + state;
    private static string TicketKey(string ticket) => "sso:ticket:" + ticket;
}
