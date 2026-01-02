/**
 * Agent Actions Tracker Module
 * Integrates AI agent workflows into the sidebar chat
 * Provides Cursor-style action tracking with Keep/Undo functionality
 */

(function () {
    'use strict';

    // If this script gets injected twice (AJAX nav / duplicate layout), don't crash.
    if (window.AgentActions) {
        return;
    }

// Agent Actions State
const AgentActionsState = {
    actions: [],           // Array of proposed/executed actions
    currentIndex: 0,       // Current action index being viewed
    correlationId: null,   // Current session correlation ID
    isVisible: false,      // Whether tracker overlay is visible
    isProcessing: false,   // Whether actions are being processed
    runId: null,           // Current run ID for idempotency
    conversationId: null,  // Current conversation context
    orgId: null,           // Current organization ID
    mode: 'agent',         // Current AI mode: 'agent' or 'ask'
    modelTier: 'Auto'      // Current AI model tier: Auto, GPT4o, GPT4oMini, GPT5, GPT51, GPT52
};

// Model Tier Labels for display
const ModelTierLabels = {
    'Auto': 'Auto',
    'GPT4o': 'Azure GPT-4o',
    'GPT4oMini': 'Azure GPT-4o-mini',
    'GPT5': 'Azure GPT-5',
    'GPT51': 'Azure GPT-5.1',
    'GPT52': 'Azure GPT-5.2'
};

// Action Types
const AgentActionTypes = {
    CreateTask: 'CreateTask',
    AttachFile: 'AttachFile',
    AddNote: 'AddNote',
    StartTimer: 'StartTimer',
    NavigateTo: 'NavigateTo'
};

// Action Status
const AgentActionStatus = {
    Pending: 'Pending',
    Approved: 'Approved',
    Rejected: 'Rejected',
    Running: 'Running',
    Done: 'Done',
    Failed: 'Failed',
    RolledBack: 'RolledBack'
};

// Initialize Agent Actions Module
function initializeAgentActions() {
    // Get organization ID from page context
    AgentActionsState.orgId = getCurrentOrganizationId();
    
    // Generate a new correlation ID for this session
    AgentActionsState.correlationId = generateCorrelationId();
    
    // Load saved mode from localStorage
    const savedMode = localStorage.getItem('aiMode') || 'agent';
    AgentActionsState.mode = savedMode;
    
    // Load model tier - first check localStorage, then try to get from org settings
    const savedTier = localStorage.getItem('aiModelTier');
    if (savedTier) {
        AgentActionsState.modelTier = savedTier;
    } else {
        // Default to Auto - the backend will use the org's configured tier
        AgentActionsState.modelTier = 'Auto';
    }
    
    // Initialize the tracker UI
    initializeActionTrackerUI();
    
    // Initialize mode selector UI
    initializeModeSelectorUI();
    
    // Set up event listeners
    setupAgentActionListeners();
    
    // Try to sync with org's AI model tier setting
    syncOrgModelTier();
    
    console.log('[AgentActions] Initialized with correlationId:', AgentActionsState.correlationId);
    console.log('[AgentActions] Mode:', AgentActionsState.mode, 'ModelTier:', AgentActionsState.modelTier);
}

// Sync with organization's AI model tier from settings
async function syncOrgModelTier() {
    const orgId = AgentActionsState.orgId;
    
    // Always try to fetch the current tier from the server first
    if (orgId) {
        try {
            const response = await fetch(`/Client/${orgId}/Settings/GetAIModelTier`);
            if (response.ok) {
                const result = await response.json();
                if (result.success && result.tier) {
                    AgentActionsState.modelTier = result.tier;
                    localStorage.setItem('aiModelTier', result.tier);
                    syncModelToUI();
                    
                    // Also sync with settings page select if it exists
                    const settingsSelect = document.getElementById('aiModelTier');
                    if (settingsSelect && settingsSelect.value !== result.tier) {
                        settingsSelect.value = result.tier;
                        settingsSelect.setAttribute('data-original', result.tier);
                    }
                    
                    console.log('[AgentActions] Model tier loaded from server:', AgentActionsState.modelTier);
                }
            }
        } catch (error) {
            console.warn('[AgentActions] Could not fetch org AI model tier from server:', error);
            // Fall back to localStorage value which was already loaded in initializeAgentActions
        }
    }
    
    // Check if there's a settings select on the page (AI Settings page)
    const settingsSelect = document.getElementById('aiModelTier');
    if (settingsSelect) {
        // Listen for changes on the settings page
        settingsSelect.addEventListener('change', function() {
            const newTier = this.value || 'Auto';
            AgentActionsState.modelTier = newTier;
            localStorage.setItem('aiModelTier', newTier);
            syncModelToUI();
            console.log('[AgentActions] Model tier synced from settings select:', AgentActionsState.modelTier);
        });
    }
}

// Initialize the Mode Selector UI
function initializeModeSelectorUI() {
    // Initialize sidebar mode selector
    initializeSingleModeSelector('aiModeSelector', 'aiModeDropdown', 'currentModeLabel', 'agentModeBadge');
    initializeSingleModelSelector('modelSelector', 'modelDropdown', 'currentModelLabel');
    
    // Initialize dashboard mode selector
    initializeSingleModeSelector('dashboardAiModeSelector', 'dashboardAiModeDropdown', 'dashboardCurrentModeLabel', 'dashboardAgentModeBadge');
    initializeSingleModelSelector('dashboardModelSelector', 'dashboardModelDropdown', 'dashboardCurrentModelLabel');
    
    // Sync initial state to UI
    syncModeToUI();
}

// ==========================
// Dropdown "portal" rendering
// ==========================
// The dashboard + sidebar composer rows use overflow clipping, so absolute-position dropdowns can be hidden.
// We "portal" dropdowns to <body> and position them with position:fixed near the trigger.
const _dropdownPortalState = new WeakMap();

function closeDropdownPortal(dropdownEl) {
    if (!dropdownEl) return;
    const state = _dropdownPortalState.get(dropdownEl);
    if (!state) {
        dropdownEl.classList.remove('show');
        dropdownEl.style.position = '';
        dropdownEl.style.left = '';
        dropdownEl.style.top = '';
        dropdownEl.style.bottom = '';
        dropdownEl.style.right = '';
        dropdownEl.style.zIndex = '';
        dropdownEl.style.minWidth = '';
        dropdownEl.style.display = '';
        return;
    }

    window.removeEventListener('resize', state.onReposition, true);
    window.removeEventListener('scroll', state.onReposition, true);

    dropdownEl.classList.remove('show');
    dropdownEl.style.position = '';
    dropdownEl.style.left = '';
    dropdownEl.style.top = '';
    dropdownEl.style.bottom = '';
    dropdownEl.style.right = '';
    dropdownEl.style.zIndex = '';
    dropdownEl.style.minWidth = '';
    dropdownEl.style.display = '';

    try {
        if (state.nextSibling && state.nextSibling.parentNode === state.originalParent) {
            state.originalParent.insertBefore(dropdownEl, state.nextSibling);
        } else {
            state.originalParent.appendChild(dropdownEl);
        }
    } catch (e) {
        // ignore
    }

    _dropdownPortalState.delete(dropdownEl);
}

function openDropdownPortal(selectorEl, dropdownEl, { align = 'left' } = {}) {
    if (!selectorEl || !dropdownEl) return;

    // If already open, close it
    if (_dropdownPortalState.has(dropdownEl) || dropdownEl.classList.contains('show')) {
        closeDropdownPortal(dropdownEl);
        selectorEl.classList.remove('open');
        return;
    }

    // Close any other open dropdowns
    document.querySelectorAll('.ai-mode-dropdown.show, .model-dropdown.show').forEach(d => {
        closeDropdownPortal(d);
    });
    document.querySelectorAll('.ai-mode-selector.open').forEach(s => s.classList.remove('open'));

    // Record original placement so we can restore it
    const originalParent = dropdownEl.parentNode;
    const nextSibling = dropdownEl.nextSibling;

    // Portal to body
    document.body.appendChild(dropdownEl);

    // Ensure visible for measurement
    dropdownEl.classList.add('show');
    dropdownEl.style.display = 'block';
    dropdownEl.style.position = 'fixed';
    dropdownEl.style.zIndex = '20000';

    const reposition = () => {
        const rect = selectorEl.getBoundingClientRect();

        // Measure dropdown (it is displayed)
        const ddRect = dropdownEl.getBoundingClientRect();
        const ddW = ddRect.width || 180;
        const ddH = ddRect.height || 140;

        const padding = 10;
        const gap = 8;
        const spaceBelow = window.innerHeight - rect.bottom;
        const spaceAbove = rect.top;

        // If there isn't enough room below and there is more room above, open above; else below.
        const openAbove = (spaceBelow < ddH + gap) && (spaceAbove > spaceBelow);

        let top = openAbove ? (rect.top - ddH - gap) : (rect.bottom + gap);
        let left = align === 'right' ? (rect.right - ddW) : rect.left;

        // Clamp to viewport
        top = Math.max(padding, Math.min(top, window.innerHeight - ddH - padding));
        left = Math.max(padding, Math.min(left, window.innerWidth - ddW - padding));

        dropdownEl.style.top = `${top}px`;
        dropdownEl.style.left = `${left}px`;
        dropdownEl.style.right = 'auto';
        dropdownEl.style.bottom = 'auto';
    };

    // Save portal state + listeners
    const onReposition = () => reposition();
    _dropdownPortalState.set(dropdownEl, { originalParent, nextSibling, onReposition });
    window.addEventListener('resize', onReposition, true);
    window.addEventListener('scroll', onReposition, true);

    // Position now (and again next frame in case fonts/layout shift)
    reposition();
    requestAnimationFrame(reposition);
}

// Initialize a single mode selector
function initializeSingleModeSelector(selectorId, dropdownId, labelId, badgeId) {
    const selector = document.getElementById(selectorId);
    const dropdown = document.getElementById(dropdownId);
    const label = document.getElementById(labelId);
    const badge = document.getElementById(badgeId);
    
    if (!selector || !dropdown) return;
    
    // Toggle dropdown on click
    selector.addEventListener('click', function(e) {
        e.stopPropagation();

        selector.classList.toggle('open');
        openDropdownPortal(selector, dropdown, { align: 'left' });
    });
    
    // Handle mode option clicks
    dropdown.querySelectorAll('.ai-mode-option').forEach(option => {
        option.addEventListener('click', function(e) {
            e.stopPropagation();
            const mode = this.dataset.mode;
            
            // Update state
            AgentActionsState.mode = mode;
            localStorage.setItem('aiMode', mode);
            
            // Sync UI across all selectors
            syncModeToUI();
            
            // Close dropdown
            closeDropdownPortal(dropdown);
            selector.classList.remove('open');
            
            console.log('[AgentActions] Mode changed to:', mode);
        });
    });
}

// Initialize a single model tier selector
function initializeSingleModelSelector(selectorId, dropdownId, labelId) {
    const selector = document.getElementById(selectorId);
    const dropdown = document.getElementById(dropdownId);
    const label = document.getElementById(labelId);
    
    if (!selector || !dropdown) return;
    
    // Toggle dropdown on click
    selector.addEventListener('click', function(e) {
        e.stopPropagation();

        document.querySelectorAll('.ai-mode-selector.open').forEach(s => s.classList.remove('open'));
        openDropdownPortal(selector, dropdown, { align: 'right' });
    });
    
    // Handle model tier option clicks
    dropdown.querySelectorAll('.model-option').forEach(option => {
        option.addEventListener('click', async function(e) {
            e.stopPropagation();
            const tier = this.dataset.model;
            
            // Update state
            AgentActionsState.modelTier = tier;
            localStorage.setItem('aiModelTier', tier);
            
            // Sync UI across all selectors
            syncModelToUI();
            
            // Close dropdown
            closeDropdownPortal(dropdown);
            
            // Also update the organization's setting if we have access
            await updateOrgModelTier(tier);
            
            console.log('[AgentActions] Model tier changed to:', tier);
        });
    });
}

// Update organization's AI model tier setting
async function updateOrgModelTier(tier) {
    const orgId = AgentActionsState.orgId;
    if (!orgId) return;
    
    try {
        const response = await fetch(`/Client/${orgId}/Settings/UpdateAIModelTier`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ tier: tier })
        });
        
        if (response.ok) {
            console.log('[AgentActions] Org AI model tier updated successfully');
            
            // Also sync with the settings page select if it exists
            const settingsSelect = document.getElementById('aiModelTier');
            if (settingsSelect) {
                settingsSelect.value = tier;
                // Update data-original so the settings page change handler knows this is the new baseline
                settingsSelect.setAttribute('data-original', tier);
            }
        }
    } catch (error) {
        console.warn('[AgentActions] Could not update org AI model tier:', error);
        // Silently fail - the user can still use the local setting
    }
}

