# People approve agent actions

The model is not allowed to insert or update product rows on its own. It proposes an `AgentAction`. A user approves or rejects it. Execution is a separate step, and a completed action can be rolled back when the handler supports it.

That keeps a bad or prompt-injected reply from creating events or sending mail by itself. It also means the interesting code is the approve and execute path in `AgentActionService`, not the prompt text.
