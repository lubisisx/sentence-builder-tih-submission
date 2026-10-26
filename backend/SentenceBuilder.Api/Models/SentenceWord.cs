namespace SentenceBuilder.Api.Models;

public sealed class SentenceWord
{
    public int SentenceId { get; set; }
    public int Position { get; set; }
    public int WordId { get; set; }
    public required string WordText { get; set; }
    public Sentence Sentence { get; set; } = null!;
    public Word Word { get; set; } = null!;
}