// Sync mode state to all UI elements
function syncModeToUI() {
    const mode = AgentActionsState.mode;
    const isAgent = mode === 'agent';
    
    // Update all mode labels
    document.querySelectorAll('#currentModeLabel, #dashboardCurrentModeLabel').forEach(label => {
        if (label) label.textContent = isAgent ? 'Agent' : 'Ask';
    });
    
    // Update all mode icons
    document.querySelectorAll('.ai-mode-selector .mode-icon').forEach(icon => {
        icon.className = `fas ${isAgent ? 'fa-infinity' : 'fa-comment-dots'} mode-icon`;
    });
    
    // Update all agent badges
    document.querySelectorAll('#agentModeBadge, #dashboardAgentModeBadge').forEach(badge => {
        if (badge) badge.style.display = isAgent ? 'block' : 'none';
    });
    
    // Update all dropdown selected states
    document.querySelectorAll('.ai-mode-dropdown').forEach(dropdown => {
        dropdown.querySelectorAll('.ai-mode-option').forEach(option => {
            option.classList.toggle('selected', option.dataset.mode === mode);
        });
    });
}

// Sync model tier state to all UI elements
function syncModelToUI() {
    const tier = AgentActionsState.modelTier;
    
    // Update all model tier labels
    document.querySelectorAll('#currentModelLabel, #dashboardCurrentModelLabel').forEach(label => {
        if (label) label.textContent = ModelTierLabels[tier] || tier;
    });
    
    // Update all dropdown selected states
    document.querySelectorAll('.model-dropdown').forEach(dropdown => {
        dropdown.querySelectorAll('.model-option').forEach(option => {
            option.classList.toggle('selected', option.dataset.model === tier);
        });
    });
}

// Sync model tier from settings page (called when settings page select changes)
function syncModelFromSettings(tier) {
    if (!tier) return;
    
    AgentActionsState.modelTier = tier;
    localStorage.setItem('aiModelTier', tier);
    syncModelToUI();
    
    console.log('[AgentActions] Model tier synced from settings page:', tier);
}

// Close dropdowns when clicking outside
document.addEventListener('click', function(e) {
    if (!e.target.closest('.ai-mode-selector') && !e.target.closest('.model-selector')) {
        document.querySelectorAll('.ai-mode-dropdown.show, .model-dropdown.show').forEach(d => {
            closeDropdownPortal(d);
        });
        document.querySelectorAll('.ai-mode-selector.open').forEach(s => {
            s.classList.remove('open');
        });
    }
});

// Close the Actions panel when clicking outside (Cursor-style popup behavior)
document.addEventListener('click', function(e) {
    const tracker = document.getElementById('agentActionTracker');
    const actionsLink = document.getElementById('actionsLink');
    if (!tracker || !actionsLink) return;
    if (!AgentActionsState.isVisible) return;
    if (e.target.closest('#agentActionTracker') || e.target.closest('#actionsLink')) return;
    closeActionTrackerPanel();
});

// Close on Escape
document.addEventListener('keydown', function(e) {
    if (e.key !== 'Escape') return;
    if (!AgentActionsState.isVisible) return;
    closeActionTrackerPanel();
});

// Get current AI mode
function getAIMode() {
    return AgentActionsState.mode;
}

// Get current AI model tier
function getAIModel() {
    return AgentActionsState.modelTier;
}

// Alias for clarity
function getAIModelTier() {
    return AgentActionsState.modelTier;
}

// Check if in agent mode
function isAgentMode() {
    return AgentActionsState.mode === 'agent';
}

// Generate a unique correlation ID
function generateCorrelationId() {
    return 'sess-' + Date.now() + '-' + Math.random().toString(36).substring(2, 9);
}

// Generate a unique run ID for idempotency
function generateRunId() {
    return 'run-' + Date.now() + '-' + Math.random().toString(36).substring(2, 9);
}

// Initialize the Action Tracker UI
function initializeActionTrackerUI() {
    // Check if tracker already exists
    if (document.getElementById('agentActionTracker')) return;
    
    const trackerHTML = `
        <div id="agentActionTracker" class="agent-action-tracker" style="display: none;">
            <!-- Tracker Card (similar to timer card) -->
            <div id="actionTrackerCard" class="action-tracker-card">
                <!-- Top Row: Keep/Undo (left) + History/Close (right) -->
                <div class="action-tracker-top-row">
                    <div class="action-tracker-top-left">
                        <button id="actionKeepBtn" class="action-btn action-keep-btn" title="Keep this change">
                            <i class="fas fa-check"></i>
                            Keep
                        </button>
                        <button id="actionUndoBtn" class="action-btn action-undo-btn" title="Undo this change">
                            <i class="fas fa-undo"></i>
                            Undo
                        </button>
                    </div>
                    <div class="action-tracker-top-right">
                        <button id="actionHistoryBtn" class="tracker-icon-btn" title="View action history">
                            <i class="fas fa-history"></i>
                        </button>
                        <button id="actionCloseBtn" class="tracker-icon-btn" title="Close">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
                
                <!-- Action Preview Card -->
                <div class="action-preview-card" id="actionPreviewCard">
                    <div class="action-preview-header">
                        <div id="actionTypeIcon" class="action-type-icon">
                            <i class="fas fa-tasks"></i>
                        </div>
                        <div class="action-preview-info">
                            <h6 id="actionTitle" class="action-title">Processing...</h6>
                            <p id="actionDescription" class="action-description">AI is analyzing your request</p>
                        </div>
                    </div>
                    <div id="actionDetails" class="action-details" style="display: none;">
                        <!-- Dynamic action details will be populated here -->
                    </div>
                </div>
                
                <!-- Navigation Controls -->
                <div class="action-navigation" id="actionNavigation" style="display: none;">
                    <button id="actionPrevBtn" class="nav-btn" title="Previous action">
                        <i class="fas fa-chevron-left"></i>
                    </button>
                    <div class="action-pager" id="actionPager" aria-label="Action position">
                        <span id="actionPagerCurrent">1</span>
                        <span class="action-separator">/</span>
                        <span id="actionPagerTotal">1</span>
                    </div>
                    <button id="actionNextBtn" class="nav-btn" title="Next action">
                        <i class="fas fa-chevron-right"></i>
                    </button>
                </div>
                
                <!-- Quick Actions Bar -->
                <div class="action-quick-bar" id="actionQuickBar" style="display: none;">
                    <button class="quick-action-btn" id="keepAllBtn" title="Keep all changes">
                        <i class="fas fa-check-double"></i>
                        Keep All
                    </button>
                    <button class="quick-action-btn" id="undoAllBtn" title="Undo all changes">
                        <i class="fas fa-undo-alt"></i>
                        Undo All
                    </button>
                </div>
            </div>
            
        </div>
    `;
    
    // Insert after the floating timer
    const floatingTimer = document.getElementById('floatingTimerOverlay');
    if (floatingTimer) {
        floatingTimer.insertAdjacentHTML('afterend', trackerHTML);
    } else {
        // Fallback: append to body
        document.body.insertAdjacentHTML('beforeend', trackerHTML);
    }
    
    // Make tracker draggable
    makeTrackerDraggable();
    
    // Restore position from localStorage
    restoreTrackerPosition();
}

// Setup event listeners
function setupAgentActionListeners() {
    // Keep button
    document.getElementById('actionKeepBtn')?.addEventListener('click', keepCurrentAction);
    
    // Undo button
    document.getElementById('actionUndoBtn')?.addEventListener('click', undoCurrentAction);
    
    // Navigation buttons
    document.getElementById('actionPrevBtn')?.addEventListener('click', showPreviousAction);
    document.getElementById('actionNextBtn')?.addEventListener('click', showNextAction);
    
    // Keep All button
    document.getElementById('keepAllBtn')?.addEventListener('click', keepAllActions);
    
    // Undo All button
    document.getElementById('undoAllBtn')?.addEventListener('click', undoAllActions);
    
    // Global Actions navbar button
    document.getElementById('actionsLink')?.addEventListener('click', function(e) {
        e.preventDefault();
        toggleActionTracker({ anchorToActionsButton: true });
    });
    
    // Close button (X icon)
    document.getElementById('actionCloseBtn')?.addEventListener('click', function(e) {
        e.stopPropagation();
        closeActionTrackerPanel();
    });
    
    // History button
    document.getElementById('actionHistoryBtn')?.addEventListener('click', function(e) {
        e.stopPropagation();
        // Navigate to history/activity page
        const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
        if (orgId) {
            window.location.href = `/Client/${orgId}/History`;
        }
    });
    
    // Button hover effects
    addButtonHoverEffects();
}

// Toggle action tracker visibility
function toggleActionTracker(options = {}) {
    const tracker = document.getElementById('agentActionTracker');
    if (!tracker) {
        initializeActionTrackerUI();
    }

    // If opened from the Actions navbar item, anchor it like other nav popups (unless user dragged/saved a position)
    if (options.anchorToActionsButton) {
        anchorTrackerToActionsButton();
    }

    // Clicking the Actions button should always toggle open/close (even in empty state).
    const isOpen = (tracker.style.display !== 'none') && AgentActionsState.isVisible;
    if (isOpen) {
        closeActionTrackerPanel();
        return;
    }
    
    if (AgentActionsState.actions.length === 0) {
        // No actions - show empty state
        showActionTracker();
        showEmptyState();
    } else if (tracker.style.display === 'none' || !AgentActionsState.isVisible) {
        showActionTracker();
        updateTrackerDisplay();
    } else {
        // Cursor-like: clicking Actions again closes the panel (does not clear pending actions)
        closeActionTrackerPanel();
    }
}

