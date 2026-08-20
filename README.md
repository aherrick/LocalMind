# LocalMind

[![Release](https://img.shields.io/github/v/release/aherrick/LocalMind?display_name=tag&sort=semver)](https://github.com/aherrick/LocalMind/releases)

LocalMind is a Windows desktop chat app for running local AI models through Foundry Local and Ollama.

## Get Started

1. Download and install the latest Windows release from [Releases](https://github.com/aherrick/LocalMind/releases). It is self-contained and includes the Windows App SDK runtime.
2. Open LocalMind and select an installed model, or download a Foundry Local model in **Settings**.
3. Start a chat. You can change the model between responses at any time.

To use Ollama models, install and run [Ollama](https://ollama.com/), then pull a model such as:

```powershell
ollama pull llama3.2
```

LocalMind detects available Ollama models automatically.

## Features

- Local model chat with Foundry Local and Ollama
- Chat history, pinning, search, and conversation export
- Markdown messages, timestamps, copy, and regenerate
- System prompt, theme, startup, and update settings
- Tray controls and local diagnostics logs

## Build From Source

Only needed for development. Installed releases are self-contained and require no SDK.

Prerequisites: Windows and the .NET 10 SDK.

```powershell
dotnet build LocalMind.csproj -c Debug /p:Platform=x64
dotnet run --project LocalMind.csproj -c Debug /p:Platform=x64
```