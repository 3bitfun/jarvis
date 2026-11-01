# Windows installer

Run `dist\installer\JARVIS-Setup-1.0.0-win-x64.exe` to install for the current Windows account. Setup includes the .NET runtime and does not require administrator access. Default destination: `%LOCALAPPDATA%\Programs\JARVIS`.

Setup offers an optional GGUF file picker. Leave it empty for cloud-only use, or select a complete downloaded GGUF to copy into the installed `models` folder. The GGUF header is checked before installation. Setup preserves a model if its destination filename already exists. Additional models can be imported later from Settings → Engine. No model is downloaded automatically.

Start menu and optional desktop shortcuts are created. Launch the installed app, then use Alt+Space for the quick bar or Alt+J for the full window. Close any other copy first, since only one copy can own those global hotkeys. Cloud API keys are entered in the app after setup.

Uninstall through Windows Settings → Apps or the Start menu shortcut. Uninstall removes known application files and shortcuts; it preserves model files and the existing `%LOCALAPPDATA%\JARVIS` configuration, chats, and memory. Delete those manually only if you want to erase them.

## Build setup

Use manager.bat option 3 for the normal installer, or option 4 to bundle a chosen GGUF. The normal installer never packages files from the project's models directory. A bundled GGUF is a separate, unchecked component in setup. Make sure you have permission to redistribute the selected model and include any required model license files before sharing a bundled installer. NSIS has an installer size limit, so large GGUF files should be imported during or after setup rather than embedded.

The equivalent PowerShell commands are:

```powershell
.\installer\Build-Installer.ps1
.\installer\Build-Installer.ps1 -GgufPath 'C:\Models\your-model.gguf'
```

Builds use a fresh self-contained Windows x64 publish with model copying disabled. The build script obtains a portable NSIS compiler in artifacts, checks the downloaded archive against a pinned SHA-256, creates an installer, and writes a neighboring SHA-256 file. No compiler installation or global configuration is required. NSIS project: https://nsis.sourceforge.io/Download

The installer is currently unsigned. Code signing can be added when a signing certificate is available.
