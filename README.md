# Blazor Online WalkieTalkie — 2-person text and TTS voice chat

> Walkie-talkie style chat between 2 logged-in users over the internet.
> Lobby text chat plus hold-to-talk voice: your speech is transcribed in the
> browser, sent as a voice message, and spoken aloud on the other person's
> device using your chosen TTS voice.
>
> Users must be in the same group/s to be able to chat with each other. See the
> Home page in the app for details. A player can only be logged in once at a
> time. Login sessions time out (see Admin settings).

Derived from the [Draughts/Checkers game app](https://github.com/djaus2/Drafts)
with the game-play removed — only the lobby chat and walkie-talkie voice
functionality remains.

> Latest: Added FlipPhone UI.

## How it works

- **Text chat**: type a message in Lobby Chat and press Enter. Messages go to
  all groups you are a member of.
- **Talk**: hold the **Talk** button and speak. Speech is converted to text by
  the browser's speech recognition and sent as a `[VOICE]` message. On other
  logged-in group members' devices it is spoken aloud via speech synthesis,
  using the sender's preferred voice.
- **Mute**: mutes voice playback/talk for everyone until the muter unmutes.
- **Voice selection**: desktop users can pick a TTS voice on the Player page.
  Phone clients use the default voice.

## Admin

The Admin user can manage players and groups, curate the allowed TTS voice
list, change settings (max login hours, PIN-change permission), and view
activity logs.

## Tech

- Blazor Server (.NET 10), SQLite + EF Core for users/groups/settings/logs
- In-memory lobby chat + voice floor services
- Web Speech API (`SpeechRecognition` + `speechSynthesis`) in
  `wwwroot/js/walkieTalkie.js`
