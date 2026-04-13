using System.Text.Json;
using Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Startup;

/// <summary>
///     Formats exceptions thrown by the API into consistent HTTP responses.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    ) {
        var task = exception switch {
            AppException appException => WriteAppExceptionAsync(context, appException, cancellationToken),
            BadHttpRequestException badHttpRequestException => WriteBadRequestAsync(
                context,
                badHttpRequestException,
                cancellationToken
            ),
            JsonException jsonException => WriteMalformedJsonAsync(context, jsonException, cancellationToken),
            _ => WriteUnexpectedErrorAsync(context, exception, cancellationToken)
        };

        return new ValueTask<bool>(AwaitHandled(task));

        static async Task<bool> AwaitHandled(Task task) {
            await task;
            return true;
        }
    }

    private Task WriteAppExceptionAsync(
        HttpContext context,
        AppException exception,
        CancellationToken cancellationToken
    ) {
        var problemDetails = exception.Details;
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        LogException(context, exception, problemDetails, context.Response.StatusCode);

        return context.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), cancellationToken);
    }

    private Task WriteBadRequestAsync(
        HttpContext context,
        BadHttpRequestException exception,
        CancellationToken cancellationToken
    ) {
        var innerJsonException = exception.InnerException as JsonException;
        var invalidOperationException = innerJsonException?.InnerException as InvalidOperationException;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        var problemDetails = new ProblemDetails {
            Title = "Invalid request body",
            Type = "https://localhost:5000/errors/InvalidRequest",
            Status = StatusCodes.Status400BadRequest,
            Detail = invalidOperationException?.Message ?? exception.Message
        };

        LogException(context, exception, problemDetails, context.Response.StatusCode);

        return context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private Task WriteMalformedJsonAsync(
        HttpContext context,
        JsonException exception,
        CancellationToken cancellationToken
    ) {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";

        var problemDetails = new ProblemDetails {
            Title = "Malformed JSON",
            Type = "https://localhost:5000/errors/MalformedRequest",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message
        };

        var responseBody = new ExtendedProblemDetail(problemDetails, exception.Data);

        LogException(context, exception, responseBody, context.Response.StatusCode);

        return context.Response.WriteAsJsonAsync(responseBody, cancellationToken);
    }

    private Task WriteUnexpectedErrorAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    ) {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        const string responseBody = "Unexpected error";

        LogException(context, exception, responseBody, context.Response.StatusCode);

        return context.Response.WriteAsync(responseBody, cancellationToken);
    }

    private void LogException(HttpContext context, Exception exception, object responseBody, int statusCode) {
        var serializedResponseBody = JsonSerializer.Serialize(responseBody);

        if (statusCode >= StatusCodes.Status500InternalServerError) {
            logger.LogError(
                exception,
                "Unhandled exception returned {StatusCode}. Response body: {ResponseBody}",
                statusCode,
                serializedResponseBody
            );
            return;
        }

        logger.LogWarning(
            exception,
            "Request exception returned {StatusCode}. Response body: {ResponseBody}",
            statusCode,
            serializedResponseBody
        );
    }
}
