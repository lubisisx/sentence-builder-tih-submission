namespace SentenceBuilder.Api.Models;

public sealed class Sentence
{
    public int Id { get; set; }
    public required string Text { get; set; }
    // Persist UTC values. Timestamp generation belongs to the future application service.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<SentenceWord> SentenceWords { get; set; } = new List<SentenceWord>();
}
