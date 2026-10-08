# Chat Out Loud

Chat Out Loud is a lightweight Windows desktop TTS application for Twitch chat.

It connects directly to Twitch and reads incoming chat messages aloud using Windows voices or supported cloud TTS providers.

## Features

- Twitch account authentication
- Saved Twitch login and automatic session restoration
- Live Twitch chat connection
- Automatic chat reconnection
- Windows TTS voice selection
- Google Cloud Text-to-Speech support
- Microsoft Azure Speech support
- ElevenLabs TTS support
- Searchable voice selection grouped by provider
- Saves selected TTS provider and voice
- Secure local storage for TTS provider credentials
- TTS provider connection verification
- Local voice catalog caching
- Adjustable speech volume
- Adjustable speech speed
- Selectable audio output device
- FIFO speech queue
- Skip current message
- Clear speech queue
- Viewers / Bots management and search
- Manual Twitch user lookup
- Mute specific users
- Mute specific words and phrases
- Filter commands
- Filter links
- Filter numbers
- Filter special symbols
- Filter Twitch emotes
- Custom join sound
- Adjustable join sound volume
- Optional username announcement for join sounds
- RUN / STOP live chat controls
- Saved local settings
- Single-instance application behavior

## Requirements

- Windows 10 or Windows 11
- 64-bit Windows
- Twitch account
- Internet connection
- Windows-compatible audio output device

Cloud TTS providers require users to supply their own accounts and credentials.

## Installation

Download the latest `ChatOutLoud_Setup.exe` from the Releases section.

Run the installer and launch Chat Out Loud.

Each user authorizes their own Twitch account and configures their preferred TTS provider, voice, audio output, and other settings.

Settings and authorization are saved locally and restored when the application is reopened.

## Version

**v0.4.0**

Chat Out Loud is free to use.

## Privacy

Chat Out Loud stores user settings, Twitch authentication information, and optional TTS provider credentials locally on the user's computer.

Sensitive authentication data and provider credentials are stored using Windows-protected storage.

User-specific settings, authentication data, and provider credentials are not included with the distributed installer.