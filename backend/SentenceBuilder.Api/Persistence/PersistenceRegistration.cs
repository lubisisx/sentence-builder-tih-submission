using Microsoft.EntityFrameworkCore;

namespace SentenceBuilder.Api.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddSentenceBuilderPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SentenceBuilderDbContext>(options =>
        {
            var connection = DatabaseConfiguration.RequiredConnectionString(configuration);
            options.UseSqlServer(connection);
        });
        return services;
    }
}
