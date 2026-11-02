# Models and commands

In Settings → Engine, select a provider, enter its API key and base URL, then click **Fetch provider models**. Pick a model from the returned list and save engine settings. The **Custom model ID…** option exposes a text field for any model ID or slug. Listing failures do not prevent using a custom ID. Availability and permission to run a listed model depend on the provider and account.

NVIDIA NIM, Groq, OpenRouter, and Custom OpenAI use the configured base URL's `/models` endpoint. Gemini uses its models API, follows pagination, and lists models that support `generateContent`. Listing uses the key currently entered in settings, including before saving it.

For local models, use **Settings → Engine → Download a local model** to choose a curated GGUF or paste a direct HTTPS link ending in `.gguf`. The app shows progress and can cancel a download. It checks the GGUF header and removes partial files after failure or cancellation. You can still select **Import GGUF** to copy a file from disk, or **Open models folder** to copy one yourself. Select Local, refresh, and choose the file to load it. Debug and published builds each look beside their own executable; use the path shown in settings. Archives, safetensors, and split downloads are not supported. The current local engine uses CPU inference, and RAM must cover weights plus context. Check the model publisher's license and suitability before downloading.

**Settings → About → Check for updates** checks the latest published GitHub release. You can review its notes, download the installer, and choose when to open the verified setup. Draft releases are not offered.

Type `/` in the composer for a clickable command menu:

- `/model`: open model settings and fetch the selected provider's model list.
- `/model slug`: immediately select a cloud model ID, or an existing local GGUF filename.
- `/status`: show the active provider/model, memory/search state, and MCP/skill counts.
- `/reasoning [on|off]`: control deliberate mode. `/reasonnig` is accepted too.
- `/search [on|off]`: control web search.
- `/memory [on|off]`: pause or resume memory.
- `/new`: start a conversation.
- `/settings`, `/mcp`, `/skills`: open the corresponding settings page.
- `/image`: toggle image mode in the composer; `/image a red fox` generates directly in the current chat.
- `/help`: show the full command reference.

Commands are handled by the app before any model inference or web search. A custom cloud slug is saved immediately by `/model slug`; selecting a model in settings applies when **Save engine settings** is pressed.

Provider references: [Gemini model listing](https://ai.google.dev/api/models), [Groq API reference](https://console.groq.com/docs/api-reference), [NVIDIA API reference](https://docs.api.nvidia.com/nim/reference/llm-apis).

The fetched model list and local GGUF list can be filtered by name. The cloud type filter uses model names as a hint; it does not guarantee a model's capabilities. Engine settings also offer local temperature and maximum reply tokens.

Choose **Image** in the composer, enter a description, and press Enter. Images appear in the chat and are saved in the workspace `images` folder. **Image model** opens the image settings directly. The **Chat provider** backend reuses the selected provider's endpoint and key, with an optional image model ID; the selected model must support image generation. OpenRouter and Gemini use their respective image APIs. You can search provider image models or enter a custom ID. A separate OpenAI-compatible image service is also supported.

On Windows x64, **Install local image model** downloads a pinned CPU build of [stable-diffusion.cpp](https://github.com/leejet/stable-diffusion.cpp) and the 4.27 GB [Stable Diffusion 1.5 checkpoint](https://huggingface.co/sd-research/stable-diffusion-v1-5), verifies their SHA-256 checksums, and selects **Local CPU**. Review the model's CreativeML Open RAIL-M license before downloading. Local generation requires substantial RAM and may take several minutes per image. The installer can be cancelled; partial files are removed. On other platforms, connect a local OpenAI-compatible image server instead.

Keyboard shortcuts: Ctrl+N new chat, Ctrl+K search chats, Ctrl+L focus the composer, Ctrl+, settings, Ctrl+Shift+E export, Tab complete a slash command, Escape close settings/command menu or cancel the active request. The system tray provides access when the deck is hidden. Clicking **Edit last message** copies it into the composer as a new draft; **Delete conversation** asks before deleting and updates recovery backups too.
