# Tasks Modal Field Mapping

## Overview
The Task Details Modal is now fully integrated with the Tasks schema and can be used to create new tasks. All fields in the modal are properly mapped to the `TaskItem` domain model.

## Field Mappings

### TaskItem Schema → Modal Fields

| Schema Field | Modal Field | Location in Modal | Type |
|--------------|-------------|------------------|------|
| `Id` | Internal | Hidden (stored in data attribute) | int |
| `OrgId` | Automatic | Set from user's organization | int |
| `MatterId` | Automatic | Currently uses first available matter | int |
| `Title` | Task Title | Editable h2 at top of modal | string (200) |
| `Description` | Description | Textarea in Description section | string (2000) |
| `Status` | Status | Header dropdown & Status section | string (20) |
| `Priority` | Priority | Priority section dropdown | string (20) |
| `Location` | Location | Location section with Google Maps | string (200) |
| `Order` | Automatic | Set to 0 for new tasks | int |
| `CreatedAt` | Automatic | Set on creation | DateTime |
| `StartedAt` | Start Date | Dates section - Start Date field | DateTime? |
| `LastModifiedAt` | Automatic | Set on updates | DateTime? |
| `DueDate` | Due Date | Dates section - Due Date field | DateTime? |
| `CompletedAt` | Automatic | Set when status = Completed | DateTime? |
| `ParentTaskItemId` | Future | Not implemented yet | int? |
| `SubTaskItems` | Subtasks | Subtasks section (checklist) | Collection |
| `TaskAssignments` | Assignees | Assignees section at top | Collection |
| `Comments` | Comments | Right panel activity log | Collection |
| `RelatedDocuments` | Attachments | Attachments section | Collection |

## Features Implemented

### 1. Add Task Button Integration
- **Button Location**: "Add a task" button in the inbox view (line 36-42 of Index.cshtml)
- **Action**: Opens the task details modal in "create" mode
- **Functionality**: Clears all fields and sets default values

### 2. Save Functionality
- **Save Button**: Automatically added to modal header (top-right, before close button)
- **Validation**: 
  - Title is required
  - Matter must be available
- **API Endpoint**: `POST /Tasks/Create`
- **Success Action**: Reloads page to show new task

### 3. Field Behaviors

#### Title Field
- Contenteditable h2 element
- Focus is automatically set when modal opens
- Required field with validation

#### Status Field
- Two synced dropdowns:
  - Header dropdown (for quick access)
  - Status section dropdown (in main form)
- Options: Pending, InProgress, Review, Completed
- Default: "Pending"
- Updates task icon color based on status

#### Priority Field
- Dropdown with options: Low, Medium, High, Critical
- Default: "Medium"

#### Date Fields
- **Start Date**: Maps to `StartedAt` in schema
- **Due Date**: Maps to `DueDate` in schema
- Both are optional (DateTime?)
- HTML5 date picker input

#### Description
- Textarea with placeholder
- Supports up to 2000 characters
- Optional field

#### Location
- Google Maps integration with autocomplete
- Search and select location
- Shows embedded map preview
- Stores location name and address

#### Progress Tracking
- Header shows subtask completion percentage
- Synced with subtasks section
- Calculates percentage based on checked items

### 4. Modal Modes

#### Create Mode (Current Implementation)
- Triggered by: "Add a task" button
- Clears all fields
- Sets defaults (Status: Pending, Priority: Medium)
- Saves to: `POST /Tasks/Create`

#### Edit Mode (TODO)
- Load existing task data
- Populate all fields from database
- Save to: `POST /Tasks/Update`

## API Updates

### CreateTaskRequest Model
```csharp
public class CreateTaskRequest
{
    public int MatterId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? StartedAt { get; set; }  // NEW
    public DateTime? DueDate { get; set; }
    public string? Location { get; set; }      // NEW
    public int Order { get; set; }
}
```

### UpdateTaskRequest Model
```csharp
public class UpdateTaskRequest
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? StartedAt { get; set; }  // NEW
    public DateTime? DueDate { get; set; }
    public string? Location { get; set; }      // NEW
    public int? Order { get; set; }
}
```

## JavaScript Functions

### New Functions Added

1. **`openTaskModal(taskId, defaultStatus)`**
   - Opens modal for create or edit
   - Clears fields for new tasks
   - Loads data for existing tasks

2. **`clearTaskModal()`**
   - Resets all form fields
   - Clears location and map
   - Resets subtasks section
   - Sets defaults for status/priority

3. **`resetTaskModal()`**
   - Wrapper for clearTaskModal()
   - Called when modal closes

4. **`saveTask()`**
   - Validates required fields
   - Gathers all field values
   - Sends POST request to API
   - Shows loading state during save
   - Reloads page on success

5. **`initializeSaveTask()`**
   - Adds Save button to modal header
   - Attaches click handler
   - Called on page load

6. **`updateChecklistProgressManual(percentage)`**
   - Updates progress bars in header and subtasks section
   - Accepts percentage value (0-100)

7. **`updateTaskTitleIconFromModal(status)`**
   - Updates the icon next to task title
   - Changes color based on status
   - Icons:
     - Pending: question circle (planned)
     - InProgress: play circle
     - Review: exclamation circle
     - Completed: check circle

## Status Icon Colors

The modal uses visual indicators based on task status:

| Status | Icon | Color Class |
|--------|------|-------------|
| Pending/Planned | `fa-circle-question` | `status-planned` |
| In Progress | `fa-play-circle` | `status-in-progress` |
| Review | `fa-exclamation-circle` | `status-review` |
| Completed | `fa-check-circle` | `status-completed` |

## Future Enhancements

### Not Yet Implemented

1. **Matter Selection**
   - Currently uses first available matter
   - Should add dropdown to select matter

2. **Task Editing**
   - Load task data into modal
   - Populate all fields from existing task
   - Update existing task instead of create

3. **Subtasks Management**
   - Add new subtasks
   - Edit subtask text
   - Delete subtasks
   - Save subtasks to database

4. **Assignees Management**
   - Add/remove assignees
   - Select from organization users
   - Set assignment type and role

5. **Comments**
   - Add new comments
   - Display existing comments
   - Real-time updates

6. **Attachments**
   - File upload functionality
   - Download attachments
   - Remove attachments
   - File preview

7. **Dependencies**
   - Add task dependencies
   - Dependency type selection
   - Visual dependency graph

## Testing the Integration

### To Test Creating a New Task:

1. Navigate to Tasks page (`/Client/{orgId}/Tasks`)
2. Click "Add a task" button in the inbox view
3. Modal opens with blank fields
4. Enter task details:
   - Type a title (required)
   - Add description (optional)
   - Select status (defaults to Pending)
   - Select priority (defaults to Medium)
   - Set start and due dates (optional)
   - Search and select a location (optional)
5. Click "Save" button in top-right
6. Page reloads and shows new task

### Expected Behavior:

✅ Modal opens when "Add a task" is clicked
✅ All fields are empty/default values
✅ Title field is focused automatically
✅ Save button appears in header
✅ Title is required (validation)
✅ Status dropdown syncs with form status
✅ Task icon updates based on status
✅ Progress shows 0% for new task
✅ Save creates task in database
✅ Page reloads to show new task

## Notes

- The modal maintains the exact same beautiful UI you wanted
- All fields are properly mapped to the schema
- The integration is clean and follows the existing patterns
- Google Maps integration is preserved for location features
- Subtasks progress calculation is wired up
- The modal can be extended for editing tasks in the future

