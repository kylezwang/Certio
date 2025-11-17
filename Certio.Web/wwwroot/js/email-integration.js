// Email Integration JavaScript
let emailAccountStatus = null;
let isEmailMode = false;
let currentEmailRecipient = null;

function emitEmailModeChanged() {
    if (typeof window !== 'undefined') {
        window.isEmailModeEnabled = () => isEmailMode;
        try {
            window.dispatchEvent(new CustomEvent('emailModeChanged', { detail: { isEmailMode } }));
        } catch (err) {
            console.warn('Failed to dispatch emailModeChanged event', err);
        }
    }
}

emitEmailModeChanged();

// Initialize email integration
async function initializeEmailIntegration() {
    await loadEmailAccountStatus();
    setupEmailEventListeners();
}

// Load email account status
async function loadEmailAccountStatus() {
    try {
        const response = await fetch('/api/email-oauth/status');
        const result = await response.json();
        
        if (result.success) {
            emailAccountStatus = result.connected ? result.emailAccount : null;
            updateEmailStatusUI();
            
            // Update inbox count if connected
            if (emailAccountStatus) {
                updateInboxCount();
            }
        }
    } catch (error) {
        console.error('Error loading email account status:', error);
    }
}

// Update email status UI
function updateEmailStatusUI() {
    const connectedDiv = document.getElementById('emailConnected');
    const notConnectedDiv = document.getElementById('emailNotConnected');
    const sendAsEmailToggle = document.getElementById('sendAsEmailToggle');
    const inboxSection = document.getElementById('emailInboxSection');
    const emailIntegrationSection = document.getElementById('emailIntegrationSection');
    const channelsSection = document.querySelector('.channels-section');
    
    if (emailAccountStatus) {
        if (connectedDiv) connectedDiv.style.display = 'block';
        if (notConnectedDiv) notConnectedDiv.style.display = 'none';
        if (sendAsEmailToggle) sendAsEmailToggle.style.display = 'block';
        if (inboxSection) inboxSection.style.display = 'block';
        
        const emailAddr = document.getElementById('connectedEmailAddress');
        const provider = document.getElementById('connectedProvider');
        if (emailAddr) emailAddr.textContent = emailAccountStatus.emailAddress;
        if (provider) provider.textContent = `(${emailAccountStatus.provider})`;
        
        // Update provider icon based on provider type
        const providerIcon = connectedDiv?.querySelector('.fab');
        if (providerIcon) {
            if (emailAccountStatus.provider === 'Gmail') {
                providerIcon.className = 'fab fa-google';
                providerIcon.style.color = '#4285f4';
            } else if (emailAccountStatus.provider === 'Outlook') {
                providerIcon.className = 'fab fa-microsoft';
                providerIcon.style.color = '#0078d4';
            }
        }
        
        // Reorder sections when connected: INBOX at top, Email Integration at bottom
        if (inboxSection && emailIntegrationSection && channelsSection) {
            // Move INBOX to top (before channels-section)
            channelsSection.parentNode.insertBefore(inboxSection, channelsSection);
            // Move Email Integration to bottom (after direct messages section)
            // Find the Direct Messages title by its text content
            const directMessagesTitle = Array.from(document.querySelectorAll('.team-title'))
                .find(el => el.textContent.includes('Direct Messages'));
            if (directMessagesTitle) {
                const teamMembers = directMessagesTitle.nextElementSibling;
                if (teamMembers && teamMembers.classList.contains('team-members')) {
                    // Insert after the team-members container
                    const nextSibling = teamMembers.nextElementSibling;
                    if (nextSibling && nextSibling !== emailIntegrationSection) {
                        teamMembers.parentNode.insertBefore(emailIntegrationSection, nextSibling);
                    } else if (!nextSibling) {
                        teamMembers.parentNode.appendChild(emailIntegrationSection);
                    }
                } else {
                    // Fallback: insert after direct messages title
                    directMessagesTitle.parentNode.appendChild(emailIntegrationSection);
                }
            }
        }
        
        // Don't trigger sync automatically on page load - let it happen when user opens inbox
        // This prevents rapid firing on page load
    } else {
        if (connectedDiv) connectedDiv.style.display = 'none';
        if (notConnectedDiv) notConnectedDiv.style.display = 'block';
        if (sendAsEmailToggle) sendAsEmailToggle.style.display = 'none';
        if (inboxSection) inboxSection.style.display = 'none';
        
        // Reorder sections when not connected: Email Integration at top
        if (emailIntegrationSection && channelsSection) {
            // Move Email Integration to top (before channels-section)
            channelsSection.parentNode.insertBefore(emailIntegrationSection, channelsSection);
        }
    }
}

// Setup event listeners
function setupEmailEventListeners() {
    const connectGmailBtn = document.getElementById('connectGmailBtn');
    const connectOutlookBtn = document.getElementById('connectOutlookBtn');
    const disconnectEmailBtn = document.getElementById('disconnectEmailBtn');
    const sendAsEmailToggle = document.getElementById('sendAsEmailToggle');
    const toggleEmailInputBtn = document.getElementById('toggleEmailInputBtn');
    const cancelEmailBtn = document.getElementById('cancelEmailBtn');
    const sendEmailBtn = document.getElementById('sendEmailBtn');
    const inboxItem = document.getElementById('inboxItem');
    
    console.log('Setting up email integration event listeners...');
    console.log('connectGmailBtn found:', !!connectGmailBtn);
    
    if (connectGmailBtn) {
        // Remove any existing listeners first
        const newGmailBtn = connectGmailBtn.cloneNode(true);
        connectGmailBtn.parentNode?.replaceChild(newGmailBtn, connectGmailBtn);
        
        newGmailBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            console.log('Connect Gmail button clicked!');
            connectEmail('gmail');
        });
    } else {
        console.warn('connectGmailBtn not found!');
    }
    
    if (connectOutlookBtn) {
        // Remove any existing listeners first
        const newOutlookBtn = connectOutlookBtn.cloneNode(true);
        connectOutlookBtn.parentNode?.replaceChild(newOutlookBtn, connectOutlookBtn);
        
        newOutlookBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            console.log('Connect Outlook button clicked!');
            connectEmail('outlook');
        });
    }
    
    if (disconnectEmailBtn) {
        disconnectEmailBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            disconnectEmail();
        });
    }
    
    if (sendAsEmailToggle) {
        sendAsEmailToggle.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            toggleEmailMode();
        });
    }
    
    if (toggleEmailInputBtn) {
        toggleEmailInputBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            toggleEmailMode(false);
        });
    }
    
    if (cancelEmailBtn) {
        cancelEmailBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            toggleEmailMode(false);
        });
    }
    
    if (sendEmailBtn) {
        sendEmailBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            sendEmailFromDM();
        });
    }
    
    if (inboxItem) {
        // Remove any existing listeners first (same pattern as Gmail/Outlook buttons)
        const newInboxItem = inboxItem.cloneNode(true);
        inboxItem.parentNode?.replaceChild(newInboxItem, inboxItem);
        
        newInboxItem.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            console.log('Inbox item clicked!');
            openInbox();
        });
        console.log('Inbox item click listener attached');
    } else {
        console.warn('inboxItem not found!');
    }
    
    // Close modal handlers
    const closeInboxModalBtn = document.getElementById('closeInboxModal');
    const refreshInboxBtn = document.getElementById('refreshInboxBtn');
    const inboxModal = document.getElementById('emailInboxModal');
    const inboxSearchInput = document.getElementById('inboxSearchInput');
    
    if (closeInboxModalBtn) {
        closeInboxModalBtn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            closeInboxModal();
        });
    }
    
    if (refreshInboxBtn) {
        refreshInboxBtn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            await triggerEmailSync();
            setTimeout(async () => {
                await loadInboxEmails(true); // Reset pagination on refresh
                setTimeout(() => adjustEmailHeaderForScrollbar(), 100);
            }, 1000); // Reload after sync
        });
    }
    
    // Search input handler
    if (inboxSearchInput) {
        let searchTimeout;
        const scheduleSearch = (query, immediate = false) => {
            clearTimeout(searchTimeout);
            if (immediate) {
                loadInboxEmails(true, query);
            } else {
                searchTimeout = setTimeout(() => {
                    loadInboxEmails(true, query);
                }, SEARCH_DEBOUNCE_MS);
            }
        };

        inboxSearchInput.addEventListener('input', function(e) {
            e.stopPropagation();
            const query = e.target.value.trim();
            scheduleSearch(query);
        });
        
        inboxSearchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                const query = e.target.value.trim();
                scheduleSearch(query, true);
            } else if (e.key === 'Escape') {
                e.target.value = '';
                scheduleSearch('', true);
            }
        });
    }
    
    // Close modal on backdrop click
    if (inboxModal) {
        inboxModal.addEventListener('click', function(e) {
            if (e.target === inboxModal) {
                closeInboxModal();
            }
        });
    }
    
    console.log('Email integration event listeners set up complete');
}

