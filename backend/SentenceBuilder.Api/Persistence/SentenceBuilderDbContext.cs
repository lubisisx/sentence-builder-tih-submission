using Microsoft.EntityFrameworkCore;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Persistence;

public sealed class SentenceBuilderDbContext(DbContextOptions<SentenceBuilderDbContext> options)
    : DbContext(options)
{
    public DbSet<WordType> WordTypes => Set<WordType>();
    public DbSet<Word> Words => Set<Word>();
    public DbSet<Sentence> Sentences => Set<Sentence>();
    public DbSet<SentenceWord> SentenceWords => Set<SentenceWord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SentenceBuilderDbContext).Assembly);
}
