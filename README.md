# LocalMind

[![Release](https://img.shields.io/github/v/release/aherrick/LocalMind?display_name=tag&sort=semver)](https://github.com/aherrick/LocalMind/releases)

LocalMind is a Windows desktop chat app for running local AI models through Foundry Local, Ollama, and llama.cpp.

## Get Started

1. Download and install the latest Windows release from [Releases](https://github.com/aherrick/LocalMind/releases). It is self-contained and includes the Windows App SDK runtime.
2. Open LocalMind and select an installed model, or download a Foundry Local model in **Settings**.
3. Start a chat. You can change the model between responses at any time.

To use Ollama models, install and run [Ollama](https://ollama.com/), then pull a model such as:

```powershell
ollama pull llama3.2
```

LocalMind detects available Ollama models automatically.

To use llama.cpp, install it and start a server. LocalMind connects to a running llama-server on `http://127.0.0.1:8080` and lists whatever model it is serving.

1. Install llama.cpp:

   ```powershell
   winget install llama.cpp
   ```

   Restart your shell (or LocalMind) afterward so the new `PATH` takes effect.

2. Start a server for a model. For example, to serve [Muse Glimmer 30B](https://huggingface.co/meta-models/Muse-Glimmer-30B-GGUF):

   ```powershell
   llama-server -hf meta-models/Muse-Glimmer-30B-GGUF:Q4_K_M --host 127.0.0.1 --port 8080
   ```

   The first run downloads the model (~17 GB); later runs reuse the cache. Add `--offline` to skip the network check once it is cached.

3. In LocalMind, the served model appears under **llama.cpp** in **Settings** and in the model picker. Keep the server running while you chat.

## Features

- Local model chat with Foundry Local, Ollama, and llama.cpp
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