// Connect email account
async function connectEmail(provider) {
    console.log(`connectEmail called for provider: ${provider}`);
    try {
        console.log(`Fetching authorization URL for ${provider}...`);
        const response = await fetch(`/api/email-oauth/${provider}/authorize`);
        const result = await response.json();
        
        console.log(`Authorization response:`, result);
        
        if (result.success && result.authorizationUrl) {
            console.log(`Opening OAuth popup with URL: ${result.authorizationUrl}`);
            // Open OAuth popup
            const popup = window.open(result.authorizationUrl, 'Email OAuth', 'width=600,height=700');
            
            if (!popup) {
                alert('Please allow popups for this site to connect your email account.');
                return;
            }
            
            console.log('OAuth popup opened, setting up message listeners...');
            
            // Listen for OAuth callback via postMessage
            const messageHandler = (event) => {
                // Verify origin
                if (event.origin !== window.location.origin) {
                    console.log('Ignoring message from different origin:', event.origin);
                    return;
                }
                
                console.log('Received postMessage:', event.data);
                
                if (event.data.type === 'email-oauth-success') {
                    console.log(`Successfully connected ${event.data.provider} account:`, event.data.emailAddress);
                    // Reload email status
                    loadEmailAccountStatus();
                    alert(`${event.data.provider} account connected successfully!`);
                    window.removeEventListener('message', messageHandler);
                } else if (event.data.type === 'email-oauth-error') {
                    const errorMsg = event.data.errorDescription || event.data.error || 'Unknown error occurred';
                    console.error(`Failed to connect ${event.data.provider}:`, errorMsg);
                    alert(`Failed to connect ${event.data.provider}: ${errorMsg}`);
                    window.removeEventListener('message', messageHandler);
                }
            };
            
            window.addEventListener('message', messageHandler);
            
            // Fallback: check if popup closed (in case postMessage doesn't work)
            const checkInterval = setInterval(() => {
                if (popup.closed) {
                    clearInterval(checkInterval);
                    window.removeEventListener('message', messageHandler);
                    // Reload email status as fallback
                    setTimeout(() => loadEmailAccountStatus(), 500);
                }
            }, 1000);
        } else {
            console.error('Failed to get authorization URL:', result);
            alert(`Failed to get authorization URL: ${result.error || 'Unknown error'}`);
        }
    } catch (error) {
        console.error(`Error connecting ${provider}:`, error);
        alert(`Failed to connect ${provider}. Please try again.`);
    }
}

// Disconnect email account
async function disconnectEmail() {
    if (!emailAccountStatus) return;
    
    if (!confirm('Are you sure you want to disconnect your email account?')) {
        return;
    }
    
    try {
        const response = await fetch(`/api/email-oauth/${emailAccountStatus.id}`, {
            method: 'DELETE'
        });
        const result = await response.json();
        
        if (result.success) {
            emailAccountStatus = null;
            updateEmailStatusUI();
            toggleEmailMode(false);
            alert('Email account disconnected successfully.');
        }
    } catch (error) {
        console.error('Error disconnecting email:', error);
        alert('Failed to disconnect email account.');
    }
}

// Toggle email mode
function toggleEmailMode(enabled = null) {
    const regularInput = document.getElementById('regularMessageInput');
    const emailInput = document.getElementById('emailMessageInput');
    const sendAsEmailToggle = document.getElementById('sendAsEmailToggle');
    
    if (!emailAccountStatus) {
        if (enabled === false) {
            isEmailMode = false;
            if (regularInput) regularInput.style.display = 'flex';
            if (emailInput) emailInput.style.display = 'none';
            if (sendAsEmailToggle) sendAsEmailToggle.textContent = 'Switch to Email Mode';
            emitEmailModeChanged();
            return;
        }
        alert('Please connect your email account first.');
        return;
    }
    
    // Check if we're in direct message mode and have a thread
    const isDirectMessageMode = typeof window.isDirectMessageMode !== 'undefined' ? window.isDirectMessageMode : false;
    const currentThreadId = typeof window.currentDirectThreadId !== 'undefined' ? window.currentDirectThreadId : null;
    const currentOtherUserId = typeof window.currentDirectOtherUserId !== 'undefined' ? window.currentDirectOtherUserId : null;
    
    if (!isDirectMessageMode || !currentThreadId || !currentOtherUserId) {
        alert('Please open a direct message thread first to send emails.');
        return;
    }
    
    if (enabled === null) {
        isEmailMode = !isEmailMode;
    } else {
        isEmailMode = enabled;
    }
    
    if (isEmailMode) {
        // Get recipient email
        const recipientEmail = getRecipientEmailForCurrentThread();
        const emailToInput = document.getElementById('emailToInput');
        if (emailToInput) {
            emailToInput.value = recipientEmail || '';
        }
        
        if (regularInput) regularInput.style.display = 'none';
        if (emailInput) emailInput.style.display = 'block';
        if (sendAsEmailToggle) sendAsEmailToggle.textContent = 'Switch to DM Mode';
    } else {
        if (regularInput) regularInput.style.display = 'flex';
        if (emailInput) emailInput.style.display = 'none';
        if (sendAsEmailToggle) sendAsEmailToggle.textContent = 'Switch to Email Mode';
    }
    
    emitEmailModeChanged();
}

// Get recipient email for current thread
function getRecipientEmailForCurrentThread() {
    // This would need to fetch from API or thread data
    // For now, return empty string - will be populated when thread opens
    return currentEmailRecipient || '';
}

// Update recipient email when opening DM thread
function setEmailRecipientForThread(threadId, recipientEmail) {
    currentEmailRecipient = recipientEmail;
    const emailToInput = document.getElementById('emailToInput');
    if (emailToInput) {
        emailToInput.value = recipientEmail || '';
    }
}

// Hook into direct message thread opening
const originalOpenDirectThread = window.openDirectThread;
if (typeof originalOpenDirectThread === 'function') {
    window.openDirectThread = function(otherUserId, otherUserName) {
        // Get recipient email from user data
        // This would need to be fetched from API or stored in thread data
        const recipientEmail = getRecipientEmailFromUserId(otherUserId);
        setEmailRecipientForThread(null, recipientEmail);
        
        // Call original function
        return originalOpenDirectThread(otherUserId, otherUserName);
    };
}

// Get recipient email from user ID (placeholder - would need API call)
function getRecipientEmailFromUserId(userId) {
    // In a full implementation, this would fetch user data from API
    // For now, return empty string - will be populated from thread data
    return '';
}

