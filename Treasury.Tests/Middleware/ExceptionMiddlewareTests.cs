using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Treasury.Api.Middleware;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task ConflictException_Returns409()
    {
        var logger =
            new Mock<
                ILogger<ExceptionMiddleware>>();

        var middleware =
            new ExceptionMiddleware(
                _ => throw new ConflictException(
                    "Account balance changed."),
                logger.Object);

        var context =
            new DefaultHttpContext();

        context.Response.Body =
            new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        var response =
            await JsonSerializer.DeserializeAsync<
                ErrorResponse>(
                    context.Response.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });

        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);

        Assert.NotNull(response);

        Assert.Equal(
            "conflict",
            response!.Code);

        Assert.Equal(
            "Account balance changed.",
            response.Message);
    }

    [Fact]
    public async Task UnknownException_HidesInternalMessage()
    {
        var logger =
            new Mock<
                ILogger<ExceptionMiddleware>>();

        var middleware =
            new ExceptionMiddleware(
                _ => throw new Exception(
                    "Sensitive database details"),
                logger.Object);

        var context =
            new DefaultHttpContext();

        context.Response.Body =
            new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        using var reader =
            new StreamReader(
                context.Response.Body);

        var body =
            await reader.ReadToEndAsync();

        Assert.Equal(
            StatusCodes
                .Status500InternalServerError,
            context.Response.StatusCode);

        Assert.DoesNotContain(
            "Sensitive database details",
            body);
    }

    private sealed class ErrorResponse
    {
        public string Code { get; set; }
            = string.Empty;

        public string Message { get; set; }
            = string.Empty;
    }
}