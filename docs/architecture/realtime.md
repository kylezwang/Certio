# Realtime

Four hubs live in `Certio.Web/Hubs`:

| Hub | Used for |
|-----|----------|
| `ChatHub` | Channel and AI conversation messages |
| `DirectHub` | Direct messages and typing |
| `NotificationHub` | In-app notifications |
| `UpdatesHub` | Registered and authorized, but the class is empty |

All four require an authenticated user.

`Program.cs` adds a Redis backplane only when Redis itself is enabled. With one app instance that does not matter. With two instances and no backplane, a message handled on instance A never reaches a browser connected to instance B. The startup log says which mode you are in.

The JavaScript clients live in `wwwroot/js` (`chat.js`, `communications.js`, `direct-messages.js`, and others). They are large. A change to a hub method name has to be matched by hand in those files.