// Close the tracker panel UI without clearing pending actions from session
function closeActionTrackerPanel() {
    const tracker = document.getElementById('agentActionTracker');
    if (tracker) {
        tracker.style.display = 'none';
        AgentActionsState.isVisible = false;
    }
}

// Position the tracker to the right of the global Actions nav button (like other popups),
// but keep the current tracker shape.
function anchorTrackerToActionsButton() {
    const tracker = document.getElementById('agentActionTracker');
        const card = document.getElementById('actionTrackerCard');
    const actionsLink = document.getElementById('actionsLink');
    if (!tracker || !card || !actionsLink) return;

    // Ensure visible for measurement
    const prevDisplay = tracker.style.display;
    tracker.style.display = 'flex';

    const linkRect = actionsLink.getBoundingClientRect();
    const cardRect = card.getBoundingClientRect();

    // Default: right of the button, vertically centered
    let left = linkRect.right + 12;
    let top = linkRect.top + (linkRect.height / 2) - (cardRect.height / 2);

    // Clamp to viewport
    const padding = 12;
    left = Math.max(padding, Math.min(left, window.innerWidth - cardRect.width - padding));
    top = Math.max(padding, Math.min(top, window.innerHeight - cardRect.height - padding));

    tracker.style.left = `${left}px`;
    tracker.style.top = `${top}px`;
    tracker.style.right = 'auto';
    tracker.style.bottom = 'auto';
    tracker.style.transform = 'none';

    tracker.style.display = prevDisplay;
}

// Show empty state when no actions
function showEmptyState() {
    const titleEl = document.getElementById('actionTitle');
    const descEl = document.getElementById('actionDescription');
    const iconEl = document.getElementById('actionTypeIcon');
    const counterDiv = document.querySelector('.action-counter');
    const controlsDiv = document.querySelector('.action-controls');
    const navDiv = document.getElementById('actionNavigation');
    const quickBar = document.getElementById('actionQuickBar');
    const headerDiv = document.querySelector('.action-tracker-header');
    const previewCard = document.getElementById('actionPreviewCard');
    
    if (titleEl) titleEl.textContent = 'No Pending Actions';
    if (descEl) descEl.textContent = 'Agent actions will appear here w...';
    if (iconEl) {
        iconEl.innerHTML = '<i class="fas fa-check-circle"></i>';
        // Match Dashboard Quick Access Calendar green (Bootstrap text-success)
        iconEl.style.background = '#198754';
    }
    if (counterDiv) counterDiv.style.display = 'none';
    if (controlsDiv) controlsDiv.style.display = 'none';
    if (headerDiv) headerDiv.style.display = 'none';
    if (navDiv) navDiv.style.display = 'none';
    if (quickBar) quickBar.style.display = 'none';
    
    // Make the preview card look like a proper card in empty state
    if (previewCard) {
        previewCard.classList.remove('status-pending', 'status-approved', 'status-running', 'status-done', 'status-failed', 'status-rolledback', 'status-rejected');
    }
}

// Update the actions badge in navbar
function updateActionsBadge() {
    const badge = document.getElementById('actionsBadge');
    const link = document.getElementById('actionsLink');
    
    if (!badge) return;
    
    const pendingCount = AgentActionsState.actions.filter(a => 
        a.status === AgentActionStatus.Pending || 
        a.status === 'Pending'
    ).length;
    
    if (pendingCount > 0) {
        badge.textContent = pendingCount.toString();
        badge.style.display = 'flex';
        link?.classList.add('has-pending');
    } else {
        badge.style.display = 'none';
        link?.classList.remove('has-pending');
    }
}

// Add hover effects to buttons
function addButtonHoverEffects() {
    const buttons = document.querySelectorAll('.action-btn, .quick-action-btn');
    buttons.forEach(btn => {
        btn.addEventListener('mouseenter', function() {
            this.style.transform = 'scale(1.05)';
        });
        btn.addEventListener('mouseleave', function() {
            this.style.transform = 'scale(1)';
        });
    });
}

// Make the tracker draggable
function makeTrackerDraggable() {
    const tracker = document.getElementById('agentActionTracker');
    const card = document.getElementById('actionTrackerCard');
    if (!tracker || !card) return;
    
    let isDragging = false;
    let currentX, currentY, initialX, initialY;
    let xOffset = 0, yOffset = 0;
    
    // Restore saved position
    const savedPosition = localStorage.getItem('actionTrackerPosition');
    if (savedPosition) {
        const position = JSON.parse(savedPosition);
        xOffset = position.xOffset || 0;
        yOffset = position.yOffset || 0;
        tracker.style.left = position.left;
        tracker.style.top = position.top;
        tracker.style.right = '';
        tracker.style.bottom = '';
    }
    
    card.addEventListener('mousedown', dragStart);
    card.addEventListener('touchstart', dragStart);
    document.addEventListener('mousemove', drag);
    document.addEventListener('touchmove', drag);
    document.addEventListener('mouseup', dragEnd);
    document.addEventListener('touchend', dragEnd);
    
    function dragStart(e) {
        // Don't drag when clicking buttons
        if (e.target.closest('button')) return;
        
        if (e.type === 'touchstart') {
            initialX = e.touches[0].clientX - xOffset;
            initialY = e.touches[0].clientY - yOffset;
        } else {
            initialX = e.clientX - xOffset;
            initialY = e.clientY - yOffset;
        }
        
        isDragging = true;
        tracker.style.transition = 'none';
    }
    
    function drag(e) {
        if (!isDragging) return;
        e.preventDefault();
        
        if (e.type === 'touchmove') {
            currentX = e.touches[0].clientX - initialX;
            currentY = e.touches[0].clientY - initialY;
        } else {
            currentX = e.clientX - initialX;
            currentY = e.clientY - initialY;
        }
        
        xOffset = currentX;
        yOffset = currentY;
        
        // Constrain to viewport
        const rect = tracker.getBoundingClientRect();
        const maxX = window.innerWidth - rect.width;
        const maxY = window.innerHeight - rect.height;
        
        let newLeft = Math.max(0, Math.min(currentX, maxX));
        let newTop = Math.max(0, Math.min(currentY, maxY));
        
        tracker.style.left = newLeft + 'px';
        tracker.style.top = newTop + 'px';
        tracker.style.right = 'auto';
        tracker.style.bottom = 'auto';
        
        xOffset = newLeft;
        yOffset = newTop;
    }
    
    function dragEnd() {
        if (isDragging) {
            isDragging = false;
            localStorage.setItem('actionTrackerPosition', JSON.stringify({
                left: tracker.style.left,
                top: tracker.style.top,
                xOffset: xOffset,
                yOffset: yOffset
            }));
        }
    }
}

// Restore tracker position from localStorage
function restoreTrackerPosition() {
    const tracker = document.getElementById('agentActionTracker');
    if (!tracker) return;

    // Cursor-like UX: this panel behaves like a navbar popup (anchored to Actions),
    // not a floating widget. Ignore any old saved drag position.
    try { localStorage.removeItem('actionTrackerPosition'); } catch (e) { /* ignore */ }

    // Anchor positioning is applied when the tracker is shown (needs DOM measurement).
    tracker.style.left = '';
    tracker.style.top = '';
    tracker.style.right = '';
    tracker.style.bottom = '';
    tracker.style.transform = 'none';
}

// Show the action tracker
function showActionTracker() {
    const tracker = document.getElementById('agentActionTracker');
    if (tracker) {
        tracker.style.display = 'flex';
        AgentActionsState.isVisible = true;

        // Always keep the tracker anchored to the Actions navbar button (popup behavior).
        // Defer one frame so layout is settled (correct card height for vertical centering).
        requestAnimationFrame(() => {
            try { anchorTrackerToActionsButton(); } catch (e) { /* ignore */ }
        });
    }
}

// Hide the action tracker
function hideActionTracker() {
    const tracker = document.getElementById('agentActionTracker');
    if (tracker) {
        tracker.style.display = 'none';
        AgentActionsState.isVisible = false;
    }
    // Clear session storage when hiding (all actions complete or dismissed)
    clearActionsFromSession();
}

// Toggle tracker visibility
function toggleTrackerVisibility() {
    const card = document.getElementById('actionTrackerCard');
    const toggle = document.getElementById('actionTrackerToggle');
    
    if (card.style.display === 'none') {
        card.style.display = 'flex';
        toggle.title = 'Hide Actions Tracker';
        toggle.innerHTML = '<i class="fas fa-times"></i>';
    } else {
        card.style.display = 'none';
        toggle.title = 'Show Actions Tracker';
        toggle.innerHTML = '<i class="fas fa-robot"></i>';
    }
}

// Update the tracker display with current action
function updateTrackerDisplay() {
    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;
    
    // Always update the navbar badge
    updateActionsBadge();
    
    if (actions.length === 0) {
        showEmptyState();
        return;
    }
    
    showActionTracker();
    
    const currentAction = actions[index];
    
    // Update pager (shown in nav row)
    const pagerCurrent = document.getElementById('actionPagerCurrent');
    const pagerTotal = document.getElementById('actionPagerTotal');
    if (pagerCurrent) pagerCurrent.textContent = String(index + 1);
    if (pagerTotal) pagerTotal.textContent = String(actions.length);
    
    // Update action preview
    updateActionPreview(currentAction);
    
    // Update navigation
    updateNavigationControls();
    
    // Update pager visibility/values
    updateActionPager();

    // Re-anchor after content updates (card height can change).
    requestAnimationFrame(() => {
        try { anchorTrackerToActionsButton(); } catch (e) { /* ignore */ }
    });
}

// Update action preview with current action details
function updateActionPreview(action) {
    const iconEl = document.getElementById('actionTypeIcon');
    const titleEl = document.getElementById('actionTitle');
    const descEl = document.getElementById('actionDescription');
    const detailsEl = document.getElementById('actionDetails');
    
    // Set icon based on action type
    const iconMap = {
        // Match in-chat action card icons (createActionCardHTML)
        [AgentActionTypes.CreateTask]: 'fa-solid fa-bars-progress',
        [AgentActionTypes.AttachFile]: 'fas fa-paperclip',
        [AgentActionTypes.AddNote]: 'fas fa-sticky-note',
        [AgentActionTypes.StartTimer]: 'fas fa-clock',
        [AgentActionTypes.NavigateTo]: 'fas fa-external-link-alt'
    };
    
    // Use the same icon pill styling as in-chat action cards for tasks (remove the blue background).
    // Other action types keep their existing gradient feel for quick visual differentiation.
    const styleMap = {
        [AgentActionTypes.CreateTask]: { background: 'rgba(61, 16, 25, 0.08)', color: '#3d1019' },
        [AgentActionTypes.AttachFile]: { background: 'linear-gradient(135deg, #8b5cf6, #6d28d9)', color: '#ffffff' },
        [AgentActionTypes.AddNote]: { background: 'linear-gradient(135deg, #f59e0b, #d97706)', color: '#ffffff' },
        [AgentActionTypes.StartTimer]: { background: 'linear-gradient(135deg, #10b981, #059669)', color: '#ffffff' },
        [AgentActionTypes.NavigateTo]: { background: 'linear-gradient(135deg, #a32b43, #3d1019)', color: '#ffffff' }
    };
    
    const iconClass = iconMap[action.actionType] || 'fas fa-cog';
    const style = styleMap[action.actionType] || { background: 'linear-gradient(135deg, #6b7280, #374151)', color: '#ffffff' };
    
    iconEl.innerHTML = `<i class="${iconClass}"></i>`;
    iconEl.style.background = style.background;
    iconEl.style.color = style.color;
    
    // Set title and description
    titleEl.textContent = getActionTitle(action);
    descEl.textContent = action.description || getActionDescription(action);
    
    // Update status indicator
    updateStatusIndicator(action.status);
    
    // Show/hide details
    if (action.payload) {
        detailsEl.innerHTML = formatActionDetails(action);
        detailsEl.style.display = 'block';
    } else {
        detailsEl.style.display = 'none';
    }
}

