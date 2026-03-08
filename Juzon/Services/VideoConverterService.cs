using Juzon.Models;
using System.Diagnostics;

namespace Juzon.Services
{
    public sealed class VideoConverterService : IVideoConverterService
    {
        private readonly ILogger<VideoConverterService> _logger;

        public VideoConverterService(ILogger<VideoConverterService> logger)
        {
            _logger = logger;
        }

        public async Task<ConvertedFile> ConvertAsync(string url, string format, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Url is required.");

            format = format?.Trim().ToLowerInvariant() ?? "mp3";

            if (format is not ("mp3" or "mp4"))
                throw new ArgumentException("Only mp3 and mp4 are supported.");

            string tempFolder = Path.Combine(
                Path.GetTempPath(),
                "YoutubeConverterApi",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempFolder);

            string outputTemplate = Path.Combine(tempFolder, "%(title)s.%(ext)s");
            string toolsFolder = Path.Combine(AppContext.BaseDirectory, "Tools");
            string ytDlpPath = Path.Combine(toolsFolder, "yt-dlp.exe");
            string ffmpegPath = Path.Combine(toolsFolder, "ffmpeg.exe");
            string ffprobePath = Path.Combine(toolsFolder, "ffprobe.exe");

            if (!File.Exists(ytDlpPath))
                throw new FileNotFoundException("yt-dlp.exe not found.", ytDlpPath);

            if (!File.Exists(ffmpegPath))
                throw new FileNotFoundException("ffmpeg.exe was not found.", ffmpegPath);

            if (!File.Exists(ffprobePath))
                throw new FileNotFoundException("ffprobe.exe was not found.", ffprobePath);

            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempFolder
            };

            // Tell yt-dlp where ffmpeg and ffprobe are
            psi.ArgumentList.Add("--ffmpeg-location");
            psi.ArgumentList.Add(toolsFolder);

            // Important: never download playlists
            psi.ArgumentList.Add("--no-playlist");

            // Better output stability
            psi.ArgumentList.Add("--restrict-filenames");

            if (format == "mp3")
            {
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("bestaudio");

                psi.ArgumentList.Add("--extract-audio");
                psi.ArgumentList.Add("--audio-format");
                psi.ArgumentList.Add("mp3");

                psi.ArgumentList.Add("--audio-quality");
                psi.ArgumentList.Add("0");
            }
            else
            {
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("bv*+ba/b");

                psi.ArgumentList.Add("--merge-output-format");
                psi.ArgumentList.Add("mp4");
            }

            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(outputTemplate);

            psi.ArgumentList.Add(url);

            using var process = new Process { StartInfo = psi };

            process.Start();

            string stdOut = await process.StandardOutput.ReadToEndAsync(ct);
            string stdErr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "yt-dlp failed. ExitCode={ExitCode}. Url={Url}. Output={Output}. Error={Error}",
                    process.ExitCode, url, stdOut, stdErr);

                string errorText = (stdErr + Environment.NewLine + stdOut).ToLowerInvariant();

                if (errorText.Contains("unsupported url"))
                    throw new ArgumentException("Only valid YouTube video links are allowed.");

                if (errorText.Contains("video unavailable"))
                    throw new ArgumentException("This YouTube video is unavailable.");

                if (errorText.Contains("private video"))
                    throw new ArgumentException("This YouTube video is private.");

                if (errorText.Contains("sign in to confirm your age"))
                    throw new ArgumentException("This YouTube video is age-restricted and cannot be processed.");

                if (errorText.Contains("unable to extract"))
                    throw new ArgumentException("Could not read this YouTube video link.");

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stdErr) ? "Conversion failed." : stdErr);
            }

            string extension = format == "mp3" ? ".mp3" : ".mp4";

            string? file = Directory.GetFiles(tempFolder, $"*{extension}", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetCreationTimeUtc)
                .FirstOrDefault();

            if (file is null)
                throw new FileNotFoundException("Converted file was not found.");

            return new ConvertedFile
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                ContentType = format == "mp3" ? "audio/mpeg" : "video/mp4"
            };
        }
    }
}