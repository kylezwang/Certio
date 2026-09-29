# Domain model

Almost every row is owned by an organization. Queries that forget `OrganizationId` are the usual way to leak another tenant's data, so list and get paths take an org id and filter on it.

## Organizations

`Organization.Type` is one of `Client`, `LawFirm`, `EventPlanner`, `Government`, or `NonProfit`. A user joins an org through `UserOrganization`, which also carries the role used for permissions.

Notal started as a product for law firms, which is why `LawFirm` is still in that list. The product now is for event teams.

`OrganizationRelationship` links two orgs, such as an event team and a client. Direct messages and some channel access use that link so a person in one org can reach a person in the other without being a member of both.

You will still see `Matter` in the code. In the UI that entity is usually called an event.

## Work

- `Matter` is the event. Assignments and an access level (`Everyone` or specific people) decide who can see it.
- `TaskItem` hangs off a matter. Tasks have assignments, comments, and comment reactions. A reaction is stored as a short key such as `like`, not as an emoji character.
- `CalendarEvent` is the schedule, with optional Google and Outlook sync.
- Billing types (`TimeEntry`, expense, invoice, retainer) live under `Certio.Domain/Billing`.

## Documents and mail

`Document` is a file the org uploaded or pulled from Google Drive or OneDrive. The AI service can index text from those files. Mail connections are per user (Gmail or Outlook), and sync writes messages the unified inbox can show.

## Conversations

`Conversation` is either a channel (team chat, often tied to a matter) or a private AI thread. `DirectThread` is the one-to-one message model and is separate from channels.

## Agent actions

When the model wants to change data, it does not write the row itself. It creates an `AgentAction` in `Pending`. A person approves it, then the app executes it. Status values are `Pending`, `Approved`, `Rejected`, `Running`, `Done`, `Failed`, and `RolledBack`.
