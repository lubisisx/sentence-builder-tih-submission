using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SentenceBuilder.Api.Models;
using SentenceBuilder.Api.Persistence;

namespace SentenceBuilder.Tests.Persistence;

public sealed class PersistenceModelTests
{
    private static SentenceBuilderDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SentenceBuilderDbContext>()
            .UseSqlServer("Server=localhost;Database=TIH_ModelOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void Model_maps_only_the_four_requested_entities()
    {
        using var db = CreateContext();
        Assert.Equal(4, db.Model.GetEntityTypes().Count());
        Assert.Equal(50, db.Model.FindEntityType(typeof(WordType))!.FindProperty(nameof(WordType.Name))!.GetMaxLength());
        Assert.Equal(100, db.Model.FindEntityType(typeof(Word))!.FindProperty(nameof(Word.Value))!.GetMaxLength());
        Assert.Equal(100, db.Model.FindEntityType(typeof(SentenceWord))!.FindProperty(nameof(SentenceWord.WordText))!.GetMaxLength());
        Assert.Equal(10099, db.Model.FindEntityType(typeof(Sentence))!.FindProperty(nameof(Sentence.Text))!.GetMaxLength());
    }

    [Fact]
    public void Sentence_positions_form_the_composite_key_and_words_may_repeat()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SentenceWord))!;
        Assert.Equal(new[] { "SentenceId", "Position" }, entity.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.DoesNotContain(entity.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == "WordId"));
    }

    [Fact]
    public void Snapshot_and_vocabulary_are_required_and_version_is_database_generated()
    {
        using var db = CreateContext();
        Assert.False(db.Model.FindEntityType(typeof(SentenceWord))!.FindProperty("WordText")!.IsNullable);
        Assert.False(db.Model.FindEntityType(typeof(Word))!.FindProperty("Value")!.IsNullable);
        var version = db.Model.FindEntityType(typeof(Sentence))!.FindProperty("Version")!;
        Assert.True(version.IsShadowProperty());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    [Fact]
    public void Migration_snapshot_matches_current_model()
    {
        using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Single(db.Database.GetMigrations());
    }

    [Fact]
    public void Offline_migration_script_contains_relational_constraints_and_no_seed()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [SentenceWord]", sql);
        Assert.Contains("PRIMARY KEY ([SentenceId], [Position])", sql);
        Assert.Contains("ON DELETE CASCADE", sql);
        Assert.Contains("CK_SentenceWord_Position", sql);
        Assert.Contains("CK_Sentence_Text_Length", sql);
        Assert.Contains("[Version] rowversion NOT NULL", sql);
        Assert.Contains("CREATE UNIQUE INDEX [IX_WordType_Name]", sql);
        Assert.DoesNotContain("INSERT INTO [WordType]", sql);
    }
}
