using ChatOutLoud.TtsProviders;
using ChatOutLoud.TtsProviders.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Net.WebSockets;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChatOutLoud;

public partial class MainWindow : Window
{
    private static readonly HttpClient Http = new();

    private string? _twitchAccessToken;
    private string? _twitchRefreshToken;
    private string? _twitchUserId;

    private ClientWebSocket? _twitchChatSocket;
    private CancellationTokenSource? _twitchChatCancellation;
    private int _twitchChatKeepaliveTimeoutSeconds = 10;
    private bool _isTwitchChatIntentionallyStopped;

    private readonly SpeechSynthesizer _speechSynthesizer = new();

    private readonly GoogleCloudTtsProvider _googleTtsProvider =
        (GoogleCloudTtsProvider)(
            TtsProviderManager.Shared.GetProvider(
                "google-cloud")
            ?? throw new InvalidOperationException(
                "Google Cloud TTS provider is not registered."));

    private readonly BulkObservableCollection<TtsVoice> _availableTtsVoices =
        [];

    private MemoryStream? _ttsAudioStream;
    private WaveFileReader? _ttsWaveReader;
    private WasapiPlayer? _ttsAudioPlayer;
    private MMDevice? _ttsAudioDevice;

    private string? _joinSoundPath =
        JoinSoundStore.Load();

    private AudioFileReader? _joinSoundReader;
    private WasapiPlayer? _joinSoundPlayer;
    private MMDevice? _joinSoundDevice;

    private readonly Queue<string> _speechQueue = new();
    private bool _isSpeechPlaying;
    private bool _isStoppingSpeechPlayback;

    private bool _isLoadingAudioOutputDevices;

    private readonly DispatcherTimer _twitchChattersRefreshTimer =
        new()
        {
            Interval = TimeSpan.FromSeconds(10)
        };

    private readonly HashSet<string> _knownTwitchChatters =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _currentTwitchChatters =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _observedSessionChatters =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _observedSharedChatChatters =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _userDisplayNames =
        new(StringComparer.OrdinalIgnoreCase);

    private string? _sharedChatSessionId;

    private readonly HashSet<string> _sharedChatParticipantBroadcasterIds =
        new(StringComparer.Ordinal);

    private bool _hasTwitchChatterBaseline;

    private readonly HashSet<string> _mutedUsers =
        MutedUserStore.Load();

    private string _userSearchText = "";

    private readonly List<string> _mutedPhrases =
        MutedPhraseStore.Load();

    private string? _selectedUsername;

    public MainWindow()
    {
        InitializeComponent();

        JoinSoundFileNameText.Text =
            string.IsNullOrWhiteSpace(_joinSoundPath)
                ? "No sound selected"
                : Path.GetFileName(_joinSoundPath);

        SpeechSettings savedSpeechSettings =
            SpeechSettingsStore.Load();

        VolumeSlider.Value =
            Math.Clamp(
                savedSpeechSettings.Volume,
                VolumeSlider.Minimum,
                VolumeSlider.Maximum);

        SpeechSpeedSlider.Value =
            Math.Clamp(
                savedSpeechSettings.SpeechSpeed,
                SpeechSpeedSlider.Minimum,
                SpeechSpeedSlider.Maximum);

        VolumeSlider.ValueChanged +=
            (_, _) => SaveSpeechSettings();

        SpeechSpeedSlider.ValueChanged +=
            (_, _) => SaveSpeechSettings();

        if (FindName("SpeakUsernamesCheckBox")
                is System.Windows.Controls.CheckBox speakUsernamesCheckBox)
        {
            speakUsernamesCheckBox.IsChecked =
                savedSpeechSettings.SpeakUsernames;
        }

        if (FindName("MuteNumbersCheckBox")
                is System.Windows.Controls.CheckBox muteNumbersCheckBox)
        {
            muteNumbersCheckBox.IsChecked =
                savedSpeechSettings.MuteNumbers;
        }

        if (FindName("MuteSpecialSymbolsCheckBox")
                is System.Windows.Controls.CheckBox muteSpecialSymbolsCheckBox)
        {
            muteSpecialSymbolsCheckBox.IsChecked =
                savedSpeechSettings.MuteSpecialSymbols;
        }

        if (FindName("MuteEmotesCheckBox")
                is System.Windows.Controls.CheckBox muteEmotesCheckBox)
        {
            muteEmotesCheckBox.IsChecked =
                savedSpeechSettings.MuteEmotes;
        }

        if (FindName("MuteLinksCheckBox")
                is System.Windows.Controls.CheckBox muteLinksCheckBox)
        {
            muteLinksCheckBox.IsChecked =
                savedSpeechSettings.MuteLinks;
        }

        if (FindName("MuteCommandsCheckBox")
                is System.Windows.Controls.CheckBox muteCommandsCheckBox)
        {
            muteCommandsCheckBox.IsChecked =
                savedSpeechSettings.MuteCommands;
        }

        if (FindName("EnableJoinSoundCheckBox")
                is System.Windows.Controls.CheckBox enableJoinSoundCheckBox)
        {
            enableJoinSoundCheckBox.IsChecked =
                savedSpeechSettings.EnableJoinSound;
        }

        if (FindName("EnableJoinUsernameCheckBox")
                is System.Windows.Controls.CheckBox enableJoinUsernameCheckBox)
        {
            enableJoinUsernameCheckBox.IsChecked =
                savedSpeechSettings.EnableJoinUsername;
        }

        if (FindName("JoinSoundVolumeSlider")
                is System.Windows.Controls.Slider joinSoundVolumeSlider)
        {
            joinSoundVolumeSlider.Value =
                Math.Clamp(
                    savedSpeechSettings.JoinSoundVolume,
                    0.0,
                    100.0);

            joinSoundVolumeSlider.ValueChanged +=
                (_, _) =>
                {
                    if (_joinSoundReader is not null)
                    {
                        _joinSoundReader.Volume =
                            (float)(
                                joinSoundVolumeSlider.Value /
                                100.0);
                    }

                    SaveSpeechSettings();
                };
        }

        foreach (string checkBoxName in new[]
        {
            "SpeakUsernamesCheckBox",
            "MuteLinksCheckBox",
            "MuteCommandsCheckBox",
            "MuteNumbersCheckBox",
            "MuteSpecialSymbolsCheckBox",
            "MuteEmotesCheckBox",
            "EnableJoinSoundCheckBox",
            "EnableJoinUsernameCheckBox"
        })
        {
            if (FindName(checkBoxName)
                    is System.Windows.Controls.CheckBox settingsCheckBox)
            {
                settingsCheckBox.Checked +=
                    (_, _) => SaveSpeechSettings();

                settingsCheckBox.Unchecked +=
                    (_, _) => SaveSpeechSettings();
            }
        }

        VoiceComboBox.ItemsSource =
            _availableTtsVoices;

        LoadInstalledVoices();

        VoiceComboBox.Items.GroupDescriptions.Clear();

        VoiceComboBox.Items.GroupDescriptions.Add(
            new System.Windows.Data.PropertyGroupDescription(
                nameof(TtsVoice.ProviderName)));

        TtsVoice? savedTtsVoice =
            null;

        foreach (TtsVoice voice in
                 _availableTtsVoices)
        {
            if (string.Equals(
                    voice.ProviderId,
                    savedSpeechSettings.VoiceProviderId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    voice.VoiceId,
                    savedSpeechSettings.VoiceId,
                    StringComparison.Ordinal))
            {
                savedTtsVoice =
                    voice;

                break;
            }
        }

        if (savedTtsVoice is not null &&
            VoiceComboBox.Items.Contains(
                savedTtsVoice))
        {
            VoiceComboBox.SelectedItem =
                savedTtsVoice;
        }
        else if (!string.IsNullOrWhiteSpace(
                     savedSpeechSettings.VoiceName) &&
                 VoiceComboBox.Items.Contains(
                     savedSpeechSettings.VoiceName))
        {
            VoiceComboBox.SelectedItem =
                savedSpeechSettings.VoiceName;
        }

        VoiceComboBox.SelectionChanged +=
            (_, _) => SaveSpeechSettings();

        LoadAudioOutputDevices();

        foreach (string phrase in _mutedPhrases)
        {
            MutedPhrasesList.Items.Add(phrase);
        }

        RefreshDetectedUsersList();

        DisconnectTwitchButton.IsEnabled = false;

        Loaded += MainWindow_Loaded;

        _twitchChattersRefreshTimer.Tick +=
            TwitchChattersRefreshTimer_Tick;
    }

