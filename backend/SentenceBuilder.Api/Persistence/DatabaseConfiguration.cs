using Microsoft.Data.SqlClient;

namespace SentenceBuilder.Api.Persistence;

public static class DatabaseConfiguration
{
    public static string RequiredConnectionString(IConfiguration configuration)
    {
        const string message = "Configure ConnectionStrings:SentenceBuilder with an explicit SQL Server and database before starting the API or EF tooling.";
        var connection = configuration.GetConnectionString("SentenceBuilder");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException(message);
        SqlConnectionStringBuilder target;
        try { target = new SqlConnectionStringBuilder(connection); }
        catch (ArgumentException) { throw new InvalidOperationException(message); }
        if (string.IsNullOrWhiteSpace(target.DataSource) || string.IsNullOrWhiteSpace(target.InitialCatalog))
            throw new InvalidOperationException(message);
        return connection;
    }
}
