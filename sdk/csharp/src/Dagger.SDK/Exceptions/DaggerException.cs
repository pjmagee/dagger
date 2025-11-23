using Dagger.GraphQL;

namespace Dagger.Exceptions;

/// <summary>
/// Base exception for all Dagger-related errors.
/// </summary>
public class DaggerException : Exception
{
    public DaggerException(string message)
        : base(message) { }

    public DaggerException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a GraphQL query returns an error.
/// </summary>
public class QueryException : DaggerException
{
    /// <summary>
    /// Gets the GraphQL errors returned by the server.
    /// </summary>
    public IReadOnlyList<GraphQLError> Errors { get; }

    /// <summary>
    /// Gets the GraphQL query that caused the error.
    /// </summary>
    public string Query { get; }

    public QueryException(string message, IReadOnlyList<GraphQLError> errors, string query)
        : base(message)
    {
        Errors = errors;
        Query = query;
    }

    public QueryException(
        string message,
        IReadOnlyList<GraphQLError> errors,
        string query,
        Exception innerException
    )
        : base(message, innerException)
    {
        Errors = errors;
        Query = query;
    }
}

/// <summary>
/// Exception thrown when an exec operation fails.
/// </summary>
public class ExecException : QueryException
{
    /// <summary>
    /// Gets the command that was executed.
    /// </summary>
    public IReadOnlyList<string> Command { get; }

    /// <summary>
    /// Gets the exit code of the command.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets the stdout output of the command.
    /// </summary>
    public string Stdout { get; }

    /// <summary>
    /// Gets the stderr output of the command.
    /// </summary>
    public string Stderr { get; }

    public ExecException(
        string message,
        IReadOnlyList<GraphQLError> errors,
        string query,
        IReadOnlyList<string> command,
        int exitCode,
        string stdout,
        string stderr
    )
        : base(message, errors, query)
    {
        Command = command;
        ExitCode = exitCode;
        Stdout = stdout;
        Stderr = stderr;
    }

    public override string ToString()
    {
        var cmdStr = string.Join(" ", Command);
        return $"{Message}\nCommand: {cmdStr}\nExit Code: {ExitCode}\nStdout: {Stdout}\nStderr: {Stderr}";
    }
}
