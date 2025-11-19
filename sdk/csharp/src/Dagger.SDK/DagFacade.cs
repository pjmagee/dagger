using System;
using Dagger.SDK.GraphQL;

namespace Dagger.SDK;

/// <summary>
/// Provides access to the root Dagger query.
/// </summary>
public static class Dagger
{
    private static readonly Lazy<Query> _query = new(
        () => new Query(QueryBuilder.Builder(), new GraphQLClient())
    );

    /// <summary>
    /// Gets a singleton <see cref="Query"/> instance for building DAG operations.
    /// </summary>
    public static Query Dag => _query.Value;
}