using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SentenceBuilder.Api.Persistence;

namespace SentenceBuilder.Tests.Persistence;

public sealed class DatabaseConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Server=localhost")]
    [InlineData("invalid-connection")]
    public void Host_rejects_missing_or_incomplete_connection_before_serving_requests(string? connection)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SentenceBuilder", connection);
        });
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Configure ConnectionStrings:SentenceBuilder", error.Message);
        if (!string.IsNullOrWhiteSpace(connection)) Assert.DoesNotContain(connection, error.Message);
    }
}
