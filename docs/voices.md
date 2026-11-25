# Voices: who hears which voice

WalkieTalkie reads voice messages aloud with the **receiving** browser's text-to-speech (TTS).
A player picks their voice on the Player page and it is saved with their account.
Every receiver then *tries* to use that voice for them, but a browser can only speak with voices it has.
So what a receiver hears depends on the receiver's browser, not the sender's.

## Which voices each browser has

| Browser | Voices it can use |
|---|---|
| Edge (Windows) | Voices installed in Windows (e.g. Microsoft David, Zira, Mark) **plus** Microsoft's online "Online (Natural)" voices in many languages |
| Chrome (Windows) | Voices installed in Windows **plus** Google's own voices ("Google UK English Female", "Google Nederlands", …) |
| Phone (Android / iPhone) | WalkieTalkie always uses the phone's default voice |

Edge's "Online (Natural)" voices exist only in Edge, and Google's voices exist only in Chrome.
Windows-installed voices exist in both, on PCs that have the same language pack installed.

## What the receiver hears

| Sender picks | Receiver in Edge | Receiver in Chrome | Receiver on a phone |
|---|---|---|---|
| Edge "Online (Natural)" voice | That voice | Not available: fallback (see below), usually the default voice | Phone default voice |
| Windows voice (David, Zira, Mark, or another installed language) | That voice | That voice, if the PC has the same Windows voice | Phone default voice |
| Chrome "Google …" voice | Not available: fallback in the same language (e.g. Google Nederlands → Microsoft Fenna) | That voice | Phone default voice |

In short:
- **Edge to Edge:** a voice chosen by one player is used for them on the other player's browser, both ways.
- **Edge to Chrome:** Edge's online voices aren't in Chrome, so Chrome usually speaks with its default voice.
- **Chrome to Edge:** a voice chosen in Chrome is used in Edge when it's a Windows voice. For a Google voice, Edge uses another voice in the same language.
- **Phones (including Chrome on Android):** always the phone's default voice.

## Fallback when the voice isn't available

The receiving browser picks, in order:
1. The exact voice the sender chose.
2. A voice with the same language and region (e.g. en-NZ).
3. A voice with the same language (e.g. any English voice).
4. The browser's default voice.

Chrome on Windows often only has US English voices, so for most voices chosen in Edge it ends up on step 3 or 4, i.e. Microsoft David, which is also its default.

## Seeing what was used

When a player saves a voice, everyone in their groups gets a chat line, e.g.

`Mary chose voice: Microsoft Molly Online (Natural) - English (New Zealand) [en-NZ]; here: not available, using Default voice`

The part after `here:` is worked out by each receiving browser: `available (…)`, or `not available, using …`. Phones show `Default voice (phone)`.

## Getting more voices in Chrome

Install the language's speech pack in Windows: **Settings → Time & language → Speech → Add voices**, or **Language & region → Add a language** with only Text-to-speech ticked. Or run in an administrator PowerShell:

```powershell
Add-WindowsCapability -Online -Name "Language.TextToSpeech~~~fi-FI~0.0.1.0"
```

Then close every Chrome window and reopen it. Edge's online voices can't be added to Chrome.

## Speech-to-text is different

Turning speech into text happens in the **sender's** browser and needs no installed voices.
Edge sends the audio to Microsoft's speech service and Chrome sends it to Google's.
Only the read-aloud (TTS) side depends on the voices installed on the receiving device.
