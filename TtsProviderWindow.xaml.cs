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

    private readonly ElevenLabsTtsProvider _elevenLabsProvider =
        (ElevenLabsTtsProvider)(
            TtsProviderManager.Shared.GetProvider(
                "elevenlabs")
            ?? throw new InvalidOperationException(
                "ElevenLabs TTS provider is not registered."));

    private readonly AzureSpeechTtsProvider _azureProvider =
        (AzureSpeechTtsProvider)(
            TtsProviderManager.Shared.GetProvider(
                "microsoft-azure")
            ?? throw new InvalidOperationException(
                "Microsoft Azure Speech provider is not registered."));

    public TtsProviderWindow()
    {
        InitializeComponent();

        UpdateGoogleStatusUi();
        UpdateElevenLabsStatusUi();
        UpdateAzureStatusUi();
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
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Google Cloud TTS verification failed: {ex.GetType().Name}");

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
        SectionHelpWindow helpWindow =
            new(
                "Connect Google Cloud TTS",
                "Follow these steps to connect your own Google Cloud account to Chat Out Loud.",
                new[]
                {
                    new SectionHelpItem(
                        "IMPORTANT — THIRD-PARTY SERVICE AND BILLING",
                        "Google Cloud Text-to-Speech is a separate third-party service and is not included with Chat Out Loud. You use your own Google Cloud account, project, billing account, and credentials. Google may charge you directly according to your Google Cloud account, usage, and current pricing. Chat Out Loud does not provide Google Cloud credits, pay Google fees, reimburse usage charges, or control Google Cloud billing. Review Google Cloud Text-to-Speech pricing and billing before connecting the service."),

                    new SectionHelpItem(
                        "BEFORE YOU START",
                        "You need a Google account, a Google Cloud project, billing enabled for that project, the Cloud Text-to-Speech API enabled, and permission to create a service account and service-account key."),

                    new SectionHelpItem(
                        "1. OPEN GOOGLE CLOUD",
                        "Go to console.cloud.google.com and sign in with the Google account that will own the Text-to-Speech project."),

                    new SectionHelpItem(
                        "2. CREATE OR SELECT A PROJECT",
                        "Use the project selector at the top of Google Cloud Console. Select an existing project or choose New Project. A name such as Chat Out Loud TTS makes the project easy to recognize."),

                    new SectionHelpItem(
                        "3. ENABLE BILLING",
                        "Google Cloud Text-to-Speech requires billing to be enabled on the selected project. Link an existing billing account or create one when Google prompts you. Charges depend on actual Google Cloud usage and the applicable Google pricing and quotas."),

                    new SectionHelpItem(
                        "4. ENABLE CLOUD TEXT-TO-SPEECH API",
                        "In the Google Cloud Console search bar, search for Cloud Text-to-Speech API. Open it, confirm the correct project is selected, and click Enable."),

                    new SectionHelpItem(
                        "5. CREATE A SERVICE ACCOUNT",
                        "Open IAM & Admin, then Service Accounts. Click Create Service Account. Give it a recognizable name such as chat-out-loud and finish creating the account."),

                    new SectionHelpItem(
                        "6. CREATE THE JSON CREDENTIAL KEY",
                        "Open the service account you created. Select the Keys tab. Choose Add Key, then Create new key. Select JSON and click Create. Google downloads the credential JSON file to your computer. Google does not allow that same private key file to be downloaded again later, so keep it secure."),

                    new SectionHelpItem(
                        "7. STORE THE JSON FILE SAFELY",
                        "Treat the JSON file like a password. Do not upload it to GitHub, post it online, email it publicly, or share it with other users. Store the original file somewhere private that you control."),

                    new SectionHelpItem(
                        "8. ADD THE CREDENTIAL TO CHAT OUT LOUD",
                        "Return to Chat Out Loud, open Manage TTS Providers, select Google Cloud TTS, and click Configure. Click Choose File and select the Google service-account JSON file you downloaded."),

                    new SectionHelpItem(
                        "9. VERIFY THE CONNECTION",
                        "Click Verify Connection. Chat Out Loud validates the credential, authenticates with Google Cloud, and confirms that Google voices can be retrieved. If verification succeeds, the status changes to Connected."),

                    new SectionHelpItem(
                        "10. AUTOMATIC SECURE STORAGE",
                        "After successful verification, Chat Out Loud automatically stores an encrypted copy of the Google credential for the current Windows user. There is no separate Save button."),

                    new SectionHelpItem(
                        "11. WHAT HAPPENS TO THE ORIGINAL JSON?",
                        "After Chat Out Loud has successfully verified and saved the credential, Chat Out Loud does not depend on the original JSON file remaining at that exact path. You may move or rename the original file. Keep it somewhere secure in case you later remove the Chat Out Loud connection and need to reconnect."),

                    new SectionHelpItem(
                        "12. CONFIGURED VS. CONNECTED",
                        "Connected means Chat Out Loud has successfully contacted Google during the current verification. Configured means a previously verified credential is securely stored locally. Chat Out Loud does not contact Google merely because the program starts or because you open the provider window."),

                    new SectionHelpItem(
                        "13. REMOVE CONNECTION",
                        "Remove Connection deletes Chat Out Loud's encrypted saved Google credential and cached Google voice information. It does not delete credentials for another TTS provider and it does not delete the key from your Google Cloud account."),

                    new SectionHelpItem(
                        "14. RECONNECT LATER",
                        "If you remove the connection, choose the JSON file again and run Verify Connection. If the original JSON was lost or its key was deleted or revoked in Google Cloud, create a new JSON key from the service account's Keys page.")
                })
            {
                Owner = this
            };

        helpWindow.ShowDialog();
    }
    private void ElevenLabsConfigureButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ProviderListView.Visibility =
            Visibility.Collapsed;

        ElevenLabsSetupView.Visibility =
            Visibility.Visible;

        UpdateElevenLabsStatusUi();
    }

    private void ElevenLabsBackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ElevenLabsSetupView.Visibility =
            Visibility.Collapsed;

        ProviderListView.Visibility =
            Visibility.Visible;

        UpdateElevenLabsStatusUi();
    }

    private void AzureConfigureButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ProviderListView.Visibility =
            Visibility.Collapsed;

        AzureSetupView.Visibility =
            Visibility.Visible;

        UpdateAzureStatusUi();
    }

    private void AzureBackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        AzureSetupView.Visibility =
            Visibility.Collapsed;

        ProviderListView.Visibility =
            Visibility.Visible;

        UpdateAzureStatusUi();
    }

    private async void AzureVerifyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string subscriptionKey =
            AzureSpeechKeyPasswordBox.Password.Trim();

        string region =
            AzureRegionTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                subscriptionKey))
        {
            AzureSetupStatusText.Text =
                "Enter your Microsoft Azure Speech resource key first.";

            return;
        }

        if (string.IsNullOrWhiteSpace(
                region))
        {
            AzureSetupStatusText.Text =
                "Enter your Microsoft Azure Speech resource region first.";

            return;
        }

        AzureVerifyButton.IsEnabled =
            false;

        AzureSetupStatusText.Text =
            "Checking...";

        AzureStatusText.Text =
            "Checking...";

        try
        {
            bool connected =
                await _azureProvider.ConfigureAsync(
                    subscriptionKey,
                    region);

            if (connected)
            {
                AzureSetupStatusText.Text =
                    "Connected";

                AzureStatusText.Text =
                    "Connected";

                AzureSpeechKeyPasswordBox.Password =
                    "";
            }
            else
            {
                AzureSetupStatusText.Text =
                    "Needs attention";

                AzureStatusText.Text =
                    "Needs attention";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Azure Speech verification failed: {ex.GetType().Name}");

            AzureSetupStatusText.Text =
                "Connection failed. Check the Azure Speech resource key, region, internet connection, and Speech resource access.";

            AzureStatusText.Text =
                "Needs attention";
        }
        finally
        {
            AzureVerifyButton.IsEnabled =
                true;
        }
    }

    private void AzureRemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmationDialog dialog =
            new(
                "Remove Microsoft Azure Speech",
                "Remove the saved Microsoft Azure Speech connection and credentials?")
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _azureProvider.RemoveConfiguration();

        AzureSpeechKeyPasswordBox.Password =
            "";

        AzureRegionTextBox.Text =
            "";

        UpdateAzureStatusUi();
    }

    private async void ElevenLabsVerifyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string apiKey =
            ElevenLabsApiKeyPasswordBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(
                apiKey))
        {
            ElevenLabsSetupStatusText.Text =
                "Enter your ElevenLabs API key first.";

            return;
        }

        ElevenLabsVerifyButton.IsEnabled =
            false;

        ElevenLabsSetupStatusText.Text =
            "Checking...";

        ElevenLabsStatusText.Text =
            "Checking...";

        try
        {
            bool connected =
                await _elevenLabsProvider.ConfigureAsync(
                    apiKey);

            if (connected)
            {
                ElevenLabsSetupStatusText.Text =
                    "Connected";

                ElevenLabsStatusText.Text =
                    "Connected";

                ElevenLabsApiKeyPasswordBox.Password =
                    "";
            }
            else
            {
                ElevenLabsSetupStatusText.Text =
                    "Needs attention";

                ElevenLabsStatusText.Text =
                    "Needs attention";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"ElevenLabs verification failed: {ex.GetType().Name}");

            ElevenLabsSetupStatusText.Text =
                "Connection failed. Check the API key, permissions, internet connection, and ElevenLabs account access.";

            ElevenLabsStatusText.Text =
                "Needs attention";
        }
        finally
        {
            ElevenLabsVerifyButton.IsEnabled =
                true;
        }
    }

    private void ElevenLabsRemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmationDialog dialog =
            new(
                "Remove ElevenLabs",
                "Remove the saved ElevenLabs connection and API key?")
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _elevenLabsProvider.RemoveConfiguration();

        ElevenLabsApiKeyPasswordBox.Password =
            "";

        UpdateElevenLabsStatusUi();
    }

    private void ElevenLabsHelpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SectionHelpWindow helpWindow =
            new(
                "Connect ElevenLabs",
                "Follow these steps to connect your own ElevenLabs account to Chat Out Loud.",
                new[]
                {
                    new SectionHelpItem(
                        "IMPORTANT — THIRD-PARTY SERVICE AND BILLING",
                        "ElevenLabs is a separate third-party service and is not included with Chat Out Loud. You use your own ElevenLabs account and API key. ElevenLabs may consume credits or charge you directly according to your ElevenLabs account, subscription, usage, and current pricing. Chat Out Loud does not provide ElevenLabs credits, pay ElevenLabs fees, reimburse usage charges, or control ElevenLabs billing. Review your ElevenLabs plan and pricing before connecting the service."),

                    new SectionHelpItem(
                        "1. CREATE OR SIGN IN TO ELEVENLABS",
                        "Go to the ElevenLabs website and create an account or sign in to the ElevenLabs account you want to use with Chat Out Loud."),

                    new SectionHelpItem(
                        "2. OPEN API KEYS",
                        "In ElevenLabs, open Developers from the left-side menu and select API Keys."),

                    new SectionHelpItem(
                        "3. CREATE AN API KEY",
                        "Create a new API key. Give it a recognizable name such as Chat Out Loud. ElevenLabs only shows the complete API key when it is first created, so copy it before leaving the creation screen."),

                    new SectionHelpItem(
                        "4. API KEY PERMISSIONS",
                        "If the key is restricted, allow Text to Speech → Access, Voices → Read, and User → Access. Chat Out Loud uses Text to Speech to generate speech, Voices to retrieve the voices available to the account, and User access to determine the account's subscription tier so the voice list can be filtered correctly. Other ElevenLabs features are not required for normal Chat Out Loud TTS."),

                    new SectionHelpItem(
                        "5. SET A CREDIT LIMIT",
                        "ElevenLabs allows an API key to have its own usage or credit limit. Setting a limit is recommended if you want to restrict how much usage can be generated through this key. The limit is managed by ElevenLabs, not Chat Out Loud."),

                    new SectionHelpItem(
                        "6. KEEP THE KEY PRIVATE",
                        "Treat your ElevenLabs API key like a password. Do not post it publicly, upload it to GitHub, share it in screenshots, or give it to other users."),

                    new SectionHelpItem(
                        "7. ADD THE KEY TO CHAT OUT LOUD",
                        "Return to Chat Out Loud, open Manage TTS Providers, select ElevenLabs, and click Configure. Paste the ElevenLabs API key into the API Key field."),

                    new SectionHelpItem(
                        "8. VERIFY CONNECTION",
                        "Click Verify Connection. Chat Out Loud uses the key to contact ElevenLabs and retrieve the compatible voices available to that account. If verification succeeds, the status changes to Connected."),

                    new SectionHelpItem(
                        "9. AUTOMATIC SECURE STORAGE",
                        "After successful verification, Chat Out Loud automatically stores the API key using protected Windows storage for the current Windows user. There is no separate Save button. The API key field is cleared after the connection succeeds."),

                    new SectionHelpItem(
                        "10. CONNECTED DOES NOT MEAN ACTIVE",
                        "Connecting ElevenLabs does not automatically make ElevenLabs your speech provider and does not automatically generate paid speech. ElevenLabs is used for synthesis only when you explicitly select an ElevenLabs voice in Speech Control."),

                    new SectionHelpItem(
                        "11. AVAILABLE VOICES",
                        "Chat Out Loud retrieves ElevenLabs voices that are directly usable by the connected account. On Free accounts, Voice Library and community voices that cannot be used through the ElevenLabs API are excluded, while compatible default and personal voices remain available. On paid plans, compatible Voice Library voices can also appear. Voices that require additional verification or special access are excluded when they are not directly usable by the connected account."),

                    new SectionHelpItem(
                        "12. REMOVE CONNECTION",
                        "Remove Connection deletes the ElevenLabs API key and cached ElevenLabs voice information saved by Chat Out Loud. It does not delete the API key from your ElevenLabs account. If you no longer want the key to exist, delete or revoke it from ElevenLabs as well.")
                })
            {
                Owner = this
            };

        helpWindow.ShowDialog();
    }

    private void AzureHelpButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SectionHelpWindow helpWindow =
            new(
                "Connect Microsoft Azure Speech",
                "Follow these steps to connect your own Microsoft Azure Speech resource to Chat Out Loud.",
                new[]
                {
                    new SectionHelpItem(
                        "IMPORTANT — THIRD-PARTY SERVICE AND BILLING",
                        "Microsoft Azure Speech is a separate third-party service and is not included with Chat Out Loud. You use your own Microsoft Azure account, subscription, Speech resource, and credentials. Microsoft may consume allowances, credits, or charge you directly according to your Azure subscription, Speech resource, usage, and current pricing. Chat Out Loud does not provide Azure credits, pay Microsoft Azure fees, reimburse usage charges, or control Microsoft Azure billing. Review Microsoft Azure Speech pricing and billing before connecting the service."),

                    new SectionHelpItem(
                        "1. CREATE OR SIGN IN TO MICROSOFT AZURE",
                        "Sign in to the Microsoft Azure portal using the Microsoft account that owns or has access to the Azure subscription you want to use for Speech."),

                    new SectionHelpItem(
                        "2. CREATE A SPEECH RESOURCE",
                        "Create or open a Microsoft Azure Speech resource. The resource provides the credentials and regional endpoint used by Chat Out Loud for text-to-speech."),

                    new SectionHelpItem(
                        "3. FIND THE RESOURCE KEY AND REGION",
                        "Open the Speech resource and locate Keys and Endpoint. Copy one of the resource keys and note the resource Region. The region entered in Chat Out Loud must match the Azure Speech resource because Azure Speech keys are region-specific."),

                    new SectionHelpItem(
                        "4. KEEP THE KEY PRIVATE",
                        "Treat the Microsoft Azure Speech resource key like a password. Do not post it publicly, upload it to GitHub, share it in screenshots, or give it to other users."),

                    new SectionHelpItem(
                        "5. ADD THE CREDENTIALS TO CHAT OUT LOUD",
                        "Return to Chat Out Loud, open Manage TTS Providers, select Microsoft Azure Speech, and click Configure. Paste the Speech resource key into the Speech Resource Key field and enter the matching Azure region identifier in the Region field."),

                    new SectionHelpItem(
                        "6. VERIFY CONNECTION",
                        "Click Verify Connection. Chat Out Loud contacts Microsoft Azure Speech using the supplied resource key and region and retrieves the compatible voices available through that Speech resource. If verification succeeds, the status changes to Connected."),

                    new SectionHelpItem(
                        "7. AUTOMATIC SECURE STORAGE",
                        "After successful verification, Chat Out Loud automatically stores the Azure Speech resource key and region using protected Windows storage for the current Windows user. There is no separate Save button. The resource key field is cleared after the connection succeeds."),

                    new SectionHelpItem(
                        "8. CONNECTED DOES NOT MEAN ACTIVE",
                        "Connecting Microsoft Azure Speech does not automatically make Azure your speech provider and does not automatically generate paid speech. Azure is used for synthesis only when you explicitly select a Microsoft Azure voice in Speech Control."),

                    new SectionHelpItem(
                        "9. AVAILABLE VOICES",
                        "Chat Out Loud retrieves normal generally available neural voices returned through the connected Azure Speech resource and supported by Chat Out Loud's standard text-to-speech path. Preview, HD, custom, premium, or other special-access voice paths are not exposed unless they are specifically supported and verified for normal use."),

                    new SectionHelpItem(
                        "10. REFRESH VOICES",
                        "Use Refresh Voices in Speech Control when you want Chat Out Loud to explicitly retrieve the current Azure voice catalog. Chat Out Loud does not repeatedly poll Microsoft Azure in the background."),

                    new SectionHelpItem(
                        "11. REMOVE CONNECTION",
                        "Remove Connection deletes the Microsoft Azure Speech credentials and cached Azure voice information saved by Chat Out Loud. It does not delete the Speech resource or its keys from your Microsoft Azure account, and it does not remove credentials for another TTS provider.")
                })
            {
                Owner = this
            };

        helpWindow.ShowDialog();
    }

    private void UpdateElevenLabsStatusUi()
    {
        string statusText =
            _elevenLabsProvider.Status switch
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

        ElevenLabsStatusText.Text =
            statusText;

        ElevenLabsSetupStatusText.Text =
            statusText;
    }

    private void UpdateAzureStatusUi()
    {
        string statusText =
            _azureProvider.Status switch
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

        AzureStatusText.Text =
            statusText;

        AzureSetupStatusText.Text =
            statusText;
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