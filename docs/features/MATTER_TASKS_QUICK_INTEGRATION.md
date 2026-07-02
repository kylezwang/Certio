# Matter Tasks - Quick Integration Guide

## 3-Step Integration

### Step 1: Add Controller Action
In `MatterController.cs`:

```csharp
[Authorize(Policy = "OrgMember")]
public async Task<IActionResult> GetMatterTasks(int matterId)
{
    // Get matter with tasks
    var matter = await _context.Matters
        .Where(m => m.Id == matterId && m.OrganizationId == GetCurrentOrgId())
        .Include(m => m.Tasks)
            .ThenInclude(t => t.Assignments)
            .ThenInclude(a => a.User)
        .Include(m => m.Tasks)
            .ThenInclude(t => t.SubTasks)
        .FirstOrDefaultAsync();
    
    if (matter == null)
        return NotFound();
    
    // Build ViewModel
    var viewModel = new TasksViewModel
    {
        Matters = new List<Matter> { matter },
        Users = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == GetCurrentOrgId())
            .Select(ou => ou.User)
            .ToListAsync(),
        AllTasks = matter.Tasks.Where(t => !t.IsDeleted).ToList()
    };
    
    // Set ViewBag data
    ViewBag.CurrentUserId = GetCurrentUserId();
    ViewBag.CurrentUserName = User.Identity.Name;
    ViewBag.CurrentUserInitials = GetUserInitials();
    ViewBag.CurrentUserEmail = User.FindFirstValue(ClaimTypes.Email);
    ViewBag.GoogleMapsEnabled = true; // Or from config
    ViewBag.GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"];
    
    return PartialView("_MatterTasks", viewModel);
}
```

### Step 2: Update Matter Details View
In `Views/Matter/Details.cshtml`, add Tasks tab content:

```html
<div class="tab-content mt-3">
    <!-- Summary Tab -->
    <div class="tab-pane fade show active" id="summary" role="tabpanel">
        @* Existing summary content *@
    </div>
    
    <!-- Tasks Tab -->
    <div class="tab-pane fade" id="tasks" role="tabpanel">
        <div id="matter-tasks-container">
            <div class="text-center p-5">
                <i class="fas fa-spinner fa-spin fa-2x text-primary"></i>
                <p class="mt-2 text-muted">Loading tasks...</p>
            </div>
        </div>
    </div>
    
    <!-- Other tabs... -->
</div>
```

### Step 3: Add AJAX Loading JavaScript
In `Views/Matter/Details.cshtml` or `matter-details.js`:

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const matterId = @Model.Id;
    const tasksTab = document.querySelector('[data-bs-target="#tasks"]');
    let tasksLoaded = false;
    
    if (tasksTab) {
        tasksTab.addEventListener('shown.bs.tab', function() {
            if (!tasksLoaded) {
                const container = document.getElementById('matter-tasks-container');
                
                fetch(`/Matter/GetMatterTasks?matterId=${matterId}`)
                    .then(response => {
                        if (!response.ok) throw new Error('Failed to load tasks');
                        return response.text();
                    })
                    .then(html => {
                        container.innerHTML = html;
                        
                        // Initialize the tasks view
                        if (typeof window.initializeMatterTasks === 'function') {
                            window.initializeMatterTasks();
                            tasksLoaded = true;
                        } else {
                            console.error('initializeMatterTasks function not found');
                        }
                    })
                    .catch(error => {
                        console.error('Error loading tasks:', error);
                        container.innerHTML = `
                            <div class="alert alert-danger m-3">
                                <i class="fas fa-exclamation-circle"></i>
                                Failed to load tasks. Please try again.
                                <button class="btn btn-sm btn-outline-danger ms-2" onclick="location.reload()">
                                    Retry
                                </button>
                            </div>
                        `;
                    });
            }
        });
    }
});
```

## That's It! 🎉

After these 3 steps:
1. Tasks tab will load tasks via AJAX when clicked
2. All task functionality works (create, edit, subtasks, etc.)
3. Matter badges and selectors are automatically hidden
4. Tasks are scoped to the current matter

## Troubleshooting

**Tasks not loading?**
- Check browser console for errors
- Verify controller route is correct
- Check ViewBag data is set

**Modal not opening?**
- Ensure task details modal exists in `_ClientLayout.cshtml`
- Check for JavaScript errors

**Selectors not working?**
- Verify `window.initializeMatterTasks()` was called
- Check `.matter-tasks-content` element exists
- Look for console errors

## Optional: Preload Tasks
To load tasks immediately instead of on tab click:

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const matterId = @Model.Id;
    
    // Load immediately
    fetch(`/Matter/GetMatterTasks?matterId=${matterId}`)
        .then(response => response.text())
        .then(html => {
            document.getElementById('matter-tasks-container').innerHTML = html;
            window.initializeMatterTasks();
        });
});
```

