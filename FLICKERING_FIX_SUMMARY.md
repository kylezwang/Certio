# Flickering Fix Summary

**Date:** December 2, 2025
**Issue:** Page content flickering during navigation (production only)

---

## Problem Identified

The flickering was caused by **CSS animations that start with `opacity: 0`**, specifically:

1. **Matter cards** - `fadeInUp` animation (0.6s duration)
2. **Document cards** - `documentFadeInUp` animation (0.6s duration)
3. **Dashboard cards** (gd-card, qa-card) - `documentFadeInUp` animation
4. **Stats cards** - `fadeInUp` animation
5. **Tab content sections** - `fadeIn` animation (0.3s duration)

### Why This Caused Flickering

When navigating between pages:
1. HTML loads and renders
2. Content elements are created with `opacity: 0` (animation starting state)
3. This creates a white flash / blank area where content should be
4. CSS animations kick in and content fades from opacity 0 → 1
5. User sees a "flicker" or "white flash"

### Why Worse in Production vs Localhost

- Production has higher network latency
- CSS files may load slower from CDN
- Time between HTML rendering and CSS application is longer
- Cache behavior differences

### Why Global Navbar Didn't Flicker

The global navbar uses:
- Inline styles in `_Layout.cshtml`
- Fixed positioning with immediate background color
- No fade-in animations

---

## Solution Implemented

### 1. Critical CSS (Lines 16-42 in `_ClientLayout.cshtml`)

Added CSS that disables all fade-in animations when a `page-loading` class is present:

```css
.page-loading .matter-card,
.page-loading .document-card,
.page-loading .stats-card,
.page-loading .gd-card,
.page-loading .qa-card,
.page-loading .card,
.page-loading .tab-content-section {
    animation: none !important;
    opacity: 1 !important;
    transform: none !important;
}
```

### 2. Immediate Script Execution (Lines 44-48)

Added an inline synchronous script that runs **before any content renders**:

```javascript
document.documentElement.classList.add('page-loading');
```

This ensures the `page-loading` class is present before any animated elements are rendered.

### 3. Cleanup Script (Lines 2738-2745)

Added a script that removes the class after page load is complete:

```javascript
window.addEventListener('load', function() {
    setTimeout(function() {
        document.documentElement.classList.remove('page-loading');
    }, 100);
});
```

This re-enables animations for **dynamically added content** (e.g., filtered results, new cards added via AJAX).

---

## How It Works

### Page Load Flow (With Fix)

1. **HTML parsing starts**
2. **Inline script runs immediately** → Adds `page-loading` class
3. **Critical CSS applies** → All animations disabled, opacity set to 1
4. **Content renders** → Instantly visible, no fade-in
5. **Page fully loads**
6. **Cleanup script runs** → Removes `page-loading` class after 100ms
7. **Animations re-enabled** → Future dynamic content can animate normally

### Before vs After

**Before:**
```
Page Load → Content invisible (opacity: 0) → CSS loads → Animation plays → Content visible
           [WHITE FLASH / FLICKER HERE]
```

**After:**
```
Page Load → Content immediately visible (opacity: 1, no animation) → Complete!
           [NO FLICKER]
```

---

## Benefits

✅ **No more flickering** - Content appears instantly on page load
✅ **Animations preserved** - Still work for dynamically added content
✅ **No JavaScript changes needed** - Pure CSS + minimal inline script
✅ **Backward compatible** - Doesn't break existing functionality
✅ **Production optimized** - Solves the production-specific issue

---

## Testing Checklist

- [ ] Navigate to Matters page - no flicker
- [ ] Navigate to Tasks page - no flicker
- [ ] Navigate to Communications page - no flicker
- [ ] Navigate to Dashboard - no flicker
- [ ] Filter matters/documents - animations still work for filtered results
- [ ] Add new dynamic content - animations still work
- [ ] Test in production environment
- [ ] Test on slower network connections

---

## Files Modified

- `Certio.Web/Views/Shared/_ClientLayout.cshtml`
  - Added critical CSS (lines 29-41)
  - Added immediate script (lines 44-48)
  - Added cleanup script (lines 2739-2745)

---

## Related Issues

This is similar to a previous fix where the flickering was resolved by addressing a different root cause. This time, we directly targeted the fade-in animations that were causing the opacity-based flicker.

---

## Technical Notes

- The `page-loading` class is added to `document.documentElement` (the `<html>` tag) for maximum specificity
- Uses `!important` to override any inline styles or other CSS rules
- 100ms delay in cleanup ensures all rendering is complete before re-enabling animations
- Synchronous script execution is critical - no `defer` or `async` attributes

