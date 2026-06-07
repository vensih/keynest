<div align="center">

# Keynest

**A minimal, offline desktop password manager**

*by Vensi*

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=flat-square&logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-22c55e?style=flat-square)

</div>

---

Keynest is a no-frills password vault that runs entirely on your machine. No cloud sync. No accounts. No telemetry. Every credential you store is encrypted on disk and never transmitted anywhere.

The app is pure C# — a platform-agnostic `Keynest.Core` library handles all crypto and storage, and a `Keynest.Windows` WinUI 3 project provides the native Windows UI.

> **Note:** This is not a hardened security tool. It is intended for low-stakes credentials like app logins, Wi-Fi codes, and developer tokens. Do not use it for banking credentials, private keys, seed phrases, or anything where a breach would be catastrophic.

---

## Features

| | |
|---|---|
| **Offline-first** | No network dependency. Everything runs locally. |
| **Encrypted at rest** | Entries are encrypted with AES-128 Fernet using a randomly generated key stored only on your machine. |
| **View without a password** | The vault opens automatically on launch so you can browse entries freely. The master password only gates add, edit, and delete. |
| **App Lock** | Optionally lock the entire app on next launch, requiring your Master Password to get in. |
| **Auto-lock** | The edit gate locks automatically when the app loses focus. |
| **One-click copy** | Copy a username or password to the clipboard in a single click. |
| **Keyboard shortcuts** | Ctrl+N for new entry, Ctrl+F to search, Escape to dismiss overlays. |
| **Export and import** | Export your vault to a JSON file you control. Import merges entries by UUID so duplicates are skipped. |
| **Vault reset** | Wiping the vault requires verifying your identity through Windows Hello so it cannot be done accidentally. |
| **Native Windows UI** | Built with WinUI 3 and the Windows App SDK. No Electron, no browser runtime. |

---

## Getting started

**Requires the .NET 10 SDK only.**

Open `Keynest.sln` in Visual Studio 2022 and press F5, or build from the command line:

```bash
dotnet build -c Debug
dotnet run --project Keynest.Windows -c Debug
```

On first launch you will be prompted to set a Master Password. This creates `vault.key` and `vault.json` in `%APPDATA%\Keynest\`. From that point on the vault opens automatically on startup.

---

## Usage

### First launch

On first run, a setup screen asks you to choose a Master Password (minimum 8 characters). Confirm it and click **Create vault**. This generates your encryption key and creates the vault files in `%APPDATA%\Keynest\`.

### Browsing entries

The vault opens automatically every time you launch the app — no password required to read. Each entry card shows the title, username, and a masked password. Click the **username** or **password** to copy it to the clipboard instantly. Toggle the eye icon on a password to reveal it on screen.

If an entry has a shortcut URL or file path, an open icon appears on its card. Clicking it launches the URL in your default browser or opens the file.

Use the **search bar** at the top or press **Ctrl+F** to filter entries by title, username, or shortcut.

### Locking and unlocking

The lock button (bottom-right FAB) toggles the edit gate:

- **Locked** — you can browse and copy, but the add and settings buttons are hidden and edit/delete icons are not shown on cards.
- **Unlocked** — full editing access. Click the lock button again to re-engage the gate.

The edit gate also locks automatically whenever the app loses focus.

> After **5 consecutive wrong attempts**, the password field is disabled and the button changes to **Reset vault**.

### App Lock

App Lock secures the entire app behind your Master Password on next launch, not just the edit gate.

To enable it, open **Vault Settings** and tap **Lock App**. The next time you open Keynest, a lock screen will appear before anything is accessible. Enter your Master Password to continue.

To disable it, open **Vault Settings** and tap **Unlock App**.

When App Lock is active, the FAB lock button toggles editing freely within the session — you already proved your identity at launch.

### Adding and editing entries

With the vault unlocked:

- Click **+** (bottom FAB) or press **Ctrl+N** to add a new entry. Fill in the title (required), username, password, and an optional shortcut URL or file path.
- Click **Generate** next to the password field to fill it with a cryptographically random password.
- If you enter a title that already exists, a warning appears. Click **Add** a second time to confirm.
- To edit an existing entry, click the pencil icon on its card. To delete it, click the red trash icon and confirm.

### Vault Settings

Click the settings button (bottom FAB, only visible when unlocked) to access:

| Option | What it does |
|--------|--------------|
| **Lock App / Unlock App** | Toggle the persistent app lock feature. |
| **Export Vault** | Saves all entries as a plaintext JSON file you choose. |
| **Import Vault** | Picks a previously exported JSON file and merges entries by UUID. Duplicates are skipped. |
| **Change Password** | Verify your current Master Password, then set a new one. |

### Always-on-top

Click the pin icon in the top-right corner to keep the Keynest window above all other windows. Click again to unpin.

### Vault reset

If you forget your Master Password and exhaust all 5 unlock attempts, click **Reset vault**. This triggers a Windows Hello prompt (PIN, fingerprint, or face). Passing it permanently deletes `vault.key` and `vault.json` and returns you to the first-launch setup screen. **All stored entries are lost and cannot be recovered.**

---

## How it works

### Architecture

Keynest is split into two projects:

**`Keynest.Core`** — a platform-agnostic C# class library. It contains all crypto (`CryptoHelper`), vault logic (`VaultService`), and the two platform interfaces (`IVaultStorage`, `IOsCredentialVerifier`). It has no UI dependency and no platform-specific code.

**`Keynest.Windows`** — the WinUI 3 application. It implements `IVaultStorage` and `IOsCredentialVerifier` for Windows, wires them into `VaultService`, and handles all UI using an MVVM pattern built on CommunityToolkit.Mvvm.

```
Keynest.Windows (WinUI 3 / MVVM)
    |
    |  direct in-process calls
    v
