namespace AgrismartAPI.Application.Common;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class ValidationException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

/// <summary>An upstream dependency (e.g. a weather provider) failed or returned unusable data.</summary>
public class ExternalServiceException(string message) : Exception(message);
