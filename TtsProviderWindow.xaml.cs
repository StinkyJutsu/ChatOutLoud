using ChatOutLoud.TtsProviders;
using ChatOutLoud.TtsProviders.Providers;
using Microsoft.Win32;
using System.Windows;

namespace ChatOutLoud;

public partial class TtsProviderWindow : Window
{
    private readonly GoogleCloudTtsProvider _googleProvider =
        (GoogleCloudTtsProvider)(
            TtsProviderManager.Shared.GetProvider(
                "google-cloud")
            ?? throw new InvalidOperationException(
                "Google Cloud TTS provider is not registered."));

    public TtsProviderWindow()
    {
        InitializeComponent();

        UpdateGoogleStatusUi();
    }

    private void GoogleConfigureButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ProviderListView.Visibility =
            Visibility.Collapsed;

        GoogleSetupView.Visibility =
            Visibility.Visible;
    }

    private void GoogleBackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        GoogleSetupView.Visibility =
            Visibility.Collapsed;

        ProviderListView.Visibility =
            Visibility.Visible;
    }

    private void GoogleChooseCredentialButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenFileDialog dialog =
            new()
            {
                Title =
                    "Choose Google Cloud credentials",

                Filter =
                    "Google credential files (*.json)|*.json|All files (*.*)|*.*",

                CheckFileExists =
                    true,

                Multiselect =
                    false
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        GoogleCredentialPathTextBox.Text =
            dialog.FileName;
    }

    private async void GoogleVerifyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string credentialPath =
            GoogleCredentialPathTextBox.Text;

        if (string.IsNullOrWhiteSpace(
                credentialPath) ||
            !System.IO.File.Exists(
                credentialPath))
        {
            GoogleSetupStatusText.Text =
                "Choose a Google credentials file first.";

            return;
        }

        GoogleVerifyButton.IsEnabled =
            false;

        GoogleSetupStatusText.Text =
            "Checking...";

        try
        {
            string credentialJson =
                await System.IO.File.ReadAllTextAsync(
                    credentialPath);

            bool connected =
                await _googleProvider.ConfigureAsync(
                    credentialJson);

            if (connected)
            {
                GoogleSetupStatusText.Text =
                    "Connected";

                GoogleStatusText.Text =
                    "Connected";
            }
            else
            {
                GoogleSetupStatusText.Text =
                    "Needs attention";

                GoogleStatusText.Text =
                    "Needs attention";
            }
        }
        catch
        {
            GoogleSetupStatusText.Text =
                "Connection failed. Check the credential file and Google Cloud TTS access.";

            GoogleStatusText.Text =
                "Needs attention";
        }
        finally
        {
            GoogleVerifyButton.IsEnabled =
                true;
        }
    }

    private void GoogleRemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmationDialog dialog =
            new(
                "Remove Google Cloud TTS",
                "Remove the saved Google Cloud TTS connection and credentials?")
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _googleProvider.RemoveConfiguration();

        GoogleCredentialPathTextBox.Text =
            "";

        UpdateGoogleStatusUi();
    }

    private void GoogleHelpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        GoogleCloudHelpWindow dialog =
            new()
            {
                Owner = this
            };

        dialog.ShowDialog();
    }

    private void UpdateGoogleStatusUi()
    {
        string statusText =
            _googleProvider.Status switch
            {
                TtsProviderStatus.Configured =>
                    "Configured",

                TtsProviderStatus.Connected =>
                    "Connected",

                TtsProviderStatus.Checking =>
                    "Checking...",

                TtsProviderStatus.NeedsAttention =>
                    "Needs attention",

                _ =>
                    "Not configured"
            };

        GoogleStatusText.Text =
            statusText;

        GoogleSetupStatusText.Text =
            statusText;
    }
}