// Send email from Direct Message
async function sendEmailFromDM() {
    const inEmailMode = typeof window !== 'undefined' && typeof window.isEmailModeEnabled === 'function'
        ? window.isEmailModeEnabled()
        : isEmailMode;
    
    if (!inEmailMode) {
        toggleEmailMode(true);
        return;
    }
    
    if (!emailAccountStatus || !currentDirectThreadId) {
        alert('No email account connected or no active thread.');
        return;
    }
    
    if (typeof window.isDirectMessageMode !== 'undefined' && !window.isDirectMessageMode) {
        alert('Please open a direct message conversation before sending an email.');
        return;
    }
    
    const subjectInput = document.getElementById('emailSubjectInput');
    const bodyInput = document.getElementById('emailBodyInput');
    const emailToInput = document.getElementById('emailToInput');
    const sendEmailBtn = document.getElementById('sendEmailBtn');
    
    const subject = subjectInput?.value?.trim() || '';
    const body = bodyInput?.value?.trim() || '';
    const recipientEmail = emailToInput?.value?.trim() || '';
    
    if (!body) {
        alert('Email message cannot be empty.');
        return;
    }
    
    if (!recipientEmail) {
        alert('Recipient email is required.');
        return;
    }
    
    const currentOrgId = document.querySelector('[data-organization-id]')?.dataset.organizationId;
    if (!currentOrgId) {
        alert('Unable to determine organization context for email.');
        return;
    }
    
    if (sendEmailBtn) {
        sendEmailBtn.disabled = true;
        sendEmailBtn.classList.add('is-loading');
    }
    
    try {
        const metadata = {
            EmailSubject: subject,
            EmailTo: recipientEmail,
            EmailProvider: emailAccountStatus.provider
        };
        
        const dmResponse = await fetch(`/api/dm/threads/${currentDirectThreadId}/messages?orgId=${currentOrgId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                Body: body,
                MessageType: 'Email',
                Metadata: metadata
            })
        });
        
        const dmResult = await dmResponse.json();
        
        if (!dmResponse.ok || !dmResult.success) {
            const errorMessage = dmResult.error || 'Failed to create Direct Message';
            throw new Error(errorMessage);
        }
        
        const newMessageId = dmResult.message?.id || dmResult.message?.Id || dmResult.messageId;
        if (!newMessageId) {
            throw new Error('Email message was created but message identifier was missing.');
        }
        
        const emailResponse = await fetch(`/api/dm/threads/${currentDirectThreadId}/send-email?orgId=${currentOrgId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                MessageId: newMessageId
            })
        });
        
        const emailResult = await emailResponse.json();
        
        if (!emailResponse.ok || !emailResult.success) {
            const errorMessage = emailResult.error || 'Failed to send email';
            throw new Error(errorMessage);
        }
        
        if (subjectInput) subjectInput.value = '';
        if (bodyInput) bodyInput.value = '';
        
        toggleEmailMode(false);
        
        if (typeof loadDirectMessages === 'function') {
            loadDirectMessages(currentDirectThreadId);
        }
    } catch (error) {
        console.error('Error sending email:', error);
        alert('Failed to send email: ' + (error.message || error));
    } finally {
        if (sendEmailBtn) {
            sendEmailBtn.disabled = false;
            sendEmailBtn.classList.remove('is-loading');
        }
    }
}

// Add email badge to message display
function addEmailBadgeToMessage(messageElement, provider) {
    if (!messageElement) return;
    
    const messageHeader = messageElement.querySelector('.message-header');
    if (messageHeader && !messageHeader.querySelector('.email-badge')) {
        const badge = document.createElement('span');
        badge.className = 'email-badge';
        badge.style.cssText = 'font-size: 0.7em; color: #007bff; margin-left: 8px; padding: 2px 6px; background: #e7f3ff; border-radius: 4px;';
        badge.innerHTML = `<i class="fas fa-envelope" style="margin-right: 3px;"></i>Sent through ${provider}`;
        messageHeader.appendChild(badge);
    }
}

// Update message display to show email badges
function updateMessageDisplayForEmails() {
    // This will be called when messages are loaded
    // Check each message's metadata for email provider
    const messages = document.querySelectorAll('.message-item');
    messages.forEach(msg => {
        const messageId = msg.dataset.messageId;
        // In a full implementation, we'd check the message metadata from the API
        // For now, check if message type is "Email"
    });
}

// Track sync state to prevent rapid firing
let emailSyncInProgress = false;
let lastSyncTriggerTime = 0;
const SYNC_DEBOUNCE_MS = 5000; // 5 second debounce

// Trigger email sync
async function triggerEmailSync() {
    if (!emailAccountStatus) return;
    
    // Prevent rapid firing - debounce sync requests
    const now = Date.now();
    if (emailSyncInProgress) {
        console.log('Email sync already in progress, skipping');
        return;
    }
    
    if (now - lastSyncTriggerTime < SYNC_DEBOUNCE_MS) {
        console.log('Email sync debounced, too soon since last sync');
        return;
    }
    
    emailSyncInProgress = true;
    lastSyncTriggerTime = now;
    
    try {
        const response = await fetch('/api/email-oauth/sync', {
            method: 'POST'
        });
        const result = await response.json();
        
        if (result.success) {
            console.log('Email sync triggered successfully');
            // Refresh inbox count
            updateInboxCount();
        }
    } catch (error) {
        console.error('Error triggering email sync:', error);
    } finally {
        // Reset sync flag after a delay to allow sync to complete
        setTimeout(() => {
            emailSyncInProgress = false;
        }, 25000); // 25 seconds - longer than typical sync time
    }
}

// Track if inbox is already open to prevent rapid calls
let inboxModalOpen = false;

// Open inbox view
async function openInbox() {
    console.log('openInbox() called');
    
    // Prevent rapid firing - if modal is already open/opening, ignore
    if (inboxModalOpen) {
        console.log('Inbox modal already open, ignoring duplicate call');
        return;
    }
    
    const modal = document.getElementById('emailInboxModal');
    if (!modal) {
        console.error('Inbox modal not found!');
        alert('Inbox modal not found.');
        return;
    }
    
    inboxModalOpen = true;
    
    // Ensure modal is appended to body for proper fixed positioning
    // This fixes issues when modal is nested in positioned containers
    if (modal.parentElement !== document.body) {
        console.log('Moving modal to body');
        document.body.appendChild(modal);
        
        // Re-attach backdrop click handler after moving to body
        modal.addEventListener('click', function(e) {
            if (e.target === modal) {
                console.log('Backdrop clicked, closing modal');
                closeInboxModal();
            }
        });
        
        // Re-attach close button handler
        const closeBtn = document.getElementById('closeInboxModal');
        if (closeBtn) {
            const newCloseBtn = closeBtn.cloneNode(true);
            closeBtn.parentNode?.replaceChild(newCloseBtn, closeBtn);
            newCloseBtn.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                console.log('Close button clicked');
                closeInboxModal();
            });
        }
        
        // Re-attach refresh button handler
        const refreshBtn = document.getElementById('refreshInboxBtn');
        if (refreshBtn) {
            const newRefreshBtn = refreshBtn.cloneNode(true);
            refreshBtn.parentNode?.replaceChild(newRefreshBtn, refreshBtn);
            newRefreshBtn.addEventListener('click', async function(e) {
                e.preventDefault();
                e.stopPropagation();
                console.log('Refresh button clicked');
                await triggerEmailSync();
                setTimeout(() => loadInboxEmails(true), 1000); // Reset pagination on refresh
            });
        }
    }
    
    // Remove inline display:none and show modal
    console.log('Showing modal');
    modal.removeAttribute('style'); // Remove inline style that might have display:none
    modal.style.display = 'flex';
    modal.style.position = 'fixed';
    modal.style.top = '7vh';
    modal.style.left = '20vw';
    modal.style.right = '320px';
    modal.style.bottom = '2rem';
    modal.style.zIndex = '1000';
    modal.style.backgroundColor = 'rgba(0, 0, 0, 0.5)';
    modal.style.alignItems = 'center';
    modal.style.justifyContent = 'center';
    
    // Ensure modal content is visible
    const modalDialog = modal.querySelector('.modal-dialog');
    if (modalDialog) {
        console.log('Modal dialog found');
        const dialogStyle = window.getComputedStyle(modalDialog);
        console.log('Modal dialog dimensions:', {
            width: dialogStyle.width,
            height: dialogStyle.height,
            display: dialogStyle.display
        });
    } else {
        console.error('Modal dialog not found inside modal!');
    }
    
    // Debug: Check modal computed styles
    setTimeout(() => {
        const computedStyle = window.getComputedStyle(modal);
        console.log('Modal computed styles:', {
            display: computedStyle.display,
            position: computedStyle.position,
            visibility: computedStyle.visibility,
            opacity: computedStyle.opacity,
            zIndex: computedStyle.zIndex,
            top: computedStyle.top,
            left: computedStyle.left,
            right: computedStyle.right,
            bottom: computedStyle.bottom,
            width: computedStyle.width,
            height: computedStyle.height,
            backgroundColor: computedStyle.backgroundColor
        });
        
        // Check if modal is actually in viewport
        const rect = modal.getBoundingClientRect();
        console.log('Modal bounding rect:', {
            top: rect.top,
            left: rect.left,
            width: rect.width,
            height: rect.height,
            visible: rect.width > 0 && rect.height > 0
        });
    }, 100);
    
    // Debug: Check if modal is in body
    console.log('Modal parent:', modal.parentElement?.tagName);
    console.log('Modal in body:', modal.parentElement === document.body);
    
    // Load emails (reset pagination when opening modal)
    // Don't trigger sync here - let the GetInbox endpoint handle it intelligently
    await loadInboxEmails(true);
    
    // Adjust header for scrollbar after modal opens
    setTimeout(() => adjustEmailHeaderForScrollbar(), 100);
    
    // Adjust modal position based on sidebar state
    adjustEmailModalPosition();
}

