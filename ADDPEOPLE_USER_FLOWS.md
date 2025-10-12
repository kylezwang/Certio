# AddPeople Page - User Flow Diagrams

## Flow 1: Law Firm User in Law Firm Organization

```
┌─────────────────────────────────────────────────────────────┐
│ Page Load: /Client/{lawFirmOrgId}/AddPeople                │
│ Context: User.UserType = "LawFirm"                         │
│          CurrentOrg = Law Firm Organization                 │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 1: Card Selection                                       │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│ │ Internal    │  │ Client      │  │ External    │         │
│ │ Team        │  │             │  │ Party       │         │
│ └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                              │
│ Description: "Add attorneys, paralegals, or staff to firm"  │
└─────────────────────────────────────────────────────────────┘
           ↓                    ↓                    ↓
    [Internal Team]        [Client]           [External]
           ↓                    ↓                    ↓
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│ STEP 2:          │   │ STEP 2:          │   │ STEP 2:          │
│ Join Code Form   │   │ Join Code Form   │   │ Join Code Form   │
│                  │   │                  │   │                  │
│ • Email          │   │ • Email          │   │ • Email          │
│ • Law Firm Role  │   │ • Client Role    │   │ • External Role  │
│ • Department     │   │ • Department     │   │                  │
│ • Job Title      │   │ • Job Title      │   │                  │
└──────────────────┘   └──────────────────┘   └──────────────────┘
           ↓                    ↓                    ↓
┌──────────────────────────────────────────────────────────────┐
│ Generate Join Code                                           │
│ • organizationId = lawFirmOrgId                              │
│ • invitedUserType = "LawFirm" | "Client" | "External"       │
│ • invitedRole = [selected role]                              │
└──────────────────────────────────────────────────────────────┘
           ↓
┌──────────────────────────────────────────────────────────────┐
│ Modal: Display Join Code (7-day expiration)                  │
└──────────────────────────────────────────────────────────────┘
```

---

## Flow 2: Law Firm User in Client Organization

```
┌─────────────────────────────────────────────────────────────┐
│ Page Load: /Client/{clientOrgId}/AddPeople                 │
│ Context: User.UserType = "LawFirm"                         │
│          CurrentOrg = Client Organization                   │
│          Relationship Exists: LawFirm ↔ Client              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 1: Card Selection                                       │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│ │ Internal    │  │ Client      │  │ External    │         │
│ │ Team        │  │             │  │ Party       │         │
│ └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                              │
│ Description: "Add members from [Law Firm] to work on this   │
│               client's matters"                              │
└─────────────────────────────────────────────────────────────┘
           ↓                    ↓                    ↓
    [Internal Team]        [Client]           [External]
           ↓                    ↓                    ↓
┌─────────────────────────┐   ┌──────────────┐   ┌──────────┐
│ STEP 2:                 │   │ STEP 2:      │   │ STEP 2:  │
│ Team Member Selection   │   │ Join Code    │   │ Join Code│
│ ╔═══════════════════╗   │   │ Form         │   │ Form     │
│ ║ Search Box        ║   │   └──────────────┘   └──────────┘
│ ╚═══════════════════╝   │            ↓                ↓
│                         │   Generate join code for:
│ Selected Members:       │   - Client org member
│ ┌───────────────────┐   │   - External party
│ │ [Chips with names]│   │
│ └───────────────────┘   │
│                         │
│ Available Members:      │
│ ┌───────────────────┐   │
│ │ ☐ John Smith      │   │
│ │   Partner         │   │
│ │                   │   │
│ │ ☑ YOU (disabled)  │   │
│ │   Partner         │   │
│ │                   │   │
│ │ ☑ Jane Doe        │   │
│ │   Already Assigned│   │
│ └───────────────────┘   │
└─────────────────────────┘
           ↓
┌────────────────────────────────────────────────────────────┐
│ Submit Selected Members                                     │
│                                                             │
│ For each selected member:                                  │
│   Create OrganizationRelationshipAssignedUser {            │
│     RelationshipId: [LawFirm ↔ Client relationship ID]     │
│     UserId: [selected member's ID]                         │
│     AssignedAt: DateTime.UtcNow                            │
│     AssignedById: [current user's ID]                      │
│   }                                                         │
│                                                             │
│ ⚠️ NO UserOrganization entries created                      │
│ ✅ Member maintains single org membership (Law Firm)        │
│ ✅ Access granted through relationship assignment           │
└────────────────────────────────────────────────────────────┘
           ↓
┌────────────────────────────────────────────────────────────┐
│ Success: "Successfully assigned X team member(s)"          │
│ Redirect to Teams page                                     │
└────────────────────────────────────────────────────────────┘
```

