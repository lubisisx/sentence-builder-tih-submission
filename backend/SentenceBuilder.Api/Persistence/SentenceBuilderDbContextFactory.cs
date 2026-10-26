using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SentenceBuilder.Api.Persistence;

public sealed class SentenceBuilderDbContextFactory : IDesignTimeDbContextFactory<SentenceBuilderDbContext>
{
    public SentenceBuilderDbContext CreateDbContext(string[] args)
    {
        // Use the API's copied settings and standard environment/secrets/CLI providers.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SentenceBuilderDbContextFactory).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(typeof(SentenceBuilderDbContextFactory).Assembly.Location)
        });
        var connection = DatabaseConfiguration.RequiredConnectionString(builder.Configuration);
        // Constructing a context does not connect or apply migrations.
        return new(new DbContextOptionsBuilder<SentenceBuilderDbContext>().UseSqlServer(connection).Options);
    }
}
