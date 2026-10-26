using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentenceBuilder.Api.Models;

namespace SentenceBuilder.Api.Persistence.Configurations;

internal sealed class WordTypeConfiguration : IEntityTypeConfiguration<WordType>
{
    public void Configure(EntityTypeBuilder<WordType> builder)
    {
        builder.ToTable("WordType", table => table.HasCheckConstraint("CK_WordType_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class WordConfiguration : IEntityTypeConfiguration<Word>
{
    public void Configure(EntityTypeBuilder<Word> builder)
    {
        builder.ToTable("Word", table => table.HasCheckConstraint("CK_Word_Value_NotBlank", "LEN(LTRIM(RTRIM([Value]))) > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(100);
        builder.HasOne(x => x.WordType).WithMany(x => x.Words).HasForeignKey(x => x.WordTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.WordTypeId, x.Value }).IsUnique();
    }
}

internal sealed class SentenceConfiguration : IEntityTypeConfiguration<Sentence>
{
    public void Configure(EntityTypeBuilder<Sentence> builder)
    {
        builder.ToTable("Sentence", table =>
        {
            table.HasCheckConstraint("CK_Sentence_Text_NotBlank", "LEN(LTRIM(RTRIM([Text]))) > 0");
            table.HasCheckConstraint("CK_Sentence_Timestamps", "[UpdatedAt] >= [CreatedAt]");
            table.HasCheckConstraint("CK_Sentence_Text_Length", "DATALENGTH([Text]) <= 20198");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(10099);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property<byte[]>("Version").IsRequired().IsRowVersion();
        builder.HasIndex(x => new { x.CreatedAt, x.Id }).IsDescending(true, true);
    }
}

internal sealed class SentenceWordConfiguration : IEntityTypeConfiguration<SentenceWord>
{
    public void Configure(EntityTypeBuilder<SentenceWord> builder)
    {
        builder.ToTable("SentenceWord", table =>
        {
            table.HasCheckConstraint("CK_SentenceWord_Position", "[Position] >= 0");
            table.HasCheckConstraint("CK_SentenceWord_WordText_NotBlank", "LEN(LTRIM(RTRIM([WordText]))) > 0");
        });
        builder.HasKey(x => new { x.SentenceId, x.Position });
        builder.Property(x => x.Position).ValueGeneratedNever();
        builder.Property(x => x.WordText).IsRequired().HasMaxLength(100);
        builder.HasOne(x => x.Sentence).WithMany(x => x.SentenceWords).HasForeignKey(x => x.SentenceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Word).WithMany(x => x.SentenceWords).HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.WordId);
    }
}
