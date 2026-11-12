using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MasteringModernCSharp.Ch09;

public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);

// Turns domain exceptions into RFC 9457 problem responses.
public sealed class DomainExceptionHandler(
    IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (404, "Not found"),
            ConflictException => (409, "Conflict"),
            _ => (0, ""),
        };
        if (status == 0) return false; // Let the default handler run.

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
            },
        });
    }
}

public static class ErrorHandling
{
    public static IServiceCollection AddErrorHandling(
        this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = ctx =>
                // Every error carries an ID to search the logs for.
                ctx.ProblemDetails.Extensions["traceId"] =
                    ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<DomainExceptionHandler>();
        return services;
    }

    public static void MapErrorDemo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/{id:int}", (int id) => id == 42
            ? Results.Ok(new { id })
            : throw new NotFoundException($"Order {id} doesn't exist."));
        // A lambda that only throws needs an explicit return type.
        app.MapPost("/orders/{id:int}/cancel", IResult (int id) =>
            throw new ConflictException(
                "Shipped orders can't be cancelled."));
        app.MapGet("/boom", IResult () =>
            throw new InvalidOperationException("Secret internals"));
    }
}
