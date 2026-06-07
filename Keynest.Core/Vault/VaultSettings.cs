using System.Text.Json.Serialization;

namespace Keynest.Core.Vault;

public sealed class VaultSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "auto";

    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; } = false;

    [JsonPropertyName("app_locked")]
    public bool AppLocked { get; set; } = false;
}
