# MCP connections

Open **Settings → MCP** to add a curated server or configure a custom server. MCP is for connected tool servers; local `SKILL.md` guidance lives separately in **Settings → Skills**.

Curated entries include Filesystem, Git, Playwright, Fetch, Memory, GitHub, and Sequential Thinking. Local servers use stdio; remote servers use Streamable HTTP. Local entries require their runtime (Node.js, `uvx`, or Docker) to be installed.

After connecting, enable the tools you want J.A.R.V.I.S. to consider. Each tool call shows its server, tool name, and arguments; **Allow once** approves only that call. The model receives the tool result and can continue, with a limit of three calls per request. Tool descriptions are compacted before they enter the chat context so a large server catalog does not crowd out the conversation. If a connection fails, the MCP page now keeps the actual launch or handshake error visible.

For a custom stdio server, enter the executable and a JSON array of arguments. Environment variables are a JSON object. For a custom remote server, enter an HTTPS endpoint (or localhost HTTP) and a JSON object of HTTP headers. API keys in these fields are stored in the encrypted app configuration. Local server processes inherit a limited standard environment plus the variables you provide.

Connections end when J.A.R.V.I.S. closes. Select **Reconnect automatically when J.A.R.V.I.S. starts** for servers you want launched again on the next run. Local MCP servers run with the same Windows user permissions as J.A.R.V.I.S.; connect only servers you trust.

The chat composer also has a persistent **Search on/off** switch. With it on, each prompt gets up to five web search results as untrusted source material; J.A.R.V.I.S. should cite the source links. **Image** opens a direct image prompt and saves generated assets under the configured workspace's `images` folder; choose a cloud provider with an OpenAI-compatible image endpoint. Drop up to five text or source files into the composer (1 MB each, 4 MB total); they are included as reference text in the next prompt, and can be cleared before sending. Cloud providers receive the text of attached files with that prompt.

Import a trusted file named `SKILL.md` under **Settings → Skills**. Skills are local prompt guidance, not executable MCP tools, and can be enabled or disabled independently.