// Get action title based on type and payload
function getActionTitle(action) {
    const payload = action.payload || {};
    
    switch (action.actionType) {
        case AgentActionTypes.CreateTask:
            return payload.title || 'Create Task';
        case AgentActionTypes.AttachFile:
            return 'Attach File';
        case AgentActionTypes.AddNote:
            return 'Add Note';
        case AgentActionTypes.StartTimer:
            return 'Start Timer';
        case AgentActionTypes.NavigateTo:
            return 'Navigate to Page';
        default:
            return action.actionType || 'Unknown Action';
    }
}

// Get action description based on type
function getActionDescription(action) {
    const payload = action.payload || {};
    
    switch (action.actionType) {
        case AgentActionTypes.CreateTask:
            return payload.description || `Priority: ${payload.priority || 'Normal'}`;
        case AgentActionTypes.AttachFile:
            return payload.fileName || 'File attachment';
        case AgentActionTypes.AddNote:
            return payload.content?.substring(0, 50) + '...' || 'Note content';
        case AgentActionTypes.StartTimer:
            return payload.description || 'Time tracking';
        case AgentActionTypes.NavigateTo:
            return payload.url || payload.pagePath || 'Navigation';
        default:
            return 'Processing action...';
    }
}

// Format action details for display
function formatActionDetails(action) {
    const payload = action.payload || {};
    let details = '<div class="action-details-list">';
    
    switch (action.actionType) {
        case AgentActionTypes.CreateTask:
            if (payload.title) details += `<div class="detail-item"><strong>Title:</strong> ${payload.title}</div>`;
            if (payload.description) details += `<div class="detail-item"><strong>Description:</strong> ${payload.description}</div>`;
            if (payload.priority) details += `<div class="detail-item"><strong>Priority:</strong> ${payload.priority}</div>`;
            if (payload.dueDate) details += `<div class="detail-item"><strong>Due:</strong> ${new Date(payload.dueDate).toLocaleDateString()}</div>`;
            break;
            
        case AgentActionTypes.AttachFile:
            if (payload.fileName) details += `<div class="detail-item"><strong>File:</strong> ${payload.fileName}</div>`;
            if (payload.targetEntity) details += `<div class="detail-item"><strong>Target:</strong> ${payload.targetEntity}</div>`;
            break;
            
        case AgentActionTypes.AddNote:
            if (payload.content) details += `<div class="detail-item"><strong>Content:</strong> ${payload.content.substring(0, 100)}${payload.content.length > 100 ? '...' : ''}</div>`;
            break;
            
        case AgentActionTypes.StartTimer:
            if (payload.description) details += `<div class="detail-item"><strong>Description:</strong> ${payload.description}</div>`;
            if (payload.billingCode) details += `<div class="detail-item"><strong>Billing Code:</strong> ${payload.billingCode}</div>`;
            break;
            
        case AgentActionTypes.NavigateTo:
            if (payload.url || payload.pagePath) details += `<div class="detail-item"><strong>Page:</strong> ${payload.url || payload.pagePath}</div>`;
            break;
    }
    
    details += '</div>';
    return details;
}

// Update status indicator
function updateStatusIndicator(status) {
    const card = document.getElementById('actionPreviewCard');
    if (!card) return;
    
    // Remove existing status classes
    card.classList.remove('status-pending', 'status-approved', 'status-running', 'status-done', 'status-failed', 'status-rolledback');
    
    // Add current status class
    card.classList.add(`status-${status.toLowerCase()}`);
}

// Update navigation controls
function updateNavigationControls() {
    const nav = document.getElementById('actionNavigation');
    const quickBar = document.getElementById('actionQuickBar');
    const prevBtn = document.getElementById('actionPrevBtn');
    const nextBtn = document.getElementById('actionNextBtn');
    
    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;
    
    if (actions.length > 1) {
        nav.style.display = 'flex';
        quickBar.style.display = 'flex';
        
        prevBtn.disabled = index === 0;
        nextBtn.disabled = index === actions.length - 1;
    } else {
        nav.style.display = 'none';
        quickBar.style.display = 'none';
    }
}

// Update action pager indicator ("1 / N" replacing dots)
function updateActionPager() {
    const pager = document.getElementById('actionPager');
    const currentEl = document.getElementById('actionPagerCurrent');
    const totalEl = document.getElementById('actionPagerTotal');
    if (!pager || !currentEl || !totalEl) return;

    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;

    currentEl.textContent = String(index + 1);
    totalEl.textContent = String(actions.length || 1);
}

// Show previous action
function showPreviousAction() {
    if (AgentActionsState.currentIndex > 0) {
        AgentActionsState.currentIndex--;
        updateTrackerDisplay();
    }
}

// Show next action
function showNextAction() {
    if (AgentActionsState.currentIndex < AgentActionsState.actions.length - 1) {
        AgentActionsState.currentIndex++;
        updateTrackerDisplay();
    }
}

// Keep current action (approve + execute)
async function keepCurrentAction() {
    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;
    
    if (actions.length === 0 || index < 0) return;
    
    const action = actions[index];
    
    if (action.status === AgentActionStatus.Done) {
        // Already done, move to next
        showNextAction();
        return;
    }
    
    // Update UI to show processing
    const keepBtn = document.getElementById('actionKeepBtn');
    keepBtn.disabled = true;
    keepBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Applying...';
    
    // Update chat card to show running status
    updateActionCardStatus(action.id, 'Running', getRunningTitle(action.actionType));
    
    try {
        // If pending, approve first
        if (action.status === AgentActionStatus.Pending) {
            await approveAction(action.id);
            action.status = AgentActionStatus.Approved;
        }
        
        // Execute the action
        if (action.status === AgentActionStatus.Approved) {
            await executeAction(action.id);
            action.status = AgentActionStatus.Done;
        }
        
        // Update chat card to show done status
        updateActionCardStatus(action.id, 'Done', getDoneTitle(action.actionType));
        
        // Update display and save state
        updateTrackerDisplay();
        saveActionsToSession();
        
        // Move to next if available
        if (index < actions.length - 1) {
            setTimeout(() => showNextAction(), 500);
        } else {
            // All done - show success and optionally hide
            showActionNotification('All changes applied successfully!', 'success');
            if (actions.every(a => a.status === AgentActionStatus.Done)) {
                setTimeout(() => {
                    hideActionTracker();
                    AgentActionsState.actions = [];
                }, 2000);
            }
        }
    } catch (error) {
        console.error('[AgentActions] Error keeping action:', error);
        showActionNotification('Failed to apply change. Please try again.', 'error');
        action.status = AgentActionStatus.Failed;
        
        // Update chat card to show failed status
        updateActionCardStatus(action.id, 'Failed', getFailedTitle(action.actionType));
        
        updateTrackerDisplay();
        saveActionsToSession();
    } finally {
        keepBtn.disabled = false;
        keepBtn.innerHTML = '<i class="fas fa-check"></i> Keep';
    }
}

// Get title for running state
function getRunningTitle(actionType) {
    const titles = {
        CreateTask: 'Creating task...',
        AttachFile: 'Attaching file...',
        AddNote: 'Adding note...',
        StartTimer: 'Starting timer...',
        NavigateTo: 'Navigating...'
    };
    return titles[actionType] || 'Processing...';
}

// Get title for done state
function getDoneTitle(actionType) {
    const titles = {
        CreateTask: 'Task created',
        AttachFile: 'File attached',
        AddNote: 'Note added',
        StartTimer: 'Timer started',
        NavigateTo: 'Navigated'
    };
    return titles[actionType] || 'Done';
}

// Get title for failed state
function getFailedTitle(actionType) {
    const titles = {
        CreateTask: 'Task creation failed',
        AttachFile: 'File attach failed',
        AddNote: 'Note add failed',
        StartTimer: 'Timer start failed',
        NavigateTo: 'Navigation failed'
    };
    return titles[actionType] || 'Failed';
}

// Undo current action (rollback or reject)
async function undoCurrentAction() {
    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;
    
    if (actions.length === 0 || index < 0) return;
    
    const action = actions[index];
    
    // Update UI to show processing
    const undoBtn = document.getElementById('actionUndoBtn');
    undoBtn.disabled = true;
    undoBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Undoing...';
    
    try {
        if (action.status === AgentActionStatus.Done) {
            // Rollback executed action
            await rollbackAction(action.id);
            action.status = AgentActionStatus.RolledBack;
            
            // Update chat card to show rolled back status
            updateActionCardStatus(action.id, 'RolledBack', getUndoneTitle(action.actionType));
        } else if (action.status === AgentActionStatus.Pending || action.status === AgentActionStatus.Approved) {
            // Reject pending/approved action
            await rejectAction(action.id);
            action.status = AgentActionStatus.Rejected;
            
            // Update chat card to show rejected status
            updateActionCardStatus(action.id, 'Rejected', getRejectedTitle(action.actionType));
        }
        
        // Update display and save state
        updateTrackerDisplay();
        saveActionsToSession();
        
        // Move to next if available
        if (index < actions.length - 1) {
            setTimeout(() => showNextAction(), 500);
        }
        
        showActionNotification('Change undone successfully', 'info');
    } catch (error) {
        console.error('[AgentActions] Error undoing action:', error);
        showActionNotification('Failed to undo change. Please try again.', 'error');
    } finally {
        undoBtn.disabled = false;
        undoBtn.innerHTML = '<i class="fas fa-undo"></i> Undo';
    }
}

// Get title for undone/rolled back state
function getUndoneTitle(actionType) {
    const titles = {
        CreateTask: 'Task removed',
        AttachFile: 'File detached',
        AddNote: 'Note removed',
        StartTimer: 'Timer stopped',
        NavigateTo: 'Navigation cancelled'
    };
    return titles[actionType] || 'Undone';
}

// Get title for rejected state
function getRejectedTitle(actionType) {
    const titles = {
        CreateTask: 'Task cancelled',
        AttachFile: 'File attach cancelled',
        AddNote: 'Note cancelled',
        StartTimer: 'Timer cancelled',
        NavigateTo: 'Navigation cancelled'
    };
    return titles[actionType] || 'Cancelled';
}

