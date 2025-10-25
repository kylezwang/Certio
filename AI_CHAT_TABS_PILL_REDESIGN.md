# AI Chat Tabs Pill-Shaped Badge Redesign

## Overview
Redesigned the conversation tabs in the AI chat sidebar to be pill-shaped badges with transparent backgrounds for unselected tabs, while maintaining the brand color (#3d1019) for selected tabs. The tabs now align properly with the sticky message and account for scrollbar width.

## Key Changes

### 1. Pill-Shaped Design
**Before:** Rounded top corners only (tab-like)
**After:** Fully rounded corners (border-radius: 20px) creating pill badges

### 2. Transparent Background
**Before:** White background for unselected tabs
**After:** Transparent background with subtle border for unselected tabs

### 3. Proper Alignment
- Tabs container padding: `0.5rem 1rem 0.5rem 0.5rem` (extra right padding for scrollbar)
- Aligns with sticky message width
- Consistent spacing throughout

## CSS Changes

### Tab Container
```css
.chat-tabs-container {
  padding: 0.5rem 1rem 0.5rem 0.5rem; /* Match sticky message padding */
  border-bottom: none;
  width: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  position: relative;
  z-index: 1;
}
```

### Tab Styling
```css
.conversation-tab {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  background-color: transparent; /* Transparent background */
  border: 1px solid rgba(61, 16, 25, 0.2); /* Subtle border */
  border-radius: 20px; /* Full pill shape */
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
  min-width: 100px;
  max-width: 180px;
  position: relative;
  overflow: hidden;
  color: #3d1019;
  font-size: 0.875rem;
}
```

### Hover States

#### Unselected Tab Hover
```css
.conversation-tab:hover:not(.active) {
  background-color: rgba(61, 16, 25, 0.1); /* Subtle tint */
  border-color: rgba(61, 16, 25, 0.4); /* Darker border */
  color: #3d1019;
}
```

#### Selected Tab (Active)
```css
.conversation-tab.active {
  background-color: #3d1019; /* Brand color solid */
  border-color: #3d1019;
  color: white;
  font-weight: 500; /* Slightly bolder */
}
```

#### Selected Tab Hover
```css
.conversation-tab.active:hover {
  background-color: #3d1019; /* Keep solid */
  border-color: #3d1019;
  color: white;
}
```

### Close Button Styling

#### Base Style
```css
.conversation-tab .tab-close {
  opacity: 0; /* Hidden by default */
  transition: all 0.2s ease;
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%; /* Circular */
  background: rgba(61, 16, 25, 0.2);
  border: none;
  cursor: pointer;
  color: #3d1019;
  font-size: 11px;
  flex-shrink: 0;
  margin-left: 0.25rem;
}
```

#### Unselected Tab Close Button
```css
.conversation-tab:not(.active):hover .tab-close {
  opacity: 1;
  background: rgba(61, 16, 25, 0.2);
  color: #3d1019;
}

.conversation-tab:not(.active) .tab-close:hover {
  background-color: #3d1019; /* Solid on hover */
  color: white;
}
```

#### Selected Tab Close Button
```css
.conversation-tab.active .tab-close {
  color: white;
  background: rgba(255, 255, 255, 0.1);
}

.conversation-tab.active:hover .tab-close {
  background-color: rgba(255, 255, 255, 0.2);
  color: white;
}

.conversation-tab.active .tab-close:hover {
  background-color: rgba(255, 255, 255, 0.2);
  color: white;
}
```

### New Conversation Button
```css
.new-conversation-btn {
  background-color: #3d1019;
  border-color: #3d1019;
  color: white;
  border-radius: 20px; /* Pill shape to match tabs */
  padding: 0.5rem 0.75rem;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
}
```

## Dark Mode Support
```css
body.dark-mode .conversation-tab {
  background-color: transparent !important;
  color: #ffffff !important;
  border-color: rgba(255, 255, 255, 0.2) !important;
}

body.dark-mode .conversation-tab:hover {
  background-color: rgba(61, 16, 25, 0.3) !important;
  border-color: rgba(61, 16, 25, 0.5) !important;
}

body.dark-mode .conversation-tab.active {
  background-color: #3d1019 !important;
  border-color: #3d1019 !important;
  color: #ffffff !important;
}
```

## Visual Design Principles

### 1. **Transparency & Layering**
- Unselected tabs are transparent to reduce visual clutter
- Subtle borders provide definition without weight
- Selected tab stands out with solid brand color

### 2. **Pill Shape**
- Modern, friendly appearance
- Differentiates from traditional tab UX
- Better suits badge/tag paradigm

### 3. **Consistent Spacing**
- Tabs align with sticky message below
- Extra right padding accounts for scrollbar
- Proper gaps between elements

### 4. **Progressive Disclosure**
- Close buttons hidden by default
- Appear on hover for cleaner look
- Circular shape complements pill design

### 5. **Interactive Feedback**
- Clear hover states for all elements
- Smooth transitions (0.2s ease)
- Color changes indicate interactivity

## Alignment with Sticky Message

Both elements now share:
- Extra right padding (1rem) for scrollbar space
- Consistent left padding (0.5rem)
- Full-width spanning
- Proper visual hierarchy

```
┌─────────────────────────────────────────┐
│ [Tab1] [Tab2] [Tab3*] [+]              │ ← Tabs (pills)
├─────────────────────────────────────────┤
│ User message sticky overlay             │ ← Sticky message
├─────────────────────────────────────────┤
│                                         ║ ← Scrollbar
│ Chat messages                           ║
│                                         ║
```

## Benefits

1. **Cleaner Look:** Transparent backgrounds reduce visual noise
2. **Better Hierarchy:** Selected tab clearly stands out
3. **Modern Design:** Pill badges are contemporary and friendly
4. **Proper Alignment:** Tabs and sticky message width match
5. **Scrollbar Awareness:** Extra right padding prevents overlap
6. **Dark Mode Ready:** Full dark mode support included

## Files Modified

- **Certio.Web/wwwroot/css/site.css**
  - `.chat-tabs-container` - Updated padding
  - `.conversation-tabs` - Added margin reset
  - `.tab-list` - Updated gap and alignment
  - `.conversation-tab` - Complete redesign to pill shape
  - `.conversation-tab:hover` - New hover states
  - `.conversation-tab.active` - Updated active state
  - `.tab-close` - Redesigned as circular button
  - `.new-conversation-btn` - Updated to pill shape
  - Dark mode styles updated

## Testing Checklist

- ✅ Unselected tabs appear transparent with subtle border
- ✅ Selected tab has solid #3d1019 background
- ✅ Tabs have full pill shape (rounded all sides)
- ✅ Tabs align with sticky message width
- ✅ Scrollbar doesn't overlap tabs or sticky message
- ✅ Hover states work for all tab types
- ✅ Close buttons appear on hover
- ✅ Close buttons have proper styling (circular)
- ✅ New conversation button matches pill design
- ✅ Dark mode styles apply correctly
- ✅ Responsive behavior maintained

## Browser Compatibility

- ✅ Chrome/Edge (Chromium)
- ✅ Firefox
- ✅ Safari
- ✅ Mobile browsers (iOS Safari, Chrome Mobile)

All modern browsers with CSS3 support will render the design correctly.

