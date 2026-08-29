# Chat Out Loud

Chat Out Loud is a lightweight Windows desktop TTS application designed for Twitch chat.

It connects directly to Twitch, processes incoming chat messages through configurable filters, and reads them aloud through the user's selected TTS voice and Windows audio output device.

## Features

Current implemented features include:

- Twitch account authentication
- Saved Twitch authorization
- Automatic Twitch session restoration
- Live Twitch chat connection
- RUN / STOP live chat controls
- Automatic chat reconnection support
- Twitch user/profile detection
- Users / Bots roster
- Users / Bots search
- Manual Twitch user lookup
- Persistent muted users
- Mute specific users
- Mute specific words and phrases
- Filter commands
- Filter links
- Filter numbers
- Filter special symbols
- Twitch emote filtering
- Speech username announcements
- FIFO speech queue
- Skip current message
- Clear speech queue
- Selectable Windows audio output device
- Saved audio output selection
- Virtual Audio Cable compatible output routing
- Custom chat join sound
- Adjustable join sound volume
- Optional username announcement when a viewer joins
- Saved local application settings
- Single-instance application behavior

### TTS Voices

Chat Out Loud currently supports:

- Built-in Windows TTS voices
- Google Cloud Text-to-Speech
- Grouped voice selection by provider
- Searchable voice list
- Persistent selected provider and voice
- Local voice-catalog caching
- Secure local storage for provider credentials
- Shared audio playback/output routing for Windows and cloud-generated speech

Google Cloud TTS configuration currently includes:

- Service-account credential selection
- Connection verification
- Automatic secure credential storage
- Voice catalog retrieval
- Cached Google voice catalog
- Removal of saved Google configuration
- Google voice synthesis through the normal Chat Out Loud speech queue

Additional TTS provider support is currently being developed, including:

- Microsoft Azure Speech
- ElevenLabs
- Amazon Polly
- Deepgram
- Cartesia
- PlayAI

These providers should not be considered fully supported until their configuration, voice retrieval, synthesis, persistence, and real-service testing are completed.

### Shared Chat / Stream Share

Support for Twitch Shared Chat / Stream Share is also being developed.

The application contains logic for detecting and managing users observed through Shared Chat, but full real-world multi-channel testing is still required before this feature should be considered complete.

## Development Status

**Chat Out Loud is still under active development.**

Although many core features are functional, the application is **not feature-complete or fully optimized yet**.

There are still:

- unfinished TTS provider integrations
- features requiring additional real-world testing
- possible bugs that have not yet been discovered
- known behaviors that require additional regression testing
- startup and voice-catalog loading performance work
- UI responsiveness optimizations
- provider error-handling improvements
- external-provider speech speed and volume work
- Shared Chat testing
- emote/emoji filtering verification
- Users / Bots and manual-user-search verification
- long-session stability testing
- additional security and privacy review
- final release cleanup and packaging work

Performance work is currently underway to ensure large voice catalogs and multiple configured TTS providers do not cause slow application startup or UI freezes.

Once all intended TTS-provider pairing and configuration systems are complete, Chat Out Loud will go through an extensive bug-fixing, regression-testing, optimization, and long-run testing phase before being prepared for release.

## Requirements

- Windows 10 or Windows 11
- 64-bit Windows
- Twitch account
- Internet connection for Twitch and cloud-based TTS providers
- Windows-compatible audio output device

Windows TTS voices do not require a cloud TTS account.

Optional cloud TTS providers require users to configure their own provider accounts and credentials.

## Installation

Download the latest available Chat Out Loud release and follow the included installation instructions.

Each user authorizes their own Twitch account and configures their own local settings and optional TTS-provider credentials.

## Version

Current release: **v0.1.0**

Development builds may contain functionality that has not yet been included in a public release.

## Privacy

Chat Out Loud stores user settings and Twitch authentication information locally on the user's computer.

Optional TTS-provider credentials are also stored locally using protected storage where applicable.

User-specific settings, Twitch authentication data, API credentials, service-account files, and other private configuration data are not intended to be included with distributed builds or public source releases.
