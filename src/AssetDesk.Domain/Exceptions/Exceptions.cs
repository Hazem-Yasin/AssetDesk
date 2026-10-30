namespace AssetDesk.Domain.Exceptions;

/// <summary>A business rule was violated (HTTP 409).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>The requested record does not exist (HTTP 404).</summary>
public class NotFoundException(string entity, object key) : Exception($"{entity} '{key}' was not found.");

/// <summary>Credentials were missing or wrong (HTTP 401).</summary>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>The caller is known but not allowed to perform the action (HTTP 403).</summary>
public class ForbiddenException(string message) : Exception(message);
