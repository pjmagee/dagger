namespace Dagger.GraphQL;

public class Argument(string key, Value value)
{
    public string Key { get; } = key;
    private Value Value { get; } = value;

    public Task<string> FormatValue(CancellationToken cancellationToken = default) =>
        Value.FormatAsync(cancellationToken);
}
