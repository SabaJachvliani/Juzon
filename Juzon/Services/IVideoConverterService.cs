using Juzon.Models;

namespace Juzon.Services
{
    public interface IVideoConverterService
    {
        Task<ConvertedFile> ConvertAsync(string url, string format, CancellationToken ct);
    }
}
