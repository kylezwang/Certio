# Development Overlay Component

A reusable component to indicate features that are still in development with a clickable overlay.

## Features

-  **Visual Indicator**: White hourglass icon with "Development in Progress" message
-  **Click to Dismiss**: Users can click anywhere on the overlay to proceed
-  **Session Persistence**: Dismissal is remembered for the current browser session
-  **Backdrop Blur**: Professional glass-morphism effect
-  **Smooth Animations**: Fade in/out transitions
-  **Flexible Positioning**: Can be applied to specific elements or full-screen

## Usage

### Full-Screen Overlay (e.g., Billing Page)

Covers the entire page content (but not the navbar):

```cshtml
<div class="page-container" style="position: relative;">
    @{
        ViewData["targetSelector"] = "";
        ViewData["fullScreen"] = true;
    }
    @await Html.PartialAsync("_DevelopmentOverlay")
    
    <!-- Your page content here -->
</div>
```

### Element-Specific Overlay (e.g., Planner View Card)

Covers a specific element or card:

```cshtml
<div id="myFeature" style="position: relative;">
    @{
        ViewData["targetSelector"] = "#myFeature";
        ViewData["fullScreen"] = false;
        ViewData["verticalMargin"] = "1rem";  // Optional: adds top/bottom margin
        ViewData["borderRadius"] = "0.5rem";  // Optional: rounds corners
    }
    @await Html.PartialAsync("_DevelopmentOverlay")
    
    <!-- Your feature content here -->
</div>
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `targetSelector` | string | `""` | CSS selector of element to cover. Empty for fullscreen. |
| `fullScreen` | bool | `false` | If true, creates fixed fullscreen overlay. |
| `verticalMargin` | string | `"0"` | Top/bottom margin (e.g., "1rem") for vertical spacing. |
| `borderRadius` | string | `"0"` | Border radius (e.g., "0.5rem") for rounded corners. |

## Examples

### Applied to a Card
```cshtml
<div class="card" id="feature-card" style="position: relative;">
    @{
        ViewData["targetSelector"] = "#feature-card";
        ViewData["fullScreen"] = false;
    }
    @await Html.PartialAsync("_DevelopmentOverlay")
    
    <div class="card-body">
        <!-- Card content -->
    </div>
</div>
```

### Applied to a Tab Panel
```cshtml
<div class="tab-pane" id="settings-tab" style="position: relative;">
    @{
        ViewData["targetSelector"] = "#settings-tab";
        ViewData["fullScreen"] = false;
    }
    @await Html.PartialAsync("_DevelopmentOverlay")
    
    <!-- Settings content -->
</div>
```

### Applied to Entire Page
```cshtml
<div class="container" style="position: relative;">
    @{
        ViewData["fullScreen"] = true;
    }
    @await Html.PartialAsync("_DevelopmentOverlay")
    
    <!-- All page content -->
</div>
```

## Current Implementations

- **Billing Page**: Full-screen overlay with `z-index: 100000 !important` to cover Session Manager sidebar and filters (`/Views/Billing/Index.cshtml`)
- **Tasks Planner View**: Element overlay with 1rem vertical margin and 0.5rem border radius (`/Views/Tasks/Index.cshtml`)

## Styling Notes

- Parent element must have `position: relative` or `position: absolute` for non-fullscreen overlays
- Element overlays use `z-index: 100` to stay below navigation elements
- **Fullscreen overlays:**
  - Use `z-index: 100000 !important` to cover all page UI
  - Are appended to `document.body` to cover sidebars and all elements
  - Use `position: fixed` with `left: 6vw` to start after the left navbar
  - Cover entire viewport except the 6vw left navigation bar
- Background: 40% opacity black with 3px blur for lighter, more subtle appearance
- Text has enhanced shadow for better readability on lighter background
- Supports vertical margins and border radius for flexible positioning
- Pulse animation on hourglass icon
- Smooth fade-out on dismissal

## Session Storage

Each overlay stores its dismissal state in `sessionStorage` with a unique key:
```
dev-overlay-dismissed-{overlayId}
```

This means:
-  Dismissal persists across page navigations in the same tab
-  Each overlay instance is tracked separately
-  Dismissal resets when browser tab is closed
-  Dismissal is not shared across browser tabs

## Customization

To customize the appearance, modify the styles in `_DevelopmentOverlay.cshtml`:

- **Background**: `.dev-overlay { background: rgba(0, 0, 0, 0.4); }` (40% opacity - lighter)
- **Backdrop Blur**: `.dev-overlay { backdrop-filter: blur(3px); }` (subtle blur)
- **Z-Index**: Element: `100`, Fullscreen: `100000 !important` (covers all UI including sidebars)
- **Vertical Margin**: Dynamic via `verticalMargin` parameter (e.g., "1rem")
- **Border Radius**: Dynamic via `borderRadius` parameter (e.g., "0.5rem")
- **Icon**: `.dev-overlay-icon { font-size: 3rem; }`
- **Text Shadow**: Enhanced shadows for better readability on lighter background
- **Animation**: `@keyframes pulse { ... }`

