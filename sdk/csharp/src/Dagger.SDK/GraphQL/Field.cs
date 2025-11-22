using System.Collections.Immutable;

namespace Dagger.GraphQL;

public class Field(string name, ImmutableList<Argument> args)
{
    public string Name { get; } = name;

    public ImmutableList<Argument> Args { get; } = args;
}
