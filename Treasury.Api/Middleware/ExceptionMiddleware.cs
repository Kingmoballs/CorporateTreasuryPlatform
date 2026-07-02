using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Treasury.Api.Models;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    private readonly ILogger<ExceptionMiddleware>
        _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await HandleException(
                context,
                exception);
        }
    }

    private async Task HandleException(
        HttpContext context,
        Exception exception)
    {
        var details =
            GetExceptionDetails(exception);

        if (details.StatusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled error. Trace ID: {TraceId}",
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request failed with {Code}. " +
                "Trace ID: {TraceId}",
                details.Code,
                context.TraceIdentifier);
        }

        context.Response.Clear();

        context.Response.StatusCode =
            details.StatusCode;

        context.Response.ContentType =
            "application/json";

        var response = new ApiErrorResponse
        {
            Code = details.Code,

            Message = details.Message,

            TraceId =
                context.TraceIdentifier,

            Errors = details.Errors
        };

        await context.Response
            .WriteAsJsonAsync(response);
    }

    private static ExceptionDetails
        GetExceptionDetails(
            Exception exception)
    {
        return exception switch
        {
            FluentValidation.ValidationException
                validationException =>
                    new ExceptionDetails(
                        StatusCodes
                            .Status400BadRequest,
                        "validation_error",
                        "One or more validation " +
                        "errors occurred.",
                        MapValidationErrors(
                            validationException)),

            RequestValidationException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status400BadRequest,
                    "invalid_request",
                    exception.Message),

            ArgumentException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status400BadRequest,
                    "invalid_request",
                    exception.Message),

            UnauthorizedAccessException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status401Unauthorized,
                    "authentication_failed",
                    exception.Message),

            ForbiddenOperationException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status403Forbidden,
                    "operation_forbidden",
                    exception.Message),

            ResourceNotFoundException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status404NotFound,
                    "resource_not_found",
                    exception.Message),

            KeyNotFoundException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status404NotFound,
                    "resource_not_found",
                    exception.Message),

            ConflictException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status409Conflict,
                    "conflict",
                    exception.Message),

            DbUpdateConcurrencyException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status409Conflict,
                    "concurrency_conflict",
                    "The resource changed while " +
                    "the request was processing."),

            BusinessRuleException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status422UnprocessableEntity,
                    "business_rule_violation",
                    exception.Message),

            InvalidOperationException =>
                new ExceptionDetails(
                    StatusCodes
                        .Status422UnprocessableEntity,
                    "invalid_operation",
                    exception.Message),

            _ =>
                new ExceptionDetails(
                    StatusCodes
                        .Status500InternalServerError,
                    "internal_error",
                    "An unexpected error occurred.")
        };
    }

    private static IReadOnlyDictionary<
        string,
        string[]>
        MapValidationErrors(
            FluentValidation.ValidationException
                exception)
    {
        return exception.Errors
            .GroupBy(error =>
                error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error =>
                        error.ErrorMessage)
                    .Distinct()
                    .ToArray());
    }

    private sealed record ExceptionDetails(
        int StatusCode,
        string Code,
        string Message,
        IReadOnlyDictionary<string, string[]>?
            Errors = null);
}