// Keep all actions
async function keepAllActions() {
    const actions = AgentActionsState.actions;
    const keepAllBtn = document.getElementById('keepAllBtn');
    
    keepAllBtn.disabled = true;
    keepAllBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Applying All...';
    
    try {
        // Get all pending/approved action IDs
        const pendingIds = actions
            .filter(a => a.status === AgentActionStatus.Pending)
            .map(a => a.id);
        
        const approvedIds = actions
            .filter(a => a.status === AgentActionStatus.Approved)
            .map(a => a.id);
        
        // Bulk approve pending
        if (pendingIds.length > 0) {
            await bulkApproveActions(pendingIds);
            pendingIds.forEach(id => {
                const action = actions.find(a => a.id === id);
                if (action) {
                    action.status = AgentActionStatus.Approved;
                    updateActionCardStatus(id, 'Approved');
                }
            });
        }
        
        // Execute all approved (including newly approved)
        const allApprovedIds = actions
            .filter(a => a.status === AgentActionStatus.Approved)
            .map(a => a.id);
        
        for (const id of allApprovedIds) {
            const action = actions.find(a => a.id === id);
            if (action) {
                updateActionCardStatus(id, 'Running', getRunningTitle(action.actionType));
            }
            
            await executeAction(id);
            
            if (action) {
                action.status = AgentActionStatus.Done;
                updateActionCardStatus(id, 'Done', getDoneTitle(action.actionType));
            }
            updateTrackerDisplay();
        }
        
        showActionNotification('All changes applied successfully!', 'success');
        
        // Hide tracker after delay
        setTimeout(() => {
            hideActionTracker();
            AgentActionsState.actions = [];
        }, 2000);
        
    } catch (error) {
        console.error('[AgentActions] Error keeping all actions:', error);
        showActionNotification('Some changes failed to apply.', 'error');
        updateTrackerDisplay();
    } finally {
        keepAllBtn.disabled = false;
        keepAllBtn.innerHTML = '<i class="fas fa-check-double"></i> Keep All';
    }
}

// Undo all actions
async function undoAllActions() {
    const actions = AgentActionsState.actions;
    const undoAllBtn = document.getElementById('undoAllBtn');
    
    undoAllBtn.disabled = true;
    undoAllBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Undoing All...';
    
    try {
        // Rollback done actions
        const doneActions = actions.filter(a => a.status === AgentActionStatus.Done);
        for (const action of doneActions) {
            await rollbackAction(action.id);
            action.status = AgentActionStatus.RolledBack;
            updateActionCardStatus(action.id, 'RolledBack', getUndoneTitle(action.actionType));
            updateTrackerDisplay();
        }
        
        // Reject pending/approved actions
        const pendingIds = actions
            .filter(a => a.status === AgentActionStatus.Pending || a.status === AgentActionStatus.Approved)
            .map(a => a.id);
        
        if (pendingIds.length > 0) {
            await bulkRejectActions(pendingIds);
            pendingIds.forEach(id => {
                const action = actions.find(a => a.id === id);
                if (action) {
                    action.status = AgentActionStatus.Rejected;
                    updateActionCardStatus(id, 'Rejected', getRejectedTitle(action.actionType));
                }
            });
        }
        
        showActionNotification('All changes undone', 'info');
        
        // Hide tracker after delay
        setTimeout(() => {
            hideActionTracker();
            AgentActionsState.actions = [];
        }, 2000);
        
    } catch (error) {
        console.error('[AgentActions] Error undoing all actions:', error);
        showActionNotification('Some changes failed to undo.', 'error');
        updateTrackerDisplay();
    } finally {
        undoAllBtn.disabled = false;
        undoAllBtn.innerHTML = '<i class="fas fa-undo-alt"></i> Undo All';
    }
}

// API Functions

async function proposeAction(actionType, payload, options = {}) {
    const runId = generateRunId();
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    if (!orgId) {
        throw new Error('Organization ID not available');
    }
    
    const response = await fetch('/api/agent-actions/propose', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId.toString(),
            'x-run-id': runId,
            'x-correlation-id': AgentActionsState.correlationId || generateCorrelationId()
        },
        body: JSON.stringify({
            actionType: actionType,
            payload: payload,
            matterId: options.matterId,
            description: options.description,
            aiAgentType: 'NotalAI',
            sourceConversationId: AgentActionsState.conversationId
        })
    });
    
    if (!response.ok) {
        const errorText = await response.text();
        console.error('[AgentActions] Propose failed:', response.status, errorText);
        throw new Error(`Failed to propose action: ${response.status} - ${errorText}`);
    }
    
    return await response.json();
}

async function approveAction(actionId) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch(`/api/agent-actions/${actionId}/approve`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        },
        body: JSON.stringify({ notes: 'Approved via AI chat' })
    });
    
    if (!response.ok) {
        const errorText = await response.text().catch(() => '');
        console.error('[AgentActions] Approve failed:', response.status, errorText);
        throw new Error(`Failed to approve action: ${response.status} ${errorText}`);
    }
    
    return await response.json();
}

async function rejectAction(actionId) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch(`/api/agent-actions/${actionId}/reject`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        },
        body: JSON.stringify({ notes: 'Rejected via AI chat' })
    });
    
    if (!response.ok) {
        throw new Error(`Failed to reject action: ${response.status}`);
    }
    
    return await response.json();
}

async function executeAction(actionId) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch(`/api/agent-actions/${actionId}/execute`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        }
    });
    
    if (!response.ok) {
        const errorText = await response.text().catch(() => '');
        console.error('[AgentActions] Execute failed:', response.status, errorText);
        throw new Error(`Failed to execute action: ${response.status} ${errorText}`);
    }
    
    return await response.json();
}

async function rollbackAction(actionId) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch(`/api/agent-actions/${actionId}/rollback`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        },
        body: JSON.stringify({ notes: 'Rolled back via AI chat' })
    });
    
    if (!response.ok) {
        throw new Error(`Failed to rollback action: ${response.status}`);
    }
    
    return await response.json();
}

async function bulkApproveActions(actionIds) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch('/api/agent-actions/bulk-approve', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        },
        body: JSON.stringify({ actionIds: actionIds, notes: 'Bulk approved via AI chat' })
    });
    
    if (!response.ok) {
        throw new Error(`Failed to bulk approve actions: ${response.status}`);
    }
    
    return await response.json();
}

async function bulkRejectActions(actionIds) {
    const orgId = AgentActionsState.orgId || getCurrentOrganizationId();
    
    const response = await fetch('/api/agent-actions/bulk-reject', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Organization-Id': orgId ? orgId.toString() : ''
        },
        body: JSON.stringify({ actionIds: actionIds, notes: 'Bulk rejected via AI chat' })
    });
    
    if (!response.ok) {
        throw new Error(`Failed to bulk reject actions: ${response.status}`);
    }
    
    return await response.json();
}

