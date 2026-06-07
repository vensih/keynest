using CommunityToolkit.Mvvm.ComponentModel;
using Keynest.Core.Vault;

namespace Keynest.Windows.ViewModels;

public partial class EntryViewModel : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _shortcut = "";
    [ObservableProperty] private DateTimeOffset _createdAt;
    [ObservableProperty] private DateTimeOffset _updatedAt;

    public static EntryViewModel FromEntry(VaultEntry e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Username = e.Username,
        Password = e.Password,
        Shortcut = e.Shortcut,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