function escapeHtml(text) {
    const tempDiv = document.createElement('div');
    tempDiv.textContent = text;
    return tempDiv.innerHTML;
}

// Close inbox modal
function closeInboxModal() {
    const modal = document.getElementById('emailInboxModal');
    if (modal) {
        modal.style.display = 'none';
        inboxModalOpen = false; // Reset flag when closing
    }
    document.body.style.overflow = '';
    // Clean up scroll listener and pagination state
    removeInboxScrollListener();
    inboxPaginationState = {
        skip: 0,
        hasMore: true,
        loading: false,
        total: 0
    };
    // Clear search state
    currentSearchQuery = '';
    
    // Clear search input
    const searchInput = document.getElementById('inboxSearchInput');
    if (searchInput) {
        searchInput.value = '';
    }
}

// Adjust header row margin for scrollbar (same as History page)
function adjustEmailHeaderForScrollbar() {
    const container = document.getElementById('inboxEmailList');
    const headerCard = document.querySelector('#emailInboxModal .billing-column-header-card');
    
    if (!container || !headerCard) return;
    
    // Check if scrollbar is active
    const hasScrollbar = container.scrollHeight > container.clientHeight;
    
    if (hasScrollbar) {
        // Get scrollbar width (typically 6px from our custom scrollbar)
        const scrollbarWidth = container.offsetWidth - container.clientWidth;
        headerCard.style.marginRight = `${scrollbarWidth}px`;
    } else {
        headerCard.style.marginRight = '0';
    }
}

// Adjust email modal position based on sidebar state and resize handle position
function adjustEmailModalPosition() {
    const modal = document.getElementById('emailInboxModal');
    if (!modal || modal.style.display === 'none') return;
    
    const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
    if (!mainContentWrapper) return;
    
    // Get the main-content-wrapper's computed right position - this is the most accurate
    // as it's dynamically updated when the panel is resized
    const wrapperStyle = window.getComputedStyle(mainContentWrapper);
    const wrapperRight = wrapperStyle.right;
    
    // Set modal right position to match main-content-wrapper's right position
    // This ensures perfect alignment with the resize handle's left edge
    if (document.body.classList.contains('chat-hidden')) {
        modal.style.right = '1rem';
    } else if (wrapperRight && wrapperRight !== 'auto') {
        modal.style.right = wrapperRight;
    } else {
        // Fallback: calculate from resize handle or localStorage
        const resizeHandle = document.getElementById('resizeHandle');
        let panelWidth = 320; // Default width
        
        if (resizeHandle && resizeHandle.style.display !== 'none') {
            // Calculate panel width from resize handle position
            // resizeHandle.right = panelWidth - 12px, so panelWidth = resizeHandle.right + 12px
            const handleStyle = window.getComputedStyle(resizeHandle);
            const handleRight = parseFloat(handleStyle.right) || 0;
            if (handleRight > 0) {
                panelWidth = handleRight + 12;
            }
        }
        
        // Fallback to localStorage if resize handle not available
        if (panelWidth === 320) {
            const activeSidebar = localStorage.getItem('activeSidebar');
            if (activeSidebar === 'ai') {
                panelWidth = parseInt(localStorage.getItem('chatPanelWidth') || '320');
            } else if (activeSidebar === 'notifications' || activeSidebar === 'comms') {
                panelWidth = parseInt(localStorage.getItem('notificationsPanelWidth') || '320');
            }
        }
        
        modal.style.right = panelWidth + 'px';
    }
    
    // Set modal left position based on sidebar state
    if (document.body.classList.contains('sidebar-hidden')) {
        modal.style.left = 'calc(6vw + 1rem)';
    } else {
        // Check if global dashboard layout
        if (mainContentWrapper.classList.contains('global-dashboard-layout')) {
            modal.style.left = '23vw';
        } else {
            modal.style.left = '20vw';
        }
    }
}

// Email inbox pagination state
let inboxPaginationState = {
    skip: 0,
    hasMore: true,
    loading: false,
    total: 0
};

let currentSearchQuery = '';
const SEARCH_DEBOUNCE_MS = 300;
const SEARCH_RESULT_FETCH_SIZE = 200;

// Load emails into inbox modal with lazy loading / search support
async function loadInboxEmails(reset = false, overrideSearchQuery = null) {
    const emailList = document.getElementById('inboxEmailList');
    const loadingState = document.getElementById('inboxLoadingState');
    const emptyState = document.getElementById('inboxEmptyState');
    const searchInput = document.getElementById('inboxSearchInput');
    
    if (!emailList || !loadingState || !emptyState) return;
    
    if (overrideSearchQuery !== null && overrideSearchQuery !== undefined)
    {
        currentSearchQuery = overrideSearchQuery.trim();
        if (searchInput && searchInput.value !== overrideSearchQuery) {
            searchInput.value = overrideSearchQuery;
        }
        reset = true;
    }
    
    const isSearching = currentSearchQuery.length > 0;
    
    // Reset pagination state if requested
    if (reset) {
        inboxPaginationState = {
            skip: 0,
            hasMore: !isSearching,
            loading: false,
            total: 0
        };
        emailList.innerHTML = '';
        if (!isSearching && searchInput && searchInput.value) {
            searchInput.value = '';
        }
        removeInboxScrollListener();
    }
    
    // Prevent concurrent loads
    if (inboxPaginationState.loading) {
        return;
    }
    
    // Show loading indicator only on first load (or search reset)
    if (reset || inboxPaginationState.skip === 0 || isSearching) {
        emailList.style.display = 'none';
        loadingState.style.display = 'block';
        emptyState.style.display = 'none';
    } else {
        // Show bottom loading indicator for pagination when not searching
        if (!isSearching) {
            showInboxPaginationLoading();
        }
    }
    
    inboxPaginationState.loading = true;
    
    try {
        const currentOrgId = document.querySelector('[data-organization-id]')?.dataset.organizationId;
        if (!currentOrgId) {
            throw new Error('Organization ID not found.');
        }
        
        const fetchParams = new URLSearchParams({ orgId: currentOrgId });
        if (isSearching) {
            fetchParams.set('skip', '0');
            fetchParams.set('take', SEARCH_RESULT_FETCH_SIZE.toString());
            fetchParams.set('search', currentSearchQuery);
        } else {
            fetchParams.set('skip', inboxPaginationState.skip.toString());
            fetchParams.set('take', '25');
        }

        const response = await fetch(`/api/email-oauth/inbox?${fetchParams.toString()}`);
        const result = await response.json();
        
        // Hide initial loading
        if (reset || inboxPaginationState.skip === 0 || isSearching) {
            loadingState.style.display = 'none';
        } else {
            hideInboxPaginationLoading();
        }
        
        if (result.success && result.messages && result.messages.length > 0) {
            emailList.style.display = 'flex';
            emailList.style.flexDirection = 'column';
            emptyState.style.display = 'none';
            
            if (reset) {
                emailList.innerHTML = '';
            }

            result.messages.forEach(email => {
                const emailItem = createEmailListItem(email);
                emailList.appendChild(emailItem);
            });
            
            if (isSearching) {
                inboxPaginationState.skip = result.messages.length;
                inboxPaginationState.hasMore = false;
                inboxPaginationState.total = result.total || result.messages.length;
                removeInboxScrollListener();
            } else {
                // Update pagination state
                inboxPaginationState.skip += result.messages.length;
                inboxPaginationState.hasMore = result.hasMore || false;
                inboxPaginationState.total = result.total || 0;
                
                // Setup scroll listener if we have more emails
                if (inboxPaginationState.hasMore) {
                    setupInboxScrollListener();
                } else {
                    removeInboxScrollListener();
                }
            }
            
            // Update unread badge
            updateInboxModalBadge();
            
            // Adjust header for scrollbar after loading emails
            setTimeout(() => adjustEmailHeaderForScrollbar(), 100);
        } else {
            if (reset || inboxPaginationState.skip === 0) {
                emailList.style.display = 'none';
                emptyState.style.display = 'block';
                if (isSearching && currentSearchQuery) {
                    emptyState.innerHTML = `
                        <i class="fas fa-search" style="font-size: 3rem; margin-bottom: 1rem; opacity: 0.3;"></i>
                        <div>No emails found matching "${escapeHtml(currentSearchQuery)}"</div>
                    `;
                } else {
                    emptyState.innerHTML = `
                        <i class="fas fa-inbox" style="font-size: 3rem; margin-bottom: 1rem; opacity: 0.3;"></i>
                        <div>No emails yet. Emails will appear here once synced.</div>
                    `;
                }
            }
            // Remove scroll listener if no more emails
            removeInboxScrollListener();
        }
    } catch (error) {
        console.error('Error loading inbox:', error);
        if (reset || inboxPaginationState.skip === 0) {
            loadingState.style.display = 'none';
            emptyState.style.display = 'block';
            emptyState.innerHTML = `
                <i class="fas fa-exclamation-circle" style="font-size: 3rem; margin-bottom: 1rem; opacity: 0.3;"></i>
                <div>Failed to load emails. Please try again.</div>
            `;
        } else {
            hideInboxPaginationLoading();
        }
        removeInboxScrollListener();
    } finally {
        inboxPaginationState.loading = false;
    }
}

