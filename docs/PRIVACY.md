# Privacy and data handling

This describes the current application source. The public release may be older; consult its version and release notes. This application does not create a Yui cloud account or send analytics to a Yui-operated collection service.

## What stays on your device

Conversation text, saved answers, imported avatars, thumbnails and character settings stay in the app's local data directory until you delete them. Text history is separate from the limited context sent to the AI. Generated audio is temporary and is not a permanent audio archive. Secret Mode skips conversation-history saving; manually saving an answer is still an explicit user action.

On-device AI processes messages locally. Model downloads contact their hosting service, which can see ordinary download metadata such as your IP address. Platform speech services follow the OS permission and processing rules; not every OS recognizer guarantees offline processing.

On iOS/macOS, the app saves your direct OpenAI key in the system Keychain. Existing app-settings keys are migrated after successful secure storage. The current Windows/Android implementation still uses application settings; secure storage on those platforms remains a release acceptance item. Clear the API key in Settings and save to remove it. Keychain items can survive uninstalling the app.

## What is sent when using connected services

Before the first content request to a destination, the app asks for permission. A different Backend URL requires new permission. Help → Quick guide → Privacy allows you to withdraw permission for future requests. Declining leaves the content unsent; local-only AI does not need this permission.

| Route | Recipient and data |
| --- | --- |
| Direct OpenAI | OpenAI receives your API key, messages, recent context, character instructions and any audio/images sent for transcription or analysis. Web search may use external search providers. See [OpenAI's privacy policy](https://openai.com/policies/privacy-policy/). |
| Backend | Your selected server receives messages, context, character instructions, audio, images, voice requests and app-generated user/character identifiers as needed. Its configuration determines onward AI/TTS providers, database storage, logging and retention. The app's direct OpenAI key is not forwarded to this Backend. Use a server whose operator and policies you trust. |
| Native/local TTS | Speech synthesis processes the text on your device. A separate TTS server follows that server's own settings and retention. |

Secret Mode does **not** disable network requests or override the receiving provider's retention. Withdrawing permission does not erase already transmitted data. For server-side retention or deletion, contact the server administrator or service provider. The self-hosted Backend offers character-memory/history management; it is not a centrally operated Yui account service.

## Permissions and control

Microphone and camera access are used when you request voice input or camera input. Selected images and avatar documents are accessed through the platform picker; importing an avatar copies it into app-owned storage. Removing that imported copy does not remove the original file. Local history and saved answers can be deleted from History. Character and appearance data can be managed from Settings → Character.

For a public App Store listing, the distributor must supply a working support/contact address, this policy's hosted URL and accurate App Privacy answers for the actual release. The source document alone does not establish store acceptance or replace the individual providers' policies.

On macOS, a locked Keychain or a changed development signature can prevent automatic key access. The app remains usable; Settings offers an explicit unlock action. An unread key is not erased by saving an empty field. OS authentication must be completed by the device owner.