---

## Flow 3: Client User in Client Organization

```
┌─────────────────────────────────────────────────────────────┐
│ Page Load: /Client/{clientOrgId}/AddPeople                 │
│ Context: User.UserType = "Client"                          │
│          CurrentOrg = Client Organization                   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ SINGLE PAGE: Simple Form (No Steps, No Cards)              │
│                                                             │
│ Add People to [Organization Name]                          │
│                                                             │
│ Email Address: [_____________________] *                    │
│                                                             │
│ Role: [Owner / Manager / Member / Lawyer] *                │
│                                                             │
│ Department: [_____________________]                         │
│                                                             │
│ Job Title: [_____________________]                          │
│                                                             │
│ [Generate Join Code →]                                      │
│                                                             │
│ Hidden: UserType = "Client"                                 │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Generate Join Code                                          │
│ • organizationId = clientOrgId                              │
│ • invitedUserType = "Client" (always)                       │
│ • invitedRole = [selected client role]                      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Modal: Display Join Code                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Data Model Relationships

### UserOrganization (Single membership per org)
```
┌─────────────────────────────────────────┐
│ UserOrganization                        │
├─────────────────────────────────────────┤
│ Id: int                                 │
│ UserId: int → User                      │
│ OrganizationId: int → Organization      │
│ UserType: string (LawFirm/Client/etc.)  │
│ Role: string (Partner/Owner/etc.)       │
│ IsActive: bool                          │
│ IsPrimary: bool                         │
└─────────────────────────────────────────┘
```

### OrganizationRelationship (Law Firm ↔ Client)
```
┌─────────────────────────────────────────┐
│ OrganizationRelationship                │
├─────────────────────────────────────────┤
│ Id: int                                 │
│ SourceOrganizationId: int (Law Firm)    │
│ TargetOrganizationId: int (Client)      │
│ RelationshipType: string                │
│ AccessLevel: string                     │
│ IsActive: bool                          │
└─────────────────────────────────────────┘
```

### OrganizationRelationshipAssignedUser (Assignment)
```
┌─────────────────────────────────────────┐
│ OrganizationRelationshipAssignedUser    │
├─────────────────────────────────────────┤
│ Id: int                                 │
│ RelationshipId: int → Relationship      │
│ UserId: int → User (Law Firm member)    │
│ AssignedAt: DateTime                    │
│ AssignedById: int → User                │
└─────────────────────────────────────────┘
```

### Example Data Flow

**Scenario: Assign John Smith (law firm partner) to Acme Corp (client)**

```
1. John has UserOrganization:
   - OrganizationId: 1 (Smith & Associates Law Firm)
   - UserType: "LawFirm"
   - Role: "Partner"

2. OrganizationRelationship exists:
   - SourceOrganizationId: 1 (Smith & Associates)
   - TargetOrganizationId: 5 (Acme Corp)
   - RelationshipType: "LawFirmClient"