// Show pagination loading indicator at bottom of list
function showInboxPaginationLoading() {
    const emailList = document.getElementById('inboxEmailList');
    if (!emailList) return;
    
    // Remove existing loading indicator if any
    const existingLoader = emailList.querySelector('.inbox-pagination-loader');
    if (existingLoader) return;
    
    const loader = document.createElement('div');
    loader.className = 'inbox-pagination-loader';
    loader.style.cssText = 'text-align: center; padding: 1rem; color: #9ca3af;';
    loader.innerHTML = `
        <div class="spinner-border spinner-border-sm" role="status" style="margin-right: 0.5rem;">
            <span class="visually-hidden">Loading...</span>
        </div>
        <span>Loading more emails...</span>
    `;
    emailList.appendChild(loader);
}

// Hide pagination loading indicator
function hideInboxPaginationLoading() {
    const emailList = document.getElementById('inboxEmailList');
    if (!emailList) return;
    
    const loader = emailList.querySelector('.inbox-pagination-loader');
    if (loader) {
        loader.remove();
    }
}

// Setup scroll listener for infinite scroll
let inboxScrollListener = null;
function setupInboxScrollListener() {
    // Remove existing listener if any
    removeInboxScrollListener();
    
    const emailList = document.getElementById('inboxEmailList');
    if (!emailList || !inboxPaginationState.hasMore) return;
    
    inboxScrollListener = () => {
        // Check if user is near bottom (within 200px)
        const scrollTop = emailList.scrollTop;
        const scrollHeight = emailList.scrollHeight;
        const clientHeight = emailList.clientHeight;
        
        if (scrollHeight - scrollTop - clientHeight < 200 && 
            inboxPaginationState.hasMore && 
            !inboxPaginationState.loading) {
            loadInboxEmails(false);
        }
    };
    
    emailList.addEventListener('scroll', inboxScrollListener);
}

// Remove scroll listener
function removeInboxScrollListener() {
    const emailList = document.getElementById('inboxEmailList');
    if (!emailList || !inboxScrollListener) return;
    
    emailList.removeEventListener('scroll', inboxScrollListener);
    inboxScrollListener = null;
}