// Show action notification
function showActionNotification(message, type = 'info') {
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `action-notification action-notification-${type}`;
    notification.innerHTML = `
        <div class="notification-content">
            <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-circle' : 'fa-info-circle'}"></i>
            <span>${message}</span>
        </div>
    `;
    
    // Add to body
    document.body.appendChild(notification);
    
    // Animate in
    setTimeout(() => notification.classList.add('show'), 10);
    
    // Remove after delay
    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

// Parse AI response for action commands
function parseAIResponseForActions(content) {
    // Only parse actions if we're in agent mode
    if (!isAgentMode()) {
        console.log('[AgentActions] Skipping action parsing - not in agent mode');
        return [];
    }
    
    const actions = [];
    
    // Look for action blocks in the AI response
    // Format: [ACTION:CreateTask]{...payload...}[/ACTION]
    const actionRegex = /\[ACTION:(\w+)\]([\s\S]*?)\[\/ACTION\]/g;
    let match;
    
    while ((match = actionRegex.exec(content)) !== null) {
        const actionType = match[1];
        try {
            const payload = JSON.parse(match[2]);
            actions.push({
                actionType: actionType,
                payload: payload,
                rawMatch: match[0] // Store the raw match for removal later
            });
            console.log('[AgentActions] Parsed action:', actionType, payload);
        } catch (e) {
            console.warn('[AgentActions] Failed to parse action payload:', e, match[2]);
        }
    }
    
    return actions;
}

// Replace action commands in content with action cards (streaming-safe)
// - Completed blocks become full cards
// - In-progress / partial trailing block becomes a placeholder card (no raw [ACTION] text ever shown)
function replaceActionCommandsWithCardsStreaming(content) {
    if (!content) return content;
    
    // Replace fully-formed action blocks first
    let result = replaceActionCommandsWithCards(content);
    
    // If an action block is currently being streamed and hasn't closed yet, replace the tail with a placeholder card
    const start = result.lastIndexOf('[ACTION:');
    if (start !== -1) {
        const tail = result.substring(start);
        const typeMatch = tail.match(/\[ACTION:(\w+)\]/);
        if (typeMatch && typeMatch[1]) {
            const actionType = typeMatch[1];
            result = result.substring(0, start) + createActionCardHTML(actionType, {});
        } else {
            // Unknown/partial tag, just hide it
            result = result.substring(0, start);
        }
    }
    
    return result;
}

// Strip action commands from content for display
function stripActionCommandsFromContent(content) {
    // Remove action blocks for cleaner display
    return content.replace(/\[ACTION:\w+\][\s\S]*?\[\/ACTION\]/g, '').trim();
}

// Parse natural language action indicators from AI response
function parseNaturalLanguageActions(content) {
    const actions = [];
    const lowerContent = content.toLowerCase();
    
    // Detect task creation intent - expanded patterns
    const taskCreationPatterns = [
        /i['']ll create a task/i,
        /creating a task/i,
        /let me add a task/i,
        /i['']m creating/i,
        /i will create/i,
        /creating the following task/i,
        /new task:/i,
        /task created:/i,
        /here['']s the task/i,
        /adding a new task/i
    ];
    
    const hasTaskIntent = taskCreationPatterns.some(pattern => pattern.test(content));
    
    if (hasTaskIntent) {
        // Try to extract task details with multiple patterns
        const titlePatterns = [
            /(?:titled?|called|named|task:)\s*["']?([^"'\n,]+)["']?/i,
            /title:\s*["']?([^"'\n,]+)["']?/i,
            /["']([^"']+)["']/  // Quoted strings
        ];
        
        let title = null;
        for (const pattern of titlePatterns) {
            const match = content.match(pattern);
            if (match && match[1] && match[1].length > 3) {
                title = match[1].trim();
                break;
            }
        }
        
        // Extract priority
        const priorityMatch = content.match(/priority[:\s]+(\w+)/i) || 
                              content.match(/(high|medium|low|urgent)\s+priority/i);
        const priority = priorityMatch ? priorityMatch[1] : 'Normal';
        
        // Extract due date
        const dueDateMatch = content.match(/due[:\s]+([^\n,]+)/i) ||
                             content.match(/by\s+([\w\s,]+\d{4})/i);
        const dueDate = dueDateMatch ? dueDateMatch[1].trim() : null;
        
        // Extract description
        const descMatch = content.match(/description[:\s]+["']?([^"'\n]+)["']?/i);
        const description = descMatch ? descMatch[1].trim() : 'Created by Notal AI';
        
        if (title) {
            actions.push({
                actionType: AgentActionTypes.CreateTask,
                payload: {
                    title: title,
                    priority: priority.charAt(0).toUpperCase() + priority.slice(1).toLowerCase(),
                    description: description,
                    dueDate: dueDate
                },
                detected: true,
                confidence: 'natural_language'
            });
            console.log('[AgentActions] NL detected CreateTask:', title);
        }
    }
    
    // Detect navigation intent - expanded patterns
    const navPatterns = [
        /(?:navigate|go|head|let['']s go)\s+to\s+(?:the\s+)?(\w+(?:\s+\w+)?)/i,
        /open(?:ing)?\s+(?:the\s+)?(\w+)\s*(?:page|screen|section)?/i,
        /taking you to\s+(?:the\s+)?(\w+)/i,
        /redirecting to\s+(?:the\s+)?(\w+)/i
    ];
    
    for (const pattern of navPatterns) {
        const match = content.match(pattern);
        if (match && match[1]) {
            actions.push({
                actionType: AgentActionTypes.NavigateTo,
                payload: {
                    pageName: match[1].trim()
                },
                detected: true,
                confidence: 'natural_language'
            });
            console.log('[AgentActions] NL detected NavigateTo:', match[1]);
            break;
        }
    }
    
    // Detect note addition intent
    const notePatterns = [
        /add(?:ing)?\s+a?\s*note/i,
        /make?\s+a?\s*note/i,
        /creating?\s+a?\s*note/i,
        /note\s+added/i
    ];
    
    if (notePatterns.some(pattern => pattern.test(content))) {
        // Extract note content
        const noteMatch = content.match(/note[:\s]+["']?([^"'\n]+)["']?/i);
        actions.push({
            actionType: AgentActionTypes.AddNote,
            payload: {
                content: noteMatch ? noteMatch[1].trim() : 'Note from AI conversation'
            },
            detected: true,
            confidence: 'natural_language'
        });
        console.log('[AgentActions] NL detected AddNote');
    }
    
    // Detect timer start intent
    const timerPatterns = [
        /start(?:ing)?\s+(?:a\s+)?timer/i,
        /timer\s+started/i,
        /begin(?:ning)?\s+time\s+tracking/i
    ];
    
    if (timerPatterns.some(pattern => pattern.test(content))) {
        actions.push({
            actionType: AgentActionTypes.StartTimer,
            payload: {
                description: 'AI-initiated time tracking'
            },
            detected: true,
            confidence: 'natural_language'
        });
        console.log('[AgentActions] NL detected StartTimer');
    }
    
    return actions;
}

// Process detected actions from AI response
async function processDetectedActions(actions, conversationId) {
    if (actions.length === 0) return;

    // Defensive dedupe/idempotency:
    // Streaming / prompt quirks can occasionally cause the same action block to appear twice,
    // or the client to invoke processing twice. Never propose duplicate actions.
    function stableNormalizeForFingerprint(value) {
        if (value === null || value === undefined) return value;
        if (Array.isArray(value)) return value.map(stableNormalizeForFingerprint);
        if (typeof value === 'object') {
            const out = {};
            for (const key of Object.keys(value).sort()) {
                // Lowercase keys so Title/title/etc. collapse.
                out[String(key).toLowerCase()] = stableNormalizeForFingerprint(value[key]);
            }
            return out;
        }
        return value;
    }

    function fingerprintAction(action) {
        const t = action?.actionType || action?.ActionType || '';
        const p = stableNormalizeForFingerprint(action?.payload || action?.Payload || {});
        return `${String(t)}:${JSON.stringify(p)}`;
    }

    const seen = new Set();
    const uniqueActions = [];
    for (const a of actions) {
        const fp = fingerprintAction(a);
        if (seen.has(fp)) {
            console.warn('[AgentActions] Skipping duplicate detected action:', a?.actionType, a?.payload);
            continue;
        }
        seen.add(fp);
        uniqueActions.push(a);
    }

    // If we were invoked twice with the exact same action set, ignore the second call.
    // (e.g., if multiple completion paths fire or multiple listeners exist)
    const actionSetHash = Array.from(seen).sort().join('|');
    const nowTs = Date.now();
    const last = AgentActionsState._lastProcessedActionSet;
    if (last &&
        last.conversationId === conversationId &&
        last.hash === actionSetHash &&
        (nowTs - last.ts) < 15000) {
        console.warn('[AgentActions] Duplicate processActions call suppressed for conversation', conversationId);
        return;
    }
    AgentActionsState._lastProcessedActionSet = { conversationId, hash: actionSetHash, ts: nowTs };
    
    // Ensure org ID is set
    if (!AgentActionsState.orgId) {
        AgentActionsState.orgId = getCurrentOrganizationId();
    }

    // Best-effort matter context (Cursor-style: actions should be contextual to the page when possible)
    const contextMatterId = getCurrentMatterId();
    
    AgentActionsState.conversationId = conversationId;
    AgentActionsState.currentIndex = 0;
    AgentActionsState.actions = [];
    
    // Show tracker immediately with processing state
    showActionTracker();
    
    for (const actionData of uniqueActions) {
        try {
            console.log('[AgentActions] Proposing action:', actionData.actionType, actionData.payload);
            
            // Propose the action
            const payloadMatterId = actionData?.payload?.matterId || actionData?.payload?.MatterId;

            // Normalize payload for server-side DTO parsing:
            // - Ensure CreateTask has assigneeIds (default to current user)
            // - Ensure dueDate is ISO (YYYY-MM-DD or full ISO) so System.Text.Json can parse DateTime?
            // - Normalize priority to proper case (Low, Medium, High, Critical)
            let normalizedPayload = actionData.payload;
            if (actionData.actionType === AgentActionTypes.CreateTask && actionData.payload && typeof actionData.payload === 'object') {
                normalizedPayload = { ...actionData.payload };

                // Default assigneeIds to current user if omitted
                const hasAssignees = ('assigneeIds' in normalizedPayload) || ('AssigneeIds' in normalizedPayload);
                if (!hasAssignees) {
                    const userId = (function getCurrentUserIdFromContext() {
                        const ctx = document.getElementById('appContext');
                        const raw = ctx?.getAttribute('data-current-user-id');
                        const n = raw ? parseInt(raw, 10) : NaN;
                        return Number.isFinite(n) && n > 0 ? n : null;
                    })();
                    if (userId) {
                        normalizedPayload.assigneeIds = [userId];
                    }
                }

                // Normalize priority to proper case (Low, Medium, High, Critical)
                const priority = normalizedPayload.priority ?? normalizedPayload.Priority;
                if (typeof priority === 'string' && priority.trim().length > 0) {
                    const normalizedPriority = priority.trim().toLowerCase();
                    const priorityMap = {
                        'low': 'Low',
                        'medium': 'Medium',
                        'high': 'High',
                        'critical': 'Critical',
                        'normal': 'Medium',  // Map "normal" to "Medium"
                        'urgent': 'High'     // Map "urgent" to "High"
                    };
                    normalizedPayload.priority = priorityMap[normalizedPriority] || 'Medium';
                    delete normalizedPayload.Priority;
                } else {
                    // Default to Medium if not specified
                    normalizedPayload.priority = 'Medium';
                }

                // Normalize status to proper case (Pending, In Progress, Review, Completed, On Hold, Cancelled)
                const status = normalizedPayload.status ?? normalizedPayload.Status;
                if (typeof status === 'string' && status.trim().length > 0) {
                    const normalizedStatus = status.trim().toLowerCase();
                    const statusMap = {
                        'pending': 'Pending',
                        'in progress': 'In Progress',
                        'in-progress': 'In Progress',
                        'inprogress': 'In Progress',
                        'review': 'Review',
                        'in review': 'Review',
                        'in-review': 'Review',
                        'completed': 'Completed',
                        'complete': 'Completed',
                        'done': 'Completed',
                        'on hold': 'On Hold',
                        'on-hold': 'On Hold',
                        'onhold': 'On Hold',
                        'hold': 'On Hold',
                        'cancelled': 'Cancelled',
                        'canceled': 'Cancelled',
                        'cancel': 'Cancelled'
                    };
                    normalizedPayload.status = statusMap[normalizedStatus] || 'Pending';
                    delete normalizedPayload.Status;
                } else {
                    // Default to Pending if not specified
                    normalizedPayload.status = 'Pending';
                }

                // Normalize dueDate if provided as a human string like "December 25, 2025"
                const due = normalizedPayload.dueDate ?? normalizedPayload.DueDate;
                if (typeof due === 'string' && due.trim().length > 0) {
                    const raw = due.trim();

                    // If already YYYY-MM-DD, keep it.
                    if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
                        normalizedPayload.dueDate = raw;
                        delete normalizedPayload.DueDate;
                    } else {
                        // Prefer a month-name parse without timezone drift
                        const monthMap = {
                            january: 0, february: 1, march: 2, april: 3, may: 4, june: 5,
                            july: 6, august: 7, september: 8, october: 9, november: 10, december: 11
                        };
                        const m = raw.match(/^([A-Za-z]+)\s+(\d{1,2}),\s*(\d{4})$/);
                        if (m) {
                            const monthIdx = monthMap[m[1].toLowerCase()];
                            const day = parseInt(m[2], 10);
                            const year = parseInt(m[3], 10);
                            if (Number.isFinite(monthIdx) && day > 0 && year > 1900) {
                                const dLocal = new Date(year, monthIdx, day);
                                const yyyy = dLocal.getFullYear();
                                const mm = String(dLocal.getMonth() + 1).padStart(2, '0');
                                const dd = String(dLocal.getDate()).padStart(2, '0');
                                normalizedPayload.dueDate = `${yyyy}-${mm}-${dd}`;
                                delete normalizedPayload.DueDate;
                            }
                        } else {
                            // Fallback: Date.parse sometimes interprets date-only strings as UTC.
                            // Use UTC components to avoid off-by-one in negative timezones.
                            const parsed = Date.parse(raw);
                            if (!Number.isNaN(parsed)) {
                                const d = new Date(parsed);
                                const yyyy = d.getUTCFullYear();
                                const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
                                const dd = String(d.getUTCDate()).padStart(2, '0');
                                normalizedPayload.dueDate = `${yyyy}-${mm}-${dd}`;
                                delete normalizedPayload.DueDate;
                            } else {
                                // If we can't parse it reliably, omit it (DueDate is optional server-side)
                                delete normalizedPayload.dueDate;
                                delete normalizedPayload.DueDate;
                            }
                        }
                    }
                }
            }
            const result = await proposeAction(
                actionData.actionType,
                normalizedPayload,
                {
                    matterId: payloadMatterId || contextMatterId,
                    description: actionData.description || getActionDescription({ actionType: actionData.actionType, payload: normalizedPayload })
                }
            );
            
            console.log('[AgentActions] Propose result:', result);
            
            // Add to our tracking array
            const action = result.action || result;
            const actionId = action.id || action.Id;
            
            AgentActionsState.actions.push({
                id: actionId,
                actionType: actionData.actionType,
                payload: normalizedPayload,
                description: action.description || actionData.description,
                status: action.status || AgentActionStatus.Pending,
                runId: action.runId
            });
            
            // Update the chat card with the actual action ID so status updates work
            const tempCardId = generateCardIdFromPayload(actionData.actionType, normalizedPayload);
            linkCardToActionId(tempCardId, actionId, actionData.actionType, normalizedPayload);
            
        } catch (error) {
            console.error('[AgentActions] Failed to propose action:', error);
            // Still add it to show the failure
            AgentActionsState.actions.push({
                id: null,
                actionType: actionData.actionType,
                payload: actionData.payload,
                description: 'Failed to propose',
                status: AgentActionStatus.Failed
            });
        }
    }
    
    // Save to sessionStorage for persistence across navigations
    saveActionsToSession();
    
    // Update display
    updateTrackerDisplay();
}