3. Create OrganizationRelationshipAssignedUser:
   - RelationshipId: [relationship ID from step 2]
   - UserId: [John's user ID]
   - AssignedAt: 2025-10-12 14:30:00
   - AssignedById: [current user's ID]

✅ Result:
   - John still has ONE UserOrganization (law firm)
   - John now has access to Acme Corp through the relationship
   - No duplicate UserOrganization entry created
   - Clean, normalized data structure
```

---

## UI Components Hierarchy

```
AddPeople.cshtml
├── Container (min-h-screen bg-gradient-subtle)
│   ├── Back Button (← Back to Teams)
│   ├── Alert Messages (Success/Error/Info)
│   │
│   └── Conditional Rendering:
│       │
│       ├── [IF Law Firm User]
│       │   ├── Step 1: Card Selection Grid
│       │   │   ├── Internal Team Card (blue)
│       │   │   ├── Client Card (green)
│       │   │   └── External Card (orange)
│       │   │
│       │   └── Step 2: Dynamic Form
│       │       ├── [IF Client Context + Internal Team]
│       │       │   ├── Search Box
│       │       │   ├── Selected Members Chips
│       │       │   └── Available Members List
│       │       │       └── Checkboxes (with badges)
│       │       │
│       │       └── [ELSE: Join Code Form]
│       │           ├── Email Input
│       │           ├── Role Dropdown
│       │           ├── Department Input (optional)
│       │           └── Job Title Input (optional)
│       │
│       └── [ELSE: Client User]
│           └── Single Page Simple Form
│               ├── Email Input
│               ├── Role Dropdown
│               ├── Department Input
│               └── Job Title Input
│
└── Join Code Modal (conditional)
    ├── Modal Header
    ├── Code Input (readonly) + Copy Button
    └── Modal Footer
```

---

## JavaScript Event Handlers

```javascript
// Card Selection
document.querySelectorAll('.user-type-card')
  └── onClick: Set SelectionType → Submit form
  └── onMouseEnter: Transform & shadow effect
  └── onMouseLeave: Reset transform & shadow

// Team Member Search
document.getElementById('teamMemberSearch')
  └── onInput: Filter .user-item by name/email

// Team Member Checkboxes
document.querySelectorAll('.member-checkbox')
  └── onChange: updateSelectedDisplay()
      ├── Generate chips for selected members
      ├── Show/hide empty message
      └── Add remove handlers to chip buttons

// Join Code Modal
if (ViewBag.JoinCode exists)
  └── Show modal on page load
      └── Copy button: navigator.clipboard.writeText()
          └── Show check icon (1.5s) → reset to copy icon
```

---

## Context Detection Logic

```javascript
// In PrepareAddPeopleViewModel()

1. Get user's primary organization
   ↓
2. Check if UserType == "LawFirm"
   ↓ YES
3. Check if relationship exists:
   SourceOrganizationId = User's LawFirm Org
   TargetOrganizationId = Current Org (from URL)
   ↓ YES
4. Context: "Client Organization"
   - Show client-specific card descriptions
   - Load law firm members for Internal Team
   - Check for existing assignments
   ↓ NO
5. Context: "Law Firm Organization"
   - Show law firm card descriptions
   - All cards lead to join code forms
```

---

## Permission & Authorization

```
┌────────────────────────────────────────────────────────┐
│ [Authorize(Policy = "OrgMember")]                      │
│ ↓                                                       │
│ User must be active member of target organization      │
└────────────────────────────────────────────────────────┘
          ↓
┌────────────────────────────────────────────────────────┐
│ customUser.CanCreateJoinCodes(orgId)                   │
│ ↓                                                       │
│ User has permission to create join codes               │
└────────────────────────────────────────────────────────┘
          ↓
┌────────────────────────────────────────────────────────┐
│ For relationship assignments:                          │
│ ↓                                                       │
│ Verify OrganizationRelationship exists                 │
│ Verify user is from source organization (law firm)     │
│ Only assign users who are active law firm members      │
└────────────────────────────────────────────────────────┘
```

---

## Error Handling

| Scenario | Error Message | Action |
|----------|---------------|--------|
| User not authenticated | "Unable to resolve current user." | Redirect to Teams |
| Organization not found | "Organization not found." | Redirect to Home |
| No permission to create codes | "You do not have permission to create join codes." | Show error, stay on page |
| No team members selected | "Please select at least one team member." | Show validation error |
| Relationship not found | "Organization relationship not found." | Show error, stay on page |
| Invalid selection type | "Invalid selection type." | Show error, stay on page |
| Email validation | Standard ASP.NET validation | Show field error |

---

## Success States

| Action | Success Message | Navigation |
|--------|-----------------|------------|
| Team members assigned | "Successfully assigned X team member(s) to [Org Name]." | Redirect to Teams |
| Join code generated | "Join code generated successfully." | Stay on page, show modal |
| All members already assigned | "All selected members were already assigned." (Info) | Redirect to Teams |

---

## Browser Compatibility

- ✅ Modern browsers (Chrome, Firefox, Safari, Edge)
- ✅ Uses ES6+ JavaScript (async/await, arrow functions)
- ✅ Bootstrap 5 components
- ✅ CSS Grid and Flexbox
- ⚠️ Requires `navigator.clipboard` API for copy functionality
- ⚠️ Graceful degradation for older browsers

---

## Mobile Responsiveness

- Cards stack vertically on mobile (col-md-4 → full width)
- Form inputs are touch-friendly (large size)
- Modal is centered and responsive
- Search and selection UI work on touch devices
- Back button accessible on all screen sizes

