# Certio mobile style template

Reference extracted from the Certio.Web stylesheet layer (`wwwroot/css/`, primarily `site.css`, `login.css`, `tasks.css`, `communications.css`, sidebars, `agent-actions.css`, `task-details-modal.css`). Use this when building mobile UIs so new screens match the existing web app’s visual language.

---

## How to use

- Treat values below as **design tokens**: prefer reusing the same hex/rgba and radii before inventing new ones.
- **Primary brand** is burgundy (`#3d1019`) with a **rose accent** (`#a32b43`) in gradients; **legal / UI blue** appears as `rgba(11, 54, 94, …)` (≈ `#0b366e`) for shadows, borders, and tinted surfaces.
- **Neutrals** follow a Tailwind-like gray scale (`#f9fafb` … `#1f2937`) throughout the app.

---

## Color palette

### Brand

| Token | Value | Typical use |
|--------|--------|--------------|
| `brand-primary` | `#3d1019` | Buttons, icons, active chat rows, FABs, focus rings (with alpha), gradient end stops |
| `brand-accent` | `#a32b43` | Gradient start with `brand-primary` (headers, avatars, task chrome) |
| `brand-primary-hover` | `#6d2932` | Communications toggle hover (sidebar) |
| `brand-tint-surface` | `#C7B7A333` | Page body background (8-digit hex with alpha) |
| `brand-wash` | `rgba(61, 16, 25, 0.06–0.18)` | Chips, hover fills, subtle borders on agent UI |

### Legal / “navy” accent (blue)

| Token | Value | Typical use |
|--------|--------|--------------|
| `legal-blue` | `rgb(11, 54, 94)` / `#0b366e` | Shadow tints, borders `rgba(11, 54, 94, 0.1)`, card hover elevation |
| `legal-blue-soft` | `rgba(11, 54, 94, 0.03–0.2)` | Section backgrounds, gradients, dividers |

### Neutrals (most common)

| Token | Value | Typical use |
|--------|--------|--------------|
| `gray-50` | `#f9fafb` | Cards, inputs, conversation hover |
| `gray-100` | `#f3f4f6` | Badges, secondary surfaces |
| `gray-200` | `#e5e7eb` | Default borders, search pill border, sidebar rule |
| `gray-300` | `#d1d5db` | Input border on focus |
| `gray-400` | `#9ca3af` | Placeholders, tertiary text |
| `gray-500` | `#6b7280` | Secondary text, icons |
| `gray-600` | `#4a5568` | Body on tinted panels (communications) |
| `gray-700` | `#374151` | Primary body text, inputs |
| `gray-800` | `#1f2937` | Headings |
| `gray-900` | `#111827` | Dark mode surfaces (login) |
| `slate-text` | `#232b39` | Chat-style labels (tasks / communications) |

### Borders & lines (alternate scales)

- `#e0e0e0`, `#eeeeee`, `#e9ecef` — legacy dividers and task list rules  
- `#cbd5e1` — emphasized card border on hover (communications)

### Semantic

| Role | Background | Border / text | Notes |
|------|------------|----------------|--------|
| Success | `#f0fdf4`, `#10b981` usage | `#166534`, `#bbf7d0` | Login success; online dot `#10b981` |
| Error | `#fef2f2` | `#dc2626`, `#fecaca` | Login error, validation |
| Warning / orange | `#fff3e0` | `#e65100` | Task status chips |
| Info / blue surface | `#e3f2fd` | `#1565c0` | Task chips |
| Destructive accent | `#e74c3c`, `#dc3545` | Notifications badge, borders |

### Link & interactive (Bootstrap overlap)

- Links: `#0077cc` (`_Layout.cshtml.css`); primary blue `#007bff` for task modal actions and message bubbles in communications.

### Agent / system accents (agent-actions)

- Blue `#3b82f6`, purple `#8b5cf6`, amber `#f59e0b`, green `#10b981`, red `#ef4444` / `#dc2626` for tags, pulses, and gradient buttons.

---

## Backgrounds & surfaces

