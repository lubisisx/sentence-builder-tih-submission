using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SentenceBuilder.Api.Contracts;
using SentenceBuilder.Api.Models;
using SentenceBuilder.Api.Persistence;
using SentenceBuilder.Tests.Persistence;

namespace SentenceBuilder.Tests.Api;

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection;

[Collection("SqlServer")]
[Trait("Category", "SqlServerIntegration")]
public sealed class ApiIntegrationTests : IAsyncLifetime
{
    private SqlConnection _connection = null!;
    private SqlTransaction _transaction = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private int[] _wordIds = [];
    private int _typeId;
    private bool _failWrites;

    private SentenceBuilderDbContext Context()
    {
        var options = new DbContextOptionsBuilder<SentenceBuilderDbContext>().UseSqlServer(_connection)
            .AddInterceptors(new InvalidChildInterceptor(() => _failWrites)).Options;
        var context = new SentenceBuilderDbContext(options);
        context.Database.UseTransaction(_transaction);
        return context;
    }

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("TIH_SQL_TESTS_APPROVED") != "1") throw new InvalidOperationException("SQL writes not approved.");
        var connection = Environment.GetEnvironmentVariable("TIH_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing test connection.");
        var target = new SqlConnectionStringBuilder(connection);
        if (target.InitialCatalog != "TIH_SentenceBuilder_Test") throw new InvalidOperationException("Only the dedicated test database is allowed.");
        _connection = new SqlConnection(connection);
        await _connection.OpenAsync();
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
        await using (var db = Context())
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seed-vocabulary.sql")));
            // The seed enables XACT_ABORT. Restore its default for EF savepoint failure assertions.
            await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT OFF;");
            var words = await db.Words.OrderBy(x => x.Id).Take(3).ToArrayAsync();
            _wordIds = words.Select(x => x.Id).ToArray();
            _typeId = words[0].WordTypeId;
        }
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SentenceBuilder", connection);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<SentenceBuilderDbContext>();
                services.AddScoped(_ => Context());
            });
        });
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_transaction is not null) { await _transaction.RollbackAsync(); await _transaction.DisposeAsync(); }
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private async Task<SentenceDetail> Create(params int[] ids)
    {
        var response = await _client.PostAsJsonAsync("/api/sentences", new SentenceRequest(ids));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<SentenceDetail>())!;
        Assert.EndsWith($"/api/sentences/{detail.Id}", response.Headers.Location!.ToString());
        return detail;
    }

    [SqlServerFact]
    public async Task Vocabulary_is_database_backed_sorted_and_uses_DTOs()
    {
        var types = (await _client.GetFromJsonAsync<WordTypeResponse[]>("/api/word-types"))!;
        Assert.Equal(9, types.Length);
        Assert.Equal(types.OrderBy(x => x.Name).Select(x => x.Id), types.Select(x => x.Id));
        var words = (await _client.GetFromJsonAsync<WordResponse[]>($"/api/words?wordTypeId={_typeId}"))!;
        Assert.Equal(3, words.Length);
        Assert.All(words, x => Assert.Equal(_typeId, x.WordTypeId));
        Assert.Equal(words.OrderBy(x => x.Text).Select(x => x.Id), words.Select(x => x.Id));
        var json = await _client.GetStringAsync("/api/word-types");
        Assert.DoesNotContain("sentenceWords", json);
        await using var db = Context();
        db.WordTypes.Add(new WordType { Name = "Z-test-category" });
        await db.SaveChangesAsync();
        Assert.Equal(10, (await _client.GetFromJsonAsync<WordTypeResponse[]>("/api/word-types"))!.Length);
    }

    [SqlServerFact]
    public async Task Create_and_detail_persist_authoritative_text_order_duplicates_and_UTC_timestamps()
    {
        var ids = new[] { _wordIds[2], _wordIds[0], _wordIds[2] };
        var created = await Create(ids);
        await using var db = Context();
        var words = await db.Words.ToDictionaryAsync(x => x.Id, x => x.Value);
        Assert.Equal(string.Join(" ", ids.Select(x => words[x])), created.Text);
        Assert.Equal(ids, created.Words.Select(x => x.WordId));
        Assert.Equal(new[] { 0, 1, 2 }, created.Words.Select(x => x.Position));
        Assert.Equal(DateTimeKind.Utc, created.CreatedAtUtc.Kind);
        var detail = (await _client.GetFromJsonAsync<SentenceDetail>($"/api/sentences/{created.Id}"))!;
        Assert.Equal(created.Text, detail.Text);
        var saved = await db.Sentences.Include(x => x.SentenceWords).SingleAsync(x => x.Id == created.Id);
        Assert.Equal(created.Text, saved.Text);
        Assert.Equal(created.Words.Select(x => x.Text), saved.SentenceWords.OrderBy(x => x.Position).Select(x => x.WordText));
        Assert.DoesNotContain("version", await _client.GetStringAsync($"/api/sentences/{created.Id}"), StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Pagination_defaults_boundaries_and_order_are_consistent()
    {
        var first = await Create(_wordIds[0]);
        var second = await Create(_wordIds[1]);
        await using var db = Context();
        var entities = await db.Sentences.ToListAsync();
        foreach (var entity in entities) { entity.CreatedAt = first.CreatedAtUtc; entity.UpdatedAt = second.UpdatedAtUtc; }
        await db.SaveChangesAsync();
        var page = (await _client.GetFromJsonAsync<SentencePage>("/api/sentences?page=1&pageSize=1"))!;
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(second.Id, Assert.Single(page.Items).Id);
        var next = (await _client.GetFromJsonAsync<SentencePage>("/api/sentences?page=2&pageSize=1"))!;
        Assert.Equal(first.Id, Assert.Single(next.Items).Id);
        Assert.Empty((await _client.GetFromJsonAsync<SentencePage>("/api/sentences?page=3&pageSize=1"))!.Items);
        var defaults = (await _client.GetFromJsonAsync<SentencePage>("/api/sentences"))!;
        Assert.Equal(1, defaults.Page);
        Assert.Equal(20, defaults.PageSize);
    }

    [SqlServerFact]
    public async Task Editing_preserves_retained_occurrences_snapshots_and_uses_current_text_for_added_occurrences()
    {
        var created = await Create(_wordIds[0], _wordIds[1]);
        await using var db = Context();
        var word = (await db.Words.FindAsync(_wordIds[0]))!;
        word.Value = "new vocabulary value";
        await db.SaveChangesAsync();
        var response = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([_wordIds[1], _wordIds[0], _wordIds[0]]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<SentenceDetail>())!;
        Assert.Equal(new[] { created.Words[1].Text, created.Words[0].Text, word.Value }, edited.Words.Select(x => x.Text));
        Assert.Equal(created.CreatedAtUtc, edited.CreatedAtUtc);
        Assert.True(edited.UpdatedAtUtc >= created.UpdatedAtUtc);
        Assert.Equal(string.Join(" ", edited.Words.Select(x => x.Text)), edited.Text);
        response = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([_wordIds[0]]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shortened = (await response.Content.ReadFromJsonAsync<SentenceDetail>())!;
        Assert.Single(shortened.Words);
        Assert.Equal(created.Words[0].Text, shortened.Text);
        Assert.Equal(1, await db.SentenceWords.CountAsync(x => x.SentenceId == created.Id));
    }

    [SqlServerFact]
    public async Task Snapshot_FIFO_is_persisted_after_duplicate_removal_and_reordering()
    {
        // All changes, including vocabulary changes, use this test's rollback transaction.
        await using var db = Context();
        var word = (await db.Words.FindAsync(_wordIds[0]))!;
        word.Value = "old snapshot";
        await db.SaveChangesAsync();
        var created = await Create(word.Id);
        Assert.Equal("old snapshot", created.Text);

        word.Value = "new snapshot";
        await db.SaveChangesAsync();
        // The same-ID replacement payload cannot ask for a fresh snapshot.
        var sameIdReplacement = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([word.Id]));
        Assert.Equal(HttpStatusCode.OK, sameIdReplacement.StatusCode);
        Assert.Equal("old snapshot", (await sameIdReplacement.Content.ReadFromJsonAsync<SentenceDetail>())!.Text);

        // This naturally creates two snapshots of the same WordId with different text.
        var grow = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([word.Id, word.Id]));
        Assert.Equal(HttpStatusCode.OK, grow.StatusCode);
        var mixed = (await grow.Content.ReadFromJsonAsync<SentenceDetail>())!;
        Assert.Equal(new[] { "old snapshot", "new snapshot" }, mixed.Words.Select(x => x.Text));
        Assert.All(mixed.Words, x => Assert.Equal(word.Id, x.WordId));

        // Swapping the two UI occurrences still sends the identical [A,A] body.
        var reorder = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([word.Id, word.Id]));
        Assert.Equal(HttpStatusCode.OK, reorder.StatusCode);
        Assert.Equal("old snapshot new snapshot", (await reorder.Content.ReadFromJsonAsync<SentenceDetail>())!.Text);

        // Removing either UI occurrence sends [A]; FIFO retains the first old snapshot.
        var remove = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([word.Id]));
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var shortened = (await remove.Content.ReadFromJsonAsync<SentenceDetail>())!;
        Assert.Equal("old snapshot", shortened.Text);
        Assert.Equal("old snapshot", Assert.Single(shortened.Words).Text);

        // Fresh SQL reads prove that the response reflects persisted rows, not a DTO-only issue.
        db.ChangeTracker.Clear();
        var stored = await db.Sentences.Include(x => x.SentenceWords).SingleAsync(x => x.Id == created.Id);
        Assert.Equal("old snapshot", stored.Text);
        var child = Assert.Single(stored.SentenceWords);
        Assert.Equal(0, child.Position);
        Assert.Equal(word.Id, child.WordId);
        Assert.Equal("old snapshot", child.WordText);
        Assert.Equal("new snapshot", (await db.Words.FindAsync(word.Id))!.Value);
    }

    [SqlServerTheory]
    [InlineData("{}")]
    [InlineData("{\"wordIds\":null}")]
    [InlineData("{\"wordIds\":[]}")]
    [InlineData("{\"wordIds\":[-1]}")]
    [InlineData("{\"wordIds\":[2147483647]}")]
    [InlineData("{\"wordIds\":[\"bad\"]}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task Invalid_bodies_return_validation_problem_without_writes(string json)
    {
        var response = await _client.PostAsync("/api/sentences", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(400, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        await using var db = Context();
        Assert.False(await db.Sentences.AnyAsync());
    }

    [SqlServerFact]
    public async Task Word_limit_is_enforced_and_repeated_words_are_allowed_at_limit()
    {
        var response = await _client.PostAsJsonAsync("/api/sentences", new SentenceRequest(Enumerable.Repeat(_wordIds[0], 101).ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(100, (await Create(Enumerable.Repeat(_wordIds[0], 100).ToArray())).Words.Count);
    }

    [SqlServerTheory]
    [InlineData("/api/words", 400)]
    [InlineData("/api/words?wordTypeId=0", 400)]
    [InlineData("/api/words?wordTypeId=no", 400)]
    [InlineData("/api/words?wordTypeId=2147483647", 404)]
    [InlineData("/api/sentences?page=0", 400)]
    [InlineData("/api/sentences?pageSize=101", 400)]
    [InlineData("/api/sentences?pageSize=0", 400)]
    [InlineData("/api/sentences?page=no", 400)]
    [InlineData("/api/sentences?page=2147483647&pageSize=100", 400)]
    [InlineData("/api/sentences/0", 400)]
    [InlineData("/api/sentences/2147483647", 404)]
    public async Task Invalid_queries_and_missing_resources_have_problem_details(string path, int expected)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [SqlServerFact]
    public async Task Unknown_edit_returns_404_and_invalid_edit_keeps_saved_sentence()
    {
        var missing = await _client.PutAsJsonAsync("/api/sentences/2147483647", new SentenceRequest([_wordIds[0]]));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var created = await Create(_wordIds[0]);
        var bad = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([int.MaxValue]));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(created.Text, (await _client.GetFromJsonAsync<SentenceDetail>($"/api/sentences/{created.Id}"))!.Text);
    }

    [SqlServerFact]
    public async Task Failed_SQL_writes_return_safe_500_and_leave_no_partial_create_or_edit()
    {
        var created = await Create(_wordIds[0]);
        _failWrites = true;
        var failedCreate = await _client.PostAsJsonAsync("/api/sentences", new SentenceRequest([_wordIds[1]]));
        Assert.Equal(HttpStatusCode.InternalServerError, failedCreate.StatusCode);
        var body = await failedCreate.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("TIH_SentenceBuilder", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("application/problem+json", failedCreate.Content.Headers.ContentType!.MediaType);
        var failedEdit = await _client.PutAsJsonAsync($"/api/sentences/{created.Id}", new SentenceRequest([_wordIds[1]]));
        Assert.Equal(HttpStatusCode.InternalServerError, failedEdit.StatusCode);
        _failWrites = false;
        await using var db = Context();
        Assert.Equal(1, await db.Sentences.CountAsync());
        Assert.Equal(1, await db.SentenceWords.CountAsync());
        var detail = (await _client.GetFromJsonAsync<SentenceDetail>($"/api/sentences/{created.Id}"))!;
        Assert.Equal(created.Text, detail.Text);
        Assert.Equal(created.Words[0].WordId, Assert.Single(detail.Words).WordId);
    }

    private sealed class InvalidChildInterceptor(Func<bool> shouldFail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (shouldFail())
                foreach (var child in eventData.Context!.ChangeTracker.Entries<SentenceWord>().Where(x => x.State is EntityState.Added or EntityState.Modified))
                    child.Entity.WordId = -1;
            return ValueTask.FromResult(result);
        }
    }
}