Keynest.Core
    VaultService  ->  CryptoHelper  (AES-128-CBC, HMAC-SHA256, PBKDF2)
    IVaultStorage        implemented by WindowsVaultStorage
    IOsCredentialVerifier  implemented by WindowsCredentialVerifier
```

Because `Keynest.Core` has no platform dependency, porting to a new platform (macOS, Linux) only requires implementing those two interfaces in a new UI project.

### Security model

**Encryption key:** On first launch, a random 32-byte key is generated and written to `vault.key`. This key is loaded from disk on every startup and used to decrypt the entry blob automatically. It never leaves the machine. If `vault.key` is lost, the vault is unrecoverable.

**Master Password:** The Master Password is never stored in plaintext. A random 16-byte salt is generated at vault creation and the password is hashed using PBKDF2-SHA256 with 480,000 iterations. The hash and salt live inside `vault.json`. On every unlock attempt, the entered password is re-derived with the stored salt and compared using a constant-time equality check. The Master Password does not affect encryption or decryption — it only gates whether edit operations are permitted.

**App Lock:** When enabled, the app lock state is persisted to `settings.json`. On next launch, a lock screen is shown before the vault is accessible. The lock is lifted by verifying the Master Password. Disabling App Lock removes the flag from `settings.json`.

**Fernet format:** Entries are encrypted with a C# implementation of the Fernet token format (AES-128-CBC + HMAC-SHA256, compatible with Python's `cryptography` library). HMAC is verified before decryption.

**Edit gate:** The vault is always readable after startup. To add, edit, or delete an entry, the user must unlock with the Master Password. The edit gate locks automatically when the app loses focus, and re-locking does not re-encrypt anything.

**Vault reset:** Resetting deletes `vault.key` and `vault.json` permanently. Before doing so, `WindowsCredentialVerifier` triggers a Windows Hello prompt. If the user cannot pass the OS credential check, the reset is blocked.

### Storage

All data lives in `%APPDATA%\Keynest\` on Windows.

| File | Contents |
|------|----------|
| `vault.key` | Raw 32-byte Fernet encryption key (base64url) |
| `vault.json` | PBKDF2 salt, PBKDF2 hash, and the Fernet-encrypted entry blob |
| `settings.json` | App preferences including App Lock state |

The entry blob inside `vault.json` is a Fernet-encrypted JSON array. Each entry carries a stable UUID, title, username, password, an optional shortcut URL or file path, and ISO-8601 created/updated timestamps.

---

## Project structure

```
Keynest.Core/
  Abstractions/
    IVaultStorage.cs          # Resolve the vault directory for any platform
    IOsCredentialVerifier.cs  # Trigger an OS credential/biometric prompt
  Vault/
    VaultService.cs           # Full public API: CRUD, lock/unlock, app lock, export/import, reset
    CryptoHelper.cs           # Fernet (AES-128-CBC + HMAC-SHA256) and PBKDF2
    VaultEntry.cs             # Entry record type
    VaultTypes.cs             # VaultResult<T>, ImportResult
    VaultSettings.cs          # Settings model (app lock state, etc.)

Keynest.Windows/
  Services/
    WindowsVaultStorage.cs       # IVaultStorage -> %APPDATA%\Keynest\
    WindowsCredentialVerifier.cs # IOsCredentialVerifier -> Windows Hello
    UpdateChecker.cs             # GitHub release update checks
  ViewModels/
    MainViewModel.cs             # ObservableObject, vault state, app lock wrappers
    EntryViewModel.cs            # Per-entry view model
  Views/
    EntriesPage.xaml(.cs)        # Main entries view, all overlays and dialogs
  App.xaml.cs                    # App lifecycle, VaultService init
  MainWindow.xaml.cs             # Root window
```

---

## Tech stack

| Layer | Technology |
|-------|------------|
| UI | [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) (Windows App SDK 2.0) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) 8.3 |
| Crypto | `System.Security.Cryptography` — AES-128-CBC, HMAC-SHA256, PBKDF2-SHA256 |
| Format | Fernet token (compatible with Python `cryptography`) |

---

## License

[MIT](LICENSE) - Copyright (c) 2026 Vensi