| Pattern | Value | Use |
|---------|--------|-----|
| App canvas | `#C7B7A333` | Default `body` |
| Card / sheet | `#ffffff` | Cards, sidebars, modals |
| Hover / secondary | `#f9fafb`, `#eef1f3`, `#fafafa` | Folder cards, team cards, panels |
| Login hero | `linear-gradient(135deg, #f8fafc 0%, #f1f5f0 100%)` | Login page |
| Login card glass | `rgba(255, 255, 255, 0.95)` + `backdrop-filter: blur(10px)` | Login card |
| Dark mode login (optional) | `#1f2937` → `#111827` gradient | `prefers-color-scheme: dark` in `login.css` |
| Overlay | `rgba(0, 0, 0, 0.5)` | Modal backdrops (communications, task modal) |
| Message / bubble gray | `#f3f4f6` with `#1e293b` text | Communications composer area |
| Warm neutrals (agent) | `#c7b7a3`, `#ede0d0`, `#f3f4f6` | Suggestion / secondary buttons |

---

## Border radius

| Scale | Value | Use |
|-------|--------|-----|
| `radius-xs` | `3px` | Scrollbar thumbs |
| `radius-sm` | `4px` | Close buttons, small controls, login alerts |
| `radius-md` | `6px` | List rows, small cards |
| `radius-lg` | `8px` | Search fields, filters, inputs |
| `radius-xl` | `12px` | **Most cards**, conversation items, matter folders, agent cards |
| `radius-2xl` | `16px` | Task modal dialog |
| `radius-sheet` | `18px` | Some chat bubbles |
| `radius-top-sheet` | `20px 20px 0 0` | Bottom sheets: communications, notifications, task modal top |
| `radius-pill` | `24px` | Large search container |
| `radius-fab` | `25px` | Floating pill container (tasks) |
| `radius-full` | `50%` / `9999px` | Avatars, icon buttons, badges |

---

## Box shadows

Grouped by how often they appear and for what elevation.

### Focus rings (accessibility)

- `0 0 0 3px rgba(61, 16, 25, 0.08)` — buttons  
- `0 0 0 3px rgba(61, 16, 25, 0.06)` — inputs, checkboxes  
- `0 0 0 3px rgba(255, 107, 53, 0.1)` — login focus (accent)  
- `0 0 0 3px rgba(220, 38, 38, 0.1)` / `rgba(16, 185, 129, 0.1)` — invalid / valid form  
- `0 0 0 3px rgba(11, 54, 94, 0.1)` — blue focus variant (site.css)  
- `0 0 0 0.2rem rgba(11, 54, 94, 0.25)` — Bootstrap-style focus (forms)

### Low elevation (bars, subtle cards)

- `0 2px 4px rgba(0, 0, 0, 0.05)` — mobile header, sidebar edge  
- `0 2px 4px rgba(0, 0, 0, 0.1)` — compact cards, tasks  
- `0 2px 4px rgba(0, 0, 0, 0.18)` — search + filter chips  
- `0 1px 2px rgba(0, 0, 0, 0.1)` — notification composer footer  
- `0 1px 3px rgba(0, 0, 0, 0.1)` — comms message cards  
- `0 1px 4px rgba(0, 0, 0, 0.25–0.3)` — floating bubbles, compact UI  

### Medium elevation (default cards, sidebars)

- `0 2px 8px rgba(0, 0, 0, 0.1)` — avatars, medium cards  
- `0 2px 8px rgba(0, 0, 0, 0.25)` — **very common** cards and inputs  
- `0 2px 8px rgba(0, 0, 0, 0.3)` — active / emphasized panels  
- `0 4px 8px rgba(0, 0, 0, 0.15)` — task panels, elevated strips  
- `0 4px 12px rgba(0, 0, 0, 0.1)` — agent panels  
- `0 4px 12px rgba(0, 0, 0, 0.15)` — agent destructive gradient button shadow  

### Blue-tinted elevation (brand secondary)

- `0 4px 12px rgba(11, 54, 94, 0.15)` — conversation item hover  
- `0 4px 12px rgba(11, 54, 94, 0.25–0.4)` — stronger blue lift  
- `0 2px 8px rgba(11, 54, 94, 0.2–0.3)` — chat avatars, headers  

### Brand-tinted

- `0 4px 12px rgba(61, 16, 25, 0.3)` — comms FAB  
- `0 2px 8px rgba(99, 102, 241, 0.3)` — alternate accent (site.css)  

