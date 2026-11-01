# Changelog

## Unreleased

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
