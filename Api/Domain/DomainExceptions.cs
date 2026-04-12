using System.Collections;
using Microsoft.AspNetCore.Mvc;

namespace Api.Domain;

/// <summary>
///     Extends problem detail responses with additional structured data.
/// </summary>
public class ExtendedProblemDetail : ProblemDetails
{
    public ExtendedProblemDetail(ProblemDetails problemDetails, IDictionary errors) {
        Title = problemDetails.Title;
        Type = problemDetails.Type;
        Instance = problemDetails.Instance;
        Status = problemDetails.Status;
        Detail = problemDetails.Detail;
        Errors = errors;
    }

    public IDictionary Errors { get; set; }
}

/// <summary>
///     Provides a base exception type that carries API problem details.
/// </summary>
public abstract class AppException : Exception
{
    public ProblemDetails Details;

    public AppException(string message, ProblemDetails details) : base(message) {
        Details = details;
    }
}

public class UnauthorizedException() : AppException(
    "Unauthorized",
    new ProblemDetails {
        Title = "Unauthorized",
        Type = $"https://localhost:5000/errors/{nameof(UnauthorizedException)}",
        Status = StatusCodes.Status401Unauthorized
    }
);

public class InvalidFormatException(string message, string? detail) : AppException(
    message,
    new ProblemDetails {
        Title = message,
        Type = $"https://localhost:5000/errors/{nameof(InvalidFormatException)}",
        Status = StatusCodes.Status400BadRequest,
        Detail = detail
    }
);

public class ForbiddenActionException(string message, string? detail) : AppException(
    message,
    new ProblemDetails {
        Title = message,
        Type = $"https://localhost:5000/errors/{nameof(ForbiddenActionException)}",
        Status = StatusCodes.Status403Forbidden,
        Detail = detail
    }
);

public class EntityNotFoundException(string message, string? detail) : AppException(
    message,
    new ProblemDetails {
        Title = message,
        Type = $"https://localhost:5000/errors/{nameof(EntityNotFoundException)}",
        Status = StatusCodes.Status404NotFound,
        Detail = detail
    }
);

public class UserAlreadyMemberException() : AppException(
    "User already member",
    new ProblemDetails {
        Title = "User already member",
        Type = $"https://localhost:5000/errors/{nameof(UserAlreadyMemberException)}",
        Status = StatusCodes.Status400BadRequest,
        Detail = "Cannot add member to workspace they are already member of"
    }
);

public class TooManyLoginAttemptsException(string message, string? detail) : AppException(
    message,
    new ProblemDetails {
        Title = message,
        Type = $"https://localhost:5000/errors/{nameof(TooManyLoginAttemptsException)}",
        Status = StatusCodes.Status429TooManyRequests,
        Detail = detail
    }
);
