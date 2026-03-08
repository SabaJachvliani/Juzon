using Juzon.Exceptions;
using Juzon.Models;
using Juzon.Services;
using Juzon.Tools;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddScoped<IVideoConverterService, VideoConverterService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "Too many requests. Please try again later."
        }, cancellationToken: token);
    };

    options.AddPolicy("convert-policy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Juzon v1");
    });
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();



app.MapPost("/convert", async (
    ConvertRequest request,
    IVideoConverterService service,
    CancellationToken ct) =>
{
    if (request is null)
        return Results.BadRequest("Request body is required.");

    if (string.IsNullOrWhiteSpace(request.Url))
        return Results.BadRequest("Url is required.");

    var format = request.Format?.Trim().ToLowerInvariant();

    if (format is not ("mp3" or "mp4"))
        return Results.BadRequest("Format must be mp3 or mp4.");

    if (!YouTubeUrlValidator.TryGetVideoId(request.Url, out var videoId))
    {
        return Results.BadRequest(
            "Please provide a valid YouTube video link. Example: https://www.youtube.com/watch?v=VIDEO_ID");
    }

    var cleanUrl = $"https://www.youtube.com/watch?v={videoId}";

    var result = await service.ConvertAsync(cleanUrl, format, ct);

    var stream = new FileStream(
        result.FilePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read);

    return Results.File(
        stream,
        contentType: result.ContentType,
        fileDownloadName: result.FileName);
})
.RequireRateLimiting("convert-policy");

app.Run();