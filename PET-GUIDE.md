# Pet quick chat

Press Alt+Space to open the pet and quick chat. If the pet is normally hidden, it appears temporarily. Press Enter to send a question. Use Alt+1 through Alt+5 to launch the numbered app results, or select a result and press Enter.

Try these commands:

- `open Notepad` opens an app with that exact indexed name.
- `start a 25 minute timer` starts a focus timer. Ask `timer status` or say `stop timer` to manage it.
- `make a note: Buy milk` saves a new text file in your workspace's `notes` folder.
- `make a note on my desktop: Buy milk` saves a new text file on your desktop.

Notes use unique file names so existing files are not overwritten. The pet reports where it saved the note. Commands that do not match an available app are sent to the assistant as conversation.

Choose the pet's appearance, bubble background, size, motion, and visibility in **Settings → Style → Pet Studio**. Bubbles support Markdown and follow the pet when it moves. The Reply button continues the current chat; Open conversation opens the same chat in the full deck.

Local responses and supported cloud responses stream into the bubble. Structured tool requests are held back until the app has processed them. Some compatible endpoints do not support streaming; if the endpoint explicitly reports this, the app retries without streaming. The Stop button cancels generation; a partial streamed response remains visible but is not saved as a completed conversation turn.

When memory is paused, messages and drafts remain in the current app session, including when you switch chats. They are not added to the saved chat archive. Closing the app clears those session-only messages.
