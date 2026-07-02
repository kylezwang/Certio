# Matter Details Page - Quick Reference

## How to Access
1. Navigate to Matter Index: `/Client/{orgId}/Matter`
2. Click on any matter card
3. You'll be redirected to: `/Client/{orgId}/Matter/Details/{matterId}`

## Tab Structure

### Implemented Tabs
| Tab | Icon | Hash | Status |
|-----|------|------|--------|
| Summary | fa-chart-pie | #summary | ✅ Fully Implemented |
| Tasks | fa-tasks | #tasks | 🔲 Placeholder |
| Timeline | fa-stream | #timeline | 🔲 Placeholder |
| Calendar | fa-calendar | #calendar | 🔲 Placeholder |
| Communications | fa-comments | #communications | 🔲 Placeholder |
| Documents | fa-folder | #documents | 🔲 Placeholder |
| Billing | fa-credit-card | #billing | 🔲 Placeholder |
| History | fa-history | #history | 🔲 Placeholder |

## Summary Tab Components

### 1. Four Stat Cards (Top Row)
```
┌──────────────┬──────────────┬──────────────┬──────────────┐
│   Completed  │    Updated   │    Created   │   Due Soon   │
│      [#]     │      [#]     │      [#]     │      [#]     │
│ last 7 days  │ last 7 days  │ last 7 days  │ next 7 days  │
└──────────────┴──────────────┴──────────────┴──────────────┘
```

### 2. Four Detail Cards (2x2 Grid)
```
┌─────────────────────┬─────────────────────┐
│  Status Overview    │  Recent Activity    │
│  (Donut Chart)      │  (Activity List)    │
├─────────────────────┼─────────────────────┤
│  Priority Breakdown │  Types of Work      │
│  (Progress Bars)    │  (Status Table)     │
└─────────────────────┴─────────────────────┘
```

## Key Files

### Backend
- **ViewModel**: `Certio.Web/ViewModels/MatterDetailsViewModel.cs`
- **Controller**: `Certio.Web/Controllers/MatterController.cs` (Details action)

### Frontend
- **View**: `Certio.Web/Views/Matter/Details.cshtml`
- **JavaScript**: `Certio.Web/wwwroot/js/matter-details.js`
- **CSS**: `Certio.Web/wwwroot/css/site.css` (lines 5484-5673)

### Modified
- **Matter Index**: `Certio.Web/Views/Matter/Index.cshtml` (cards now clickable)

## Statistics Explained

### Time-Based Metrics
- **Completed**: Tasks with Status="Completed" AND CompletedAt in last 7 days
- **Updated**: Tasks with ModifiedAt in last 7 days
- **Created**: Tasks with CreatedAt in last 7 days
- **Due Soon**: Tasks with DueDate in next 7 days AND Status != "Completed"

### Priority Breakdown
- **High**: Priority = "High"
- **Medium**: Priority = "Medium"
- **Low**: Priority = "Low"
- **Critical**: Priority = "Critical"

### Status Distribution
- **Pending**: Status = "Pending"
- **In Progress**: Status = "InProgress" OR "In Progress"
- **In Review**: Status = "Review"
- **Completed**: Status = "Completed"

## Customization Guide

### Adding a New Tab
1. **Add tab link** in `Details.cshtml`:
   ```html
   <a href="#newtab" class="matter-tab-link" data-tab="newtab">
       <i class="fas fa-icon"></i>
       <span>New Tab</span>
   </a>
   ```

2. **Add tab content** in `Details.cshtml`:
   ```html
   <div id="tab-newtab" class="tab-content-section" style="display: none;">
       <!-- Your content here -->
   </div>
   ```

3. **Update JavaScript** in `matter-details.js`:
   ```javascript
   const validTabs = [..., 'newtab'];
   ```

### Styling Tips
- Use existing `.card` class for consistency
- Use `.stats-card` for stat cards with hover effects
- Use Bootstrap utilities: `mb-3`, `p-4`, `text-muted`, etc.
- Icons use Font Awesome 5: `<i class="fas fa-icon"></i>`

## Color Palette
```css
Primary:   #3d1019  (Brand burgundy)
Success:   #10b981  (Green)
Warning:   #f59e0b  (Orange/Yellow)
Danger:    #dc2626  (Red)
Secondary: #6b7280  (Gray)
Info:      #3b82f6  (Blue)
```

## Common CSS Classes
- `.card` - Base card styling (12px radius, shadow)
- `.stats-card` - Stat card with hover effect
- `.matter-details-header` - Page header
- `.matter-tab-link` - Tab navigation link
- `.matter-icon-badge` - Circular badge with initials
- `.icon-bg-primary` - Icon background (10% opacity)
- `.bg-success.bg-opacity-10` - Success color background (10%)

## Responsive Breakpoints
- **Mobile** (< 768px): Tab labels hidden, icons only
- **Tablet** (768px - 1024px): 2-column card grid
- **Desktop** (> 1024px): Full layout

## Permission Requirements
- Route protected by `[RequireMatterAccess("id")]`
- User must have access to the matter's organization
- User must be assigned to matter OR have appropriate permissions

## Testing Checklist
- [ ] Navigate from Matter Index to Details
- [ ] Click each tab, verify it switches correctly
- [ ] Verify URL hash updates when switching tabs
- [ ] Test browser back/forward buttons
- [ ] Test direct URL with hash (e.g., `#tasks`)
- [ ] Verify stats display correctly
- [ ] Test with matter that has no tasks (empty state)
- [ ] Test responsive layout on mobile
- [ ] Verify hover effects on cards
- [ ] Test share and maximize buttons (functionality TBD)

## Known Limitations / Future Work
1. **Share button**: Currently placeholder, needs share functionality
2. **Maximize button**: Currently placeholder, needs fullscreen logic
3. **Filter button**: In Summary tab, needs implementation
4. **All placeholder tabs**: Need full implementations
5. **Recent Activity**: Currently shows task activity only, could expand to include all matter events
6. **Donut Chart**: Shows only in-progress percentage, could show multiple status percentages

## Performance Notes
- Statistics are calculated in-memory on page load
- For matters with 1000+ tasks, consider implementing caching
- Recent activity is limited to last 10 items for performance
- Task queries use `.Include()` to avoid N+1 queries

## Accessibility Notes
- Semantic HTML with proper heading hierarchy
- Tab navigation keyboard accessible
- Icons have text labels for screen readers
- Color contrast meets WCAG AA standards
- Focus states visible on all interactive elements

