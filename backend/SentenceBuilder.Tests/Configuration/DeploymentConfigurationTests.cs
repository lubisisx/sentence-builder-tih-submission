using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SentenceBuilder.Api.Configuration;

namespace SentenceBuilder.Tests.Configuration;

public sealed class DeploymentConfigurationTests
{
    private static WebApplicationFactory<Program> Host(string? origin = null, bool redirect = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SentenceBuilder", "Server=localhost;Database=TIH_SentenceBuilder_Test;Integrated Security=True;TrustServerCertificate=True");
            if (origin is not null) builder.UseSetting("Cors:AllowedOrigins:0", origin);
            builder.UseSetting("Https:Redirect", redirect.ToString());
            builder.UseSetting("https_port", "7115");
        });

    [Theory]
    [InlineData("https://*.example.com")]
    [InlineData("*")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/")]
    [InlineData("http://example.com")]
    [InlineData("https://user:secret@example.com")]
    public void Invalid_CORS_configuration_fails_without_echoing_values(string origin)
    {
        using var factory = Host(origin);
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Cors:AllowedOrigins", error.Message);
        Assert.DoesNotContain(origin, error.Message);
    }

    [Fact]
    public void No_configured_origins_means_no_cross_origin_allowlist()
    {
        Assert.Empty(CorsConfiguration.AllowedOrigins(new ConfigurationBuilder().Build()));
    }

    [Theory]
    [InlineData("https://assessment.example.com")]
    [InlineData("http://localhost:4200")]
    public async Task Only_explicit_origins_receive_preflight_permission(string origin)
    {
        using var factory = Host(origin);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/sentences");
        allowed.Headers.Add("Origin", origin);
        allowed.Headers.Add("Access-Control-Request-Method", "PUT");
        allowed.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var response = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/sentences");
        denied.Headers.Add("Origin", "https://unlisted.example.com");
        denied.Headers.Add("Access-Control-Request-Method", "PUT");
        using var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData(true, HttpStatusCode.TemporaryRedirect)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task HTTPS_redirect_defaults_can_be_preserved_or_explicitly_disabled_for_local_containers(bool redirect, HttpStatusCode expected)
    {
        using var factory = Host(redirect: redirect);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync("/missing");
        Assert.Equal(expected, response.StatusCode);
    }
}
