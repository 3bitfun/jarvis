# Release 1.3.0

## Build and verify

```powershell
.\scripts\Check.ps1 -Extended
.\installer\Build-Installer.ps1
dotnet publish JARVIS.csproj -c Release -r win-x64 --self-contained true -p:IncludeLocalModels=false -o dist\win-x64-latest
```

The normal installer excludes GGUF weights, keys, local chats, and generated files. It can import a GGUF during setup; leaving the picker empty installs the app for cloud use. A SHA-256 file and JSON build manifest accompany the installer. Version comes from `JARVIS.csproj`.

The automated checks use temporary profiles, mock provider endpoints, and local test files. They cover model discovery/auth headers/Gemini pagination, GGUF download validation, settings encryption and recovery, chat deletion and backup cleanup, private-session storage, context budgeting for skills, response bounds, workspace traversal rejection, UI initialization and Markdown rendering, launcher ranking/shortcuts/icons, bundled fonts, and native transparency availability. They do not send prompts to paid cloud services. To exercise a local model separately:

```powershell
dotnet bin\Release\net8.0\JARVIS.dll --check-model models\your-model.gguf
```

## Manual release checks

- Install as a standard Windows user, with and without a GGUF import. Check shortcuts, optional model copying, and uninstall/reinstall while preserving user data.
- Close old copies, then check Alt+Space, Alt+J, tray access, and a repeated launch. Shortcut conflicts should name the unavailable shortcut and keep the other one working.
- Check glass appearance, readable contrast, scrolling, keyboard navigation, and the settings pane on the intended Windows displays/DPI settings.
- Configure real provider credentials and verify a model fetch, a chat reply, a cancel/retry, an image request against a compatible image model, and an approved MCP call against a trusted server.
- Download a curated GGUF and a custom direct GGUF URL; cancel a partial download and confirm no model is left behind. Check the publisher's model license.
- Publish a test release with a valid installer digest, then verify the in-app updater's check, download, hash verification, setup launch, and preservation of chats/models. Draft releases are intentionally not offered.

This build has no code-signing certificate configured. The installer is unsigned. MCP/cloud account permissions, service compatibility, and model quality depend on the configured service and model.

## Support and privacy

Settings → About offers a support diagnostics summary that excludes keys, chat text, endpoint URLs, and the personal profile. Startup fault logs omit exception messages and live under the app data folder. Settings are encrypted for the current user; chat JSON is local and readable by that user. Pausing memory prevents new turns and drafts from being saved. Deleting a conversation or clearing saved details also updates rolling backups and removes old corruption snapshots for that store.

For isolated development profiles, set `JARVIS_DATA_DIR` to an absolute directory. Normal operation defaults to `%LOCALAPPDATA%\JARVIS`. User files and imported GGUF models are preserved during application uninstall.