// Try to infer current matter context from URL/querystring
function getCurrentMatterId() {
    try {
        const orgId = AgentActionsState.orgId || getCurrentOrganizationId?.() || null;
        const scopedKey = orgId ? `certio_selected_matter_id:${orgId}` : null;

        // Common patterns:
        // - /Client/{orgId}/Matter/Details/{matterId}
        // - /Client/{orgId}/Matter/{matterId}
        const path = window.location.pathname || '';
        const match = path.match(/\/Matter\/(?:Details\/)?(\d+)(?:\/|$)/i);
        if (match && match[1]) {
            const id = parseInt(match[1], 10);
            if (!Number.isNaN(id) && id > 0) return id;
        }

        // Querystring fallback (e.g. ?matterId=123)
        const params = new URLSearchParams(window.location.search || '');
        const qs = params.get('matterId');
        if (qs) {
            const id = parseInt(qs, 10);
            if (!Number.isNaN(id) && id > 0) return id;
        }

        // Tasks page / global selection fallback:
        // The Tasks UI persists a selected matter in localStorage even when URL is not matter-scoped.
        // IMPORTANT: scope by org to avoid cross-org stale IDs causing "Failed to propose".
        let selected = null;
        if (scopedKey) selected = localStorage.getItem(scopedKey);
        if (!selected) selected = localStorage.getItem('certio_selected_matter_id'); // legacy key
        // Migrate legacy -> scoped key when possible
        if (scopedKey && selected) {
            try { localStorage.setItem(scopedKey, selected); } catch { /* ignore */ }
        }
        if (selected && selected !== 'all') {
            const id = parseInt(selected, 10);
            if (!Number.isNaN(id) && id > 0) return id;
        }

        // Final fallback: if the page has `window.tasksData.matters`, pick the first available matter
        // (Cursor-style "best effort" context when the AI didn't specify a matter).
        const firstMatter = window.tasksData?.matters?.[0];
        const firstId = firstMatter?.id ?? firstMatter?.Id;
        if (firstId) {
            const id = parseInt(firstId, 10);
            if (!Number.isNaN(id) && id > 0) return id;
        }
    } catch (e) {
        // Ignore
    }
    return null;
}

// Save current actions to sessionStorage
function saveActionsToSession() {
    try {
        const stateToSave = {
            actions: AgentActionsState.actions,
            currentIndex: AgentActionsState.currentIndex,
            conversationId: AgentActionsState.conversationId,
            correlationId: AgentActionsState.correlationId,
            orgId: AgentActionsState.orgId,
            isVisible: AgentActionsState.isVisible
        };
        sessionStorage.setItem('agentActions', JSON.stringify(stateToSave));
        console.log('[AgentActions] Saved to session:', stateToSave.actions.length, 'actions');
    } catch (e) {
        console.warn('[AgentActions] Could not save to sessionStorage:', e);
    }
}

// Restore actions from sessionStorage
function restoreActionsFromSession() {
    try {
        const saved = sessionStorage.getItem('agentActions');
        if (saved) {
            const state = JSON.parse(saved);
            
            // Restore if there are ANY non-completed actions (pending, approved, running)
            const hasActiveActions = state.actions.some(a => 
                a.status === AgentActionStatus.Pending || 
                a.status === 'Pending' ||
                a.status === AgentActionStatus.Approved ||
                a.status === 'Approved' ||
                a.status === AgentActionStatus.Running ||
                a.status === 'Running'
            );
            
            if (hasActiveActions && state.actions.length > 0) {
                AgentActionsState.actions = state.actions;
                AgentActionsState.currentIndex = Math.min(state.currentIndex || 0, state.actions.length - 1);
                AgentActionsState.conversationId = state.conversationId;
                AgentActionsState.correlationId = state.correlationId;
                AgentActionsState.isVisible = state.isVisible !== false; // Default to true
                
                // Keep current orgId if already set
                if (!AgentActionsState.orgId && state.orgId) {
                    AgentActionsState.orgId = state.orgId;
                }
                
                console.log('[AgentActions] Restored from session:', state.actions.length, 'actions');
                showActionTracker();
                updateTrackerDisplay();
                return true;
            }
        }
    } catch (e) {
        console.warn('[AgentActions] Could not restore from sessionStorage:', e);
    }
    return false;
}

// Clear saved actions from session
function clearActionsFromSession() {
    try {
        sessionStorage.removeItem('agentActions');
    } catch (e) {
        // Ignore
    }
}

// Navigate to a page (for NavigateTo actions)
function navigateToPage(pageName) {
    const orgId = AgentActionsState.orgId;
    const pageMap = {
        'dashboard': `/Client/${orgId}/Dashboard`,
        'tasks': `/Client/${orgId}/Tasks`,
        'matters': `/Client/${orgId}/Matter`,
        'chapters': `/Client/${orgId}/Matter`,
        'documents': `/Client/${orgId}/Documents`,
        'communications': `/Client/${orgId}/Communications`,
        'calendar': `/Client/${orgId}/Calendar`,
        'billing': `/Client/${orgId}/Billing`,
        'settings': `/Client/${orgId}/Settings`,
        'teams': `/Client/${orgId}/Teams`
    };
    
    const url = pageMap[pageName.toLowerCase()];
    if (url) {
        window.location.href = url;
    } else {
        showActionNotification(`Unknown page: ${pageName}`, 'error');
    }
}

