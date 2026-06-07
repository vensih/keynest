using Keynest.Windows.Services;
using Keynest.Windows.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Keynest.Windows.Views;

public sealed partial class EntriesPage : Page
{
    private MainViewModel _vm = null!;
    private int _unlockFailures;
    private bool _unlockInResetMode;
    private bool _pinned;
    private CancellationTokenSource? _searchCts;
    private bool _dialogOpen;

    // Segoe MDL2 Assets glyphs
    private const string GlyphLock    = "";
    private const string GlyphUnlock  = "";
    private const string GlyphEdit    = "";
    private const string GlyphDelete  = "";
    private const string GlyphOpen    = "";
    private const string GlyphEyeShow = "";
    private const string GlyphEyeHide = "";

    public EntriesPage() => InitializeComponent();

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _vm = (MainViewModel)e.Parameter;
        base.OnNavigatedTo(e);
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitialiseAsync();

        App.CurrentWindow!.Activated += Window_Activated;
        KeyDown += Page_KeyDown;

        var newAccel = new KeyboardAccelerator { Key = global::Windows.System.VirtualKey.N, Modifiers = global::Windows.System.VirtualKeyModifiers.Control };
        newAccel.Invoked += async (_, args) => { args.Handled = true; if (!_vm.IsLocked) await ShowEntryDialogAsync(null); };
        KeyboardAccelerators.Add(newAccel);

        var searchAccel = new KeyboardAccelerator { Key = global::Windows.System.VirtualKey.F, Modifiers = global::Windows.System.VirtualKeyModifiers.Control };
        searchAccel.Invoked += (_, args) => { args.Handled = true; SearchBox.Focus(FocusState.Keyboard); };
        KeyboardAccelerators.Add(searchAccel);