    private void SaveSpeechSettings()
    {
        bool speakUsernames =
            FindName("SpeakUsernamesCheckBox")
                is System.Windows.Controls.CheckBox speakUsernamesCheckBox &&
            speakUsernamesCheckBox.IsChecked == true;

        bool muteLinks =
            FindName("MuteLinksCheckBox")
                is System.Windows.Controls.CheckBox muteLinksCheckBox &&
            muteLinksCheckBox.IsChecked == true;

        bool muteCommands =
            FindName("MuteCommandsCheckBox")
                is System.Windows.Controls.CheckBox muteCommandsCheckBox &&
            muteCommandsCheckBox.IsChecked == true;

        bool muteNumbers =
            FindName("MuteNumbersCheckBox")
                is System.Windows.Controls.CheckBox muteNumbersCheckBox &&
            muteNumbersCheckBox.IsChecked == true;

        bool muteSpecialSymbols =
            FindName("MuteSpecialSymbolsCheckBox")
                is System.Windows.Controls.CheckBox muteSpecialSymbolsCheckBox &&
            muteSpecialSymbolsCheckBox.IsChecked == true;

        bool muteEmotes =
            FindName("MuteEmotesCheckBox")
                is System.Windows.Controls.CheckBox muteEmotesCheckBox &&
            muteEmotesCheckBox.IsChecked == true;

        bool enableJoinSound =
            FindName("EnableJoinSoundCheckBox")
                is System.Windows.Controls.CheckBox enableJoinSoundCheckBox &&
            enableJoinSoundCheckBox.IsChecked == true;

        bool enableJoinUsername =
            FindName("EnableJoinUsernameCheckBox")
                is System.Windows.Controls.CheckBox enableJoinUsernameCheckBox &&
            enableJoinUsernameCheckBox.IsChecked == true;

        double joinSoundVolume =
            FindName("JoinSoundVolumeSlider")
                is System.Windows.Controls.Slider joinSoundVolumeSlider
                ? joinSoundVolumeSlider.Value
                : 100.0;

        TtsVoice? selectedVoice =
            VoiceComboBox.SelectedItem as TtsVoice;

        string? voiceName =
            selectedVoice?.DisplayName ??
            VoiceComboBox.SelectedItem as string;

        string voiceProviderId =
            selectedVoice?.ProviderId ??
            "windows";

        string? voiceId =
            selectedVoice?.VoiceId ??
            voiceName;

        double volume =
            VolumeSlider.Value;

        double speechSpeed =
            SpeechSpeedSlider.Value;

        SpeechSettingsStore.Save(
            new SpeechSettings
            {
                SpeakUsernames = speakUsernames,
                VoiceName = voiceName,
                VoiceProviderId = voiceProviderId,
                VoiceId = voiceId,
                Volume = volume,
                SpeechSpeed = speechSpeed,
                MuteLinks = muteLinks,
                MuteCommands = muteCommands,
                MuteNumbers = muteNumbers,
                MuteSpecialSymbols = muteSpecialSymbols,
                MuteEmotes = muteEmotes,
                EnableJoinSound = enableJoinSound,
                EnableJoinUsername = enableJoinUsername,
                JoinSoundVolume = joinSoundVolume
            });
    }

    private void LoadInstalledVoices()
    {
        _availableTtsVoices.Clear();

        List<TtsVoice> windowsVoices =
            [];

        foreach (InstalledVoice voice in
                 _speechSynthesizer.GetInstalledVoices())
        {
            if (!voice.Enabled)
            {
                continue;
            }

            windowsVoices.Add(
                new TtsVoice(
                    "windows",
                    "Windows Voices",
                    voice.VoiceInfo.Name,
                    voice.VoiceInfo.Name));
        }

        _availableTtsVoices.AddRange(
            windowsVoices);

        if (_availableTtsVoices.Count > 0)
        {
            VoiceComboBox.SelectedIndex = 0;
        }

        _ = LoadCachedExternalVoicesAsync();
    }

    private async Task LoadCachedExternalVoicesAsync()
    {
        await Dispatcher.Yield(
            DispatcherPriority.Background);

        List<string> configuredProviderIds =
            TtsProviderManager.Shared.Providers
                .Where(
                    provider =>
                        provider.Status ==
                            TtsProviderStatus.Configured ||
                        provider.Status ==
                            TtsProviderStatus.Connected)
                .Select(
                    provider => provider.ProviderId)
                .ToList();

        if (configuredProviderIds.Count == 0)
        {
            return;
        }

        HashSet<string> existingVoiceKeys =
            _availableTtsVoices
                .Select(
                    voice =>
                        $"{voice.ProviderId.ToUpperInvariant()}\n{voice.VoiceId}")
                .ToHashSet(
                    StringComparer.Ordinal);

        (
            List<TtsVoice> Voices,
            string? SavedProviderId,
            string? SavedVoiceId
        ) result =
            await Task.Run(
                () =>
                {
                    SpeechSettings savedSpeechSettings =
                        SpeechSettingsStore.Load();

                    List<TtsVoice> voicesToAdd =
                        [];

                    HashSet<string> knownVoiceKeys =
                        new(
                            existingVoiceKeys,
                            StringComparer.Ordinal);

                    foreach (string providerId in
                             configuredProviderIds)
                    {
                        IReadOnlyList<TtsVoice> catalog =
                            TtsVoiceCatalogStore.Load(
                                providerId);

                        foreach (TtsVoice voice in catalog)
                        {
                            string voiceKey =
                                $"{voice.ProviderId.ToUpperInvariant()}\n{voice.VoiceId}";

                            if (!knownVoiceKeys.Add(
                                    voiceKey))
                            {
                                continue;
                            }

                            voicesToAdd.Add(
                                voice);
                        }
                    }

                    return (
                        voicesToAdd,
                        savedSpeechSettings.VoiceProviderId,
                        savedSpeechSettings.VoiceId);
                });

        _availableTtsVoices.AddRange(
            result.Voices);

        TtsVoice? savedVoice =
            result.Voices.FirstOrDefault(
                voice =>
                    string.Equals(
                        voice.ProviderId,
                        result.SavedProviderId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        voice.VoiceId,
                        result.SavedVoiceId,
                        StringComparison.Ordinal));

        if (savedVoice is not null)
        {
            VoiceComboBox.SelectedItem =
                savedVoice;
        }
    }

    private async Task LoadGoogleVoicesAsync()
    {
        TtsVoice? previouslySelectedVoice =
            VoiceComboBox.SelectedItem as TtsVoice;

        bool googleVoiceWasSelected =
            previouslySelectedVoice is not null &&
            string.Equals(
                previouslySelectedVoice.ProviderId,
                _googleTtsProvider.ProviderId,
                StringComparison.OrdinalIgnoreCase);

        _availableTtsVoices.RemoveAll(
            voice =>
                string.Equals(
                    voice.ProviderId,
                    _googleTtsProvider.ProviderId,
                    StringComparison.OrdinalIgnoreCase));

        if (_googleTtsProvider.Status ==
                TtsProviderStatus.NotConfigured ||
            _googleTtsProvider.Status ==
                TtsProviderStatus.NeedsAttention)
        {
            if (googleVoiceWasSelected)
            {
                VoiceComboBox.SelectedItem =
                    _availableTtsVoices.FirstOrDefault(
                        voice =>
                            string.Equals(
                                voice.ProviderId,
                                "windows",
                                StringComparison.OrdinalIgnoreCase));
            }

            return;
        }

        try
        {
            IReadOnlyList<TtsVoice> googleVoices =
                await _googleTtsProvider.GetVoicesAsync();

            List<TtsVoice> orderedGoogleVoices =
                googleVoices
                    .OrderBy(
                        voice => voice.DisplayName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _availableTtsVoices.AddRange(
                orderedGoogleVoices);

            if (googleVoiceWasSelected)
            {
                TtsVoice? restoredGoogleVoice =
                    orderedGoogleVoices.FirstOrDefault(
                        voice =>
                            string.Equals(
                                voice.VoiceId,
                                previouslySelectedVoice!.VoiceId,
                                StringComparison.Ordinal));

                VoiceComboBox.SelectedItem =
                    restoredGoogleVoice ??
                    _availableTtsVoices.FirstOrDefault(
                        voice =>
                            string.Equals(
                                voice.ProviderId,
                                "windows",
                                StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            if (googleVoiceWasSelected)
            {
                VoiceComboBox.SelectedItem =
                    _availableTtsVoices.FirstOrDefault(
                        voice =>
                            string.Equals(
                                voice.ProviderId,
                                "windows",
                                StringComparison.OrdinalIgnoreCase));
            }
        }
    }


    private void LoadAudioOutputDevices()
    {
        _isLoadingAudioOutputDevices = true;

        try
        {
            AudioOutputComboBox.Items.Clear();

            AudioOutputComboBox.Items.Add(
                new AudioOutputDeviceOption(
                    "System Default",
                    null));

            using MMDeviceEnumerator enumerator = new();

            foreach (MMDevice device in
                     enumerator.EnumerateAudioEndPoints(
                         DataFlow.Render,
                         DeviceState.Active))
            {
                AudioOutputComboBox.Items.Add(
                    new AudioOutputDeviceOption(
                        device.FriendlyName,
                        device.ID));
            }

            string? savedDeviceId =
                AudioOutputStore.Load();

            AudioOutputComboBox.SelectedIndex = 0;

            if (!string.IsNullOrWhiteSpace(savedDeviceId))
            {
                foreach (object item in AudioOutputComboBox.Items)
                {
                    if (item is AudioOutputDeviceOption option &&
                        string.Equals(
                            option.DeviceId,
                            savedDeviceId,
                            StringComparison.Ordinal))
                    {
                        AudioOutputComboBox.SelectedItem = option;
                        break;
                    }
                }
            }
        }
        finally
        {
            _isLoadingAudioOutputDevices = false;
        }
    }

    private void RefreshDevicesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadAudioOutputDevices();
    }

    private void AudioOutputComboBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingAudioOutputDevices)
        {
            return;
        }

        if (AudioOutputComboBox.SelectedItem is not AudioOutputDeviceOption outputDevice)
        {
            return;
        }

        AudioOutputStore.Save(
            outputDevice.DeviceId);
    }

    private void TestVoiceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SpeakText(
            "This is a Chat Out Loud voice test.");
    }

