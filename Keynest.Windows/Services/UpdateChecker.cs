using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Keynest.Windows.Services;

internal static class UpdateChecker
{
    internal const string CurrentVersion = "0.2.1-alpha";

    private const string ApiUrl = "https://api.github.com/repos/vensi/keynest/releases/latest";
    private const string ReleasesUrl = "https://github.com/vensi/keynest/releases/latest";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
        DefaultRequestHeaders = { { "User-Agent", "Keynest/" + CurrentVersion } }
    };

    // Returns the newer version string if one exists, null if up-to-date or check failed.
    internal static async Task<string?> CheckAsync()
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GhRelease>(ApiUrl);
            if (release?.TagName is null) return null;

            var remote = Version.Parse(release.TagName.TrimStart('v'));
            var current = Version.Parse(CurrentVersion);
            return remote > current ? remote.ToString() : null;
        }
        catch
        {
            return null; // network error, rate limit, etc. — fail silently
        }
    }

    internal static async void OpenReleasePage() =>
        await global::Windows.System.Launcher.LaunchUriAsync(new Uri(ReleasesUrl));

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; init; }
    }
}
