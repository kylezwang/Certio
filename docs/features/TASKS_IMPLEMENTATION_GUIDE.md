# Tasks Page Implementation Guide

This guide explains the newly implemented Tasks page that provides Trello-like functionality for task management.

## Overview

The Tasks page is a comprehensive task management system that mimics Trello's layout and core functionalities. It includes:

- **Kanban Board**: Visual task management with drag-and-drop functionality
- **Task Inbox**: Quick task list with checkboxes in the left sidebar
- **CRUD Operations**: Create, read, update, and delete tasks
- **Task Details Modal**: Detailed view for each task
- **Search & Filter**: Real-time task filtering
- **Calendar Integration**: Placeholder for future calendar view

## Files Created

### Backend

1. **Entity Schema**: `Certio.Domain/Matters/TaskItem.cs`
   - Core entities: `TaskItem`, `TaskAssignment`, `TaskItemComment`, `TaskItemDependency`
   - Replaces the old `StatusItem.cs` structure

2. **Controller**: `Certio.Web/Controllers/TasksController.cs`
   - CRUD operations for tasks
   - Status updates and drag-and-drop handling
   - Comment and assignment management

3. **ViewModels**: `Certio.Web/ViewModels/TasksViewModel.cs`
   - Data transfer objects for the Tasks page
   - Includes: `TaskItemViewModel`, `TaskAssignmentViewModel`, `TaskCommentViewModel`

4. **Database Context**: Updated `Certio.Web/Data/ApplicationDbContext.cs`
   - Added TaskItem DbSets
   - Configured entity relationships
   - Added indexes for performance

### Frontend

1. **Main View**: `Certio.Web/Views/Tasks/Index.cshtml`
   - Kanban board layout with 4 columns (Planned, In Progress, Review, Completed)
   - Left sidebar task inbox
   - Modals for task creation and details

2. **Partial View**: `Certio.Web/Views/Tasks/_TaskCard.cshtml`
   - Reusable task card component
   - Shows priority, progress, due dates, and assignees

3. **JavaScript**: `Certio.Web/wwwroot/js/tasks.js`
   - Drag-and-drop functionality using Sortable.js
   - Search and filter logic
   - AJAX calls for CRUD operations
   - Modal interactions

4. **Styles**: `Certio.Web/wwwroot/css/tasks.css`
   - Custom styling for Kanban board
   - Responsive design
   - Task card animations
   - Scrollbar customizations

## Database Migration

To use the Tasks page, you need to create and apply a database migration:

```bash
# Navigate to the Web project directory
cd Certio.Web

# Create a new migration
dotnet ef migrations add AddTaskItemEntities

# Apply the migration to the database
dotnet ef database update
```

## Features Implemented

### 1. Kanban Board
- **4 Columns**: Planned, In Progress, Review, Completed
- **Drag-and-Drop**: Move tasks between columns or reorder within a column
- **Card Design**: Displays title, description, priority, due date, progress, and assignees
- **Badge Counts**: Shows number of tasks in each column

### 2. Task Inbox (Left Sidebar)
- **Quick View**: Simple vertical list of all tasks
- **Checkboxes**: Mark tasks as complete/incomplete
- **Filtering**: Tasks filter based on search input
- **Visual States**: Completed tasks show as strikethrough

### 3. Task Operations

#### Create Task
- Modal form with fields: Matter, Title, Description, Priority, Due Date, Status
- Validation and error handling
- Auto-refresh after creation

#### Update Task
- Edit task details via modal
- Change status by dragging between columns
- Toggle completion via checkbox

#### Delete Task
- Confirmation dialog
- Removes task from all views

### 4. Search & Filter
- Real-time search across task titles and descriptions
- Filters both Kanban cards and inbox items

### 5. Task Detail Modal
- Shows complete task information
- Displays description, subtasks, comments, and assignees
- Edit and delete buttons

### 6. Calendar View (Placeholder)
- Toggle button to switch between Board and Calendar views
- Ready for future implementation

## Usage

### Accessing the Tasks Page

Navigate to: `/Tasks/Index`

Or add a navigation link:
```html
<a class="nav-link" href="/Tasks/Index">
    <i class="bi bi-check2-square"></i> Tasks
</a>
```

### Creating a Task

1. Click "+ Add Task" button in the header
2. Select a Matter (required)
3. Fill in task details
4. Choose priority and status
5. Click "Save Task"

### Managing Tasks

**Drag & Drop:**
- Click and hold a task card
- Drag to a different column or position
- Release to drop

**Quick Complete:**
- Click checkbox on any task card or inbox item
- Task automatically moves to appropriate status

**View Details:**
- Click anywhere on a task card (except checkbox)
- Modal opens with full details

