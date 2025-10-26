# AJAX Navigation - Current Status

## Date: October 26, 2025

---

## ⚠️ **CURRENTLY DISABLED**

AJAX navigation is **disabled by default** (line 7 in `page-navigation.js`):

```javascript
enabled: false, // DISABLED UNTIL PAGES ARE AJAX-READY
```

---

## Why Disabled?

AJAX navigation was breaking existing page functionality:

### ❌ Broken Functionalities When Enabled
1. **Task Details** - Modals not opening properly
2. **Matter/Index Filtering** - Card filtering broken
3. **Communications Page** - Page functionality lost
4. **Other Interactive Pages** - Event handlers not re-initializing

### Root Cause
When loading pages via AJAX:
- Only HTML content is replaced
- Page-specific JavaScript event handlers don't re-attach
- Inline scripts may not execute in correct context
- Libraries expecting full page load don't re-initialize

---

## What Works Now (AJAX Disabled)

✅ **Full Page Navigation** - All pages work normally
✅ **Task Details** - Modals open correctly
✅ **Matter Filtering** - Card filtering works
✅ **Communications** - Full functionality
✅ **All Interactive Elements** - Work as expected

### Trade-offs
- ❌ Sidebars don't persist across pages (full reload)
- ❌ Slower navigation (full page reload)
- ❌ White flash between pages
- ✅ But everything **works correctly**

---

## How to Re-Enable (Not Recommended Yet)

If you want to test AJAX navigation:

### Option 1: Edit Config
```javascript
// In page-navigation.js, line 7
enabled: true,
```

### Option 2: Runtime (Browser Console)
```javascript
window.enableAjaxNavigation();
```

### Option 3: Disable Again
```javascript
window.disableAjaxNavigation();
```

---

## What Was Fixed

✅ **Loading Spinner** - Now burgundy (#3d1019) on white background
✅ **Security** - XSS and CSRF protection implemented
✅ **Error Handling** - Graceful fallback to full page load
✅ **Robustness** - Multiple content selectors

---

## To Make AJAX Work (Future)

Each page needs to be made "AJAX-ready":

### Requirements Per Page

1. **Externalize JavaScript**
   - Move inline scripts to separate functions
   - Create page initialization functions
   - Example: `window.initializeTasksPage()`

2. **Re-initialization Support**
   - Event handlers must be reattachable
   - Check if already initialized
   - Clean up old handlers before adding new ones

3. **Example Pattern**
```javascript
// In tasks.js
window.initializeTasksPage = function() {
    // Remove old handlers
    $('.task-card').off('click');
    
    // Attach new handlers
    $('.task-card').on('click', function() {
        openTaskModal($(this).data('task-id'));
    });
};

// In page-navigation.js initializeTasksScripts()
if (typeof window.initializeTasksPage === 'function') {
    window.initializeTasksPage();
}
```

4. **Test Each Page**
   - Enable AJAX for that specific page
   - Test all interactive elements
   - Ensure modals/dropdowns/filters work
   - Check event handlers re-attach

---

## Pages That Need AJAX-Ready Conversion

| Page | Status | Priority | Notes |
|------|--------|----------|-------|
| Dashboard | ❌ Not Ready | Low | Static content, works fine |
| Matter/Index | ❌ Not Ready | High | Filtering broken |
| Matter/Details | ✅ Works | - | Already uses AJAX tabs |
| Tasks/Index | ❌ Not Ready | High | Modals broken |
| Communications | ❌ Not Ready | Medium | Complex page state |
| Calendar | ❌ Not Ready | Medium | Event handlers issue |
| Documents | ❌ Not Ready | Low | Simple page |
| Teams | ❌ Not Ready | Low | Simple page |
| Settings | ❌ Not Ready | Low | Forms need special handling |

---

## Recommendation

**Keep AJAX navigation disabled** until:

1. You're ready to refactor page-specific JavaScript
2. You have time to test each page thoroughly
3. The benefits outweigh the development effort

**Current setup is stable and works correctly.**

---

## Quick Reference

### Check If Enabled
```javascript
// Browser console
console.log('AJAX nav enabled:', window.enableAjaxNavigation !== undefined);
```

### Force Full Page Load
If AJAX is enabled and causing issues:
```javascript
window.disableAjaxNavigation();
location.reload();
```

### Test Single Page
```javascript
// Enable AJAX
window.enableAjaxNavigation();

// Navigate to page
// Test all functionality

// If broken, disable and reload
window.disableAjaxNavigation();
location.reload();
```

---

## Files

- **Script**: `Certio.Web/wwwroot/js/page-navigation.js`
- **Config**: Line 7 - `enabled: false`
- **Layout**: `Certio.Web/Views/Shared/_ClientLayout.cshtml` - Line 1319 (has `sidebar-navigation` class)

---

## Notes

- AJAX navigation script is loaded but inactive
- No impact on performance when disabled
- Can be enabled anytime without code changes
- Sidebar persistence requires AJAX to be enabled
- Matter Details tabs work independently (different system)

---

## Support

If you see issues after disabling:
1. Hard refresh: Ctrl+F5
2. Clear browser cache
3. Check browser console for errors
4. Verify `page-navigation.js` has `enabled: false`

