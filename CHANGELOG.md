# Changelog

## Unreleased

## 1.3.4 — 2026-11-02

- Alt+Space opens pet chat and search, temporarily showing the pet when it is normally hidden.
- Added speech bubbles with white, black and frosted glass appearances, Markdown, reply controls and pet-following placement.
- Added streaming local replies when tool processing is disabled, with a stop button.
- Expanded Windows app discovery and added quick actions for exact app names and focus timers.
- Added a J.A.R.V.I.S. / Verity selector to Pet Studio.

## 1.3.3 — 2026-11-02

- Added Pet Studio sizing presets, activity animation controls, reduced motion and animation previews.
- Added speech bubble dimensions, text size, duration, preview and copy controls.
- Added compact quick bar, optional edge snapping, monitor recovery and pet always-on-top control.

## 1.3.2 — 2026-11-02

- Connected Alt+Space quick chat and app search to the desktop pet, with Markdown reply bubbles.
- Added pet controls for chat, model settings, memory, cancellation and positioning, plus clearer model status.
- Added release notes, cancellable downloads and an explicit install step with installer verification.
- Added an option to edit earlier messages in the composer as a new turn.

## 1.3.1 — 2026-11-02

- Added an optional draggable desktop pet with quick-chat click, context menu, ready/working status and saved position.
- Added pet size, opacity, animation, personality and custom PNG/JPEG image controls in Settings → Style.

## 1.3.0 — 2026-11-01

- Replaced the separate welcome window with an embedded five-step guide, Back/Next/Skip/Finish, GGUF downloads and a Home continuation banner.
- Added dashboard widgets for core status, CPU/RAM, GPU identification, storage, network link status, time/uptime and starter suggestions.
- Added saved widget visibility, order, drag-and-drop and two width choices.
- Added optional, manually refreshed Open-Meteo weather and BBC World headlines. Web widgets are off by default.
- Added an original animated core mark and logo-to-search-pill opening animation.
- Added hidden `/verity` mode with an original yellow smiley mark, gold appearance and playful persona; use the command again to restore J.A.R.V.I.S.


## 1.2.1 — 2026-11-01

- Added first-run welcome, optional name, model download, starter actions, privacy choice, skip, and replay from About.
- Added per-chat Instant, Quick, and Think more modes and a 1024-token default reply limit.
- Added generated chat titles with preference for installed local GGUFs and a first-message fallback; titles remain editable.
- Bounded compact summaries, reserved more context space for local replies, and added summary inspection/edit/reset.
- Fixed Enter sending before multiline text processing; Shift+Enter inserts a newline.
- Full deck now behaves as a normal window; compact mode stays above other windows.
- Added solid appearance, Minimalist, Terminal, ChatGPT Style, GitHub Style, and Neon profiles with shape/density/font differences.
- Added editable global shortcuts, About credits/tutorial access, and a published-update notice.


## 1.2.0 — 2026-11-01

- Added an in-app updater under Settings → About. It checks published GitHub releases, downloads the Windows installer on request, verifies its release SHA-256, and opens setup.
- Added curated local GGUF downloads and a custom direct GGUF URL field under Settings → Engine, with progress, cancellation, header validation, and cleanup of partial downloads.
- Kept local file import and manual models-folder copying for custom GGUF files.

## 1.1.1 — 2026-11-01

- Added an original set of 46 icons with SVG sources and 276 transparent PNG exports for light/dark backgrounds.
- Added a custom J app mark in nine PNG sizes, editable SVG, and seven-resolution Windows ICO.
- Replaced text-symbol icons across navigation, settings, chat actions, home shortcuts, and the pet pill with native theme-aware vectors.
- Added accessible names to icon buttons, an icon contact sheet, and a repeatable asset export script.

## 1.1.0 — 2026-11-01

- Added system tray controls and single-instance activation; repeat launches bring the existing deck forward.
- Added first-run engine setup, home-page prompt starters, a searchable model catalog, and a wider settings panel with vertical navigation.
- Added encrypted per-chat draft saving, conversation deletion with confirmation, an edit-last-message shortcut, and a file-picker attachment action alongside drag and drop.
- Added keyboard shortcuts for new chat, chat search, composer focus, settings, and export; Tab completes slash commands and Escape dismisses panes or stops a request.
- Added local temperature and reply-length controls plus a dedicated image endpoint/key/model configuration that also works with local chat.
- Added atomic settings/chat writes, rotating backups, recovery from damaged files, and backup purging when clearing private data.
- Fixed normal Markdown links being treated as images; added native tables, quote formatting, aligned lists, and copyable code blocks.
- Fixed search failures aborting otherwise usable replies, oversized MCP results overflowing local context, attachment excerpts exceeding small-model context, and a hotkey conflict disabling both shortcuts.
- Fixed composer actions changing configuration during generation, commands erasing newly typed text after a fetch, and quick-bar requests erasing full-deck drafts.
- Added response-size limits, isolated regression checks, a GitHub Actions build workflow, support diagnostics, app icons, and versioned installer metadata/checksums.

## 1.0.0 — Initial baseline

- Established Git version control for the existing J.A.R.V.I.S. application.
- Captured the current local/cloud engine, model fetcher, custom model IDs, slash commands, GGUF import, quick launcher, full chat UI, themes, memory, skills, MCP, search, image generation, and text attachments.
- Captured the Windows EXE installer and optional GGUF import/bundling scripts.
