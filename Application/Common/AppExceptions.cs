namespace AgrismartAPI.Application.Common;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }

    /// <param name="message">Summary shown to users that only read <c>message</c>.</param>
    /// <param name="errors">Field-level messages keyed by camelCase field path, e.g. "soil.fertility".</param>
    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }

    /// <summary>Field-level messages, when the caller can point at specific fields. Null otherwise.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}

public class ForbiddenException(string message) : Exception(message);

/// <summary>An upstream dependency (e.g. a weather provider) failed or returned unusable data.</summary>
public class ExternalServiceException(string message) : Exception(message);
