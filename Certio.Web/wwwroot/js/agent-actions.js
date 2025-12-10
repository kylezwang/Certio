/**
 * Agent Actions Tracker Module
 * Integrates AI agent workflows into the sidebar chat
 * Provides Cursor-style action tracking with Keep/Undo functionality
 */

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
    modelTier: 'Auto'      // Current AI model tier: Auto, Basic, Intermediate, Advanced, Premium
};

// Model Tier Labels for display
const ModelTierLabels = {
    'Auto': 'Auto',
    'Basic': 'Basic',
    'Intermediate': 'Intermediate',
    'Advanced': 'Advanced',
    'Premium': 'Premium'
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
    // Check if there's a settings select on the page (AI Settings page)
    const settingsSelect = document.getElementById('aiModelTier');
    if (settingsSelect) {
        // Use the value from the settings page
        AgentActionsState.modelTier = settingsSelect.value || 'Auto';
        localStorage.setItem('aiModelTier', AgentActionsState.modelTier);
        syncModelToUI();
        
        // Listen for changes on the settings page
        settingsSelect.addEventListener('change', function() {
            AgentActionsState.modelTier = this.value || 'Auto';
            localStorage.setItem('aiModelTier', AgentActionsState.modelTier);
            syncModelToUI();
            console.log('[AgentActions] Model tier synced from settings:', AgentActionsState.modelTier);
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
        
        // Close other dropdowns
        document.querySelectorAll('.ai-mode-dropdown.show, .model-dropdown.show').forEach(d => {
            if (d.id !== dropdownId) d.classList.remove('show');
        });
        document.querySelectorAll('.ai-mode-selector.open').forEach(s => {
            if (s.id !== selectorId) s.classList.remove('open');
        });
        
        // Toggle this dropdown
        dropdown.classList.toggle('show');
        selector.classList.toggle('open');
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
            dropdown.classList.remove('show');
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
        
        // Close other dropdowns
        document.querySelectorAll('.ai-mode-dropdown.show, .model-dropdown.show').forEach(d => {
            if (d.id !== dropdownId) d.classList.remove('show');
        });
        document.querySelectorAll('.ai-mode-selector.open').forEach(s => {
            s.classList.remove('open');
        });
        
        // Toggle this dropdown
        dropdown.classList.toggle('show');
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
            dropdown.classList.remove('show');
            
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
            if (settingsSelect && settingsSelect.value !== tier) {
                settingsSelect.value = tier;
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

// Close dropdowns when clicking outside
document.addEventListener('click', function(e) {
    if (!e.target.closest('.ai-mode-selector') && !e.target.closest('.model-selector')) {
        document.querySelectorAll('.ai-mode-dropdown.show, .model-dropdown.show').forEach(d => {
            d.classList.remove('show');
        });
        document.querySelectorAll('.ai-mode-selector.open').forEach(s => {
            s.classList.remove('open');
        });
    }
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
                <!-- Action Counter and Controls -->
                <div class="action-tracker-header">
                    <div class="action-counter">
                        <span id="actionCurrentIndex">0</span>
                        <span class="action-separator">/</span>
                        <span id="actionTotalCount">0</span>
                    </div>
                    <div class="action-controls">
                        <button id="actionKeepBtn" class="action-btn action-keep-btn" title="Keep this change">
                            <i class="fas fa-check"></i>
                            Keep
                        </button>
                        <button id="actionUndoBtn" class="action-btn action-undo-btn" title="Undo this change">
                            <i class="fas fa-undo"></i>
                            Undo
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
                    <div class="action-dots" id="actionDots">
                        <!-- Dot indicators will be populated dynamically -->
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
            
            <!-- Toggle Button -->
            <button id="actionTrackerToggle" class="action-tracker-toggle" title="Toggle Actions Tracker">
                <i class="fas fa-robot"></i>
            </button>
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
    
    // Toggle button
    document.getElementById('actionTrackerToggle')?.addEventListener('click', toggleTrackerVisibility);
    
    // Global Actions navbar button
    document.getElementById('actionsLink')?.addEventListener('click', function(e) {
        e.preventDefault();
        toggleActionTracker();
    });
    
    // Button hover effects
    addButtonHoverEffects();
}

// Toggle action tracker visibility
function toggleActionTracker() {
    const tracker = document.getElementById('agentActionTracker');
    if (!tracker) {
        initializeActionTrackerUI();
    }
    
    if (AgentActionsState.actions.length === 0) {
        // No actions - show empty state
        showActionTracker();
        showEmptyState();
    } else if (tracker.style.display === 'none' || !AgentActionsState.isVisible) {
        showActionTracker();
        updateTrackerDisplay();
    } else {
        // Toggle minimize
        const card = document.getElementById('actionTrackerCard');
        if (card) {
            card.style.display = card.style.display === 'none' ? 'flex' : 'none';
        }
    }
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
    
    if (titleEl) titleEl.textContent = 'No Pending Actions';
    if (descEl) descEl.textContent = 'Agent actions will appear here when you ask the AI to do something';
    if (iconEl) {
        iconEl.innerHTML = '<i class="fas fa-check-circle"></i>';
        iconEl.style.background = 'linear-gradient(135deg, #10b981, #059669)';
    }
    if (counterDiv) counterDiv.style.display = 'none';
    if (controlsDiv) controlsDiv.style.display = 'none';
    if (navDiv) navDiv.style.display = 'none';
    if (quickBar) quickBar.style.display = 'none';
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
    
    const savedPosition = localStorage.getItem('actionTrackerPosition');
    if (savedPosition) {
        const position = JSON.parse(savedPosition);
        tracker.style.left = position.left;
        tracker.style.top = position.top;
        tracker.style.right = '';
        tracker.style.bottom = '';
    } else {
        // Default position: below the timer, left side
        tracker.style.left = '10vw';
        tracker.style.bottom = '8rem';
        tracker.style.transform = 'translateX(-50%)';
    }
}

// Show the action tracker
function showActionTracker() {
    const tracker = document.getElementById('agentActionTracker');
    if (tracker) {
        tracker.style.display = 'flex';
        AgentActionsState.isVisible = true;
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
    
    // Restore counter and controls visibility
    const counterDiv = document.querySelector('.action-counter');
    const controlsDiv = document.querySelector('.action-controls');
    if (counterDiv) counterDiv.style.display = 'flex';
    if (controlsDiv) controlsDiv.style.display = 'flex';
    
    const currentAction = actions[index];
    
    // Update counter
    document.getElementById('actionCurrentIndex').textContent = index + 1;
    document.getElementById('actionTotalCount').textContent = actions.length;
    
    // Update action preview
    updateActionPreview(currentAction);
    
    // Update navigation
    updateNavigationControls();
    
    // Update dots
    updateActionDots();
}

// Update action preview with current action details
function updateActionPreview(action) {
    const iconEl = document.getElementById('actionTypeIcon');
    const titleEl = document.getElementById('actionTitle');
    const descEl = document.getElementById('actionDescription');
    const detailsEl = document.getElementById('actionDetails');
    
    // Set icon based on action type
    const iconMap = {
        [AgentActionTypes.CreateTask]: 'fa-tasks',
        [AgentActionTypes.AttachFile]: 'fa-paperclip',
        [AgentActionTypes.AddNote]: 'fa-sticky-note',
        [AgentActionTypes.StartTimer]: 'fa-clock',
        [AgentActionTypes.NavigateTo]: 'fa-external-link-alt'
    };
    
    const colorMap = {
        [AgentActionTypes.CreateTask]: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
        [AgentActionTypes.AttachFile]: 'linear-gradient(135deg, #8b5cf6, #6d28d9)',
        [AgentActionTypes.AddNote]: 'linear-gradient(135deg, #f59e0b, #d97706)',
        [AgentActionTypes.StartTimer]: 'linear-gradient(135deg, #10b981, #059669)',
        [AgentActionTypes.NavigateTo]: 'linear-gradient(135deg, #a32b43, #3d1019)'
    };
    
    const icon = iconMap[action.actionType] || 'fa-cog';
    const color = colorMap[action.actionType] || 'linear-gradient(135deg, #6b7280, #374151)';
    
    iconEl.innerHTML = `<i class="fas ${icon}"></i>`;
    iconEl.style.background = color;
    
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

// Update action dots indicator
function updateActionDots() {
    const dotsContainer = document.getElementById('actionDots');
    if (!dotsContainer) return;
    
    const actions = AgentActionsState.actions;
    const index = AgentActionsState.currentIndex;
    
    let dotsHTML = '';
    actions.forEach((action, i) => {
        const statusClass = `dot-${action.status.toLowerCase()}`;
        const activeClass = i === index ? 'active' : '';
        dotsHTML += `<span class="action-dot ${statusClass} ${activeClass}" data-index="${i}"></span>`;
    });
    
    dotsContainer.innerHTML = dotsHTML;
    
    // Add click handlers to dots
    dotsContainer.querySelectorAll('.action-dot').forEach(dot => {
        dot.addEventListener('click', function() {
            AgentActionsState.currentIndex = parseInt(this.dataset.index);
            updateTrackerDisplay();
        });
    });
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
        updateTrackerDisplay();
        saveActionsToSession();
    } finally {
        keepBtn.disabled = false;
        keepBtn.innerHTML = '<i class="fas fa-check"></i> Keep';
    }
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
        } else if (action.status === AgentActionStatus.Pending || action.status === AgentActionStatus.Approved) {
            // Reject pending/approved action
            await rejectAction(action.id);
            action.status = AgentActionStatus.Rejected;
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
                if (action) action.status = AgentActionStatus.Approved;
            });
        }
        
        // Execute all approved (including newly approved)
        const allApprovedIds = actions
            .filter(a => a.status === AgentActionStatus.Approved)
            .map(a => a.id);
        
        for (const id of allApprovedIds) {
            await executeAction(id);
            const action = actions.find(a => a.id === id);
            if (action) action.status = AgentActionStatus.Done;
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
                if (action) action.status = AgentActionStatus.Rejected;
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
        throw new Error(`Failed to approve action: ${response.status}`);
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
        throw new Error(`Failed to execute action: ${response.status}`);
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
    
    // Also look for natural language action indicators (fallback detection)
    if (actions.length === 0) {
        const nlActions = parseNaturalLanguageActions(content);
        actions.push(...nlActions);
    }
    
    return actions;
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
    
    // Ensure org ID is set
    if (!AgentActionsState.orgId) {
        AgentActionsState.orgId = getCurrentOrganizationId();
    }
    
    AgentActionsState.conversationId = conversationId;
    AgentActionsState.currentIndex = 0;
    AgentActionsState.actions = [];
    
    // Show tracker immediately with processing state
    showActionTracker();
    
    for (const actionData of actions) {
        try {
            console.log('[AgentActions] Proposing action:', actionData.actionType, actionData.payload);
            
            // Propose the action
            const result = await proposeAction(
                actionData.actionType,
                actionData.payload,
                {
                    description: actionData.description || getActionDescription({ actionType: actionData.actionType, payload: actionData.payload })
                }
            );
            
            console.log('[AgentActions] Propose result:', result);
            
            // Add to our tracking array
            const action = result.action || result;
            AgentActionsState.actions.push({
                id: action.id || action.Id,
                actionType: actionData.actionType,
                payload: actionData.payload,
                description: action.description || actionData.description,
                status: action.status || AgentActionStatus.Pending,
                runId: action.runId
            });
            
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
function createActionCardHTML(actionType, payload) {
    const iconMap = {
        'CreateTask': 'fa-tasks',
        'AttachFile': 'fa-paperclip',
        'AddNote': 'fa-sticky-note',
        'StartTimer': 'fa-clock',
        'NavigateTo': 'fa-external-link-alt'
    };
    
    const colorMap = {
        'CreateTask': '#3b82f6',
        'AttachFile': '#8b5cf6',
        'AddNote': '#f59e0b',
        'StartTimer': '#10b981',
        'NavigateTo': '#a32b43'
    };
    
    const labelMap = {
        'CreateTask': 'Creating Task',
        'AttachFile': 'Attaching File',
        'AddNote': 'Adding Note',
        'StartTimer': 'Starting Timer',
        'NavigateTo': 'Navigating'
    };
    
    const icon = iconMap[actionType] || 'fa-cog';
    const color = colorMap[actionType] || '#6b7280';
    const label = labelMap[actionType] || 'Processing';
    
    // Get specific detail from payload
    let detail = '';
    if (actionType === 'CreateTask' && payload.title) {
        detail = payload.title;
    } else if (actionType === 'NavigateTo' && (payload.pageName || payload.url)) {
        detail = payload.pageName || payload.url;
    } else if (actionType === 'AddNote' && payload.content) {
        detail = payload.content.substring(0, 50) + (payload.content.length > 50 ? '...' : '');
    } else if (actionType === 'StartTimer' && payload.description) {
        detail = payload.description;
    }
    
    return `
        <div class="agent-action-card" style="
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 12px 16px;
            background: linear-gradient(135deg, ${color}15, ${color}08);
            border: 1px solid ${color}30;
            border-radius: 12px;
            margin: 12px 0;
            animation: actionCardSlideIn 0.4s ease-out;
        ">
            <div class="action-icon-wrapper" style="
                width: 40px;
                height: 40px;
                border-radius: 10px;
                background: ${color};
                display: flex;
                align-items: center;
                justify-content: center;
                flex-shrink: 0;
                animation: actionIconPulse 2s ease-in-out infinite;
            ">
                <i class="fas ${icon}" style="color: white; font-size: 16px;"></i>
            </div>
            <div class="action-info" style="flex: 1; min-width: 0;">
                <div style="
                    font-size: 13px;
                    font-weight: 600;
                    color: ${color};
                    margin-bottom: 2px;
                    display: flex;
                    align-items: center;
                    gap: 6px;
                ">
                    <span class="action-spinner" style="
                        width: 12px;
                        height: 12px;
                        border: 2px solid ${color}40;
                        border-top-color: ${color};
                        border-radius: 50%;
                        animation: spin 0.8s linear infinite;
                        display: inline-block;
                    "></span>
                    ${label}...
                </div>
                ${detail ? `<div style="font-size: 14px; color: #374151; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${detail}</div>` : ''}
            </div>
            <div class="action-badge" style="
                padding: 4px 10px;
                background: ${color}20;
                border-radius: 20px;
                font-size: 11px;
                font-weight: 600;
                color: ${color};
                white-space: nowrap;
            ">
                Pending Approval
            </div>
        </div>
    `;
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
    for (const m of matches) {
        result = result.replace(m.fullMatch, createActionCardHTML(m.actionType, m.payload));
    }
    
    return result;
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
    createActionCard: createActionCardHTML,
    syncModelTier: syncOrgModelTier,
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
    }, 500);
});

