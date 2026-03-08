using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Juzon.Exceptions
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);

            var (statusCode, title, detail) = exception switch
            {
                ArgumentException => (
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    exception.Message),

                FileNotFoundException => (
                    StatusCodes.Status404NotFound,
                    "Not Found",
                    exception.Message),

                InvalidOperationException => (
                    StatusCodes.Status500InternalServerError,
                    "Conversion Error",
                    exception.Message),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Server Error",
                    "An unexpected error occurred.")
            };

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}