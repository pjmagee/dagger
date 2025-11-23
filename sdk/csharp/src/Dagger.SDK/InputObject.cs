using Dagger.GraphQL;

namespace Dagger;

public interface IInputObject
{
    List<KeyValuePair<string, Value>> ToKeyValuePairs();
}
