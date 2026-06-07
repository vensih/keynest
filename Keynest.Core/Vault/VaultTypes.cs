namespace Keynest.Core.Vault;

public sealed record VaultResult<T>(bool Ok, T? Value = default, string? Error = null);

public sealed record ImportResult(int Added, IReadOnlyList<VaultEntry> Entries);