// Create Gmail-style email list item using billing card layout
function createEmailListItem(email) {
    const item = document.createElement('div');
    item.className = `billing-card-item email-list-item ${!email.isRead ? 'unread' : ''}`;
    item.dataset.threadId = email.threadId;
    item.dataset.messageId = email.id;
    if (email.provider) {
        item.dataset.provider = email.provider;
    }
    if (email.externalEmailId) {
        item.dataset.externalId = email.externalEmailId;
    }
    
    const fromInfo = email.fromName || email.fromEmail;
    const subject = email.subject || '(No subject)';
    
    // Better HTML stripping for preview - handle style tags, scripts, and decode entities
    let preview = '';
    if (email.body) {
        // Create a temporary div to parse HTML
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = email.body;
        
        // Remove script and style elements
        const scripts = tempDiv.querySelectorAll('script, style');
        scripts.forEach(el => el.remove());
        
        // Get text content and clean it up
        preview = tempDiv.textContent || tempDiv.innerText || '';
        
        // Clean up whitespace
        preview = preview.replace(/\s+/g, ' ').trim();
        
        // Limit length
        if (preview.length > 80) {
            preview = preview.substring(0, 80) + '...';
        }
    } else if (email.bodyText) {
        // Use plain text body if available
        preview = email.bodyText.replace(/\s+/g, ' ').trim();
        if (preview.length > 80) {
            preview = preview.substring(0, 80) + '...';
        }
    }
    
    const receivedDate = formatEmailDate(email.receivedAt);
    const hasAttachment = false; // TODO: Check for attachments
    
    // Use billing-style card structure with grid columns
    const isImportant = email.isImportant || false;
    item.innerHTML = `
        <div class="email-checkbox-column">
            <input type="checkbox" class="email-checkbox form-check-input" style="cursor: pointer; width: 1rem; height: 1rem;" />
        </div>
        <div class="email-star-column">
            <div class="email-star">
                <i class="far fa-star"></i>
            </div>
        </div>
        <div class="email-sender-column" title="${fromInfo}">
            ${fromInfo}
        </div>
        <div class="email-subject-column">
            ${email.provider ? `<span class="email-provider-badge" data-provider="${email.provider}" data-thread-id="${email.threadId || ''}" data-external-id="${email.externalEmailId || ''}" title="Open in ${email.provider}">${email.provider}</span>` : ''}
            <span class="email-subject" title="${subject}">${subject}</span>
            ${preview ? `<span class="email-preview" title="${preview.replace(/"/g, '&quot;')}">${preview}</span>` : ''}
            ${hasAttachment ? '<i class="fas fa-paperclip email-attachment-icon"></i>' : ''}
        </div>
        <div class="email-date-column">
            ${receivedDate}
        </div>
        <div class="email-notalize-column">
            <div class="email-notalize">
                <img src="/images/Notal_Logo.png" alt="Notalize" style="width: 22px; height: 22px; object-fit: contain;" />
            </div>
        </div>
    `;
    
    // Click handler to open provider (matches badge behavior)
    item.addEventListener('click', (e) => {
        // Don't trigger if clicking checkbox, star, notalize button, or provider badge
        if (e.target.classList.contains('email-checkbox') || 
            e.target.closest('.email-star') || 
            e.target.closest('.email-notalize') ||
            e.target.classList.contains('email-provider-badge')) {
            return;
        }
        
        const provider = email.provider || item.dataset.provider;
        const threadId = email.threadId || item.dataset.threadId;
        const externalId = email.externalEmailId || item.dataset.externalId;

    if (provider) {
        const opened = openEmailInProvider(provider, threadId, externalId);
        if (opened) {
            return;
        }
        }

        // Fallback to original behavior if provider link unavailable
        closeInboxModal();
        const orgId = document.querySelector('[data-organization-id]')?.dataset.organizationId;
        loadInboxThread(email.threadId, orgId);
    });
    
    // Provider badge click handler to open in Gmail/Outlook
    const providerBadge = item.querySelector('.email-provider-badge');
    if (providerBadge) {
        providerBadge.addEventListener('click', (e) => {
            e.stopPropagation();
            const provider = providerBadge.dataset.provider;
            const threadId = providerBadge.dataset.threadId;
            const externalId = providerBadge.dataset.externalId;
            
            if (provider && (threadId || externalId)) {
                openEmailInProvider(provider, threadId, externalId);
            }
        });
    }
    
    // Star click handler
    const starEl = item.querySelector('.email-star');
    if (starEl) {
        starEl.addEventListener('click', (e) => {
            e.stopPropagation();
            const icon = starEl.querySelector('i');
            if (icon.classList.contains('far')) {
                icon.classList.remove('far');
                icon.classList.add('fas');
                starEl.classList.add('starred');
            } else {
                icon.classList.remove('fas');
                icon.classList.add('far');
                starEl.classList.remove('starred');
            }
        });
    }
    
    // Notalize click handler
    const notalizeEl = item.querySelector('.email-notalize');
    if (notalizeEl) {
        notalizeEl.addEventListener('click', async (e) => {
            e.stopPropagation();
            await notalizeEmail(email, notalizeEl);
        });
    }
    
    return item;
}

// Notalize email - Create DM thread and add email content as message
async function notalizeEmail(email, notalizeBtn = null) {
    try {
        const orgId = document.querySelector('[data-organization-id]')?.dataset.organizationId;
        if (!orgId) {
            alert('Organization ID not found.');
            return;
        }
        
        // Show loading indicator
        if (notalizeBtn) {
            notalizeBtn.style.opacity = '0.5';
            notalizeBtn.style.pointerEvents = 'none';
        }
        
        // Extract email body text (prefer bodyText, fallback to parsing body)
        let emailBodyText = email.bodyText || '';
        
        // If bodyText is empty or contains HTML tags, extract from HTML body instead
        if ((!emailBodyText || emailBodyText.includes('<') || emailBodyText.includes('>')) && email.body) {
            // Parse HTML to plain text
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = email.body;
            
            // Remove script, style, and other non-content elements
            const scripts = tempDiv.querySelectorAll('script, style, noscript, iframe, embed, object');
            scripts.forEach(el => el.remove());
            
            // Get text content (preserves newlines from block elements)
            emailBodyText = tempDiv.textContent || tempDiv.innerText || '';
        }
        
        // Preserve newlines while cleaning up whitespace (apply to both bodyText and parsed HTML)
        if (emailBodyText) {
            // Replace multiple spaces/tabs with single space (but preserve newlines)
            emailBodyText = emailBodyText.replace(/[ \t]+/g, ' ');
            // Replace multiple consecutive newlines with max 2 newlines (for paragraph breaks)
            emailBodyText = emailBodyText.replace(/\n{3,}/g, '\n\n');
            // Clean up spaces at start/end of lines
            emailBodyText = emailBodyText.replace(/[ \t]+\n/g, '\n').replace(/\n[ \t]+/g, '\n');
            emailBodyText = emailBodyText.trim();
        }
        
        // If we still don't have text, try to extract from HTML one more time
        if (!emailBodyText && email.body) {
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = email.body;
            const scripts = tempDiv.querySelectorAll('script, style, noscript, iframe, embed, object');
            scripts.forEach(el => el.remove());
            emailBodyText = (tempDiv.textContent || tempDiv.innerText || '').trim();
        }
        
        const request = {
            senderEmail: email.fromEmail,
            senderName: email.fromName || email.fromEmail,
            emailBody: email.body || '',
            emailBodyText: emailBodyText,
            subject: email.subject || '',
            provider: email.provider || '',
            externalEmailId: email.externalEmailId || '',
            emailThreadId: email.threadId || '',
            receivedAt: email.receivedAt || new Date().toISOString()
        };
        
        const response = await fetch(`/api/dm/notalize?orgId=${orgId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(request)
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            console.error('Notalize API error:', response.status, result);
            alert(result.error || `Failed to notalize email (${response.status})`);
            if (notalizeBtn) {
                notalizeBtn.style.opacity = '';
                notalizeBtn.style.pointerEvents = '';
            }
            return;
        }
        
        if (result.success && result.thread) {
            // Close inbox modal
            closeInboxModal();
            
            // Navigate to the thread
            const threadId = result.thread.id;
            const otherUserId = result.thread.otherUserId;
            const otherUserName = result.thread.otherUserName;
            const isExternalUser = result.thread.isExternalUser;
            const isNewThread = result.thread.isNewThread;
            
            // Add new thread to Direct Messages list in real-time
            const otherUserColor = result.thread.otherUserColor || (isExternalUser && isNewThread ? '#aaaaaa' : '#3d1019');
            addDirectMessageThreadToList(otherUserId, otherUserName, isExternalUser && isNewThread, otherUserColor);
            
            // Reload communications sidebar to show new user in Direct Messages list
            if (typeof loadCommsChannels === 'function') {
                console.log('Reloading communications sidebar after notalizing...');
                loadCommsChannels();
            }
            
            // Reload Direct Messages list on Communications page
            await reloadCommunicationsDirectMessagesList();
            
            // Use existing DM functions to open the thread
            if (typeof window.openDirectThread === 'function') {
                await window.openDirectThread(otherUserId, otherUserName);
            } else if (typeof window.joinDirectThread === 'function' && typeof window.loadDirectMessages === 'function') {
                await window.joinDirectThread(threadId);
                await window.loadDirectMessages(threadId);
                
                // Update header
                const channelTitle = document.querySelector('.channel-title');
                const channelDescription = document.querySelector('.channel-description');
                if (channelTitle) channelTitle.textContent = otherUserName || 'Direct Message';
                if (channelDescription) channelDescription.textContent = 'Private conversation';
            } else {
                // Fallback: reload page or show message
                console.log('DM functions not available, thread created:', threadId);
                alert(`Email notalized! Thread created with ${otherUserName || email.fromEmail}`);
            }
        } else {
            alert(result.error || 'Failed to notalize email');
        }
        
        // Restore button state
        if (notalizeBtn) {
            notalizeBtn.style.opacity = '';
            notalizeBtn.style.pointerEvents = '';
        }
    } catch (error) {
        console.error('Error notalizing email:', error);
        alert('Failed to notalize email. Please try again.');
        if (notalizeBtn) {
            notalizeBtn.style.opacity = '';
            notalizeBtn.style.pointerEvents = '';
        }
    }
}

// Add new Direct Message thread to the list in real-time
function addDirectMessageThreadToList(userId, userName, isExternalUser = false, userColor = null) {
    try {
        // Find the Direct Messages section
        const directMessagesTitle = Array.from(document.querySelectorAll('.team-title'))
            .find(el => el.textContent.includes('Direct Messages'));
        
        if (!directMessagesTitle) {
            console.warn('Direct Messages section not found');
            return;
        }
        
        const teamMembersContainer = directMessagesTitle.nextElementSibling;
        if (!teamMembersContainer || !teamMembersContainer.classList.contains('team-members')) {
            console.warn('Team members container not found');
            return;
        }
        
        // Check if user already exists in the list
        const existingItem = teamMembersContainer.querySelector(`[data-user-id="${userId}"]`);
        if (existingItem) {
            // User already in list, just highlight it
            existingItem.style.animation = 'pulse 0.5s ease-in-out';
            setTimeout(() => {
                existingItem.style.animation = '';
            }, 500);
            return;
        }
        
        // Get avatar initials
        const nameParts = userName.split(' ');
        const avatar = nameParts.length > 1 
            ? (nameParts[0].charAt(0) + nameParts[nameParts.length - 1].charAt(0)).toUpperCase()
            : userName.charAt(0).toUpperCase();
        
        // Create new team member item
        const newMemberItem = document.createElement('div');
        newMemberItem.className = 'team-member dm-user-item';
        newMemberItem.setAttribute('data-user-id', userId);
        newMemberItem.setAttribute('data-user-name', userName);
        newMemberItem.setAttribute('data-can-dm', 'true');
        newMemberItem.style.cursor = 'pointer';
        
        // Add data attribute for external user if this is a new external user
        if (isExternalUser) {
            newMemberItem.setAttribute('data-is-external', 'true');
        }
        
        // Create avatar with color from parameter or fallback
        let avatarBg = userColor || (isExternalUser ? '#aaaaaa' : '#3d1019');
        // Normalize #9ca3af to #aaaaaa for external contacts
        if (isExternalUser && avatarBg === '#9ca3af') {
            avatarBg = '#aaaaaa';
        }
        const avatarClass = isExternalUser ? 'member-avatar external-contacts-avatar' : 'member-avatar';
        
        // External users don't get status indicators
        const statusIndicatorHtml = isExternalUser ? '' : '<div class="status-indicator offline"></div>';
        
        newMemberItem.innerHTML = `
            <div class="${avatarClass}" style="background: ${avatarBg} !important;">
                <span>${avatar}</span>
                ${statusIndicatorHtml}
            </div>
            <div class="member-info">
                <div class="member-name-row">
                    <span class="member-name">${userName}</span>
                </div>
                <p class="member-activity">${isExternalUser ? 'External' : 'Email messages'}</p>
            </div>
        `;
        
        // No need for explicit click handler - event delegation in Communications.cshtml handles it
        // The .dm-user-item class and data attributes match the existing pattern
        
        // Insert after current user (if exists), otherwise at the beginning
        const currentUserItem = teamMembersContainer.querySelector('.current-user-avatar')?.closest('.team-member');
        if (currentUserItem && currentUserItem.nextSibling) {
            teamMembersContainer.insertBefore(newMemberItem, currentUserItem.nextSibling);
        } else {
            // Insert at the beginning of the list (after current user section)
            teamMembersContainer.insertBefore(newMemberItem, teamMembersContainer.firstChild);
        }
        
        // Add pulse animation to highlight the new item
        newMemberItem.style.animation = 'pulse 0.5s ease-in-out';
        setTimeout(() => {
            newMemberItem.style.animation = '';
        }, 500);
        
        // Update online count in title
        updateDirectMessagesOnlineCount();
        
    } catch (error) {
        console.error('Error adding DM thread to list:', error);
    }
}