### High elevation (modals, login)

- `0 10px 24px rgba(0, 0, 0, 0.2)` — card hover lift, notifications drawer  
- `0 20px 40px rgba(0, 0, 0, 0.1)` — login card hover  
- `0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)` — communications sheet (Tailwind-like)  
- `0 25px 50px -12px rgba(0, 0, 0, 0.4), 0 12px 24px -8px rgba(0, 0, 0, 0.3)` — task details modal dialog  
- `0 4px 20px rgba(0, 0, 0, 0.15)` — task floating panel  

### Soft / legacy

- `0 .25rem .75rem rgba(0, 0, 0, .05)` — `.box-shadow` in `_Layout.cshtml.css`  
- `0 0.5rem 1rem rgba(0, 0, 0, 0.15)` — team card hover  

### Special

- `drop-shadow(0 0 4px rgba(255,255,255,0.5))` — agent mic highlight  
- Pulse rings: `0 0 0 8px rgba(59, 130, 246, 0)` (animation keyframes in `agent-actions.css`)

---

## Borders & dividers

- Default: `1px solid #e5e7eb`  
- Subtle legacy: `#e0e0e0`, `#eeeeee`  
- Focus / hover: `#d1d5db`, `#cbd5e1`  
- Brand: `#3d1019` on active or hover chips  
- Blue divider: `rgba(11, 54, 94, 0.1)` (sidebar headers)  
- Semantic: `#fecaca`, `#fca5a5`, `#bbf7d0` (alerts)  

---

## Typography

| Element | Pattern |
|---------|---------|
| Root | `14px` mobile; `16px` from `768px` up (`site.css`) |
| Page title feel | `1.5rem`, weight `600`, `#1f2937` |
| Section / chat title | `1.1rem`–`1.25rem`, weight `600` |
| Body | `0.875rem`–`1rem`, `#374151` |
| Secondary | `0.875rem`, `#6b7280` |
| Meta / placeholder | `0.8rem`–`0.875rem`, `#9ca3af` |
| Badges | `0.875rem`, weight `500` |

---

## Motion

- **Standard:** `transition: all 0.2s ease` or `ease-in-out`  
- **Slower UI:** `0.3s ease` (sidebars, FAB)  
- **Card hover:** often `translateY(-1px)` or `-2px` with stronger shadow  

---

## Scrollbars (web; mirror concept on mobile as “thin indicator”)

- Width/height: `6px`–`8px`  
- Thumb: `rgba(0, 0, 0, 0.2)` hover `0.35`; or `rgba(155, 155, 155, 0.7)` on horizontal matters scroller  
- Radius: `3px`–`4px`  
- Firefox: `scrollbar-width: thin`; `scrollbar-color: … transparent`  

---

## Overlays & sheets

- Bottom sheets / panels: top radius `20px`, shadow `0 2px 4px`–`0 10px 24px` depending on layer  
- Modal scrim: `rgba(0, 0, 0, 0.5)`  
- Login card hover: `0 20px 40px rgba(0, 0, 0, 0.1)`  

---

## Mobile-specific notes (from `login.css`)

- Breakpoint: `max-width: 640px` — tighter padding on container and card, smaller titles (`1.75rem` login title, `1.25rem` card title)  
- Touch-friendly padding: `1.25rem` on card header/body  

---

## Quick “default component” recipe

1. **Surface:** white `#ffffff` on canvas `#C7B7A333` (or neutral gray `#f9fafb` for nested blocks).  
2. **Radius:** `12px` for cards; `8px` for inputs; `50%` for avatars.  
3. **Shadow:** `0 2px 8px rgba(0, 0, 0, 0.25)` for resting cards; increase to `0 4px 12px` + optional `rgba(11, 54, 94, 0.15)` on press/hover.  
4. **Text:** title `#1f2937`, body `#374151`, hint `#6b7280` / `#9ca3af`.  
5. **Primary action:** fill `#3d1019` or gradient `135deg, #a32b43 → #3d1019`; focus ring `rgba(61, 16, 25, 0.08)`.  

---

*Generated from Certio.Web stylesheets for consistent mobile and cross-platform UI work.*
