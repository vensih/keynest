// Not a hardened security tool — intended for low-stakes credentials such as app logins,
// Wi-Fi codes, and developer tokens. Do not store banking credentials, private keys, or seed phrases.
//
// To port to a new platform:
//   1. Implement IVaultStorage to return the appropriate config directory.
//   2. Implement IOsCredentialVerifier to call the OS biometric/credential API.
//   3. Inject both into InitAsync() from the platform UI project.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Keynest.Core.Abstractions;

namespace Keynest.Core.Vault;

public sealed class VaultService
{
    private IVaultStorage? _storage;
    private IOsCredentialVerifier? _osVerifier;

    private byte[]? _fernetKey;
    private byte[]? _vsalt;
    private byte[]? _vhash;
    private readonly List<VaultEntry> _entries = new();
    private bool _unlocked;

    public bool IsLocked => !_unlocked;

    /// <summary>Creates the vault directory if missing. Must be called before any other method.</summary>
    public Task InitAsync(IVaultStorage storage, IOsCredentialVerifier osVerifier)
    {
        _storage = storage;
        _osVerifier = osVerifier;
        Directory.CreateDirectory(storage.GetVaultDirectory());
        return Task.CompletedTask;
    }

    /// <summary>Returns true if no vault has been created yet (vault.key absent).</summary>
    public Task<bool> IsFirstLaunchAsync()
        => Task.FromResult(!File.Exists(KeyPath));