// Reload Direct Messages list on Communications page
async function reloadCommunicationsDirectMessagesList() {
    try {
        // Get organization ID from the page
        const orgIdMatch = window.location.pathname.match(/\/Client\/(\d+)\/Communications/);
        if (!orgIdMatch) {
            console.warn('Organization ID not found in URL');
            return;
        }
        
        const orgId = orgIdMatch[1];
        
        // Fetch updated team members from API
        const response = await fetch(`/Client/${orgId}/Communications/GetChannelsJson`);
        if (!response.ok) {
            console.error('Failed to reload team members:', response.status);
            return;
        }
        
        const data = await response.json();
        if (!data.success || !data.teamMembers) {
            console.error('Invalid response from API');
            return;
        }
        
        // Find the Direct Messages section
        const directMessagesTitle = Array.from(document.querySelectorAll('.team-title'))
            .find(el => el.textContent.includes('Direct Messages'));
        
        if (!directMessagesTitle) {
            console.warn('Direct Messages section not found');
            return;
        }
        
        const teamMembersContainer = directMessagesTitle.nextElementSibling;
        if (!teamMembersContainer || !teamMembersContainer.classList.contains('team-members')) {
            console.warn('Team members container not found');
            return;
        }
        
        // Get current user ID from page
        let currentUserId = null;
        
        // Try from appContext first - THIS IS THE PRIMARY SOURCE
        const appContext = document.getElementById('appContext');
        if (appContext && appContext.dataset.currentUserId) {
            const userId = appContext.dataset.currentUserId;
            if (userId && userId !== "0" && userId !== "null" && userId !== "") {
                currentUserId = userId;
            }
        }
        
        // Try from global scope (set by Communications.cshtml)
        if (!currentUserId && typeof window.currentUserId !== 'undefined') {
            currentUserId = window.currentUserId;
        }
        
        // Try from data-current-user-id attribute (specific to current user)
        if (!currentUserId) {
            const userIdElement = document.querySelector('[data-current-user-id]');
            if (userIdElement && userIdElement.dataset.currentUserId) {
                const userId = userIdElement.dataset.currentUserId;
                if (userId && userId !== "0" && userId !== "null" && userId !== "") {
                    currentUserId = userId;
                }
            }
        }
        
        // Try to find current user by looking for .current-user-avatar within .team-member
        if (!currentUserId) {
            const currentUserElement = document.querySelector('.current-user-avatar')?.closest('.team-member');
            if (currentUserElement) {
                currentUserId = currentUserElement.getAttribute('data-user-id');
            }
        }
        
        // REMOVED DANGEROUS FALLBACK: querySelector('[data-user-id]') was picking up
        // any user element on the page, including client users in scoped views
        
        if (!currentUserId) {
            console.warn('Current user ID not found, cannot reload Direct Messages list');
            return;
        }
        
        // Sort team members with priority (same logic as backend)
        const sortedMembers = [...data.teamMembers].sort((a, b) => {
            // Current user first
            if (a.userId == currentUserId) return -1;
            if (b.userId == currentUserId) return 1;
            
            // Check if external contacts
            const aIsExternal = a.isExternalContacts || a.organizationName?.endsWith("'s External Contacts");
            const bIsExternal = b.isExternalContacts || b.organizationName?.endsWith("'s External Contacts");
            
            // Determine if from current org or relationship
            const aIsCurrentOrg = a.organizationId == orgId && !aIsExternal;
            const bIsCurrentOrg = b.organizationId == orgId && !bIsExternal;
            const aIsRelationship = a.organizationId != orgId && !aIsExternal;
            const bIsRelationship = b.organizationId != orgId && !bIsExternal;
            
            // Priority order: current org > relationship users > external contacts
            if (aIsCurrentOrg && !bIsCurrentOrg) return -1;
            if (!aIsCurrentOrg && bIsCurrentOrg) return 1;
            
            if (aIsRelationship && (!bIsRelationship || bIsExternal)) return -1;
            if (!aIsRelationship && bIsRelationship && !aIsExternal) return 1;
            
            if (!aIsExternal && bIsExternal) return -1;
            if (aIsExternal && !bIsExternal) return 1;
            
            // Then by online status
            if (a.status === 'online' && b.status !== 'online') return -1;
            if (a.status !== 'online' && b.status === 'online') return 1;
            
            // Then alphabetically by name
            return a.name.localeCompare(b.name);
        });
        
        // Clear existing members (except current user)
        const currentUserItem = teamMembersContainer.querySelector('.current-user-avatar')?.closest('.team-member');
        teamMembersContainer.innerHTML = '';
        
        // Re-add current user first if exists
        if (currentUserItem) {
            teamMembersContainer.appendChild(currentUserItem);
        }
        
        // Render all other members
        sortedMembers.forEach(member => {
            if (member.userId == currentUserId) return; // Skip current user, already added
            
            const avatar = member.avatar || member.name.charAt(0);
            const isExternalContacts = member.isExternalContacts || member.organizationName?.endsWith("'s External Contacts");
            let avatarColor = member.color || member.Color || (isExternalContacts ? '#aaaaaa' : '#3d1019');
            // Normalize #9ca3af to #aaaaaa for external contacts
            if (isExternalContacts && avatarColor === '#9ca3af') {
                avatarColor = '#aaaaaa';
            }
            
            const memberItem = document.createElement('div');
            memberItem.className = 'team-member dm-user-item';
            memberItem.setAttribute('data-user-id', member.userId);
            memberItem.setAttribute('data-user-name', member.name);
            memberItem.setAttribute('data-can-dm', member.canDirectMessage ? 'true' : 'false');
            memberItem.setAttribute('data-org-id', member.organizationId);
            memberItem.setAttribute('data-org-name', member.organizationName || '');
            if (isExternalContacts) {
                memberItem.setAttribute('data-is-external', 'true');
            }
            memberItem.style.cursor = member.canDirectMessage ? 'pointer' : 'not-allowed';
            if (!member.canDirectMessage) {
                memberItem.style.opacity = '0.6';
            }
            
            const avatarClass = isExternalContacts ? 'member-avatar external-contacts-avatar' : 'member-avatar';
            
            // Only show status indicator for non-external users
            const statusIndicatorHtml = isExternalContacts ? '' : `<div class="status-indicator ${member.status || 'offline'}"></div>`;
            // Set activity text - External for external contacts, otherwise use member.activity or derive from status
            const activityText = isExternalContacts ? 'External' : (member.activity || (member.status === 'online' ? 'Online' : 'Offline'));
            
            memberItem.innerHTML = `
                <div class="${avatarClass}" style="background: ${avatarColor} !important;">
                    <span>${avatar}</span>
                    ${statusIndicatorHtml}
                </div>
                <div class="member-info">
                    <div class="member-name-row">
                        <span class="member-name">${member.name}</span>
                    </div>
                    <p class="member-activity">${activityText}</p>
                </div>
            `;
            
            teamMembersContainer.appendChild(memberItem);
        });
        
        // Update online count in title
        updateDirectMessagesOnlineCount();
        
        console.log('Direct Messages list reloaded successfully');
        
    } catch (error) {
        console.error('Error reloading Direct Messages list:', error);
    }
}

