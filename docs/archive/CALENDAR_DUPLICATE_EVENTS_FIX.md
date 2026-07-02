# Calendar Duplicate Events Fix

## Issue
Duplicate calendar events were being created when using the _MatterCalendar.cshtml partial view in the Matter Details Calendar tab. This happened for both creating new events and updating existing events.

## Root Cause
The Matter Calendar view is loaded as a partial view via AJAX when the Calendar tab is activated. The JavaScript code in the partial view is wrapped in an IIFE (Immediately Invoked Function Expression) that executes when the HTML is injected into the DOM.

The problem occurred because of **SCOPED STATE VARIABLES**:

1. **Critical State Variables Were IIFE-Scoped**: Variables like `currentEventId`, `eventAttendees`, and `isSavingEvent` were declared inside the IIFE using `let`, making them local to each IIFE instance
2. **Multiple IIFE Instances**: If the partial view's scripts ran multiple times (even once per page), each execution created its own isolated scope with its own copy of these variables
3. **Event Handler/State Mismatch**: When opening an edit modal, `currentEventId` might be set to the event ID in one IIFE instance, but when the save button was clicked, the handler might run in a different IIFE instance where `currentEventId` was still `null`
4. **Always Creating Instead of Updating**: Because `currentEventId` was `null` in the handler's scope, the save function always thought it was creating a new event, even when editing an existing one
5. **Secondary Issue - Multiple Handlers**: Using `addEventListener` instead of `onclick` also caused multiple handlers to accumulate, compounding the problem

## Evidence
Database query showed two events with identical data and the EXACT same timestamp:
```
13,4,5,Real Visible Finale?,...,2025-10-21 22:13:37.9396570,...
14,4,5,Real Visible Finale?,...,2025-10-21 22:13:37.9396570,...
```

## Solution
Fixed the root cause by implementing global shared state and multiple layers of protection:

### Critical Fix: Global Shared State
- **Created `window._matterCalendarState`**: A global object that persists across all IIFE instances
- **Moved Critical Variables to Global State**: 
  - `currentEventId` → `state.currentEventId`
  - `eventAttendees` → `state.eventAttendees`
  - `isSavingEvent` → `state.isSavingEvent`
  - `allEvents`, `allMatters`, `allUsers` → `state.allEvents`, etc.
  - `currentDate` → `state.currentDate`
- **Shared Across All Instances**: Now when the modal sets `state.currentEventId`, the save handler sees the same value, regardless of which IIFE instance it runs in

**Before (Broken):**
```javascript
// Each IIFE has its own currentEventId
(function() {
    let currentEventId = null; // ❌ Local to this IIFE instance
    
    function openEventModal(eventId) {
        currentEventId = eventId; // Sets in THIS instance
    }
    
    function saveEvent() {
        if (currentEventId) { // May check DIFFERENT instance's variable (null!)
            // Update...
        } else {
            // Create... ❌ WRONG! Should be updating!
        }
    }
})();
```

**After (Fixed):**
```javascript
// Global state shared across all IIFEs
if (!window._matterCalendarState) {
    window._matterCalendarState = {
        currentEventId: null // ✓ Global, shared by all instances
    };
}
const state = window._matterCalendarState;

(function() {
    function openEventModal(eventId) {
        state.currentEventId = eventId; // ✓ Sets in GLOBAL state
    }
    
    function saveEvent() {
        if (state.currentEventId) { // ✓ Always reads from GLOBAL state
            // Update... ✓ CORRECT!
        } else {
            // Create...
        }
    }
})();
```

### Secondary Fixes:

### 1. Fixed Event Handler Accumulation - Changed from `addEventListener` to `onclick`
- `onclick` replaces the existing handler instead of adding a new one
- This prevents accumulation of multiple handlers

**Before:**
```javascript
if (saveBtn) saveBtn.addEventListener('click', saveEvent);
```

