# TrueMinutes for Windows

TrueMinutes for Windows is a privacy-first, local meeting assistant that captures audio, transcribes speech, and generates structured summaries without joining calls as a bot.

## Prerequisites

- **Windows 10 (19041+) or Windows 11**
- **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/8)
- **Visual Studio 2022** (17.8+) with "Windows App SDK" workload, or **VS Code** + C# Dev Kit
- **Ollama** (for local summaries) — [ollama.ai](https://ollama.ai) → `ollama pull qwen2.5:7b`

## Build & Run

```powershell
# Clone + restore
git clone https://github.com/AbhiRishi96/trueminutes-windows
cd trueminutes-windows
dotnet restore

# Build
dotnet build -c Release

# Run (Debug)
dotnet run
```

## GPU Acceleration (optional, speeds up transcription 5–10×)

### NVIDIA GPU (CUDA)
Add to the `.csproj`:
```xml
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.7.3" />
```

### Any DirectX 12 GPU (DirectML — Intel, AMD, NVIDIA)
Add to the `.csproj`:
```xml
<PackageReference Include="Whisper.net.Runtime.CoreML" Version="1.7.3" />
```

## Architecture

This repository mirrors the core architecture of the macOS version while replacing platform integrations with Windows-native equivalents.

### macOS → Windows API mapping (key)

| macOS | Windows |
|-------|---------|
| `AudioHardwareCreateProcessTap` | WASAPI Loopback (`WasapiLoopbackCapture`) |
| `AVAudioEngine` mic input | WASAPI Capture (`WasapiCapture`) |
| `NSWorkspace.runningApplications` | `Process.GetProcesses()` + WMI |
| `kAudioProcessPropertyIsRunningInput` | `IAudioSessionManager2` session state |
| AppleScript tab inspection | Chrome DevTools Protocol (CDP) |
| WhisperKit / CoreML | Whisper.net (wraps whisper.cpp) |
| GRDB / SQLite | Microsoft.Data.Sqlite + Dapper |
| `NSStatusItem` | `NotifyIcon` (system tray) |
| Keychain | Windows DPAPI (`ProtectedData`) |
| Sparkle auto-update | Velopack |

## Project Structure

```
Capture/        — WASAPI loopback + mic capture + audio chunking
Detect/         — Process/window/mic monitoring + browser CDP + meeting state machine
Transcribe/     — Whisper.net ASR + transcription settings
Summarize/      — Ollama HTTP client + paragraph formatter + speaker paragraphs
Store/          — SQLite migrations (v1–v13, same schema as macOS) + repositories
Security/       — DPAPI credential store (replaces macOS Keychain)
Calendar/       — Google + Outlook OAuth (same REST as macOS) + Windows calendar
UI/             — WinUI 3 XAML (Phase 4)
App/            — App entry point, tray icon, AppState
Resources/      — Prompts (copied from macOS), icons
```

## Phase Status

- [x] **Phase 1** — Audio capture (WASAPI loopback + mic) + Whisper.net transcription
- [x] **Phase 2** — Meeting detection (Process/window/CDP/mic-hot)
- [x] **Phase 3** — Summarization (Ollama), DB migrations, credential store ← *current*
- [ ] **Phase 4** — WinUI 3 full UI (tray icon, floating pill, main window, settings)
- [ ] **Phase 5** — Signing (EV cert), Velopack updates, MSIX packaging

## Distribution

### Internal (unsigned)
Build a self-contained exe:
```powershell
dotnet publish -r win-x64 -c Release --self-contained
```
Distribute the `publish/` folder as a zip. Recipients may need to allow in Windows Security.

### Production (Velopack)
```powershell
# Install Velopack CLI
dotnet tool install -g vpk

# Package + push to GitHub Releases
vpk pack --packId TrueMinutes --packVersion 0.9.0 --packDir publish/
vpk upload github --repoUrl https://github.com/AbhiRishi96/trueminutes-windows --token $GITHUB_TOKEN
```

## Privacy Notes

- System audio is captured via WASAPI loopback (the default speaker output).
- DRM-protected content (Netflix, Spotify) is automatically excluded by the Windows audio graph.
- All transcription and summarization run locally by default (Whisper.net + Ollama).
- OAuth tokens for Google/Outlook calendar are stored encrypted via Windows DPAPI.