// Create an animated action card HTML to display in chat instead of raw action commands
function createActionCardHTML(actionType, payload, actionId = null) {
    const iconMap = {
        // Match the client navbar Tasks icon exactly
        CreateTask: 'fa-solid fa-bars-progress',
        AttachFile: 'fas fa-paperclip',
        AddNote: 'fas fa-sticky-note',
        StartTimer: 'fas fa-clock',
        NavigateTo: 'fas fa-external-link-alt'
    };

    const labelMap = {
        CreateTask: 'Creating task...',
        AttachFile: 'Attaching file...',
        AddNote: 'Adding note...',
        StartTimer: 'Starting timer...',
        NavigateTo: 'Navigating...'
    };

    const iconClass = iconMap[actionType] || 'fas fa-cog';
    const label = labelMap[actionType] || 'Processing';

    // Detail line (title/target) if available
    let detail = '';
    if (actionType === 'CreateTask' && (payload.title || payload.Title)) {
        detail = payload.title || payload.Title;
    } else if (actionType === 'NavigateTo' && (payload.pageName || payload.url)) {
        detail = payload.pageName || payload.url;
    } else if (actionType === 'AddNote' && payload.content) {
        detail = payload.content.substring(0, 60) + (payload.content.length > 60 ? '...' : '');
    } else if (actionType === 'StartTimer' && payload.description) {
        detail = payload.description;
    }

    // Generate a unique card ID for later updates
    const cardId = actionId || `action-card-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;

    return `
        <div class="agent-action-attachment action-pending" data-action-id="${cardId}" data-action-type="${actionType}">
            <div class="agent-action-icon">
                <i class="${iconClass}"></i>
            </div>
            <div class="agent-action-content">
                <div class="agent-action-title">${label}</div>
                ${detail ? `<div class="agent-action-detail">${detail}</div>` : ''}
            </div>
            <div class="agent-action-badge status-pending" data-status="pending">Pending</div>
        </div>
    `;
}

// Save action card status to sessionStorage for persistence
function saveActionCardStatusToSession(actionId, status, title) {
    try {
        const savedStatuses = JSON.parse(sessionStorage.getItem('actionCardStatuses') || '{}');
        savedStatuses[String(actionId)] = { status, title, timestamp: Date.now() };
        sessionStorage.setItem('actionCardStatuses', JSON.stringify(savedStatuses));
        console.log(`[AgentActions] Saved card status to session: ${actionId} = ${status}`);
    } catch (e) {
        console.warn('[AgentActions] Failed to save card status:', e);
    }
}

// Restore action card statuses from sessionStorage
function restoreActionCardStatuses() {
    try {
        const savedStatuses = JSON.parse(sessionStorage.getItem('actionCardStatuses') || '{}');
        const now = Date.now();
        let updated = false;
        
        for (const [actionId, data] of Object.entries(savedStatuses)) {
            // Only restore statuses less than 1 hour old
            if (now - data.timestamp < 3600000) {
                // Find cards with this action ID or temporary IDs
                const cards = document.querySelectorAll(`.agent-action-attachment`);
                cards.forEach(card => {
                    const cardId = card.getAttribute('data-action-id');
                    // Check if this card matches the action ID
                    if (cardId === actionId || cardId === String(actionId)) {
                        applyStatusToCard(card, data.status, data.title);
                    }
                });
            } else {
                // Remove old entries
                delete savedStatuses[actionId];
                updated = true;
            }
        }
        
        if (updated) {
            sessionStorage.setItem('actionCardStatuses', JSON.stringify(savedStatuses));
        }
    } catch (e) {
        console.warn('[AgentActions] Failed to restore card statuses:', e);
    }
}

// Apply status styling to a single card
function applyStatusToCard(card, status, title = null) {
    const badge = card.querySelector('.agent-action-badge');
    const titleEl = card.querySelector('.agent-action-title');
    
    if (badge) {
        badge.setAttribute('data-status', status.toLowerCase());
        
        const statusLabels = {
            'pending': 'Pending',
            'approved': 'Approved',
            'running': 'Running',
            'done': 'Done',
            'failed': 'Failed',
            'rejected': 'Rejected',
            'rolledback': 'Undone'
        };
        
        badge.textContent = statusLabels[status.toLowerCase()] || status;
        badge.classList.remove('status-pending', 'status-approved', 'status-running', 'status-done', 'status-failed', 'status-rejected', 'status-rolledback');
        badge.classList.add(`status-${status.toLowerCase()}`);
    }
    
    if (titleEl && title) {
        titleEl.textContent = title;
    }
    
    card.classList.remove('action-pending', 'action-done', 'action-failed', 'action-rejected', 'action-rolledback');
    card.classList.add(`action-${status.toLowerCase()}`);
}

// Update an action card's status in the chat UI
function updateActionCardStatus(actionId, newStatus, newTitle = null) {
    console.log(`[AgentActions] updateActionCardStatus called: id=${actionId}, status=${newStatus}, title=${newTitle}`);
    
    // Save to sessionStorage for persistence
    saveActionCardStatusToSession(actionId, newStatus, newTitle);
    
    // Normalize actionId to string for comparison
    const actionIdStr = String(actionId);
    
    // Try multiple strategies to find the card
    let cards = [];
    
    // Strategy 1: Find by exact action ID (as string)
    cards = Array.from(document.querySelectorAll(`.agent-action-attachment[data-action-id="${actionIdStr}"]`));
    console.log(`[AgentActions] Strategy 1 (exact ID ${actionIdStr}): found ${cards.length} cards`);
    
    // Strategy 2: If not found, find by temporary ID pattern that includes action type
    if (cards.length === 0) {
        // Get the action from state to know the type
        const action = AgentActionsState.actions.find(a => 
            String(a.id) === actionIdStr || a.id === actionId || a.id === parseInt(actionId)
        );
        if (action) {
            const actionType = action.actionType.toLowerCase();
            // Find cards with temporary IDs of this action type
            const tempCards = document.querySelectorAll(`.agent-action-attachment[data-action-id^="action-${actionType}"]`);
            cards = Array.from(tempCards);
            console.log(`[AgentActions] Strategy 2 (by type ${actionType}): found ${cards.length} cards`);
        }
    }
    
    // Strategy 3: Find any pending card with matching action type
    if (cards.length === 0) {
        const action = AgentActionsState.actions.find(a => a.id === actionId || a.id === parseInt(actionId));
        if (action) {
            const actionType = action.actionType;
            const pendingCards = document.querySelectorAll(`.agent-action-attachment[data-action-type="${actionType}"]`);
            // Filter to only pending ones
            cards = Array.from(pendingCards).filter(card => {
                const badge = card.querySelector('.agent-action-badge');
                const status = badge?.getAttribute('data-status') || badge?.textContent?.toLowerCase();
                return status === 'pending';
            });
            console.log(`[AgentActions] Strategy 3 (pending by type): found ${cards.length} cards`);
        }
    }
    
    // Strategy 4: Last resort - find any pending action card
    if (cards.length === 0) {
        const allCards = document.querySelectorAll('.agent-action-attachment');
        cards = Array.from(allCards).filter(card => {
            const badge = card.querySelector('.agent-action-badge');
            const status = badge?.getAttribute('data-status') || badge?.textContent?.toLowerCase();
            return status === 'pending';
        });
        console.log(`[AgentActions] Strategy 4 (any pending): found ${cards.length} cards`);
    }
    
    // Strategy 5: Search specifically within chat containers
    if (cards.length === 0) {
        const chatContainers = ['#chatMessages', '#dashboardChatMessages', '.chat-messages', '.message-list'];
        for (const selector of chatContainers) {
            const container = document.querySelector(selector);
            if (container) {
                const containerCards = container.querySelectorAll('.agent-action-attachment');
                cards = Array.from(containerCards).filter(card => {
                    const badge = card.querySelector('.agent-action-badge');
                    const status = badge?.getAttribute('data-status') || badge?.textContent?.toLowerCase();
                    return status === 'pending';
                });
                if (cards.length > 0) {
                    console.log(`[AgentActions] Strategy 5 (chat container ${selector}): found ${cards.length} cards`);
                    break;
                }
            }
        }
    }
    
    if (cards.length === 0) {
        console.warn(`[AgentActions] No cards found to update for action ${actionId}`);
        // Log all cards in document for debugging
        const debugCards = document.querySelectorAll('.agent-action-attachment');
        console.log(`[AgentActions] Total cards in document: ${debugCards.length}`);
        debugCards.forEach((card, i) => {
            console.log(`[AgentActions] Card ${i}: id=${card.getAttribute('data-action-id')}, type=${card.getAttribute('data-action-type')}, status=${card.querySelector('.agent-action-badge')?.getAttribute('data-status')}`);
        });
        return;
    }
    
    // Update the first matching card (or all if they share the same ID)
    const cardsToUpdate = cards.length === 1 ? cards : [cards[0]];
    
    cardsToUpdate.forEach(card => {
        const badge = card.querySelector('.agent-action-badge');
        const titleEl = card.querySelector('.agent-action-title');
        
        // Link the card to the actual action ID for future updates
        card.setAttribute('data-action-id', actionId);
        
        if (badge) {
            badge.setAttribute('data-status', newStatus.toLowerCase());
            
            // Update badge text and class based on status
            const statusLabels = {
                'pending': 'Pending',
                'approved': 'Approved',
                'running': 'Running',
                'done': 'Done',
                'failed': 'Failed',
                'rejected': 'Rejected',
                'rolledback': 'Undone'
            };
            
            badge.textContent = statusLabels[newStatus.toLowerCase()] || newStatus;
            
            // Remove old status classes
            badge.classList.remove('status-pending', 'status-approved', 'status-running', 'status-done', 'status-failed', 'status-rejected', 'status-rolledback');
            badge.classList.add(`status-${newStatus.toLowerCase()}`);
        }
        
        // Update the title if provided (e.g., "Creating task..." -> "Task created")
        if (titleEl && newTitle) {
            titleEl.textContent = newTitle;
        }
        
        // Update card styling based on status
        card.classList.remove('action-pending', 'action-done', 'action-failed', 'action-rejected', 'action-rolledback');
        card.classList.add(`action-${newStatus.toLowerCase()}`);
        
        console.log(`[AgentActions] Updated card for action ${actionId} to status ${newStatus}`);
    });
}

// Replace action commands in content with action cards
function replaceActionCommandsWithCards(content) {
    const actionRegex = /\[ACTION:(\w+)\]([\s\S]*?)\[\/ACTION\]/g;
    let result = content;
    let match;
    
    // Collect all matches first
    const matches = [];
    while ((match = actionRegex.exec(content)) !== null) {
        try {
            const payload = JSON.parse(match[2]);
            matches.push({
                fullMatch: match[0],
                actionType: match[1],
                payload: payload
            });
        } catch (e) {
            // Invalid JSON, just remove it
            matches.push({
                fullMatch: match[0],
                actionType: match[1],
                payload: {}
            });
        }
    }
    
    // Replace each match with an action card
    // Use payload-based ID so we can match it later with the proposed action
    for (const m of matches) {
        const cardId = generateCardIdFromPayload(m.actionType, m.payload);
        result = result.replace(m.fullMatch, createActionCardHTML(m.actionType, m.payload, cardId));
    }
    
    return result;
}

// Generate a stable card ID from payload so we can match cards to actions
function generateCardIdFromPayload(actionType, payload) {
    // For CreateTask, use title as part of the ID
    if (actionType === 'CreateTask' && (payload.title || payload.Title)) {
        const title = (payload.title || payload.Title).toLowerCase().replace(/[^a-z0-9]/g, '-').substring(0, 30);
        return `action-${actionType.toLowerCase()}-${title}`;
    }
    // For other types, use a timestamp-based ID
    return `action-${actionType.toLowerCase()}-${Date.now()}`;
}

// Link a temporary card ID to the actual action ID from the backend
function linkCardToActionId(tempCardId, actualActionId, actionType = null, payload = null) {
    console.log(`[AgentActions] linkCardToActionId: tempId=${tempCardId}, actualId=${actualActionId}, type=${actionType}`);
    
    let linked = false;
    
    // Strategy 1: Find by exact temporary ID
    let cards = document.querySelectorAll(`.agent-action-attachment[data-action-id="${tempCardId}"]`);
    if (cards.length > 0) {
        cards.forEach(card => {
            card.setAttribute('data-action-id', actualActionId);
            linked = true;
        });
        console.log(`[AgentActions] Linked ${cards.length} cards by exact tempId`);
    }
    
    // Strategy 2: Find by action type prefix
    if (!linked && actionType) {
        cards = document.querySelectorAll(`.agent-action-attachment[data-action-id^="action-${actionType.toLowerCase()}"]`);
        if (cards.length > 0) {
            // Link only the first unlinked one
            for (const card of cards) {
                const currentId = card.getAttribute('data-action-id');
                if (currentId && currentId.startsWith('action-')) {
                    card.setAttribute('data-action-id', actualActionId);
                    linked = true;
                    console.log(`[AgentActions] Linked card by type prefix: ${actionType}`);
                    break;
                }
            }
        }
    }
    
    // Strategy 3: Find pending cards by action type attribute
    if (!linked && actionType) {
        cards = document.querySelectorAll(`.agent-action-attachment[data-action-type="${actionType}"]`);
        for (const card of cards) {
            const badge = card.querySelector('.agent-action-badge');
            const status = badge?.getAttribute('data-status') || badge?.textContent?.toLowerCase();
            const currentId = card.getAttribute('data-action-id');
            // Only link if it's still pending and has a temp ID
            if (status === 'pending' && currentId && currentId.startsWith('action-')) {
                card.setAttribute('data-action-id', actualActionId);
                linked = true;
                console.log(`[AgentActions] Linked pending card by type: ${actionType}`);
                break;
            }
        }
    }
    
    // Strategy 4: Find any unlinked pending card
    if (!linked) {
        cards = document.querySelectorAll('.agent-action-attachment');
        for (const card of cards) {
            const badge = card.querySelector('.agent-action-badge');
            const status = badge?.getAttribute('data-status') || badge?.textContent?.toLowerCase();
            const currentId = card.getAttribute('data-action-id');
            // Only link if it's still pending and has a temp ID
            if (status === 'pending' && currentId && currentId.startsWith('action-')) {
                card.setAttribute('data-action-id', actualActionId);
                linked = true;
                console.log(`[AgentActions] Linked first pending card`);
                break;
            }
        }
    }
    
    if (!linked) {
        console.warn(`[AgentActions] Could not find card to link for action ${actualActionId}`);
    }
}

// Export functions for use in chat.js
window.AgentActions = {
    initialize: initializeAgentActions,
    parseResponse: parseAIResponseForActions,
    processActions: processDetectedActions,
    showTracker: showActionTracker,
    hideTracker: hideActionTracker,
    proposeAction: proposeAction,
    navigate: navigateToPage,
    getMode: getAIMode,
    getModel: getAIModel,
    getModelTier: getAIModelTier,
    isAgentMode: isAgentMode,
    stripActionCommands: stripActionCommandsFromContent,
    replaceWithCards: replaceActionCommandsWithCards,
    replaceWithCardsStreaming: replaceActionCommandsWithCardsStreaming,
    createActionCard: createActionCardHTML,
    updateCardStatus: updateActionCardStatus,
    restoreCardStatuses: restoreActionCardStatuses,
    syncModelTier: syncOrgModelTier,
    syncModelFromSettings: syncModelFromSettings,
    restoreFromSession: restoreActionsFromSession,
    saveToSession: saveActionsToSession,
    clearSession: clearActionsFromSession,
    state: AgentActionsState
};

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Small delay to ensure other scripts are loaded
    setTimeout(() => {
        initializeAgentActions();
        // Try to restore any pending actions from previous page
        restoreActionsFromSession();
        // Restore action card statuses after a short delay (to ensure cards are rendered)
        setTimeout(restoreActionCardStatuses, 500);
    }, 500);
});

// Also restore statuses periodically in case cards are rendered dynamically
setInterval(restoreActionCardStatuses, 2000);

})();