// Update Direct Messages online count in title
function updateDirectMessagesOnlineCount() {
    const directMessagesTitle = Array.from(document.querySelectorAll('.team-title'))
        .find(el => el.textContent.includes('Direct Messages'));
    
    if (directMessagesTitle) {
        const teamMembers = directMessagesTitle.nextElementSibling;
        if (teamMembers && teamMembers.classList.contains('team-members')) {
            const onlineCount = teamMembers.querySelectorAll('.status-indicator.online').length;
            directMessagesTitle.textContent = `Direct Messages — ${onlineCount} Online`;
        }
    }
}

// Open email in Gmail or Outlook
function openEmailInProvider(provider, threadId, externalId) {
    if (!provider) return false;
    
    const normalized = provider.toLowerCase();
    let url = '';
    
    if (normalized === 'gmail' || normalized === 'google') {
        const id = threadId || externalId;
        if (id) {
            if (threadId) {
                url = `https://mail.google.com/mail/u/0/#inbox/${encodeURIComponent(threadId)}`;
            } else {
                url = `https://mail.google.com/mail/u/0/#all/${encodeURIComponent(externalId)}`;
            }
        }
    } else if (normalized === 'outlook' || normalized === 'microsoft') {
        const id = externalId || threadId;
        if (id) {
            url = `https://outlook.live.com/mail/0/#/view/${encodeURIComponent(id)}`;
        }
    }
    
    if (url) {
        window.open(url, '_blank', 'noopener,noreferrer');
        return true;
    }
    
    return false;
}

// Format email date
function formatEmailDate(dateString) {
    const date = new Date(dateString);
    
    // Format time (e.g., "4:30 PM")
    const time = date.toLocaleTimeString('en-US', { 
        hour: 'numeric', 
        minute: '2-digit',
        hour12: true 
    });
    
    // Format date (e.g., "11/3/2025")
    const dateStr = date.toLocaleDateString('en-US', { 
        month: 'numeric', 
        day: 'numeric',
        year: 'numeric'
    });
    
    // Return HTML with time on top and date on bottom
    return `<div style="display: flex; flex-direction: column; align-items: flex-end; line-height: 1.3;">
        <div style="font-size: 0.875rem;">${time}</div>
        <div style="font-size: 0.75rem; color: #9ca3af;">${dateStr}</div>
    </div>`;
}

// Close inbox modal
// Update inbox modal badge
async function updateInboxModalBadge() {
    try {
        const response = await fetch('/api/email-oauth/inbox/count');
        const result = await response.json();
        
        if (result.success) {
            const badge = document.getElementById('inboxModalUnreadBadge');
            if (badge) {
                if (result.unreadCount > 0) {
                    badge.textContent = result.unreadCount;
                    badge.style.display = 'inline-block';
                } else {
                    badge.style.display = 'none';
                }
            }
        }
    } catch (error) {
        console.error('Error updating inbox badge:', error);
    }
}

// Load inbox thread (Direct Message thread)
async function loadInboxThread(threadId, orgId) {
    try {
        // Set DM mode variables if available
        if (typeof window.isDirectMessageMode !== 'undefined') {
            window.isDirectMessageMode = true;
        }
        if (typeof window.currentDirectThreadId !== 'undefined') {
            window.currentDirectThreadId = threadId;
        }
        
        // Use joinDirectThread and loadDirectMessages if available
        if (typeof window.joinDirectThread === 'function' && typeof window.loadDirectMessages === 'function') {
            await window.joinDirectThread(threadId);
            await window.loadDirectMessages(threadId);
            
            // Update header
            const channelTitle = document.querySelector('.channel-title');
            const channelDescription = document.querySelector('.channel-description');
            if (channelTitle) channelTitle.textContent = 'Direct Message';
            if (channelDescription) channelDescription.textContent = 'Private conversation';
        } else if (typeof loadDirectMessages === 'function') {
            // Fallback: try direct load
            await loadDirectMessages(threadId);
        } else {
            // Final fallback: use communications.js displayMessages
            const response = await fetch(`/api/dm/threads/${threadId}/messages?orgId=${orgId}&take=50`);
            const result = await response.json();
            
            if (result.success && result.messages && typeof displayMessages === 'function') {
                displayMessages(result.messages);
            }
        }
    } catch (error) {
        console.error('Error loading inbox thread:', error);
        alert('Failed to load thread.');
    }
}

// Update inbox unread count
async function updateInboxCount() {
    try {
        const response = await fetch('/api/email-oauth/inbox/count');
        const result = await response.json();
        
        if (result.success) {
            const badge = document.getElementById('inboxUnreadBadge');
            if (badge) {
                if (result.unreadCount > 0) {
                    badge.textContent = result.unreadCount;
                    badge.style.display = 'inline-block';
                } else {
                    badge.style.display = 'none';
                }
            }
        }
    } catch (error) {
        console.error('Error updating inbox count:', error);
    }
}

// Initialize on page load
if (typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', function() {
        initializeEmailIntegration();
        
        // Adjust header for scrollbar on window resize
        window.addEventListener('resize', function() {
            const modal = document.getElementById('emailInboxModal');
            if (modal && modal.style.display !== 'none') {
                adjustEmailHeaderForScrollbar();
                adjustEmailModalPosition();
            }
        });
        
        // Listen for sidebar toggle events
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    const modal = document.getElementById('emailInboxModal');
                    if (modal && modal.style.display !== 'none') {
                        // Small delay to ensure CSS transitions complete
                        setTimeout(() => adjustEmailModalPosition(), 50);
                    }
                }
            });
        });
        
        // Observe body class changes (sidebar-hidden, chat-hidden)
        observer.observe(document.body, {
            attributes: true,
            attributeFilter: ['class']
        });
        
        // Also observe main-content-wrapper changes
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        if (mainContentWrapper) {
            observer.observe(mainContentWrapper, {
                attributes: true,
                attributeFilter: ['class', 'style']
            });
        }
        
        // Observe resize handle for position changes
        const resizeHandle = document.getElementById('resizeHandle');
        if (resizeHandle) {
            observer.observe(resizeHandle, {
                attributes: true,
                attributeFilter: ['style', 'class']
            });
            
            // Listen for resize handle position changes during drag
            let isResizing = false;
            let resizeTimeout;
            
            // Detect when resize starts
            resizeHandle.addEventListener('mousedown', function() {
                isResizing = true;
            });
            
            // Update modal position during resize (throttled)
            document.addEventListener('mousemove', function() {
                if (isResizing) {
                    const modal = document.getElementById('emailInboxModal');
                    if (modal && modal.style.display !== 'none') {
                        clearTimeout(resizeTimeout);
                        resizeTimeout = setTimeout(() => {
                            adjustEmailModalPosition();
                        }, 10); // Throttle updates during resize
                    }
                }
            });
            
            // Update modal position when resize completes
            document.addEventListener('mouseup', function() {
                if (isResizing) {
                    isResizing = false;
                    const modal = document.getElementById('emailInboxModal');
                    if (modal && modal.style.display !== 'none') {
                        setTimeout(() => {
                            adjustEmailModalPosition();
                        }, 50); // Small delay to ensure resize handle position is updated
                    }
                }
            });
        }
        
        // Listen for storage changes (when panel width is saved)
        window.addEventListener('storage', function(e) {
            if (e.key === 'chatPanelWidth' || e.key === 'notificationsPanelWidth') {
                const modal = document.getElementById('emailInboxModal');
                if (modal && modal.style.display !== 'none') {
                    adjustEmailModalPosition();
                }
            }
        });
        
        // Check if URL has openInbox parameter and open inbox automatically
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.get('openInbox') === 'true') {
            // Wait a bit for email integration to be fully initialized
            setTimeout(() => {
                console.log('Opening inbox from URL parameter');
                if (typeof openInbox === 'function') {
                    openInbox();
                }
                // Remove the parameter from URL without reloading
                const url = new URL(window.location);
                url.searchParams.delete('openInbox');
                window.history.replaceState({}, '', url);
            }, 500);
        }
    });
}


