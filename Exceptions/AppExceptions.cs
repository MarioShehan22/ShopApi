namespace ShopApi.Exceptions;

/// <summary>Base class for errors we want mapped to a specific HTTP status.</summary>
public class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public class NotFoundException(string entity, object key)
    : AppException($"{entity} with id '{key}' was not found.", StatusCodes.Status404NotFound, "Resource not found");

public class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "Conflict");

public class BusinessRuleException(string message)
    : AppException(message, StatusCodes.Status422UnprocessableEntity, "Business rule violated");
