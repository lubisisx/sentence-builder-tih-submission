namespace SentenceBuilder.Api.Models;

public sealed class Word
{
    public int Id { get; set; }
    public int WordTypeId { get; set; }
    public required string Value { get; set; }
    public WordType WordType { get; set; } = null!;
    public ICollection<SentenceWord> SentenceWords { get; set; } = new List<SentenceWord>();
}
