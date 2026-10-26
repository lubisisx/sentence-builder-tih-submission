namespace SentenceBuilder.Api.Configuration;

public static class CorsConfiguration
{
    public const string PolicyName = "Frontend";

    public static string[] AllowedOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
                || uri.Host.Contains('*') || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
                || origin != uri.GetLeftPart(UriPartial.Authority))
                throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTPS origins (or loopback HTTP), without paths, credentials or wildcards.");
        }
        return origins.Distinct(StringComparer.Ordinal).ToArray();
    }
}
