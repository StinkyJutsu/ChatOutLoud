using System;
using System.Threading.Tasks;
using System.Windows;

namespace ChatOutLoud;

public partial class AddManualUserWindow : Window
{
    private readonly Func<string, Task<ManualTwitchUserLookupResult?>>
        _lookupUserAsync;

    private ManualTwitchUserLookupResult? _foundUser;

    internal AddManualUserWindow(
        Func<string, Task<ManualTwitchUserLookupResult?>> lookupUserAsync)
    {
        InitializeComponent();

        _lookupUserAsync =
            lookupUserAsync;
    }

    public string? SelectedLogin { get; private set; }

    public string? SelectedDisplayName { get; private set; }

    private async void SearchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string login =
            UserLookupTextBox.Text.Trim();

        _foundUser = null;
        UserResultButton.Visibility =
            Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(login))
        {
            LookupStatusText.Text =
                "Enter a Twitch username.";

            return;
        }

        LookupStatusText.Text =
            "Searching Twitch...";

        try
        {
            ManualTwitchUserLookupResult? user =
                await _lookupUserAsync(login);

            if (user is null)
            {
                LookupStatusText.Text =
                    "No Twitch user found.";

                return;
            }

            _foundUser =
                user;

            UserResultButton.Content =
                string.Equals(
                    user.Login,
                    user.DisplayName,
                    StringComparison.OrdinalIgnoreCase)
                    ? user.DisplayName
                    : $"{user.DisplayName} (@{user.Login})";

            UserResultButton.Visibility =
                Visibility.Visible;

            LookupStatusText.Text =
                "Twitch user found. Click the result to add.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Twitch user lookup error: {ex}");

            LookupStatusText.Text =
                "Unable to find the Twitch user. Please try again.";

            MessageBox.Show(
                "The Twitch user lookup failed.\n\nCheck your internet connection and Twitch authorization, then try again.",
                "Chat Out Loud - Twitch Lookup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UserResultButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_foundUser is null)
        {
            return;
        }

        SelectedLogin =
            _foundUser.Login;

        SelectedDisplayName =
            _foundUser.DisplayName;

        DialogResult = true;
    }
}

internal sealed class ManualTwitchUserLookupResult
{
    public ManualTwitchUserLookupResult(
        string login,
        string displayName)
    {
        Login = login;
        DisplayName = displayName;
    }

    public string Login { get; }

    public string DisplayName { get; }
}