using Juzon.Models;
using Juzon.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<IVideoConverterService, VideoConverterService>();

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok("YoutubeConverterApi is running."));

app.MapPost("/convert", async (
    ConvertRequest request,
    IVideoConverterService service,
    CancellationToken ct) =>
{
    if (request is null)
        return Results.BadRequest("Request body is required.");

    if (string.IsNullOrWhiteSpace(request.Url))
        return Results.BadRequest("Url is required.");

    if (request.Format is not ("mp3" or "mp4"))
        return Results.BadRequest("Format must be mp3 or mp4.");

    try
    {
        var result = await service.ConvertAsync(request.Url, request.Format, ct);

        var stream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Results.File(
            stream,
            contentType: result.ContentType,
            fileDownloadName: result.FileName);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();
