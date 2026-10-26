namespace SentenceBuilder.Api.Services;

// Routine validation and missing resources are values, not exceptions.
public sealed record OperationResult<T>(T? Value, int Status, Dictionary<string, string[]>? Errors = null)
{
    public static OperationResult<T> Success(T value) => new(value, 200);
    public static OperationResult<T> Invalid(string field, string message) => new(default, 400, new() { [field] = [message] });
    public static OperationResult<T> Missing() => new(default, 404);
}
