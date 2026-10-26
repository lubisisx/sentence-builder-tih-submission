namespace SentenceBuilder.Api.Models;

public sealed class WordType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Word> Words { get; set; } = new List<Word>();
}