    /// <summary>Creates a new vault with the given master password. Requires >= 8 chars.</summary>
    public async Task<VaultResult<IReadOnlyList<VaultEntry>>> CreateVaultAsync(string masterPassword)
    {
        if (masterPassword.Length < 8)
            return Fail<IReadOnlyList<VaultEntry>>("Master password must be at least 8 characters.");

        _fernetKey = CryptoHelper.GenerateFernetKey();
        _vsalt = RandomNumberGenerator.GetBytes(16);
        _vhash = CryptoHelper.DerivePbkdf2(masterPassword, _vsalt);
        _entries.Clear();
        _unlocked = true;

        await File.WriteAllTextAsync(KeyPath, Base64UrlEncode(_fernetKey), Encoding.ASCII);
        await FlushAsync();
        return Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly());
    }

    /// <summary>Loads and decrypts the vault. No master password required — viewing is always open.</summary>
    public async Task<VaultResult<IReadOnlyList<VaultEntry>>> LoadVaultAsync()
    {
        try
        {
            var keyRaw = (await File.ReadAllTextAsync(KeyPath, Encoding.ASCII)).Trim();
            _fernetKey = Base64UrlDecode(keyRaw);

            var json = await File.ReadAllTextAsync(VaultPath, Encoding.UTF8);
            var doc = JsonNode.Parse(json)!.AsObject();
            _vsalt = Convert.FromHexString(doc["vsalt"]!.GetValue<string>());
            _vhash = Convert.FromHexString(doc["vhash"]!.GetValue<string>());

            var plain = CryptoHelper.FernetDecrypt(_fernetKey, doc["entries"]!.GetValue<string>());
            var loaded = JsonSerializer.Deserialize<List<VaultEntry>>(plain, JsonOpts) ?? new();
            _entries.Clear();
            _entries.AddRange(loaded);
            _appLockEnabled = ((await GetSettingsAsync()).Value ?? new VaultSettings()).AppLocked;
            _appLocked = _appLockEnabled;
            return Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly());
        }
        catch (Exception ex)
        {
            return Fail<IReadOnlyList<VaultEntry>>(ex.Message);
        }
    }

    /// <summary>Verifies the master password and unlocks edit operations.</summary>
    public Task<VaultResult<bool>> UnlockAsync(string masterPassword)
    {
        if (_vsalt is null || _vhash is null)
            return Task.FromResult(Fail<bool>("Vault not loaded."));

        var derived = CryptoHelper.DerivePbkdf2(masterPassword, _vsalt);
        if (!CryptoHelper.FixedTimeEquals(derived, _vhash))
            return Task.FromResult(Fail<bool>("Incorrect password."));

        _unlocked = true;
        return Task.FromResult(Ok(true));
    }

    /// <summary>Locks the vault. Edit operations will be rejected until unlocked again.</summary>
    public Task<VaultResult<bool>> LockAsync()
    {
        _unlocked = false;
        return Task.FromResult(Ok(true));
    }

    /// <summary>Unlocks edit operations without password check. Only call when app lock has already verified identity.</summary>
    public VaultResult<bool> UnlockEdit()
    {
        _unlocked = true;
        return Ok(true);
    }

    /// <summary>Creates a new entry. Title must be non-empty. Vault must be unlocked.</summary>
    public async Task<VaultResult<IReadOnlyList<VaultEntry>>> CreateEntryAsync(
        string title, string username, string password, string shortcut)
    {
        if (!_unlocked) return Locked<IReadOnlyList<VaultEntry>>();
        title = title.Trim();
        if (string.IsNullOrEmpty(title)) return Fail<IReadOnlyList<VaultEntry>>("Title is required.");

        _entries.Add(new VaultEntry
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Username = username.Trim(),
            Password = password,
            Shortcut = shortcut.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await FlushAsync();
        return Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly());
    }

    /// <summary>Updates an existing entry by ID. Preserves Id and CreatedAt. Vault must be unlocked.</summary>
    public async Task<VaultResult<IReadOnlyList<VaultEntry>>> UpdateEntryAsync(
        string entryId, string title, string username, string password, string shortcut)
    {
        if (!_unlocked) return Locked<IReadOnlyList<VaultEntry>>();
        title = title.Trim();
        if (string.IsNullOrEmpty(title)) return Fail<IReadOnlyList<VaultEntry>>("Title is required.");

        var idx = _entries.FindIndex(e => e.Id == entryId);
        if (idx < 0) return Fail<IReadOnlyList<VaultEntry>>("Entry not found.");

        _entries[idx] = _entries[idx] with
        {
            Title = title,
            Username = username.Trim(),
            Password = password,
            Shortcut = shortcut.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await FlushAsync();
        return Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly());
    }

    /// <summary>Deletes an entry by ID. Vault must be unlocked.</summary>
    public async Task<VaultResult<IReadOnlyList<VaultEntry>>> DeleteEntryAsync(string entryId)
    {
        if (!_unlocked) return Locked<IReadOnlyList<VaultEntry>>();
        _entries.RemoveAll(e => e.Id == entryId);
        await FlushAsync();
        return Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly());
    }

    /// <summary>Case-insensitive substring search across Title, Username, Shortcut. Empty query returns all.</summary>
    public Task<VaultResult<IReadOnlyList<VaultEntry>>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(Ok<IReadOnlyList<VaultEntry>>(_entries.AsReadOnly()));

        var q = query.ToLowerInvariant();
        IReadOnlyList<VaultEntry> results = _entries
            .Where(e => e.Title.ToLowerInvariant().Contains(q)
                     || e.Username.ToLowerInvariant().Contains(q)
                     || e.Shortcut.ToLowerInvariant().Contains(q))
            .ToList()
            .AsReadOnly();
        return Task.FromResult(Ok(results));
    }

    /// <summary>Generates a cryptographically random password. Length clamped to [8, 128].</summary>
    public VaultResult<string> GeneratePassword(
        int length = 20, bool useUpper = true, bool useDigits = true, bool useSymbols = true)
    {
        length = Math.Clamp(length, 8, 128);
        var pool = "abcdefghijklmnopqrstuvwxyz";
        if (useUpper) pool += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (useDigits) pool += "0123456789";
        if (useSymbols) pool += "!@#$%^&*()-_=+[]{}|;:,.<>?";

        var buf = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = pool[buf[i] % pool.Length];
        return Ok(new string(chars));
    }

    /// <summary>Changes the master password after verifying the current one. Vault must be loaded.</summary>
    public async Task<VaultResult<bool>> ChangeMasterPasswordAsync(string current, string newPassword)
    {
        if (_vsalt is null || _vhash is null) return Fail<bool>("Vault not loaded.");
        if (!CryptoHelper.FixedTimeEquals(CryptoHelper.DerivePbkdf2(current, _vsalt), _vhash))
            return Fail<bool>("Incorrect current password.");
        if (newPassword.Length < 8) return Fail<bool>("New password must be at least 8 characters.");

        _vsalt = RandomNumberGenerator.GetBytes(16);
        _vhash = CryptoHelper.DerivePbkdf2(newPassword, _vsalt);
        await FlushAsync();
        return Ok(true);
    }

    /// <summary>Exports all entries as plaintext JSON. Vault must be unlocked.</summary>
    public Task<VaultResult<string>> ExportVaultAsync()
    {
        if (!_unlocked) return Task.FromResult(Locked<string>());
        var export = new
        {
            version = 1,
            exported_at = DateTimeOffset.UtcNow.ToString("O"),
            entries = _entries,
        };
        return Task.FromResult(Ok(JsonSerializer.Serialize(export, JsonOpts)));
    }

    /// <summary>Merges entries from a previously exported JSON. Deduplicates by ID. Vault must be unlocked.</summary>
    public async Task<VaultResult<ImportResult>> ImportVaultAsync(string json)
    {
        if (!_unlocked) return Locked<ImportResult>();
        try
        {
            var doc = JsonNode.Parse(json)!.AsObject();
            var incoming = doc["entries"]!.Deserialize<List<VaultEntry>>(JsonOpts) ?? new();
            var existing = new HashSet<string>(_entries.Select(e => e.Id));
            var toAdd = incoming.Where(e => !existing.Contains(e.Id)).ToList();
            _entries.AddRange(toAdd);
            await FlushAsync();
            return Ok(new ImportResult(toAdd.Count, _entries.AsReadOnly()));
        }
        catch (Exception ex)
        {
            return Fail<ImportResult>(ex.Message);
        }
    }

    /// <summary>Prompts for OS credentials (Windows Hello / biometrics). Graceful fallback if unsupported.</summary>
    public async Task<VaultResult<bool>> VerifyOsCredentialsAsync(string message = "Confirm your identity to reset the Keynest vault.")
    {
        if (_osVerifier is null) return Ok(true);
        try
        {
            var ok = await _osVerifier.VerifyAsync(message);
            return ok ? Ok(true) : Fail<bool>("Identity verification failed.");
        }
        catch
        {
            return Ok(true); // graceful fallback
        }
    }

    private bool _appLocked;
    private bool _appLockEnabled;
    public bool IsAppLocked => _appLocked;
    public bool IsAppLockEnabled => _appLockEnabled;

    /// <summary>Locks the entire app (and edit gate) and enables the persistent lock feature.</summary>
    public async Task<VaultResult<bool>> LockAppAsync()
    {
        _appLocked = true;
        _appLockEnabled = true;
        _unlocked = false;
        var settings = (await GetSettingsAsync()).Value ?? new VaultSettings();
        settings.AppLocked = true;
        await SaveSettingsAsync(settings);
        return Ok(true);
    }

    /// <summary>Verifies master password and unlocks the current session. Lock feature stays enabled.</summary>
    public Task<VaultResult<bool>> UnlockAppAsync(string masterPassword)
    {
        if (_vsalt is null || _vhash is null)
            return Task.FromResult(Fail<bool>("Vault not loaded."));

        var derived = CryptoHelper.DerivePbkdf2(masterPassword, _vsalt);
        if (!CryptoHelper.FixedTimeEquals(derived, _vhash))
            return Task.FromResult(Fail<bool>("Incorrect password."));

        _appLocked = false;
        return Task.FromResult(Ok(true));
    }

    /// <summary>Disables the persistent app lock feature entirely.</summary>
    public async Task<VaultResult<bool>> DisableAppLockAsync()
    {
        _appLocked = false;
        _appLockEnabled = false;
        var settings = (await GetSettingsAsync()).Value ?? new VaultSettings();
        settings.AppLocked = false;
        await SaveSettingsAsync(settings);
        return Ok(true);
    }

    /// <summary>Verifies OS credentials, then permanently deletes vault files and clears all state.</summary>
    public async Task<VaultResult<bool>> ResetVaultAsync()
    {
        var verify = await VerifyOsCredentialsAsync();
        if (!verify.Ok) return verify;

        if (File.Exists(KeyPath)) File.Delete(KeyPath);
        if (File.Exists(VaultPath)) File.Delete(VaultPath);

        _fernetKey = null; _vsalt = null; _vhash = null;
        _entries.Clear(); _unlocked = false;
        return Ok(true);
    }

    /// <summary>Loads settings from settings.json. Returns defaults if file is absent.</summary>
    public async Task<VaultResult<VaultSettings>> GetSettingsAsync()
    {
        var path = SettingsPath;
        if (!File.Exists(path)) return Ok(new VaultSettings());
        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return Ok(JsonSerializer.Deserialize<VaultSettings>(json, JsonOpts) ?? new VaultSettings());
        }
        catch { return Ok(new VaultSettings()); }
    }

    /// <summary>Saves settings to settings.json.</summary>
    public async Task<VaultResult<bool>> SaveSettingsAsync(VaultSettings settings)
    {
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(settings, JsonOpts), Encoding.UTF8);
        return Ok(true);
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task FlushAsync()
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(_entries, JsonOpts);
        var token = CryptoHelper.FernetEncrypt(_fernetKey!, plain);
        var vault = new
        {
            vsalt = Convert.ToHexString(_vsalt!).ToLowerInvariant(),
            vhash = Convert.ToHexString(_vhash!).ToLowerInvariant(),
            entries = token,
        };
        await File.WriteAllTextAsync(VaultPath, JsonSerializer.Serialize(vault, JsonOpts), Encoding.UTF8);
    }

    private string Dir => _storage!.GetVaultDirectory();
    private string KeyPath => Path.Combine(Dir, "vault.key");
    private string VaultPath => Path.Combine(Dir, "vault.json");
    private string SettingsPath => Path.Combine(Dir, "settings.json");

    private static VaultResult<T> Ok<T>(T value) => new(true, value);
    private static VaultResult<T> Fail<T>(string error) => new(false, Error: error);
    private static VaultResult<T> Locked<T>() => Fail<T>("Vault is locked. Unlock with master password first.");

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += new string('=', (4 - s.Length % 4) % 4);
        return Convert.FromBase64String(s);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };
}