**After:**
```javascript
if (saveBtn) {
    saveBtn.onclick = saveEvent;
    saveBtn.dataset.handlersInitialized = 'true';
}
```

### 2. Added Global Flag on DOM Elements
- Added `data-handlersInitialized` attribute to DOM elements to track initialization across IIFE instances
- Checked this flag before initializing handlers

```javascript
// Check global flag to prevent duplicate handlers across all IIFE instances
const saveBtn = document.getElementById('saveEventBtn');
if (saveBtn && saveBtn.dataset.handlersInitialized === 'true') {
    console.log('Modal handlers already initialized globally, skipping');
    return;
}
```

### 3. Added Timestamp-Based Debounce
- Added a 1-second debounce to the `saveEvent` function to prevent rapid double-clicks
- Uses a global timestamp to track the last save attempt

```javascript
// Prevent duplicate submissions with timestamp check
const now = Date.now();
const lastSaveAttempt = window._lastCalendarEventSaveAttempt || 0;

if (now - lastSaveAttempt < 1000) {
    console.log('Save attempted too quickly after previous save, ignoring (debounce)');
    return;
}

window._lastCalendarEventSaveAttempt = now;
```

### 4. Kept Existing Guards
- Maintained the `isSavingEvent` flag as an additional safeguard
- Maintained the `modalHandlersInitialized` flag for instance-level protection

## Files Modified
- `Certio.Web/Views/Matter/_MatterCalendar.cshtml` - Fixed global state and event handlers
- `CALENDAR_DUPLICATE_EVENTS_FIX.md` - This documentation file

## Changes Applied
1. **Created Global State Object** at the top of the IIFE:
   - Initialized `window._matterCalendarState` if it doesn't exist
   - Moved all critical state variables into this global object
   - All functions now reference `state.variableName` instead of local variables

2. **Updated ALL References** throughout the file:
   - `currentEventId` → `state.currentEventId` (most critical!)
   - `eventAttendees` → `state.eventAttendees`
   - `isSavingEvent` → `state.isSavingEvent`
   - `allEvents` → `state.allEvents`
   - `allMatters` → `state.allMatters`
   - `allUsers` → `state.allUsers`
   - `currentDate` → `state.currentDate`

3. Updated `initializeModalHandlers()` function:
   - Added global flag check using `dataset.handlersInitialized`
   - Changed all `addEventListener` calls to `onclick` assignments
   - Applied to: save button, close button, delete button, add attendee button, all-day checkbox, color options, collapsible headers

4. Updated `initializeEventHandlers()` function:
   - Added global flag check using `dataset.handlersInitialized`
   - Changed all `addEventListener` calls to `onclick` assignments
   - Applied to: prev/next month buttons, create event button, create task button

5. Updated `saveEvent()` function:
   - Added timestamp-based debounce (1 second)
   - Uses global `window._lastCalendarEventSaveAttempt` variable

## Why This Doesn't Affect Client Calendar
The Client Calendar view (`Certio.Web/Views/Client/Calendar.cshtml`) does not have this issue because:
1. It's a full page load, not an AJAX-loaded partial view
2. The IIFE only runs once per page load
3. Users navigate away from the page entirely when switching views, so there's no opportunity for the scripts to run multiple times

## Testing
To verify the fix:
1. Navigate to a Matter's Calendar tab
2. Create a new event and save it
3. Verify only one event is created in the database
4. Edit an existing event and save it
5. Verify the event is updated (not duplicated)
6. Rapidly double-click the save button
7. Verify only one event is created/updated (debounce protection)

## Prevention for Future Development
When creating AJAX-loaded partial views with JavaScript:
1. Use `onclick` instead of `addEventListener` to prevent handler accumulation
2. Add global flags on DOM elements using `data-` attributes to track initialization across script instances
3. Implement debounce/throttle for critical actions like save operations
4. Consider using event delegation on parent elements instead of direct handlers
5. Always wrap scripts in IIFEs but be aware they run on each HTML injection

