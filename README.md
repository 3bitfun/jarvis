# J.A.R.V.I.S.

A C# / .NET 8 Avalonia desktop assistant with local GGUF and cloud providers, a quick chat/app launcher, a full chat window, memory, skills, and MCP tools.

Version 1.1.0 adds tray access, single-instance launch handling, first-run setup, encrypted drafts, recovery backups, richer Markdown, and dedicated image-service settings. See [release notes and checks](RELEASE.md) and [the changelog](CHANGELOG.md).

Version 1.2.0 adds an in-app updater and GGUF downloader. Version 1.1.1 added an original vector icon system, matching SVG/PNG exports, and a new app mark. Browse [the icon library](Assets/Icons/README.md), or run `scripts/Export-Icons.ps1` to regenerate the assets and a ZIP under `dist/design`.

## Develop

Install the .NET 8 SDK or a compatible later SDK, then run:

```powershell
dotnet restore JARVIS.csproj
dotnet run --project JARVIS.csproj -c Debug
```

On Windows, `manager.bat` also offers Debug, Release publish, and installer builds. Alt+Space opens the quick bar; Alt+J toggles the full deck. Close any other running copy before starting one that needs the global hotkeys.

The core UI styles are in [Styles/Controls.axaml](Styles/Controls.axaml). See [the style guide](Styles/README.md) for layout, typography, and glass settings.

## Models and providers

Use Settings → Engine to configure a cloud provider, download a curated or custom GGUF, or import one from disk. Settings → About checks published releases and offers a verified installer download. [Models and slash commands](MODELS-AND-COMMANDS.md) describes provider model fetching, custom slugs, GGUF downloads, and the command menu. [MCP and skills](MCP.md) explains connected tools and local guidance.

Downloaded models are excluded from Git. A fresh clone contains an empty `models` folder; copy a GGUF there or use the in-app downloader/importer. Runtime credentials, chats, and memory are stored outside the repository in the user's local app data folder.

## Windows installer

```powershell
.\installer\Build-Installer.ps1
```

The resulting EXE is written to `dist/installer`. Setup can import an optional GGUF; cloud-only installation requires none. For build tooling and bundled-model options, see [installer instructions](installer/README.md).

Run `scripts/Check.ps1` for the isolated regression suite or add `-Extended` for native launcher, font, and glass checks. The same core suite is configured in `.github/workflows/build.yml` for a future GitHub remote.

## Version control

The main branch is `main`. Commit related changes together with a short description:

```powershell
git status
git diff
git add <files>
git commit -m "Describe the change"
git log --oneline
```

For larger changes, create a branch with `git switch -c feature/your-change`. Record release changes in [CHANGELOG.md](CHANGELOG.md). When a release is ready, create an annotated version tag, for example `git tag -a v1.0.0 -m "Release 1.0.0"`.

The repository starts locally. To connect a remote you own:

```powershell
git remote add origin <repository-url>
git push -u origin main
```

Builds, installers, logs, model weights, and private local data are ignored. Commit source, UI styles, documentation, installer scripts, and font license files.

### 1.2.1 controls

The composer offers Instant, Quick, and Think more, remembered per chat. Instant limits replies to 256 tokens and skips local knowledge retrieval. These modes guide the model; provider-specific hidden reasoning controls are not assumed. New profiles default to 1024 reply tokens; existing settings remain saved.

Settings → Style includes glass on/off and editable global shortcuts. Minimalist, Terminal, GitHub Style and ChatGPT Style use solid surfaces, with different corner sizes, density or font. These profiles retain the same navigation structure. About includes credits, version, updater and welcome tutorial replay. The full deck no longer stays above other apps.

The welcome tutorial accepts an optional name and offers a GGUF download, cloud/local setup, starter actions, memory choice and skip. Model downloads remain optional. Automatic titles prefer an installed local GGUF and fall back to the first prompt if generation fails; manually renamed chats are preserved. Knowledge settings can inspect, edit or reset the active compact summary. Raw chat history remains available.
