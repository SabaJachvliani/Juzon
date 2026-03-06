namespace Juzon.Models
{
    public sealed class ConvertedFile
    {
        public string FilePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = "application/octet-stream";
    }
}
