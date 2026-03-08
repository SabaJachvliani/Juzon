using Microsoft.AspNetCore.WebUtilities;
using System.Text.RegularExpressions;

namespace Juzon.Tools
{
    public static class YouTubeUrlValidator
    {
        private static readonly Regex VideoIdRegex =
            new(@"^[a-zA-Z0-9_-]{11}$", RegexOptions.Compiled);

        public static bool TryGetVideoId(string? url, out string? videoId)
        {
            videoId = null;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host.ToLowerInvariant();

            if (host.StartsWith("www."))
                host = host.Substring(4);

            if (host.StartsWith("m."))
                host = host.Substring(2);

            // youtu.be/<id>
            if (host == "youtu.be")
            {
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length == 1 && IsValidVideoId(segments[0]))
                {
                    videoId = segments[0];
                    return true;
                }

                return false;
            }

            // youtube.com/watch?v=<id>
            if (host == "youtube.com")
            {
                var path = uri.AbsolutePath.Trim('/').ToLowerInvariant();

                if (path == "watch")
                {
                    var query = QueryHelpers.ParseQuery(uri.Query);

                    if (query.TryGetValue("v", out var v) && IsValidVideoId(v.ToString()))
                    {
                        videoId = v.ToString();
                        return true;
                    }

                    return false;
                }

                // youtube.com/shorts/<id>
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length == 2 &&
                    (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
                     segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)) &&
                    IsValidVideoId(segments[1]))
                {
                    videoId = segments[1];
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool IsValidVideoId(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && VideoIdRegex.IsMatch(value);
        }
    }
}
