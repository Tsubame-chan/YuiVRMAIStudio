# Privacy and data handling

This application does not create a Yui cloud account or send analytics to a Yui-operated collection service.

## What stays on your device

Conversation text, saved answers, imported avatars, thumbnails and character settings stay in the app's local data directory until you delete them. Text history is separate from the limited context sent to the AI. Generated audio is temporary and is not a permanent audio archive. Secret Mode skips conversation-history saving; manually saving an answer is still an explicit user action.

Character memories are stored on your device separately for each character. The app can save selected user statements about preferences or information to remember; you can also add, edit, pin and delete memories. Characters do not share these records with one another. Relevant memories may be included in a request to your selected AI, including when you change between on-device AI and an online provider. Secret Mode can read existing ordinary memories but does not write new conversation history or character memories. Deleting character memories does not delete separate conversation-history archives, personality settings or records on a Backend server. These stores have their own management controls. Memory retrieval and AI interpretation are not guaranteed to be complete or accurate.

On-device AI processes messages locally. Model downloads contact their hosting service, which can see ordinary download metadata such as your IP address. Platform speech services follow the OS permission and processing rules; not every OS recognizer guarantees offline processing.

On iOS/macOS, the app saves your direct OpenAI key in the system Keychain. Existing app-settings keys are migrated after successful secure storage. Windows/Android save the key in application settings rather than the Apple Keychain. Clear the API key in Settings and save to remove it. Keychain items can survive uninstalling the app.

## What is sent when using connected services

Before the first content request to a destination, the app asks for permission. A different Backend URL requires new permission. Help → Quick guide → Privacy allows you to withdraw permission for future requests. Declining leaves the content unsent; local-only AI does not need this permission.

| Route | Recipient and data |
| --- | --- |
| Direct OpenAI | OpenAI receives your API key, messages, recent context, relevant character memories, character and response instructions and any audio/images sent for transcription or analysis. Web search may use external search providers. See [OpenAI's privacy policy](https://openai.com/policies/privacy-policy/). |
| Backend | Your selected server receives messages, context, relevant character memories, character and response instructions, audio, images, voice requests and app-generated user/character identifiers as needed. Its configuration determines onward AI/TTS providers, database storage, logging and retention. The app's direct OpenAI key is not forwarded to this Backend. Use a server whose operator and policies you trust. |
| Native/local TTS | Speech synthesis processes the text on your device. A separate TTS server follows that server's own settings and retention. |

Secret Mode does **not** disable network requests or override the receiving provider's retention. Withdrawing permission does not erase already transmitted data. For server-side retention or deletion, contact the server administrator or service provider. The self-hosted Backend offers character-memory/history management; it is not a centrally operated Yui account service.

## Permissions and control

Microphone and camera access are used when you request voice input or camera input. Selected images and avatar documents are accessed through the platform picker; importing an avatar copies it into app-owned storage. Removing that imported copy does not remove the original file. Local history and saved answers can be deleted from History. Character and appearance data can be managed from Settings → Character.

For a public App Store listing, the distributor must supply a working support/contact address, this policy's hosted URL and accurate App Privacy answers for the actual release. The source document alone does not establish store acceptance or replace the individual providers' policies.

On macOS, a locked Keychain or a changed development signature can prevent automatic key access. The app remains usable; Settings offers an explicit unlock action. An unread key is not erased by saving an empty field. OS authentication must be completed by the device owner.