        if (_vm.IsFirstLaunch) { ShowSetupOverlay(); return; }
        if (_vm.IsAppLocked) { AppLockOverlay.Visibility = Visibility.Visible; _ = AppLockPw.Focus(FocusState.Programmatic); return; }
        ApplyLockState();
        RefreshList();
        _ = CheckForUpdateAsync();
    }

    // ── Update banner ────────────────────────────────────────────────────────

    private async Task CheckForUpdateAsync()
    {
        var newVersion = await UpdateChecker.CheckAsync();
        if (newVersion is null) return;
        UpdateBannerText.Text = $"Keynest {newVersion} is available.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void UpdateDownload_Click(object _, RoutedEventArgs __) => UpdateChecker.OpenReleasePage();
    private void UpdateDismiss_Click(object _, RoutedEventArgs __) => UpdateBanner.Visibility = Visibility.Collapsed;

    private async void Window_Activated(object _, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated) return;
        if (_vm.IsLocked || _vm.IsFirstLaunch || _vm.IsAppLocked || _dialogOpen) return;
        await _vm.LockAsync();
        ApplyLockState();
        RefreshList();
    }

    private void Page_KeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Escape) return;
        if (UnlockOverlay.Visibility == Visibility.Visible) { e.Handled = true; UnlockCancel_Click(null!, null!); }
    }

    // ── Setup ───────────────────────────────────────────────────────────────

    private void ShowSetupOverlay()
    {
        SetupPw1.Password = "";
        SetupPw2.Password = "";
        SetupError.Visibility = Visibility.Collapsed;
        SetupOverlay.Visibility = Visibility.Visible;
        _ = SetupPw1.Focus(FocusState.Programmatic);
    }

    private async void SetupCreate_Click(object _, RoutedEventArgs __)
    {
        SetupError.Visibility = Visibility.Collapsed;
        if (SetupPw1.Password.Length < 8)
        { SetupError.Text = "Password must be at least 8 characters."; SetupError.Visibility = Visibility.Visible; return; }
        if (SetupPw1.Password != SetupPw2.Password)
        { SetupError.Text = "Passwords do not match."; SetupError.Visibility = Visibility.Visible; return; }

        await _vm.CreateVaultAsync(SetupPw1.Password);
        SetupOverlay.Visibility = Visibility.Collapsed;
        ApplyLockState();
        RefreshList();
    }

    // ── Unlock ──────────────────────────────────────────────────────────────

    private void ShowUnlockOverlay()
    {
        _unlockInResetMode = false;
        UnlockPw.IsEnabled = true;
        UnlockPw.Password = "";
        UnlockError.Visibility = Visibility.Collapsed;
        UnlockSubmit.Content = "Unlock";
        UnlockOverlay.Visibility = Visibility.Visible;
        _ = UnlockPw.Focus(FocusState.Programmatic);
    }

    private void UnlockPw_KeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter) UnlockSubmit_Click(null!, null!);
    }

    private async void UnlockSubmit_Click(object _, RoutedEventArgs __)
    {
        if (_unlockInResetMode) { await DoResetAsync(); return; }

        var error = await _vm.UnlockAsync(UnlockPw.Password);
        UnlockPw.Password = "";

        if (error is null)
        {
            _unlockFailures = 0;
            UnlockOverlay.Visibility = Visibility.Collapsed;
            ApplyLockState();
            RefreshList();
            return;
        }

        _unlockFailures++;
        if (_unlockFailures >= 5)
        {
            _unlockInResetMode = true;
            UnlockPw.IsEnabled = false;
            UnlockError.Text = "Too many failed attempts. You can reset the vault.";
            UnlockError.Visibility = Visibility.Visible;
            UnlockSubmit.Content = "Reset vault";
        }
        else
        {
            int remaining = 5 - _unlockFailures;
            UnlockError.Text = $"Incorrect password. {remaining} attempt{(remaining == 1 ? "" : "s")} left before vault reset.";
            UnlockError.Visibility = Visibility.Visible;
        }
    }

    private void UnlockCancel_Click(object _, RoutedEventArgs __)
    {
        _unlockFailures = 0;
        _unlockInResetMode = false;
        UnlockOverlay.Visibility = Visibility.Collapsed;
    }

    // ── FABs ────────────────────────────────────────────────────────────────

    private async void FabLock_Click(object _, RoutedEventArgs __)
    {
        if (_vm.IsLocked)
        {
            if (_vm.IsAppLockEnabled)
                _vm.UnlockEdit();
            else
                ShowUnlockOverlay();
            ApplyLockState();
            RefreshList();
            return;
        }
        await _vm.LockAsync();
        ApplyLockState();
        RefreshList();
    }

    private async void FabAdd_Click(object _, RoutedEventArgs __)  => await ShowEntryDialogAsync(null);
    private async void FabSettings_Click(object _, RoutedEventArgs __) => await ShowSettingsDialogAsync();

    // ── Pin ─────────────────────────────────────────────────────────────────

    private void PinBtn_Click(object _, RoutedEventArgs __)
    {
        _pinned = !_pinned;
        PinIcon.Foreground = _pinned
            ? Res<Brush>("AccentTextFillColorPrimaryBrush")
            : Res<Brush>("TextFillColorPrimaryBrush");

        if (App.CurrentWindow?.OverlappedPresenter is OverlappedPresenter p)
            p.IsAlwaysOnTop = _pinned;
    }

    // ── Search ──────────────────────────────────────────────────────────────

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs _)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var cts = _searchCts;
        try
        {
            await Task.Delay(150, cts.Token);
            await _vm.SearchAsync(sender.Text);
            RefreshList();
        }
        catch (OperationCanceledException) { }
    }

    private async void SearchBox_QuerySubmitted(AutoSuggestBox _, AutoSuggestBoxQuerySubmittedEventArgs __)
    {
        await _vm.SearchAsync(SearchBox.Text);
        RefreshList();
    }

    // ── Settings dialog ──────────────────────────────────────────────────────

    private async Task ShowSettingsDialogAsync()
    {
        var panel = new StackPanel { Spacing = 4 };
        var lockAppLabel = _vm.IsAppLockEnabled ? "Unlock App" : "Lock App";
        var lockAppBtn  = MenuBtn("", lockAppLabel);
        var exportBtn   = MenuBtn("", "Export Vault");
        var importBtn   = MenuBtn("", "Import Vault");
        var changePwBtn = MenuBtn("", "Change Password");
        panel.Children.Add(lockAppBtn);
        panel.Children.Add(new MenuFlyoutSeparator());
        panel.Children.Add(exportBtn);
        panel.Children.Add(importBtn);
        panel.Children.Add(new MenuFlyoutSeparator());
        panel.Children.Add(changePwBtn);

        string action = "";
        var dlg = new ContentDialog
        {
            Title = "Vault Settings", Content = panel,
            PrimaryButtonText = "Done", XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
        };
        lockAppBtn.Click  += (_, __) => { action = "lockapp";  dlg.Hide(); };
        exportBtn.Click   += (_, __) => { action = "export";   dlg.Hide(); };
        importBtn.Click   += (_, __) => { action = "import";   dlg.Hide(); };
        changePwBtn.Click += (_, __) => { action = "changepw"; dlg.Hide(); };
        await ShowDialogAsync(dlg);

        switch (action)
        {
            case "changepw": await ShowChangePwDialogAsync(); break;
            case "export":   await DoExportAsync();           break;
            case "import":   await DoImportAsync();           break;
            case "lockapp":  await DoToggleAppLockAsync();     break;
        }
    }

    private async Task DoToggleAppLockAsync()
    {
        if (_vm.IsAppLockEnabled)
            await _vm.DisableAppLockAsync();
        else
            await DoLockAppAsync();
    }

    private static Button MenuBtn(string glyph, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(10, 8, 10, 8),
        };
    }

    // ── Change password dialog ───────────────────────────────────────────────

    private async Task ShowChangePwDialogAsync()
    {
        while (true)
        {
            var cur  = new PasswordBox { PlaceholderText = "Current password" };
            var next = new PasswordBox { PlaceholderText = "New password (min. 8 characters)" };
            var conf = new PasswordBox { PlaceholderText = "Confirm new password" };
            var err  = ErrLabel();
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(cur); panel.Children.Add(next); panel.Children.Add(conf); panel.Children.Add(err);

            var dlg = new ContentDialog
            {
                Title = "Change master password", Content = panel,
                PrimaryButtonText = "Update", CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            };
            if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return;

            if (next.Password.Length < 8)       { ShowErr(err, "New password must be at least 8 characters."); continue; }
            if (next.Password != conf.Password)  { ShowErr(err, "Passwords do not match.");                    continue; }

            var error = await _vm.ChangeMasterPasswordAsync(cur.Password, next.Password);
            if (error is null) return;
            ShowErr(err, error);
        }
    }

    // ── Export / Import ──────────────────────────────────────────────────────

    private async Task DoExportAsync()
    {
        var json = await _vm.ExportVaultAsync();
        if (json is null) return;

        var picker = new FileSavePicker
        {
            SuggestedFileName = $"keynest-export-{DateTime.Now:yyyy-MM-dd}",
            DefaultFileExtension = ".json",
        };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await FileIO.WriteTextAsync(file, json);
    }

    private async Task DoImportAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var json = await FileIO.ReadTextAsync(file);
        (int added, string? importError) = await _vm.ImportVaultAsync(json);

        RefreshList();
        var importDlg = new ContentDialog
        {
            Title = "Import",
            Content = importError is null
                ? $"{added} entr{(added == 1 ? "y" : "ies")} added."
                : $"Import failed: {importError}",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await ShowDialogAsync(importDlg);
    }

    // ── Add / Edit entry dialog ──────────────────────────────────────────────

    private async Task ShowEntryDialogAsync(EntryViewModel? existing)
    {
        while (true)
        {
            var titleBox    = new TextBox    { PlaceholderText = "e.g. Gmail",           Text     = existing?.Title    ?? "" };
            var userBox     = new TextBox    { PlaceholderText = "user@example.com",      Text     = existing?.Username ?? "" };
            var pwBox       = new PasswordBox{ PlaceholderText = "Password",              Password = existing?.Password ?? "" };
            var shortcutBox = new TextBox    { PlaceholderText = "https://example.com",   Text     = existing?.Shortcut ?? "" };
            var err         = ErrLabel();
            var dupWarn     = new TextBlock
            {
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xFF, 0xA5, 0x00)),
                FontSize = 12, Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            var pwRow = new Grid();
            pwRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pwRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(pwBox, 0);
            var genBtn = new HyperlinkButton { Content = "Generate", VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 0, 0) };
            genBtn.Click += (_, __) => pwBox.Password = _vm.GeneratePassword();
            Grid.SetColumn(genBtn, 1);
            pwRow.Children.Add(pwBox); pwRow.Children.Add(genBtn);

            var panel = new StackPanel { Spacing = 4, MinWidth = 300 };
            panel.Children.Add(Cap("Title / Service"));      panel.Children.Add(titleBox);
            panel.Children.Add(Cap("Username / Email"));     panel.Children.Add(userBox);
            panel.Children.Add(Cap("Password"));             panel.Children.Add(pwRow);
            panel.Children.Add(Cap("URL / Path"));           panel.Children.Add(shortcutBox);
            panel.Children.Add(dupWarn);
            panel.Children.Add(err);

            var dlg = new ContentDialog
            {
                Title = existing is null ? "New Entry" : "Edit Entry",
                Content = panel,
                PrimaryButtonText = existing is null ? "Add" : "Update",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (existing is null)
            {
                dlg.PrimaryButtonClick += (_, args) =>
                {
                    var dup = _vm.Entries.FirstOrDefault(x =>
                        x.Title.Equals(titleBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (dup is not null && dupWarn.Visibility == Visibility.Collapsed)
                    {
                        dupWarn.Text = $"\"{dup.Title}\" already exists. Click Add again to confirm.";
                        dupWarn.Visibility = Visibility.Visible;
                        args.Cancel = true;
                    }
                };
            }
            if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return;

            var error = existing is null
                ? await _vm.CreateEntryAsync(titleBox.Text, userBox.Text, pwBox.Password, shortcutBox.Text)
                : await _vm.UpdateEntryAsync(existing.Id, titleBox.Text, userBox.Text, pwBox.Password, shortcutBox.Text);

            if (error is null) { RefreshList(); return; }
            ShowErr(err, error);
        }
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    private async Task ConfirmDeleteAsync(EntryViewModel entry)
    {
        var dlg = new ContentDialog
        {
            Title = "Delete entry?",
            Content = $"\"{entry.Title}\" will be permanently deleted. This cannot be undone.",
            PrimaryButtonText = "Delete", CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot,
        };
        if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return;
        await _vm.DeleteEntryAsync(entry.Id);
        RefreshList();
    }

    // ── App lock ─────────────────────────────────────────────────────────────

    private async Task DoLockAppAsync()
    {
        await _vm.LockAppAsync();
    }

    private void AppLockPw_KeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter) AppUnlockBtn_Click(null!, null!);
    }

    private async void AppUnlockBtn_Click(object _, RoutedEventArgs __)
    {
        AppLockError.Visibility = Visibility.Collapsed;
        var error = await _vm.UnlockAppAsync(AppLockPw.Password);
        AppLockPw.Password = "";
        if (error is null)
        {
            AppLockOverlay.Visibility = Visibility.Collapsed;
            ApplyLockState();
            RefreshList();
            _ = CheckForUpdateAsync();
        }
        else
        {
            AppLockError.Text = error;
            AppLockError.Visibility = Visibility.Visible;
        }
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    private async Task DoResetAsync()
    {
        // Dismiss the unlock overlay before opening a ContentDialog — only one can be open at a time
        UnlockOverlay.Visibility = Visibility.Collapsed;

        var dlg = new ContentDialog
        {
            Title = "Reset vault?",
            Content = "This will permanently delete all entries and your master password. This cannot be undone.",
            PrimaryButtonText = "Reset", CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot,
        };
        if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return;
        if (!await _vm.ResetVaultAsync()) return;

        _unlockFailures = 0; _unlockInResetMode = false;
        UnlockOverlay.Visibility = Visibility.Collapsed;
        CardList.Children.Clear();
        EmptyState.Visibility = Visibility.Collapsed;
        ShowSetupOverlay();
    }

    // ── State & list ─────────────────────────────────────────────────────────

    private void ApplyLockState()
    {
        var locked = _vm.IsLocked;
        FabAdd.Visibility      = locked ? Visibility.Collapsed : Visibility.Visible;
        FabSettings.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
        FabLockIcon.Glyph      = locked ? GlyphLock : GlyphUnlock;
        ToolTipService.SetToolTip(FabLock, locked ? "Unlock Vault" : "Lock Vault");
        EmptyStateHint.Text    = locked ? "Unlock to add your first entry." : "Tap + to add your first entry.";
    }

    private void RefreshList()
    {
        CardList.Children.Clear();
        foreach (var e in _vm.Entries) CardList.Children.Add(BuildCard(e));
        EmptyState.Visibility = _vm.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Card builder ─────────────────────────────────────────────────────────

    private UIElement BuildCard(EntryViewModel e)
    {
        var title = new TextBlock
        {
            Text = e.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 0);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        if (!string.IsNullOrEmpty(e.Shortcut))
            btns.Children.Add(IconBtn(GlyphOpen, () => OpenShortcut(e.Shortcut)));
        if (!_vm.IsLocked)
        {
            btns.Children.Add(IconBtn(GlyphEdit, async () => await ShowEntryDialogAsync(e)));
            var del = IconBtn(GlyphDelete, async () => await ConfirmDeleteAsync(e));
            del.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xFA, 0x52, 0x52));
            btns.Children.Add(del);
        }
        Grid.SetColumn(btns, 1);

        var header = new Grid { Padding = new Thickness(14, 11, 10, 11) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(title); header.Children.Add(btns);

        var sep = new Border { Height = 1, Background = new SolidColorBrush(ColorHelper.FromArgb(0x18, 0, 0, 0)) };

        var body = new StackPanel { Spacing = 2, Padding = new Thickness(6, 4, 10, 8) };
        body.Children.Add(FieldRow(e.Username));
        body.Children.Add(PasswordRow(e));

        var inner = new StackPanel();
        inner.Children.Add(header); inner.Children.Add(sep); inner.Children.Add(body);

        var card = new Border { CornerRadius = new CornerRadius(12) };
        if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bg))
            card.Background = bg as Brush;
        card.Child = inner;
        return card;
    }

    private static UIElement FieldRow(string value)
    {
        var empty = string.IsNullOrEmpty(value);
        var label = new TextBlock
        {
            Text = empty ? "—" : value,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Res<Brush>(empty ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush"),
        };

        if (empty) return new Border { Child = label, Padding = new Thickness(12, 4, 6, 4) };

        var btn = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = new CornerRadius(4),
        };
        ToolTipService.SetToolTip(btn, "copy");
        btn.Click += (_, __) => Copy(value);
        return btn;
    }

    private UIElement PasswordRow(EntryViewModel e)
    {
        var displayText = new TextBlock { Text = "••••••••", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var copyBtn = new Button
        {
            Content = displayText,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = new CornerRadius(4),
        };
        ToolTipService.SetToolTip(copyBtn, "copy");
        copyBtn.Click += (_, __) => Copy(e.Password);

        var eyeIcon = new FontIcon { Glyph = GlyphEyeShow, FontSize = 13 };
        var eyeBtn = new ToggleButton
        {
            Width = 32, Height = 32,
            CornerRadius = new CornerRadius(6),
            Content = eyeIcon,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(0),
        };
        eyeBtn.Checked   += (_, __) => { displayText.Text = e.Password; eyeIcon.Glyph = GlyphEyeHide; };
        eyeBtn.Unchecked += (_, __) => { displayText.Text = "••••••••"; eyeIcon.Glyph = GlyphEyeShow; };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(copyBtn, 0); Grid.SetColumn(eyeBtn, 1);
        row.Children.Add(copyBtn); row.Children.Add(eyeBtn);
        return row;
    }

    private static Button IconBtn(string glyph, Action onClick)
    {
        var btn = new Button
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
            Content = new FontIcon { Glyph = glyph, FontSize = 13 },
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(0),
        };
        btn.Click += (_, __) => onClick();
        return btn;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dlg)
    {
        _dialogOpen = true;
        try { return await dlg.ShowAsync(); }
        finally { _dialogOpen = false; }
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static TextBlock Cap(string text) => new()
    {
        Text = text, FontSize = 12,
        Foreground = Res<Brush>("TextFillColorSecondaryBrush"),
        Margin = new Thickness(0, 6, 0, 2),
    };

    private static TextBlock ErrLabel() => new()
    {
        Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xFA, 0x52, 0x52)),
        FontSize = 12, Visibility = Visibility.Collapsed,
        TextWrapping = TextWrapping.WrapWholeWords,
    };

    private static void ShowErr(TextBlock lbl, string msg)
    {
        lbl.Text = msg;
        lbl.Visibility = Visibility.Visible;
    }

    private static void Copy(string text)
    {
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }

    private static async void OpenShortcut(string shortcut)
    {
        try
        {
            if (shortcut.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                shortcut.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                await global::Windows.System.Launcher.LaunchUriAsync(new Uri(shortcut));
            else
            {
                var file = await StorageFile.GetFileFromPathAsync(shortcut);
                await global::Windows.System.Launcher.LaunchFileAsync(file);
            }
        }
        catch { }
    }

    private static IntPtr GetHwnd() => WindowNative.GetWindowHandle(App.CurrentWindow!);
}
