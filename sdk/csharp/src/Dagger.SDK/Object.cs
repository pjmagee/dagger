using Dagger.GraphQL;

namespace Dagger;

public class Object(QueryBuilder queryBuilder, GraphQLClient gqlClient)
{
    public QueryBuilder QueryBuilder { get; } = queryBuilder;
    public GraphQLClient GraphQLClient { get; } = gqlClient;
}