    private async void ManageTtsProvidersButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        TtsProviderWindow dialog =
            new()
            {
                Owner = this
            };

        dialog.ShowDialog();

        await LoadGoogleVoicesAsync();
    }

    private void ChooseJoinSoundButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dialog =
            new()
            {
                Title = "Choose Chat Join Sound",
                Filter =
                    "Audio files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*"
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _joinSoundPath =
            dialog.FileName;

        JoinSoundStore.Save(
            _joinSoundPath);

        JoinSoundFileNameText.Text =
            Path.GetFileName(_joinSoundPath);
    }

    private void TestJoinSoundButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        PlayJoinSound();
    }

    private void PlayJoinSound()
    {
        if (string.IsNullOrWhiteSpace(_joinSoundPath) ||
            !File.Exists(_joinSoundPath))
        {
            MessageBox.Show(
                "Choose a join sound first.",
                "Chat Out Loud",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (AudioOutputComboBox.SelectedItem
            is not AudioOutputDeviceOption outputDevice)
        {
            return;
        }

        DisposeJoinSoundPlayback();

        _joinSoundReader =
            new AudioFileReader(_joinSoundPath);

        _joinSoundReader.Volume =
            (float)(
                JoinSoundVolumeSlider.Value /
                100.0);

        WasapiPlayerBuilder playerBuilder =
            new WasapiPlayerBuilder();

        if (!string.IsNullOrWhiteSpace(outputDevice.DeviceId))
        {
            using MMDeviceEnumerator enumerator = new();

            _joinSoundDevice =
                enumerator.GetDevice(
                    outputDevice.DeviceId);

            playerBuilder.WithDevice(
                _joinSoundDevice);
        }

        _joinSoundPlayer =
            playerBuilder.Build();

        _joinSoundPlayer.PlaybackStopped +=
            JoinSoundPlayer_PlaybackStopped;

        _joinSoundPlayer.Init(
            _joinSoundReader);

        _joinSoundPlayer.Play();
    }

    private void JoinSoundPlayer_PlaybackStopped(
        object? sender,
        StoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (sender is not WasapiPlayer stoppedPlayer ||
                    !ReferenceEquals(
                        stoppedPlayer,
                        _joinSoundPlayer))
                {
                    return;
                }

                stoppedPlayer.PlaybackStopped -=
                    JoinSoundPlayer_PlaybackStopped;

                stoppedPlayer.Dispose();
                _joinSoundPlayer = null;

                _joinSoundReader?.Dispose();
                _joinSoundReader = null;

                _joinSoundDevice?.Dispose();
                _joinSoundDevice = null;
            });
    }

    private void DisposeJoinSoundPlayback()
    {
        if (_joinSoundPlayer is not null)
        {
            _joinSoundPlayer.PlaybackStopped -=
                JoinSoundPlayer_PlaybackStopped;

            _joinSoundPlayer.Stop();
            _joinSoundPlayer.Dispose();
            _joinSoundPlayer = null;
        }

        _joinSoundReader?.Dispose();
        _joinSoundReader = null;

        _joinSoundDevice?.Dispose();
        _joinSoundDevice = null;
    }

    private void SpeakText(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _speechQueue.Enqueue(text);

        UpdateQueueCount();

        PlayNextQueuedSpeech();
    }

    private async void PlayNextQueuedSpeech()
    {
        if (_isSpeechPlaying ||
            _speechQueue.Count == 0)
        {
            return;
        }

        if (AudioOutputComboBox.SelectedItem
            is not AudioOutputDeviceOption outputDevice)
        {
            _speechQueue.Clear();
            UpdateQueueCount();
            return;
        }

        TtsVoice? selectedVoice =
            VoiceComboBox.SelectedItem as TtsVoice;

        string? legacyWindowsVoiceName =
            VoiceComboBox.SelectedItem as string;

        if (selectedVoice is null &&
            string.IsNullOrWhiteSpace(
                legacyWindowsVoiceName))
        {
            _speechQueue.Clear();
            UpdateQueueCount();
            return;
        }

        string text =
            _speechQueue.Dequeue();

        UpdateQueueCount();

        _isSpeechPlaying = true;

        try
        {
            bool useWindowsVoice =
                selectedVoice is null ||
                string.Equals(
                    selectedVoice.ProviderId,
                    "windows",
                    StringComparison.OrdinalIgnoreCase);

            if (useWindowsVoice)
            {
                string voiceName =
                    selectedVoice?.VoiceId ??
                    legacyWindowsVoiceName!;

                _speechSynthesizer.SelectVoice(
                    voiceName);

                _speechSynthesizer.Volume =
                    (int)Math.Round(
                        VolumeSlider.Value);

                _speechSynthesizer.Rate =
                    (int)Math.Round(
                        SpeechSpeedSlider.Value);

                _ttsAudioStream =
                    new MemoryStream();

                _speechSynthesizer.SetOutputToWaveStream(
                    _ttsAudioStream);

                _speechSynthesizer.Speak(
                    text);

                _speechSynthesizer.SetOutputToNull();

                _ttsAudioStream.Position =
                    0;
            }
            else
            {
                ITtsProvider? provider =
                    TtsProviderManager.Shared.GetProvider(
                        selectedVoice!.ProviderId);

                if (provider is null)
                {
                    throw new InvalidOperationException(
                        $"TTS provider '{selectedVoice.ProviderName}' is not available.");
                }

                TtsAudioResult audioResult =
                    await provider.SynthesizeSpeechAsync(
                        text,
                        selectedVoice);

                _ttsAudioStream =
                    new MemoryStream(
                        audioResult.AudioData,
                        writable: false);
            }

            _ttsWaveReader =
                new WaveFileReader(
                    _ttsAudioStream);

            WasapiPlayerBuilder playerBuilder =
                new();

            if (!string.IsNullOrWhiteSpace(
                    outputDevice.DeviceId))
            {
                using MMDeviceEnumerator enumerator =
                    new();

                _ttsAudioDevice =
                    enumerator.GetDevice(
                        outputDevice.DeviceId);

                playerBuilder.WithDevice(
                    _ttsAudioDevice);
            }

            _ttsAudioPlayer =
                playerBuilder.Build();

            _ttsAudioPlayer.PlaybackStopped +=
                TtsAudioPlayer_PlaybackStopped;

            _ttsAudioPlayer.Init(
                _ttsWaveReader);

            _ttsAudioPlayer.Play();
        }
        catch (Exception ex)
        {
            _ttsWaveReader?.Dispose();
            _ttsWaveReader = null;

            _ttsAudioStream?.Dispose();
            _ttsAudioStream = null;

            _ttsAudioDevice?.Dispose();
            _ttsAudioDevice = null;

            _isSpeechPlaying = false;
            _isStoppingSpeechPlayback = false;

            _speechQueue.Clear();
            UpdateQueueCount();

            MessageBox.Show(
                $"TTS playback failed.\n\n{ex}",
                "Chat Out Loud",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TtsAudioPlayer_PlaybackStopped(
        object? sender,
        StoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (sender is not WasapiPlayer stoppedPlayer ||
                    !ReferenceEquals(
                        stoppedPlayer,
                        _ttsAudioPlayer))
                {
                    return;
                }

                stoppedPlayer.PlaybackStopped -=
                    TtsAudioPlayer_PlaybackStopped;

                stoppedPlayer.Dispose();

                _ttsAudioPlayer = null;

                _ttsWaveReader?.Dispose();
                _ttsWaveReader = null;

                _ttsAudioStream?.Dispose();
                _ttsAudioStream = null;

                _ttsAudioDevice?.Dispose();
                _ttsAudioDevice = null;

                _isSpeechPlaying = false;
                _isStoppingSpeechPlayback = false;

                PlayNextQueuedSpeech();
            });
    }

    private void UpdateQueueCount()
    {
        QueueCountText.Text =
            $"Queue: {_speechQueue.Count}";
    }

    private void SkipButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_ttsAudioPlayer is not null &&
            !_isStoppingSpeechPlayback)
        {
            _isStoppingSpeechPlayback = true;

            _ttsAudioPlayer.Stop();
        }
    }

    private void ClearQueueButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _speechQueue.Clear();

        UpdateQueueCount();

        if (_ttsAudioPlayer is not null &&
            !_isStoppingSpeechPlayback)
        {
            _isStoppingSpeechPlayback = true;

            _ttsAudioPlayer.Stop();
        }
    }

    private void DisposeTtsPlayback()
    {
        if (_ttsAudioPlayer is not null)
        {
            _ttsAudioPlayer.PlaybackStopped -=
                TtsAudioPlayer_PlaybackStopped;

            _ttsAudioPlayer.Stop();
            _ttsAudioPlayer.Dispose();
            _ttsAudioPlayer = null;
        }

        _ttsWaveReader?.Dispose();
        _ttsWaveReader = null;

        _ttsAudioStream?.Dispose();
        _ttsAudioStream = null;

        _ttsAudioDevice?.Dispose();
        _ttsAudioDevice = null;

        _isSpeechPlaying = false;
        _isStoppingSpeechPlayback = false;
    }

    private async void TwitchChattersRefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        try
        {
            await LoadSharedChatSessionAsync();
            await LoadCurrentTwitchChattersAsync();
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains(
                      "Unauthorized",
                      StringComparison.OrdinalIgnoreCase) ||
                  ex.Message.Contains(
                      "\"status\":401",
                      StringComparison.OrdinalIgnoreCase))
        {
            string? refreshToken =
                _twitchRefreshToken;

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _twitchChattersRefreshTimer.Stop();

                SetLiveConnectionState(false);

                TwitchConnectionStatusText.Text =
                    "Authorization expired - reconnect";

                return;
            }

            try
            {
                TokenResponse? refreshedToken =
                    await RefreshTwitchTokenAsync(
                        refreshToken);

                if (refreshedToken is null)
                {
                    _twitchChattersRefreshTimer.Stop();

                    SetLiveConnectionState(false);

                    TwitchConnectionStatusText.Text =
                        "Authorization expired - reconnect";

                    return;
                }

                _twitchAccessToken =
                    refreshedToken.AccessToken;

                _twitchRefreshToken =
                    refreshedToken.RefreshToken;

                TwitchTokenStore.Save(
                    refreshedToken.AccessToken,
                    refreshedToken.RefreshToken);

                await LoadSharedChatSessionAsync();
                await LoadCurrentTwitchChattersAsync();
            }
            catch
            {
                _twitchChattersRefreshTimer.Stop();

                SetLiveConnectionState(false);

                TwitchConnectionStatusText.Text =
                    "Authorization expired - reconnect";
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
        catch (JsonException)
        {
        }
        catch (InvalidOperationException)
        {
            TwitchConnectionStatusText.Text =
                "Twitch roster temporarily unavailable";
        }
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RestoreSavedTwitchSessionAsync();
    }

    private async void RunButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_twitchAccessToken) ||
            string.IsNullOrWhiteSpace(_twitchUserId))
        {
            SetLiveConnectionState(false);

            TwitchConnectionStatusText.Text =
                "Authorize Twitch first";

            return;
        }

        if (_twitchChatSocket?.State == WebSocketState.Open &&
            _twitchChatCancellation is not null &&
            !_twitchChatCancellation.IsCancellationRequested)
        {
            SetLiveConnectionState(true);

            TwitchConnectionStatusText.Text =
                "Connected";

            return;
        }

        _isTwitchChatIntentionallyStopped = false;

        RunButton.IsEnabled = false;

        try
        {
            await StartTwitchChatAsync();

            TwitchConnectionStatusText.Text =
                "Connected";
        }
        catch (Exception ex)
        {
            SetLiveConnectionState(false);

            TwitchConnectionStatusText.Text =
                "Connection failed";

            MessageBox.Show(
                $"Twitch chat could not start.\n\n{ex.Message}",
                "Chat Out Loud",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }

    private void StopButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _isTwitchChatIntentionallyStopped = true;

        _twitchChattersRefreshTimer.Stop();

        _twitchChatCancellation?.Cancel();

        _knownTwitchChatters.Clear();
        _currentTwitchChatters.Clear();
        _observedSessionChatters.Clear();
        _hasTwitchChatterBaseline = false;

        RefreshDetectedUsersList();

        _speechQueue.Clear();
        UpdateQueueCount();

        if (_ttsAudioPlayer is not null &&
            !_isStoppingSpeechPlayback)
        {
            _isStoppingSpeechPlayback = true;

            _ttsAudioPlayer.Stop();
        }

        SetLiveConnectionState(false);

        TwitchConnectionStatusText.Text =
            "Stopped";
    }

    private void SetLiveConnectionState(
        bool isConnected)
    {
        ConnectionStatusText.Text =
            isConnected
                ? "Connected"
                : "Not Connected";

        ConnectionStatusDot.Fill =
            new SolidColorBrush(
                isConnected
                    ? Color.FromRgb(0x45, 0xE0, 0x7B)
                    : Color.FromRgb(0xFF, 0x5A, 0x6B));

        ConnectionStatusDot.Effect =
            isConnected
                ? new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color =
                        Color.FromRgb(
                            0x45,
                            0xE0,
                            0x7B),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.9
                }
                : null;
    }

    private async void AuthorizeTwitchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        AuthorizeTwitchButton.IsEnabled = false;
        DisconnectTwitchButton.IsEnabled = false;

        TwitchUsernameText.Text = "Waiting for Twitch...";
        TwitchConnectionStatusText.Text = "Starting authorization";

        try
        {
            DeviceCodeResponse device = await StartDeviceAuthorizationAsync();

            TwitchUsernameText.Text = "Authorize Chat Out Loud in browser";
            TwitchConnectionStatusText.Text =
                $"Twitch code: {device.UserCode}";

            Process.Start(new ProcessStartInfo
            {
                FileName = device.VerificationUri,
                UseShellExecute = true
            });

            TokenResponse token =
                await WaitForAuthorizationAsync(device);

            _twitchAccessToken = token.AccessToken;
            _twitchRefreshToken = token.RefreshToken;

            TwitchTokenStore.Save(
                token.AccessToken,
                token.RefreshToken);

            await LoadTwitchProfileAsync(token.AccessToken);

            await StartTwitchChatAsync();

            TwitchConnectionStatusText.Text = "Connected";

            DisconnectTwitchButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ResetTwitchAccountUi();

            MessageBox.Show(
                $"Twitch authorization failed.\n\n{ex.Message}",
                "Chat Out Loud",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task<DeviceCodeResponse> StartDeviceAuthorizationAsync()
    {
        using FormUrlEncodedContent content = new(
            new Dictionary<string, string>
            {
                ["client_id"] = TwitchConfig.ClientId,
                ["scopes"] = "user:read:chat moderator:read:chatters"
            });

        using HttpResponseMessage response =
            await Http.PostAsync(
                "https://id.twitch.tv/oauth2/device",
                content);

        string json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Twitch rejected the device request: {json}");
        }

        DeviceCodeResponse? device =
            JsonSerializer.Deserialize<DeviceCodeResponse>(json);

        if (device is null ||
            string.IsNullOrWhiteSpace(device.DeviceCode) ||
            string.IsNullOrWhiteSpace(device.VerificationUri))
        {
            throw new InvalidOperationException(
                "Twitch returned an invalid device authorization response.");
        }

        return device;
    }

    private async Task<TokenResponse> WaitForAuthorizationAsync(
        DeviceCodeResponse device)
    {
        DateTime expiresAt =
            DateTime.UtcNow.AddSeconds(device.ExpiresIn);

        int intervalSeconds =
            Math.Max(device.Interval, 5);

        while (DateTime.UtcNow < expiresAt)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(intervalSeconds));

            using FormUrlEncodedContent content = new(
                new Dictionary<string, string>
                {
                    ["client_id"] = TwitchConfig.ClientId,
                    ["scopes"] = "user:read:chat",
                    ["device_code"] = device.DeviceCode,
                    ["grant_type"] =
                        "urn:ietf:params:oauth:grant-type:device_code"
                });

            using HttpResponseMessage response =
                await Http.PostAsync(
                    "https://id.twitch.tv/oauth2/token",
                    content);

            string json =
                await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                TokenResponse? token =
                    JsonSerializer.Deserialize<TokenResponse>(json);

                if (token is null ||
                    string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    throw new InvalidOperationException(
                        "Twitch returned an invalid access token.");
                }

                return token;
            }

            TwitchErrorResponse? error = null;

            try
            {
                error =
                    JsonSerializer.Deserialize<TwitchErrorResponse>(json);
            }
            catch
            {
            }

            string message =
                error?.Message ?? json;

            if (message.Contains(
                    "authorization_pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (message.Contains(
                    "slow_down",
                    StringComparison.OrdinalIgnoreCase))
            {
                intervalSeconds += 5;
                continue;
            }

            throw new InvalidOperationException(
                $"Twitch authorization failed: {message}");
        }

        throw new TimeoutException(
            "Twitch authorization expired before it was completed.");
    }

    private async Task RestoreSavedTwitchSessionAsync()
    {
        StoredTwitchTokens? savedTokens =
            TwitchTokenStore.Load();

        if (savedTokens is null ||
            string.IsNullOrWhiteSpace(savedTokens.AccessToken))
        {
            return;
        }

        AuthorizeTwitchButton.IsEnabled = false;
        DisconnectTwitchButton.IsEnabled = false;

        TwitchUsernameText.Text =
            "Restoring Twitch account...";

        TwitchConnectionStatusText.Text =
            "Checking saved authorization";

        try
        {
            string accessToken =
                savedTokens.AccessToken;

            string refreshToken =
                savedTokens.RefreshToken;

            bool tokenIsValid =
                await ValidateTwitchAccessTokenAsync(
                    accessToken);

            if (!tokenIsValid)
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    TwitchTokenStore.Delete();

                    ResetTwitchAccountUi();

                    TwitchConnectionStatusText.Text =
                        "Authorization expired - reconnect";

                    return;
                }

                TokenResponse? refreshedToken =
                    await RefreshTwitchTokenAsync(
                        refreshToken);

                if (refreshedToken is null)
                {
                    TwitchTokenStore.Delete();

                    ResetTwitchAccountUi();

                    TwitchConnectionStatusText.Text =
                        "Authorization expired - reconnect";

                    return;
                }

                accessToken =
                    refreshedToken.AccessToken;

                refreshToken =
                    refreshedToken.RefreshToken;

                TwitchTokenStore.Save(
                    accessToken,
                    refreshToken);
            }

            _twitchAccessToken =
                accessToken;

            _twitchRefreshToken =
                refreshToken;

            await LoadTwitchProfileAsync(
                accessToken);

            await StartTwitchChatAsync();

            TwitchConnectionStatusText.Text =
                "Connected";

            AuthorizeTwitchButton.IsEnabled = false;
            DisconnectTwitchButton.IsEnabled = true;
        }
        catch
        {
            _twitchAccessToken = null;
            _twitchRefreshToken = null;

            ResetTwitchAccountUi();

            TwitchConnectionStatusText.Text =
                "Saved login currently unavailable";
        }
    }

    private async Task<bool> ValidateTwitchAccessTokenAsync(
        string accessToken)
    {
        using HttpRequestMessage request =
            new(
                HttpMethod.Get,
                "https://id.twitch.tv/oauth2/validate");

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"OAuth {accessToken}");

        using HttpResponseMessage response =
            await Http.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode ==
            System.Net.HttpStatusCode.Unauthorized)
        {
            return false;
        }

        string json =
            await response.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Could not validate Twitch authorization: {json}");
    }

    private async Task<TokenResponse?> RefreshTwitchTokenAsync(
        string refreshToken)
    {
        using FormUrlEncodedContent content =
            new(
                new Dictionary<string, string>
                {
                    ["client_id"] =
                        TwitchConfig.ClientId,

                    ["grant_type"] =
                        "refresh_token",

                    ["refresh_token"] =
                        refreshToken
                });

        using HttpResponseMessage response =
            await Http.PostAsync(
                "https://id.twitch.tv/oauth2/token",
                content);

        string json =
            await response.Content.ReadAsStringAsync();

        if (response.StatusCode ==
                System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode ==
                System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not refresh Twitch authorization: {json}");
        }

        TokenResponse? token =
            JsonSerializer.Deserialize<TokenResponse>(
                json);

        if (token is null ||
            string.IsNullOrWhiteSpace(token.AccessToken) ||
            string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException(
                "Twitch returned an invalid refreshed authorization.");
        }

        return token;
    }

    private async Task<TwitchUser?> FindTwitchUserByLoginAsync(
        string login)
    {
        string? accessToken =
            _twitchAccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Authorize Twitch before looking up users.");
        }

        string trimmedLogin =
            login.Trim();

        if (string.IsNullOrWhiteSpace(trimmedLogin))
        {
            return null;
        }

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(trimmedLogin)}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Client-Id",
            TwitchConfig.ClientId);

        using HttpResponseMessage response =
            await Http.SendAsync(request);

        string json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not look up Twitch user: {json}");
        }

        TwitchUsersResponse? users =
            JsonSerializer.Deserialize<TwitchUsersResponse>(
                json);

        if (users?.Data is null ||
            users.Data.Count == 0)
        {
            return null;
        }

        return users.Data[0];
    }

    private async Task LoadTwitchProfileAsync(
        string accessToken)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://api.twitch.tv/helix/users");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Client-Id",
            TwitchConfig.ClientId);

        using HttpResponseMessage response =
            await Http.SendAsync(request);

        string json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not load Twitch profile: {json}");
        }

        TwitchUsersResponse? users =
            JsonSerializer.Deserialize<TwitchUsersResponse>(json);

        if (users?.Data is null ||
            users.Data.Count == 0)
        {
            throw new InvalidOperationException(
                "Twitch did not return the authorized user.");
        }

        TwitchUser user = users.Data[0];

        _twitchUserId = user.Id;

        TwitchUsernameText.Text = user.DisplayName;

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            byte[] imageBytes =
                await Http.GetByteArrayAsync(
                    user.ProfileImageUrl);

            using MemoryStream stream =
                new(imageBytes);

            BitmapImage image =
                new();

            image.BeginInit();
            image.CacheOption =
                BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            TwitchProfileImage.Fill =
                new ImageBrush(image)
                {
                    Stretch = Stretch.UniformToFill
                };
        }
    }

    private async Task<string> ConnectTwitchChatWebSocketAsync()
    {
        _twitchChatCancellation?.Cancel();
        _twitchChatCancellation?.Dispose();

        _twitchChatCancellation =
            new CancellationTokenSource();

        _twitchChatSocket?.Dispose();

        _twitchChatSocket =
            new ClientWebSocket();

        await _twitchChatSocket.ConnectAsync(
            new Uri("wss://eventsub.wss.twitch.tv/ws"),
            _twitchChatCancellation.Token);

        string welcomeJson =
            await ReceiveTwitchWebSocketMessageAsync(
                _twitchChatSocket,
                _twitchChatCancellation.Token);

        using JsonDocument document =
            JsonDocument.Parse(welcomeJson);

        string? messageType =
            document.RootElement
                .GetProperty("metadata")
                .GetProperty("message_type")
                .GetString();

        if (!string.Equals(
                messageType,
                "session_welcome",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Twitch did not send an EventSub welcome message.");
        }

        string? sessionId =
            document.RootElement
                .GetProperty("payload")
                .GetProperty("session")
                .GetProperty("id")
                .GetString();

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException(
                "Twitch did not provide an EventSub session ID.");
        }

        JsonElement session =
            document.RootElement
                .GetProperty("payload")
                .GetProperty("session");

        if (session.TryGetProperty(
                "keepalive_timeout_seconds",
                out JsonElement keepaliveTimeoutElement) &&
            keepaliveTimeoutElement.TryGetInt32(
                out int keepaliveTimeoutSeconds) &&
            keepaliveTimeoutSeconds > 0)
        {
            _twitchChatKeepaliveTimeoutSeconds =
                keepaliveTimeoutSeconds;
        }

        return sessionId;
    }

    private async Task LoadSharedChatSessionAsync()
    {
        string? accessToken =
            _twitchAccessToken;

        string? broadcasterId =
            _twitchUserId;

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(broadcasterId))
        {
            return;
        }

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.twitch.tv/helix/shared_chat/session?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Client-Id",
            TwitchConfig.ClientId);

        try
        {
            using HttpResponseMessage response =
                await Http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement data =
                document.RootElement.GetProperty("data");

            if (data.GetArrayLength() == 0)
            {
                bool hadSharedChatState =
                    !string.IsNullOrWhiteSpace(
                        _sharedChatSessionId) ||
                    _sharedChatParticipantBroadcasterIds.Count > 0 ||
                    _observedSharedChatChatters.Count > 0;

                _sharedChatSessionId = null;
                _sharedChatParticipantBroadcasterIds.Clear();
                _observedSharedChatChatters.Clear();

                if (hadSharedChatState)
                {
                    RefreshDetectedUsersList();
                }

                return;
            }

            JsonElement session =
                data[0];

            string? newSessionId =
                session
                    .GetProperty("session_id")
                    .GetString();

            if (string.IsNullOrWhiteSpace(newSessionId))
            {
                return;
            }

            bool sessionChanged =
                !string.IsNullOrWhiteSpace(
                    _sharedChatSessionId) &&
                !string.Equals(
                    _sharedChatSessionId,
                    newSessionId,
                    StringComparison.Ordinal);

            HashSet<string> participantBroadcasterIds =
                new(StringComparer.Ordinal);

            if (session.TryGetProperty(
                    "participants",
                    out JsonElement participants) &&
                participants.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement participant in
                         participants.EnumerateArray())
                {
                    string participantBroadcasterId =
                        participant
                            .GetProperty("broadcaster_id")
                            .GetString() ?? "";

                    if (!string.IsNullOrWhiteSpace(
                            participantBroadcasterId))
                    {
                        participantBroadcasterIds.Add(
                            participantBroadcasterId);
                    }
                }
            }

            if (sessionChanged)
            {
                _observedSharedChatChatters.Clear();
            }

            _sharedChatSessionId =
                newSessionId;

            _sharedChatParticipantBroadcasterIds.Clear();
            _sharedChatParticipantBroadcasterIds.UnionWith(
                participantBroadcasterIds);

            if (sessionChanged)
            {
                RefreshDetectedUsersList();
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private async Task StartTwitchChatAsync()
    {
        if (_isTwitchChatIntentionallyStopped)
        {
            return;
        }

        string sessionId =
            await ConnectTwitchChatWebSocketAsync();

        await CreateTwitchChatSubscriptionAsync(
            sessionId);

        await LoadSharedChatSessionAsync();

        await LoadCurrentTwitchChattersAsync();

        _twitchChattersRefreshTimer.Start();

        SetLiveConnectionState(true);

        _ = ListenForTwitchChatMessagesAsync();
    }

    private async Task CreateTwitchChatSubscriptionAsync(
        string sessionId)
    {
        string? accessToken =
            _twitchAccessToken;

        string? userId =
            _twitchUserId;

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "Twitch chat cannot start without an authorized Twitch account.");
        }

        var subscription = new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new
            {
                broadcaster_user_id = userId,
                user_id = userId
            },
            transport = new
            {
                method = "websocket",
                session_id = sessionId
            }
        };

        string json =
            JsonSerializer.Serialize(subscription);

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "https://api.twitch.tv/helix/eventsub/subscriptions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Client-Id",
            TwitchConfig.ClientId);

        request.Content =
            new StringContent(
                json,
                System.Text.Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await Http.SendAsync(request);

        string responseJson =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not subscribe to Twitch chat: {responseJson}");
        }
    }

    private async Task LoadCurrentTwitchChattersAsync()
    {
        string? accessToken =
            _twitchAccessToken;

        string? userId =
            _twitchUserId;

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.twitch.tv/helix/chat/chatters?broadcaster_id={userId}&moderator_id={userId}&first=1000");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Client-Id",
            TwitchConfig.ClientId);

        using HttpResponseMessage response =
            await Http.SendAsync(request);

        string json =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not load Twitch chatters: {json}");
        }

        using JsonDocument document =
            JsonDocument.Parse(json);

        HashSet<string> currentChatters =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement chatter in
                 document.RootElement.GetProperty("data").EnumerateArray())
        {
            string login =
                chatter.GetProperty("user_login").GetString() ?? "";

            string displayName =
                chatter.GetProperty("user_name").GetString() ?? login;

            if (string.IsNullOrWhiteSpace(login))
            {
                continue;
            }

            currentChatters.Add(login);

            _userDisplayNames[login] =
                string.IsNullOrWhiteSpace(displayName)
                    ? login
                    : displayName;
        }

        _currentTwitchChatters.Clear();
        _currentTwitchChatters.UnionWith(currentChatters);

        RefreshDetectedUsersList();

        if (!_hasTwitchChatterBaseline)
        {
            _knownTwitchChatters.Clear();
            _knownTwitchChatters.UnionWith(currentChatters);

            _hasTwitchChatterBaseline = true;

            return;
        }

        bool enableJoinSound =
            FindName("EnableJoinSoundCheckBox")
                is System.Windows.Controls.CheckBox enableJoinSoundCheckBox &&
            enableJoinSoundCheckBox.IsChecked == true;

        bool enableJoinUsername =
            FindName("EnableJoinUsernameCheckBox")
                is System.Windows.Controls.CheckBox enableJoinUsernameCheckBox &&
            enableJoinUsernameCheckBox.IsChecked == true;

        bool muteNumbers =
            FindName("MuteNumbersCheckBox")
                is System.Windows.Controls.CheckBox muteNumbersCheckBox &&
            muteNumbersCheckBox.IsChecked == true;

        bool muteSpecialSymbols =
            FindName("MuteSpecialSymbolsCheckBox")
                is System.Windows.Controls.CheckBox muteSpecialSymbolsCheckBox &&
            muteSpecialSymbolsCheckBox.IsChecked == true;

        foreach (string username in currentChatters)
        {
            if (_knownTwitchChatters.Contains(username))
            {
                continue;
            }

            if (enableJoinSound)
            {
                PlayJoinSound();
            }

            if (!enableJoinUsername ||
                _mutedUsers.Contains(username))
            {
                continue;
            }

            string filteredUsername =
                username;

            if (muteNumbers)
            {
                filteredUsername =
                    string.Concat(
                        filteredUsername.Where(
                            character => !char.IsDigit(character)));
            }

            if (muteSpecialSymbols)
            {
                filteredUsername =
                    string.Concat(
                        filteredUsername.Where(
                            character =>
                                char.IsLetterOrDigit(character) ||
                                char.IsWhiteSpace(character)));
            }

            if (!string.IsNullOrWhiteSpace(filteredUsername))
            {
                SpeakText(
                    $"{filteredUsername} joined");
            }
        }

        _knownTwitchChatters.Clear();
        _knownTwitchChatters.UnionWith(currentChatters);
    }

    private async Task ListenForTwitchChatMessagesAsync()
    {
        ClientWebSocket? socket =
            _twitchChatSocket;

        CancellationTokenSource? cancellation =
            _twitchChatCancellation;

        if (socket is null ||
            cancellation is null)
        {
            throw new InvalidOperationException(
                "Twitch chat is not connected.");
        }

        while (socket.State == WebSocketState.Open &&
               !cancellation.IsCancellationRequested &&
               !_isTwitchChatIntentionallyStopped)
        {
            string json;

            try
            {
                using CancellationTokenSource receiveCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellation.Token);

                receiveCancellation.CancelAfter(
                    TimeSpan.FromSeconds(
                        _twitchChatKeepaliveTimeoutSeconds + 5));

                try
                {
                    json =
                        await ReceiveTwitchWebSocketMessageAsync(
                            socket,
                            receiveCancellation.Token);
                }
                catch (OperationCanceledException)
                    when (!cancellation.IsCancellationRequested)
                {
                    throw new WebSocketException(
                        "Twitch EventSub keepalive timed out.");
                }
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException)
                when (!cancellation.IsCancellationRequested)
            {
                while (!cancellation.IsCancellationRequested &&
                       !_isTwitchChatIntentionallyStopped &&
                       !string.IsNullOrWhiteSpace(_twitchAccessToken) &&
                       !string.IsNullOrWhiteSpace(_twitchUserId))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    if (cancellation.IsCancellationRequested ||
                        string.IsNullOrWhiteSpace(_twitchAccessToken) ||
                        string.IsNullOrWhiteSpace(_twitchUserId))
                    {
                        break;
                    }
                    try
                    {
                        await StartTwitchChatAsync();
                        break;
                    }
                    catch (WebSocketException)
                    {
                        cancellation = _twitchChatCancellation;

                        if (cancellation is null)
                        {
                            break;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        cancellation = _twitchChatCancellation;

                        if (cancellation is null)
                        {
                            break;
                        }
                    }
                }

                break;
            }

            using JsonDocument document =
                JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "metadata",
                    out JsonElement metadata))
            {
                continue;
            }

            string? messageType =
                metadata.TryGetProperty(
                    "message_type",
                    out JsonElement messageTypeElement)
                    ? messageTypeElement.GetString()
                    : null;

            if (string.Equals(
                    messageType,
                    "session_reconnect",
                    StringComparison.Ordinal))
            {
                string? reconnectUrl =
                    document.RootElement
                        .GetProperty("payload")
                        .GetProperty("session")
                        .GetProperty("reconnect_url")
                        .GetString();

                if (!string.IsNullOrWhiteSpace(reconnectUrl))
                {
                    ClientWebSocket reconnectSocket = new();

                    try
                    {
                        await reconnectSocket.ConnectAsync(
                            new Uri(reconnectUrl),
                            cancellation.Token);

                        string welcomeJson =
                            await ReceiveTwitchWebSocketMessageAsync(
                                reconnectSocket,
                                cancellation.Token);

                        using JsonDocument welcomeDocument =
                            JsonDocument.Parse(welcomeJson);

                        string? welcomeMessageType =
                            welcomeDocument.RootElement
                                .GetProperty("metadata")
                                .GetProperty("message_type")
                                .GetString();

                        if (string.Equals(
                                welcomeMessageType,
                                "session_welcome",
                                StringComparison.Ordinal))
                        {
                            JsonElement reconnectSession =
                                welcomeDocument.RootElement
                                    .GetProperty("payload")
                                    .GetProperty("session");

                            if (reconnectSession.TryGetProperty(
                                    "keepalive_timeout_seconds",
                                    out JsonElement reconnectKeepaliveElement) &&
                                reconnectKeepaliveElement.TryGetInt32(
                                    out int reconnectKeepaliveSeconds) &&
                                reconnectKeepaliveSeconds > 0)
                            {
                                _twitchChatKeepaliveTimeoutSeconds =
                                    reconnectKeepaliveSeconds;
                            }

                            socket.Dispose();

                            _twitchChatSocket = reconnectSocket;
                            socket = reconnectSocket;

                            continue;
                        }
                    }
                    catch (OperationCanceledException)
                        when (cancellation.IsCancellationRequested)
                    {
                        reconnectSocket.Dispose();
                        break;
                    }
                    catch (WebSocketException)
                    {
                    }

                    reconnectSocket.Dispose();
                }

                continue;
            }

            if (!string.Equals(
                    messageType,
                    "notification",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string? subscriptionType =
                metadata.TryGetProperty(
                    "subscription_type",
                    out JsonElement subscriptionTypeElement)
                    ? subscriptionTypeElement.GetString()
                    : null;

            if (!string.Equals(
                    subscriptionType,
                    "channel.chat.message",
                    StringComparison.Ordinal))
            {
                continue;
            }

            JsonElement eventData =
                document.RootElement
                    .GetProperty("payload")
                    .GetProperty("event");

            string chatterLogin =
                eventData
                    .GetProperty("chatter_user_login")
                    .GetString() ?? "";

            string chatterDisplayName =
                eventData
                    .GetProperty("chatter_user_name")
                    .GetString() ?? chatterLogin;

            string chatterName =
                string.IsNullOrWhiteSpace(chatterLogin)
                    ? chatterDisplayName
                    : chatterLogin;

            if (!string.IsNullOrWhiteSpace(chatterName))
            {
                _userDisplayNames[chatterName] =
                    string.IsNullOrWhiteSpace(chatterDisplayName)
                        ? chatterName
                        : chatterDisplayName;
            }

            JsonElement messageElement =
                eventData
                    .GetProperty("message");

            string messageText =
                messageElement
                    .GetProperty("text")
                    .GetString() ?? "";


            bool muteEmotes =
                FindName("MuteEmotesCheckBox")
                    is System.Windows.Controls.CheckBox muteEmotesCheckBox &&
                muteEmotesCheckBox.IsChecked == true;

            if (muteEmotes)
            {
                messageText =
                    EmoteFilter.RemoveTwitchEmotes(
                        messageElement);
            }

            if (!string.IsNullOrWhiteSpace(chatterName))
            {
                string? sourceBroadcasterId =
                    eventData.TryGetProperty(
                        "source_broadcaster_user_id",
                        out JsonElement sourceBroadcasterIdElement) &&
                    sourceBroadcasterIdElement.ValueKind ==
                        JsonValueKind.String
                        ? sourceBroadcasterIdElement.GetString()
                        : null;

                if (!string.IsNullOrWhiteSpace(sourceBroadcasterId))
                {
                    _sharedChatParticipantBroadcasterIds.Add(
                        sourceBroadcasterId);

                    _observedSharedChatChatters.Add(
                        chatterName);
                }
                else
                {
                    _observedSessionChatters.Add(
                        chatterName);
                }

                RefreshDetectedUsersList();
            }

            if (_mutedUsers.Contains(chatterName))
            {
                continue;
            }

            messageText =
                RemoveMutedPhrases(messageText);

            bool muteCommands =
                FindName("MuteCommandsCheckBox")
                    is System.Windows.Controls.CheckBox muteCommandsCheckBox &&
                muteCommandsCheckBox.IsChecked == true;

            if (muteCommands)
            {
                messageText =
                    string.Join(
                        " ",
                        messageText
                            .Split(
                                (char[]?)null,
                                StringSplitOptions.RemoveEmptyEntries)
                            .Where(
                                part =>
                                    !part.StartsWith(
                                        "!",
                                        StringComparison.Ordinal)));
            }

            bool muteLinks =
                FindName("MuteLinksCheckBox")
                    is System.Windows.Controls.CheckBox muteLinksCheckBox &&
                muteLinksCheckBox.IsChecked == true;

            if (muteLinks)
            {
                messageText =
                    string.Join(
                        " ",
                        messageText
                            .Split(
                                (char[]?)null,
                                StringSplitOptions.RemoveEmptyEntries)
                            .Where(
                                part =>
                                    !part.StartsWith(
                                        "http://",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    !part.StartsWith(
                                        "https://",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    !part.StartsWith(
                                        "www.",
                                        StringComparison.OrdinalIgnoreCase)));
            }

            messageText =
                string.Join(
                    " ",
                    messageText.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                bool speakUsername =
                    FindName("SpeakUsernamesCheckBox")
                        is System.Windows.Controls.CheckBox checkBox &&
                    checkBox.IsChecked == true;

                bool muteNumbers =
                    FindName("MuteNumbersCheckBox")
                        is System.Windows.Controls.CheckBox muteNumbersCheckBox &&
                    muteNumbersCheckBox.IsChecked == true;

                bool muteSpecialSymbols =
                    FindName("MuteSpecialSymbolsCheckBox")
                        is System.Windows.Controls.CheckBox muteSpecialSymbolsCheckBox &&
                    muteSpecialSymbolsCheckBox.IsChecked == true;

                string filteredMessageText =
                    messageText;

                if (muteNumbers)
                {
                    filteredMessageText =
                        string.Concat(
                            filteredMessageText.Where(
                                character => !char.IsDigit(character)));
                }

                if (muteSpecialSymbols)
                {
                    filteredMessageText =
                        string.Concat(
                            filteredMessageText.Where(
                                character =>
                                    char.IsLetterOrDigit(character) ||
                                    char.IsWhiteSpace(character)));
                }

                if (string.IsNullOrWhiteSpace(filteredMessageText))
                {
                    continue;
                }

                string filteredChatterName =
                    string.IsNullOrWhiteSpace(chatterDisplayName)
                        ? chatterName
                        : chatterDisplayName;

                if (muteNumbers)
                {
                    filteredChatterName =
                        string.Concat(
                            filteredChatterName.Where(
                                character => !char.IsDigit(character)));
                }

                if (muteSpecialSymbols)
                {
                    filteredChatterName =
                        string.Concat(
                            filteredChatterName.Where(
                                character =>
                                    char.IsLetterOrDigit(character) ||
                                    char.IsWhiteSpace(character)));
                }

                string speechText =
                    speakUsername &&
                    !string.IsNullOrWhiteSpace(filteredChatterName)
                        ? $"{filteredChatterName} said: {filteredMessageText}"
                        : filteredMessageText;

                SpeakText(speechText);
            }
        }
    }

    private static async Task<string> ReceiveTwitchWebSocketMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];

        using MemoryStream messageBuffer = new();

        while (true)
        {
            WebSocketReceiveResult result =
                await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException(
                    "Twitch closed the EventSub WebSocket connection.");
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            messageBuffer.Write(
                buffer,
                0,
                result.Count);

            if (result.EndOfMessage)
            {
                return System.Text.Encoding.UTF8.GetString(
                    messageBuffer.ToArray());
            }
        }
    }

    private string RemoveMutedPhrases(
        string text)
    {
        foreach (string phrase in _mutedPhrases)
        {
            if (string.IsNullOrWhiteSpace(phrase))
            {
                continue;
            }

            string pattern =
                $@"(?<!\w){Regex.Escape(phrase)}(?!\w)";

            text =
                Regex.Replace(
                    text,
                    pattern,
                    "",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);
        }

        return text;
    }

    private void AddMutedPhraseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string phrase =
            MutedPhraseInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(phrase) ||
            _mutedPhrases.Exists(
                existing =>
                    string.Equals(
                        existing,
                        phrase,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _mutedPhrases.Add(phrase);
        MutedPhrasesList.Items.Add(phrase);

        MutedPhraseStore.Save(_mutedPhrases);

        MutedPhraseInput.Clear();
    }

    private void RemoveMutedPhraseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (MutedPhrasesList.SelectedItem is not string phrase)
        {
            return;
        }

        _mutedPhrases.RemoveAll(
            existing =>
                string.Equals(
                    existing,
                    phrase,
                    StringComparison.OrdinalIgnoreCase));

        MutedPhrasesList.Items.Remove(phrase);

        MutedPhraseStore.Save(_mutedPhrases);
    }

    private void VoiceSearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox searchTextBox)
        {
            return;
        }

        string searchText =
            searchTextBox.Text.Trim();

        VoiceComboBox.Items.Filter =
            item =>
            {
                if (item is not TtsVoice voice)
                {
                    return false;
                }

                return string.IsNullOrWhiteSpace(searchText) ||
                       voice.DisplayName.Contains(
                           searchText,
                           StringComparison.OrdinalIgnoreCase) ||
                       voice.ProviderName.Contains(
                           searchText,
                           StringComparison.OrdinalIgnoreCase);
            };

        VoiceComboBox.Items.Refresh();
    }

    private void UserSearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        _userSearchText =
            UserSearchTextBox.Text.Trim();

        RefreshDetectedUsersList();
    }

    private async Task<ManualTwitchUserLookupResult?>
        FindManualTwitchUserAsync(
            string login)
    {
        TwitchUser? user =
            await FindTwitchUserByLoginAsync(login);

        if (user is null)
        {
            return null;
        }

        string canonicalLogin =
            string.IsNullOrWhiteSpace(user.Login)
                ? login.Trim()
                : user.Login;

        string displayName =
            string.IsNullOrWhiteSpace(user.DisplayName)
                ? canonicalLogin
                : user.DisplayName;

        return new ManualTwitchUserLookupResult(
            canonicalLogin,
            displayName);
    }

    private void AddManualUserButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        AddManualUserWindow dialog =
            new(FindManualTwitchUserAsync)
            {
                Owner = this
            };

        if (dialog.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(dialog.SelectedLogin))
        {
            return;
        }

        string login =
            dialog.SelectedLogin.Trim();

        string displayName =
            string.IsNullOrWhiteSpace(dialog.SelectedDisplayName)
                ? login
                : dialog.SelectedDisplayName.Trim();

        _userDisplayNames[login] =
            displayName;

        if (_mutedUsers.Add(login))
        {
            MutedUserStore.Save(_mutedUsers);
        }

        RefreshDetectedUsersList();
    }

    private void DetectedUsersList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DetectedUsersList.SelectedItem is string displayText)
        {
            _selectedUsername =
                GetUsernameFromDisplayText(displayText);
        }
    }

    private void DetectedUsersList_LostKeyboardFocus(
        object sender,
        System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        DetectedUsersList.SelectedIndex = -1;
    }

    private void RefreshDetectedUsersList()
    {
        HashSet<string> visibleUsers =
            new(
                _currentTwitchChatters,
                StringComparer.OrdinalIgnoreCase);

        visibleUsers.UnionWith(
            _observedSessionChatters);

        visibleUsers.UnionWith(
            _observedSharedChatChatters);

        visibleUsers.UnionWith(
            _mutedUsers);

        IEnumerable<string> filteredUsers =
            visibleUsers;

        if (!string.IsNullOrWhiteSpace(_userSearchText))
        {
            filteredUsers =
                filteredUsers.Where(
                    username =>
                        username.Contains(
                            _userSearchText,
                            StringComparison.OrdinalIgnoreCase));
        }

        DetectedUsersList.Items.Clear();

        foreach (string username in
                 filteredUsers.OrderBy(
                     username => username,
                     StringComparer.OrdinalIgnoreCase))
        {
            DetectedUsersList.Items.Add(
                GetUserDisplayText(username));
        }
    }

    private string GetUserDisplayText(
        string username)
    {
        string displayName =
            _userDisplayNames.TryGetValue(
                username,
                out string? savedDisplayName) &&
            !string.IsNullOrWhiteSpace(savedDisplayName)
                ? savedDisplayName
                : username;

        return _mutedUsers.Contains(username)
            ? $"🔇 {displayName}"
            : displayName;
    }

    private string GetUsernameFromDisplayText(
        string displayText)
    {
        const string mutedPrefix = "🔇 ";

        string visibleName =
            displayText.StartsWith(
                mutedPrefix,
                StringComparison.Ordinal)
                ? displayText[mutedPrefix.Length..]
                : displayText;

        foreach (KeyValuePair<string, string> user in
                 _userDisplayNames)
        {
            if (string.Equals(
                    user.Value,
                    visibleName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return user.Key;
            }
        }

        return visibleName;
    }

    private void RefreshDisplayedUsername(
        string username)
    {
        for (int i = 0;
             i < DetectedUsersList.Items.Count;
             i++)
        {
            if (DetectedUsersList.Items[i] is not string displayText)
            {
                continue;
            }

            string itemUsername =
                GetUsernameFromDisplayText(displayText);

            if (!string.Equals(
                    itemUsername,
                    username,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DetectedUsersList.Items[i] =
                GetUserDisplayText(username);

            break;
        }
    }

    private void MuteUserButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedUsername))
        {
            return;
        }

        string username =
            _selectedUsername;

        if (_mutedUsers.Add(username))
        {
            MutedUserStore.Save(_mutedUsers);
        }

        RefreshDetectedUsersList();

        _selectedUsername = null;
    }

    private void UnmuteUserButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedUsername))
        {
            return;
        }

        string username =
            _selectedUsername;

        if (_mutedUsers.Remove(username))
        {
            MutedUserStore.Save(_mutedUsers);
        }

        RefreshDetectedUsersList();

        _selectedUsername = null;
    }

    private void DisconnectTwitchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _twitchChattersRefreshTimer.Stop();

        _twitchChatCancellation?.Cancel();
        _twitchChatCancellation?.Dispose();
        _twitchChatCancellation = null;

        _twitchChatSocket?.Dispose();
        _twitchChatSocket = null;

        _knownTwitchChatters.Clear();
        _currentTwitchChatters.Clear();
        _observedSessionChatters.Clear();
        _observedSharedChatChatters.Clear();

        _sharedChatSessionId = null;
        _sharedChatParticipantBroadcasterIds.Clear();

        _hasTwitchChatterBaseline = false;

        RefreshDetectedUsersList();

        _twitchAccessToken = null;
        _twitchRefreshToken = null;
        _twitchUserId = null;

        TwitchTokenStore.Delete();

        ResetTwitchAccountUi();
    }

    private void ResetTwitchAccountUi()
    {
        SetLiveConnectionState(false);

        TwitchUsernameText.Text =
            "No Twitch account authorized";

        TwitchConnectionStatusText.Text =
            "Not connected";

        TwitchProfileImage.Fill =
            new SolidColorBrush(
                Color.FromRgb(
                    0x18,
                    0x24,
                    0x3B));

        AuthorizeTwitchButton.IsEnabled = true;
        DisconnectTwitchButton.IsEnabled = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _twitchChattersRefreshTimer.Stop();

        _twitchChatCancellation?.Cancel();
        _twitchChatCancellation?.Dispose();
        _twitchChatCancellation = null;

        _twitchChatSocket?.Dispose();
        _twitchChatSocket = null;

        base.OnClosed(e);
    }

    private void MinimizeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        WindowState =
            WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private sealed class AudioOutputDeviceOption
    {
        public AudioOutputDeviceOption(
            string displayName,
            string? deviceId)
        {
            DisplayName = displayName;
            DeviceId = deviceId;
        }

        public string DisplayName { get; }

        public string? DeviceId { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = "";

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = "";

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
    }

    private sealed class TwitchErrorResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    private sealed class TwitchUsersResponse
    {
        [JsonPropertyName("data")]
        public List<TwitchUser> Data { get; set; } = [];
    }

    private sealed class TwitchUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("login")]
        public string Login { get; set; } = "";

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; } = "";
    }
}