**Edit:**
- Open task detail modal
- Click "Edit" button
- Update fields and save

**Delete:**
- Open task detail modal
- Click "Delete" button
- Confirm deletion

### Searching Tasks

Type in the search box at the top of the page. Tasks filter in real-time based on title and description matches.

## Data Model

### TaskItem
- **Core Fields**: Id, OrgId, MatterId, Title, Description, Status, Priority, Location, Order
- **Dates**: CreatedAt, StartedAt, LastModifiedAt, DueDate, CompletedAt
- **Relationships**: TaskAssignments, Comments, SubTaskItems, RelatedDocuments, Dependencies

### TaskAssignment
- Links users to tasks
- Assignment types: Assignee, Reviewer, Observer, Contributor
- Includes role description and notification preferences

### TaskItemComment
- Threaded comments on tasks
- Supports replies (parent-child relationship)
- Tracks creation and modification dates

### TaskItemDependency
- Defines dependencies between tasks
- Dependency types: FinishToStart, StartToStart, FinishToFinish, StartToFinish

## Customization

### Adding New Columns

Edit `Views/Tasks/Index.cshtml`:

```html
<div class="kanban-column" data-status="YourStatus">
    <div class="column-header">
        <div class="d-flex align-items-center gap-2">
            <h6 class="mb-0 fw-bold">Your Status</h6>
            <span class="badge bg-info">0</span>
        </div>
    </div>
    <div class="column-cards" id="column-yourstatus">
        <!-- Cards will appear here -->
    </div>
</div>
```

### Styling Customization

Modify `wwwroot/css/tasks.css` to adjust:
- Column widths
- Card colors
- Priority badges
- Animations

### Adding Task Filters

Extend the search functionality in `wwwroot/js/tasks.js`:

```javascript
function filterTasks(searchTerm, filters) {
    // Add filtering by labels, assignees, etc.
}
```

## Future Enhancements

### Phase 1 (Ready to Implement)
- [ ] Full comment system with add/edit/delete
- [ ] Assignment management UI
- [ ] Subtask creation and management
- [ ] Document attachment to tasks

### Phase 2 (Requires Additional Work)
- [ ] Calendar view integration
- [ ] Task labels/tags
- [ ] Task templates
- [ ] Bulk operations
- [ ] Advanced filtering (by label, member, date range)
- [ ] Task time tracking
- [ ] Activity history

### Phase 3 (Advanced Features)
- [ ] Task dependencies visualization
- [ ] Gantt chart view
- [ ] Task automation/workflows
- [ ] Email notifications
- [ ] Mobile app support

## API Endpoints

All endpoints require authentication with the `OrgMember` policy:

- `GET /Tasks/Index` - Main tasks page
- `POST /Tasks/Create` - Create new task
- `POST /Tasks/Update` - Update task details
- `POST /Tasks/UpdateStatus` - Update task status (drag-and-drop)
- `POST /Tasks/Delete/{id}` - Delete task
- `POST /Tasks/AddComment` - Add comment to task
- `POST /Tasks/AddAssignment` - Assign user to task
- `POST /Tasks/RemoveAssignment/{id}` - Remove assignment

## Troubleshooting

### Tasks not appearing
- Ensure you've run database migrations
- Check that user has access to organization
- Verify Matter exists for the organization

### Drag-and-drop not working
- Check console for JavaScript errors
- Ensure Sortable.js library is loaded
- Verify CSRF token is present

### Permission errors
- User must be authenticated
- User must be an OrgMember
- Check organization membership

## Dependencies

- **Sortable.js v1.15.0**: Drag-and-drop functionality
- **Bootstrap 5**: UI components and modals
- **Bootstrap Icons**: Icon library
- **.NET 9.0**: Backend framework
- **Entity Framework Core**: Database ORM

## Security Considerations

1. **Authorization**: All endpoints check OrgMember policy
2. **CSRF Protection**: Anti-forgery tokens on all POST requests
3. **Input Validation**: Server-side validation on all inputs
4. **XSS Prevention**: HTML encoding in views

## Performance Optimizations

1. **Database Indexes**: Added indexes on MatterId and OrgId
2. **Eager Loading**: Includes related entities in queries
3. **Frontend**: Debounced search input
4. **Pagination**: Ready for implementation when task count grows

## Testing

### Manual Testing Checklist
- [ ] Create task
- [ ] Edit task
- [ ] Delete task
- [ ] Drag task between columns
- [ ] Reorder tasks within column
- [ ] Search/filter tasks
- [ ] Toggle task completion via checkbox
- [ ] View task details in modal
- [ ] Responsive design on mobile/tablet

## Support

For issues or questions:
1. Check this guide first
2. Review console logs for errors
3. Verify database migrations are applied
4. Check user permissions and organization membership

