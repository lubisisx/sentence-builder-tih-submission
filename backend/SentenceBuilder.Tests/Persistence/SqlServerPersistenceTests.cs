using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SentenceBuilder.Api.Models;
using SentenceBuilder.Api.Persistence;

namespace SentenceBuilder.Tests.Persistence;

// An explicit opt-in records that the user approved this database-changing test run.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TIH_SQL_TESTS_APPROVED") != "1")
            Skip = "SQL Server write tests require approval, TIH_SQL_TESTS_APPROVED=1 and TIH_TEST_CONNECTION.";
    }
}

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("TIH_SQL_TESTS_APPROVED") != "1")
            Skip = "SQL Server write tests require approval, TIH_SQL_TESTS_APPROVED=1 and TIH_TEST_CONNECTION.";
    }
}

[Collection("SqlServer")]
[Trait("Category", "SqlServerIntegration")]
public sealed class SqlServerPersistenceTests : IAsyncLifetime
{
    private SentenceBuilderDbContext _db = null!;
    private IDbContextTransaction? _transaction;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("TIH_SQL_TESTS_APPROVED") != "1")
            throw new InvalidOperationException("Database write tests have not been approved.");
        var connection = Environment.GetEnvironmentVariable("TIH_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TIH_TEST_CONNECTION to the approved isolated SQL Server database.");
        var target = new SqlConnectionStringBuilder(connection);
        if (target.InitialCatalog != "TIH_SentenceBuilder_Test")
            throw new InvalidOperationException("Tests may only write to TIH_SentenceBuilder_Test; never development or unrelated databases.");
        _db = new(new DbContextOptionsBuilder<SentenceBuilderDbContext>().UseSqlServer(connection).Options);
        await _db.Database.OpenConnectionAsync();
        // Schema setup is a separate approved command. Tests never create/drop databases or apply migrations.
        Assert.Empty(await _db.Database.GetPendingMigrationsAsync());
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        if (_db is not null) await _db.DisposeAsync();
    }

    private async Task<Word> AddWord()
    {
        var type = new WordType { Name = "test-" + Guid.NewGuid().ToString("N") };
        var word = new Word { WordType = type, Value = "original" };
        _db.Add(word);
        await _db.SaveChangesAsync();
        return word;
    }

    private static Sentence NewSentence(string text = "original") => new()
    {
        Text = text,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [SqlServerFact]
    public async Task Word_type_relationship_round_trips()
    {
        var word = await AddWord();
        var typeId = word.WordTypeId;
        _db.ChangeTracker.Clear();
        var type = await _db.WordTypes.Include(x => x.Words).SingleAsync(x => x.Id == typeId);
        Assert.Equal(word.Id, Assert.Single(type.Words).Id);
        Assert.Equal(type.Id, Assert.Single(type.Words).WordTypeId);
    }

    [SqlServerFact]
    public async Task Sentence_relationship_preserves_positions_repeated_words_and_historical_snapshots()
    {
        var word = await AddWord();
        var sentence = NewSentence("first second");
        sentence.SentenceWords.Add(new() { Position = 1, Word = word, WordText = "second" });
        sentence.SentenceWords.Add(new() { Position = 0, Word = word, WordText = "first" });
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        word.Value = "changed";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var saved = await _db.Sentences.Include(x => x.SentenceWords).SingleAsync(x => x.Id == sentence.Id);
        var ordered = saved.SentenceWords.OrderBy(x => x.Position).ToArray();
        Assert.Equal(new[] { "first", "second" }, ordered.Select(x => x.WordText));
        Assert.All(ordered, x => Assert.Equal(word.Id, x.WordId));
        Assert.Equal("first second", saved.Text);
        Assert.Equal("changed", (await _db.Words.FindAsync(word.Id))!.Value);
    }

    [SqlServerFact]
    public async Task Sentence_listing_has_deterministic_id_tiebreaker()
    {
        var first = NewSentence("first");
        _db.Add(first);
        await _db.SaveChangesAsync();
        var second = NewSentence("second");
        _db.Add(second);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var ids = await _db.Sentences.Where(x => x.Id == first.Id || x.Id == second.Id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Id).ToArrayAsync();
        Assert.Equal(new[] { second.Id, first.Id }, ids);
    }

    [SqlServerFact]
    public async Task Duplicate_position_is_rejected_by_SQL_Server()
    {
        var word = await AddWord();
        var sentence = NewSentence();
        sentence.SentenceWords.Add(new() { Word = word, Position = 0, WordText = word.Value });
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        // Raw SQL bypasses EF identity tracking so the database, not EF, enforces the key.
        var error = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({sentence.Id}, 0, {word.Id}, N'duplicate')"));
        Assert.Contains(error.Number, new[] { 2601, 2627 });
    }

    [SqlServerFact]
    public async Task Foreign_keys_reject_orphans()
    {
        var sentence = NewSentence();
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        var word = await AddWord();
        var missingType = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dbo.Word (WordTypeId, Value) VALUES (-1, N'orphan')"));
        Assert.Equal(547, missingType.Number);
        var missingWord = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({sentence.Id}, 0, -1, N'orphan')"));
        Assert.Equal(547, missingWord.Number);
        var missingSentence = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES (-1, 0, {word.Id}, N'orphan')"));
        Assert.Equal(547, missingSentence.Number);
    }

    [SqlServerFact]
    public async Task Referenced_vocabulary_cannot_be_deleted_but_sentence_words_cascade()
    {
        var word = await AddWord();
        var sentence = NewSentence();
        sentence.SentenceWords.Add(new() { Word = word, Position = 0, WordText = word.Value });
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        var wordError = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.Word WHERE Id = {word.Id}"));
        Assert.Equal(547, wordError.Number);
        var typeError = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.WordType WHERE Id = {word.WordTypeId}"));
        Assert.Equal(547, typeError.Number);
        await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.Sentence WHERE Id = {sentence.Id}");
        Assert.False(await _db.SentenceWords.AnyAsync(x => x.SentenceId == sentence.Id));
    }

    [SqlServerTheory]
    [InlineData("INSERT INTO dbo.WordType (Name) VALUES (NULL)", 515)]
    [InlineData("INSERT INTO dbo.WordType (Name) VALUES (N'   ')", 547)]
    [InlineData("INSERT INTO dbo.WordType (Name) VALUES (REPLICATE(N'x', 51))", 2628)]
    [InlineData("INSERT INTO dbo.Word (WordTypeId, Value) VALUES ({0}, NULL)", 515)]
    [InlineData("INSERT INTO dbo.Word (WordTypeId, Value) VALUES ({0}, N'')", 547)]
    [InlineData("INSERT INTO dbo.Word (WordTypeId, Value) VALUES ({0}, REPLICATE(N'x', 101))", 2628)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (REPLICATE(CONVERT(nvarchar(max), N'x'), 10100), '20260101', '20260101')", 547)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (NULL, '20260101', '20260101')", 515)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (N' ', '20260101', '20260101')", 547)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (N'text', NULL, '20260101')", 515)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (N'text', '20260101', NULL)", 515)]
    [InlineData("INSERT INTO dbo.Sentence (Text, CreatedAt, UpdatedAt) VALUES (N'text', '20260102', '20260101')", 547)]
    [InlineData("INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({1}, 0, {2}, NULL)", 515)]
    [InlineData("INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({1}, 0, {2}, N' ')", 547)]
    [InlineData("INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({1}, -1, {2}, N'text')", 547)]
    [InlineData("INSERT INTO dbo.SentenceWord (SentenceId, Position, WordId, WordText) VALUES ({1}, 0, {2}, REPLICATE(N'x', 101))", 2628)]
    public async Task Database_rejects_missing_blank_overlength_and_invalid_values(string sql, int expectedNumber)
    {
        var word = await AddWord();
        var sentence = NewSentence();
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        // Placeholders become SQL parameters; all SQL text is static test data.
        var error = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlRawAsync(sql, word.WordTypeId, sentence.Id, word.Id));
        if (expectedNumber == 2628) Assert.Contains(error.Number, new[] { 2628, 8152 });
        else Assert.Equal(expectedNumber, error.Number);
    }

    [SqlServerFact]
    public async Task Word_type_names_and_words_within_a_type_are_unique()
    {
        var word = await AddWord();
        var typeError = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.WordType (Name) VALUES ({word.WordType.Name})"));
        Assert.Contains(typeError.Number, new[] { 2601, 2627 });
        var wordError = await Assert.ThrowsAsync<SqlException>(() => _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.Word (WordTypeId, Value) VALUES ({word.WordTypeId}, {word.Value})"));
        Assert.Contains(wordError.Number, new[] { 2601, 2627 });
    }

    [SqlServerFact]
    public async Task Failed_sentence_save_rolls_back_parent_and_children_without_partial_data()
    {
        var word = await AddWord();
        var marker = "atomic-" + Guid.NewGuid().ToString("N");
        var sentence = NewSentence(marker);
        sentence.SentenceWords.Add(new() { Position = 0, WordId = word.Id, WordText = marker });
        sentence.SentenceWords.Add(new() { Position = 1, WordId = -1, WordText = marker });
        _db.Add(sentence);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.Sentences.AnyAsync(x => x.Text == marker));
        Assert.False(await _db.SentenceWords.AnyAsync(x => x.WordText == marker));
        // EF's savepoint rollback must preserve work preceding the failed save.
        Assert.True(await _db.Words.AnyAsync(x => x.Id == word.Id));
    }

    [SqlServerFact]
    public async Task Rowversion_is_generated_changes_on_update_and_rejects_stale_writes()
    {
        var sentence = NewSentence();
        _db.Add(sentence);
        await _db.SaveChangesAsync();
        var property = _db.Entry(sentence).Property<byte[]>("Version");
        var original = property.CurrentValue!.ToArray();
        Assert.Equal(8, original.Length);
        sentence.Text = "updated";
        await _db.SaveChangesAsync();
        Assert.False(original.SequenceEqual(property.CurrentValue!));
        property.OriginalValue = original;
        sentence.Text = "stale update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _db.SaveChangesAsync());
        _db.ChangeTracker.Clear();
        Assert.Equal("updated", (await _db.Sentences.SingleAsync(x => x.Id == sentence.Id)).Text);
    }

    [SqlServerFact]
    public async Task Seed_is_repeatable_without_duplicate_or_overwritten_records()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seed-vocabulary.sql"));
        await _db.Database.ExecuteSqlRawAsync(script);
        var types = await _db.WordTypes.CountAsync();
        var words = await _db.Words.CountAsync();
        Assert.True(types >= 9);
        Assert.True(words >= 27);
        await _db.Database.ExecuteSqlRawAsync(script);
        Assert.Equal(types, await _db.WordTypes.CountAsync());
        Assert.Equal(words, await _db.Words.CountAsync());
    }
}
