// Direct Messaging System
let directConnection = null;
let currentDirectThreadId = null;
let currentDirectUserId = null;
let currentDirectUserName = null;
let currentDirectOrgId = null;
let currentDirectOtherUserId = null;
let currentDirectOtherUserName = null;
let currentDirectOtherUserEmail = null;
let directTypingTimeout = null;
let isDMTyping = false;
let isDirectMessageMode = false;

// Initialize direct messaging connection
async function initializeDirectMessaging(userId, organizationId, userName = null) {
    currentDirectUserId = userId;
    currentDirectOrgId = organizationId;
    if (userName) {
        currentDirectUserName = userName;
    } else {
        // Try to get from global context
        const appContext = document.getElementById('appContext');
        if (appContext && appContext.dataset.currentUserName) {
            currentDirectUserName = appContext.dataset.currentUserName;
        } else {
            currentDirectUserName = 'You';
        }
    }

    // Initialize SignalR connection
    if (typeof signalR === 'undefined') {
        console.warn('SignalR not available, direct messaging will work in limited mode');
        return;
    }

    try {
        directConnection = new signalR.HubConnectionBuilder()
            .withUrl(`/hubs/direct?orgId=${currentDirectOrgId}`)
            .withAutomaticReconnect()
            .build();

        // Set up event handlers
        setupDirectMessageHandlers();

        // Start connection
        await directConnection.start();
        console.log("Direct Messaging SignalR Connected");

    } catch (err) {
        console.error("Direct Messaging SignalR Connection Error: ", err);
    }
}

// Setup SignalR event handlers for DMs
function setupDirectMessageHandlers() {
    if (!directConnection) return;

    // Receive direct message (unique event name to avoid conflict with AI chat)
    directConnection.on("ReceiveDirectMessage", function (message) {
        // Only process if we're in direct message mode
        if (!isDirectMessageMode) {
            console.log('Ignoring direct message - not in DM mode');
            return;
        }
        
        // Check if message is for the current thread
        const messageThreadId = message.ThreadId || message.threadId;
        let normalizedMessageThreadId = messageThreadId ? messageThreadId.toString().toLowerCase().trim().replace(/[{}]/g, '') : null;
        let normalizedCurrentThreadId = currentDirectThreadId ? currentDirectThreadId.toString().toLowerCase().trim().replace(/[{}]/g, '') : null;
        
        if (normalizedMessageThreadId && normalizedCurrentThreadId && normalizedMessageThreadId !== normalizedCurrentThreadId) {
            console.log('Ignoring direct message - not for current thread:', normalizedMessageThreadId, 'vs', normalizedCurrentThreadId);
            return;
        }
        
        // Remove optimistic message if it exists (by checking if we have a temp message)
        const messagesContainer = document.querySelector('.messages-list');
        if (messagesContainer) {
            const tempMessage = messagesContainer.querySelector('[data-message-id^="temp-"]');
            if (tempMessage) {
                tempMessage.remove();
            }
        }
        
        appendDirectMessage(message);
        if (typeof scrollToBottom === 'function') {
            setTimeout(scrollToBottom, 100);
        }
        
        // Auto mark as read if thread is open
        if (messageThreadId === currentDirectThreadId || normalizedMessageThreadId === normalizedCurrentThreadId) {
            markDirectThreadAsRead(messageThreadId || currentDirectThreadId);
        }
    });

    // Handle user online/offline status
    directConnection.on("UserOnline", function (data) {
        console.log('User came online:', data.UserId);
        if (typeof updateUserStatus === 'function') {
            updateUserStatus(data.UserId, 'online');
        }
    });

    directConnection.on("UserOffline", function (data) {
        console.log('User went offline:', data.UserId);
        if (typeof updateUserStatus === 'function') {
            updateUserStatus(data.UserId, 'offline');
        }
    });

    // User typing indicator
    directConnection.on("UserTyping", function (data) {
        if (data.UserId !== currentDirectUserId && data.ThreadId === currentDirectThreadId) {
            if (data.IsTyping) {
                showDirectTypingIndicator();
            } else {
                hideDirectTypingIndicator();
            }
        }
    });

    // Read receipt
    directConnection.on("ReadReceipt", function (data) {
        if (data.ThreadId === currentDirectThreadId) {
            updateReadReceipt(data.UserId, data.ReadAt);
        }
    });

    // User joined
    directConnection.on("UserJoined", function (data) {
        console.log(`User joined DM thread ${data.ThreadId}`);
    });

    // Error handling
    directConnection.on("Error", function (message) {
        console.error("Direct Message Error:", message);
        showDirectMessageError(message);
    });
}

// Open or create a direct message thread with another user
async function openDirectThread(otherUserId, otherUserName, otherUserEmail = null) {
    try {
        isDirectMessageMode = true;
        currentDirectOtherUserId = otherUserId;
        currentDirectOtherUserName = otherUserName;
        currentDirectOtherUserEmail = otherUserEmail;
        
        // If email not provided, try to get it from the team member element
        if (!currentDirectOtherUserEmail) {
            const teamMemberElement = document.querySelector(`.team-member[data-user-id="${otherUserId}"]`);
            if (teamMemberElement) {
                currentDirectOtherUserEmail = teamMemberElement.dataset.userEmail || null;
            }
        }
        
        // Update UI to DM mode
        updateUIForDirectMessage(otherUserName);
        
        const response = await fetch(`/api/dm/threads?orgId=${currentDirectOrgId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ OtherUserId: parseInt(otherUserId) })
        });

        const result = await response.json();
        
        if (result.success && result.thread) {
            await joinDirectThread(result.thread.id);
            await loadDirectMessages(result.thread.id);
        } else {
            console.error("Failed to open direct thread:", result.error);
            showDirectMessageError(result.error || "Failed to open conversation");
        }
    } catch (error) {
        console.error("Error opening direct thread:", error);
        showDirectMessageError("Failed to open conversation");
    }
}

// Join a direct message thread
async function joinDirectThread(threadId) {
    if (!directConnection) {
        console.error("Direct messaging not initialized");
        return;
    }

    try {
        // Leave current thread if any
        if (currentDirectThreadId) {
            await directConnection.invoke("LeaveThread", currentDirectThreadId);
        }

        // Join new thread
        currentDirectThreadId = threadId;
        await directConnection.invoke("JoinThread", threadId);
        
        console.log(`Joined DM thread ${threadId}`);
    } catch (error) {
        console.error("Error joining DM thread:", error);
    }
}

// Load direct messages for a thread
async function loadDirectMessages(threadId, cursor = null) {
    try {
        let url = `/api/dm/threads/${threadId}/messages?orgId=${currentDirectOrgId}&take=50`;
        if (cursor) {
            url += `&cursor=${cursor}`;
        }

        const response = await fetch(url);
        const result = await response.json();

        if (result.success && result.messages) {
            displayDirectMessages(result.messages);
            
            // Mark as read
            await markDirectThreadAsRead(threadId);
            
            // Scroll to bottom
            if (typeof scrollToBottom === 'function') {
                setTimeout(scrollToBottom, 100);
            }
            
            return result;
        } else {
            console.error("Failed to load messages:", result.error);
        }
    } catch (error) {
        console.error("Error loading direct messages:", error);
    }
}

// List all direct message threads
async function loadDirectThreads(cursor = null) {
    try {
        let url = `/api/dm/threads?orgId=${currentDirectOrgId}&take=30`;
        if (cursor) {
            url += `&cursor=${cursor}`;
        }

        const response = await fetch(url);
        const result = await response.json();

        if (result.success && result.threads) {
            displayDirectThreads(result.threads);
            return result;
        } else {
            console.error("Failed to load threads:", result.error);
        }
    } catch (error) {
        console.error("Error loading direct threads:", error);
    }
}

// Send a direct message
async function sendDirectMessage(body, messageType = 'Text') {
    if (!currentDirectThreadId || !directConnection) {
        console.error("No active DM thread or connection");
        return;
    }

    // Clear input immediately for better UX
        const messageInput = document.getElementById('messageInput');
        if (messageInput) {
            messageInput.value = '';
            messageInput.style.height = 'auto';
        }

    try {
        // Get current user name if not set
        if (!currentDirectUserName) {
            const appContext = document.getElementById('appContext');
            if (appContext && appContext.dataset.currentUserName) {
                currentDirectUserName = appContext.dataset.currentUserName;
            } else {
                currentDirectUserName = 'You';
            }
        }
        
        // Create optimistic message for immediate display
        const optimisticMessage = {
            id: 'temp-' + Date.now(),
            senderId: currentDirectUserId,
            senderName: currentDirectUserName,
            body: body,
            content: body,
            createdAt: new Date().toISOString(),
            CreatedAt: new Date().toISOString(),
            Time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        };
        
        // Append optimistic message immediately
        appendDirectMessage(optimisticMessage);
        setTimeout(() => {
            const messagesContainer = document.querySelector('.messages-container');
            if (messagesContainer) {
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }
        }, 100);
        
        await directConnection.invoke("SendMessage", currentDirectThreadId, body, messageType);
        
        // Stop typing indicator
        await sendDirectTypingIndicator(false);
        
    } catch (error) {
        console.error("Error sending direct message:", error);
        showDirectMessageError("Failed to send message");
        
        // Remove optimistic message if it exists
        const messagesContainer = document.querySelector('.messages-list');
        if (messagesContainer) {
            const tempMessage = messagesContainer.querySelector('[data-message-id^="temp-"]');
            if (tempMessage) {
                tempMessage.remove();
            }
        }
        
        // Restore message in input on error
        if (messageInput) {
            messageInput.value = body;
        }
    }
}

// Send typing indicator
async function sendDirectTypingIndicator(isTyping) {
    if (!currentDirectThreadId || !directConnection) return;

    try {
        await directConnection.invoke("Typing", currentDirectThreadId, isTyping);
        isDMTyping = isTyping;
    } catch (error) {
        console.error("Error sending typing indicator:", error);
    }
}

// Handle typing in DM input
function handleDirectMessageTyping() {
    if (!isDMTyping) {
        sendDirectTypingIndicator(true);
    }

    // Clear existing timeout
    if (directTypingTimeout) {
        clearTimeout(directTypingTimeout);
    }

    // Set new timeout to stop typing indicator
    directTypingTimeout = setTimeout(async () => {
        await sendDirectTypingIndicator(false);
    }, 3000);
}

// Mark thread as read
async function markDirectThreadAsRead(threadId) {
    try {
        await fetch(`/api/dm/threads/${threadId}/read?orgId=${currentDirectOrgId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ readAt: new Date().toISOString() })
        });
    } catch (error) {
        console.error("Error marking thread as read:", error);
    }
}

// UI Helper Functions

function updateUIForDirectMessage(otherUserName) {
    // Update header to show DM mode
    const channelTitle = document.querySelector('.channel-title');
    const channelDescription = document.querySelector('.channel-description');
    const channelHeaderIcon = document.querySelector('.channel-header-icon');
    
    if (channelTitle) {
        // Show "(You)" if messaging yourself
        const displayName = currentDirectOtherUserId === currentDirectUserId ? 
            `${otherUserName} (You)` : otherUserName;
        channelTitle.textContent = displayName;
    }
    if (channelDescription) {
        channelDescription.textContent = 'Direct Message';
    }
    if (channelHeaderIcon) {
        channelHeaderIcon.className = 'fas fa-user channel-header-icon';
    }
    
    // Update message input placeholder
    const messageInput = document.getElementById('messageInput');
    if (messageInput) {
        const placeholderName = currentDirectOtherUserId === currentDirectUserId ? 
            'yourself' : otherUserName;
        messageInput.placeholder = `Message ${placeholderName}`;
    }
    
    // Clear messages container
    const messagesContainer = document.querySelector('.messages-list');
    if (messagesContainer) {
        messagesContainer.innerHTML = '';
    }
    
    // Remove active state from all channels
    document.querySelectorAll('.channel-item').forEach(ch => ch.classList.remove('active'));
}

function displayDirectMessages(messages) {
    // Use the existing messages-list container
    const messagesContainer = document.querySelector('.messages-list');
    if (!messagesContainer) {
        console.error("Messages container not found");
        return;
    }

    messagesContainer.innerHTML = '';

    // Messages come newest first from API, reverse for display
    messages.reverse().forEach((message, index) => {
        const previousMessage = index > 0 ? messages[index - 1] : null;
        appendDirectMessage(message, previousMessage);
    });
}

function appendDirectMessage(message, previousMessage = null) {
    // Double-check: Only append if we're in DM mode
    if (!isDirectMessageMode) {
        console.log('appendDirectMessage: Ignoring - not in DM mode');
        return;
    }
    
    // Find the messages container - ONLY target the main chat area (Communications page)
    // The Communications page uses.main-chat.messages-list, sidebar uses #commsMessagesList
    // We want to avoid appending to sidebar accidentally
    const messagesContainer = document.querySelector('.main-chat .messages-list');
    
    if (!messagesContainer) {
        // If main chat container doesn't exist, this might be a different page context
        // Don't append to sidebar container - that's handled by communications-sidebar.js
        console.log('appendDirectMessage: Main chat container not found, skipping (might be sidebar context)');
        return;
    }
    
    // Double-check: Make sure we're not in sidebar
    if (messagesContainer.closest('#commsSidebarPanel')) {
        console.log('appendDirectMessage: Container is in sidebar, skipping (should be handled by sidebar script)');
        return;
    }

    // Check if message already exists (prevent duplicates from SignalR and optimistic updates)
    const messageId = message.id || message.Id || message.Id;
    if (messageId && !messageId.toString().startsWith('temp-')) {
        const existingMessage = messagesContainer.querySelector(`[data-message-id="${messageId}"]`);
        if (existingMessage) {
            console.log('Message already exists, skipping duplicate:', messageId);
            return;
        }
    }

    // Get previous message from DOM if not provided
    if (!previousMessage && messagesContainer.children.length > 0) {
        const lastMessageElement = messagesContainer.children[messagesContainer.children.length - 1];
        if (lastMessageElement && lastMessageElement.dataset && lastMessageElement.dataset.messageId) {
            // Try to extract previous message data from DOM
            const prevSenderId = lastMessageElement.dataset.senderId;
            const prevCreatedAt = lastMessageElement.dataset.createdAt;
            if (prevSenderId && prevCreatedAt) {
                previousMessage = {
                    senderId: prevSenderId,
                    createdAt: prevCreatedAt
                };
            }
        }
    }

    // Check if we should group this message with the previous one
    let isGrouped = false;
    if (previousMessage) {
        const isSameSender = parseInt(message.senderId || message.SenderId) === parseInt(previousMessage.senderId || previousMessage.SenderId);
        if (isSameSender) {
            const currentTime = new Date(message.createdAt || message.CreatedAt);
            const previousTime = new Date(previousMessage.createdAt || previousMessage.CreatedAt);
            const diffInMinutes = (currentTime - previousTime) / (1000 * 60);
            isGrouped = diffInMinutes <= 15;
        }
    }

    const isOwnMessage = parseInt(message.senderId || message.SenderId) === parseInt(currentDirectUserId);
    const messageElement = document.createElement('div');
    messageElement.className = 'message-item';
    if (isOwnMessage) {
        messageElement.classList.add('current-user-message');
    }
    if (isGrouped) {
        messageElement.classList.add('grouped-message');
    }
    messageElement.dataset.messageId = message.id || message.Id || 'temp-' + Date.now();
    messageElement.dataset.senderId = message.senderId || message.SenderId;
    messageElement.dataset.createdAt = message.createdAt || message.CreatedAt;

    // Get user initials for avatar
    const initials = message.senderName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();

    // Get sender color and check if external contacts
    const isExternalContacts = message.isExternalContacts || false;
    let senderColor = message.senderColor || (isExternalContacts ? '#aaaaaa' : '#3d1019');
    // Normalize #9ca3af to #aaaaaa for external contacts
    if (isExternalContacts && senderColor === '#9ca3af') {
        senderColor = '#aaaaaa';
    }
    
    // Build avatar style with color - don't apply inline style for current user (let CSS gradient handle it)
    let avatarStyle = '';
    let avatarClass = isExternalContacts ? 'message-avatar external-contacts-avatar' : 'message-avatar';
    
    if (!isOwnMessage) {
        // Only apply inline color for non-current-user messages
        avatarStyle = `background: ${senderColor} !important;`;
    }
    // For current user, CSS will apply the gradient via.message-item.current-user-message.message-avatar

    const { dateStr, timeStr } = formatTimestamp(message.createdAt);

    const messageType = (message.messageType || message.MessageType || '').toLowerCase();
    const subjectLine = message.emailSubject || message.EmailSubject || '';
    let bodyText = message.body || message.Body || '';

    // For email messages, ensure subject is prepended if not already present
    if (messageType === 'email') {
        const subjectText = subjectLine ? String(subjectLine).trim() : '';
        if (subjectText) {
            const trimmedBodyStart = bodyText ? bodyText.trimStart() : '';
            if (!trimmedBodyStart.startsWith(subjectText)) {
                bodyText = `${subjectText}\n\n${bodyText}`.trim();
            }
        }
    }

    // If this is a Change Notice DM sync message, render Confirm/Clarify as buttons
    // and hide the raw URLs (tokens are too noisy to show inline).
    function extractChangeNoticeActionUrls(text) {
        if (!text) return { confirmUrl: null, clarifyUrl: null, cleanedText: text };

        let confirmUrl = null;
        let clarifyUrl = null;

        const lines = String(text).replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
        const kept = [];

        for (const rawLine of lines) {
            const line = rawLine || '';
            const trimmed = line.trim();

            let m = trimmed.match(/^Confirm:\s*(\S+)\s*$/i);
            if (m && m[1]) {
                confirmUrl = m[1];
                continue; // drop this line
            }

            m = trimmed.match(/^Needs\s+clarification:\s*(\S+)\s*$/i);
            if (m && m[1]) {
                clarifyUrl = m[1];
                continue; // drop this line
            }

            // Also drop older label variants if they ever appear
            m = trimmed.match(/^Needs\s+Clarification:\s*(\S+)\s*$/i);
            if (m && m[1]) {
                clarifyUrl = m[1];
                continue;
            }

            kept.push(line);
        }

        // Only treat as Change Notice if links look like public response URLs
        const looksLikeChangeNotice =
            (confirmUrl && confirmUrl.includes('/public/change-notice/respond')) ||
            (clarifyUrl && clarifyUrl.includes('/public/change-notice/respond'));

        if (!looksLikeChangeNotice) {
            return { confirmUrl: null, clarifyUrl: null, cleanedText: text };
        }

        // Clean up: remove trailing blank lines
        while (kept.length > 0 && kept[kept.length - 1].trim() === '') {
            kept.pop();
        }

        return { confirmUrl, clarifyUrl, cleanedText: kept.join('\n').trim() };
    }

    function safeUrl(url) {
        if (!url) return null;
        const u = String(url).trim();
        // Only allow http/https URLs.
        if (!/^https?:\/\//i.test(u)) return null;
        return u;
    }

    // Normalize line endings and escape HTML
    const extracted = (messageType === 'email')
        ? extractChangeNoticeActionUrls(bodyText)
        : { confirmUrl: null, clarifyUrl: null, cleanedText: bodyText };

    let normalizedText = (extracted.cleanedText || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');

    // Escape HTML to prevent XSS
    const escapedText = escapeHtml(normalizedText);

    // Convert newlines to <br> tags for proper rendering
    // First, replace sequences of 2+ newlines with double <br> (paragraph breaks)
    let withBreaks = escapedText.replace(/\n{2,}/g, '<br><br>');
    // Then replace remaining single newlines with single <br>
    withBreaks = withBreaks.replace(/\n/g, '<br>');

    const confirmUrlSafe = safeUrl(extracted.confirmUrl);
    const clarifyUrlSafe = safeUrl(extracted.clarifyUrl);
    const hasActionButtons = !!(confirmUrlSafe || clarifyUrlSafe);
    const actionsHtml = hasActionButtons ? `
        <div class="dm-change-notice-actions" style="margin-top: 10px; display: flex; gap: 8px; flex-wrap: wrap;">
            ${confirmUrlSafe ? `<a class="btn btn-sm btn-primary" href="${escapeHtml(confirmUrlSafe)}" target="_blank" rel="noopener noreferrer" style="border-radius: 8px; font-weight: 600;">Confirm</a>` : ``}
            ${clarifyUrlSafe ? `<a class="btn btn-sm btn-outline-secondary" href="${escapeHtml(clarifyUrlSafe)}" target="_blank" rel="noopener noreferrer" style="border-radius: 8px; font-weight: 600;">Needs clarification</a>` : ``}
        </div>
    ` : '';
    
    // Check if message is an email
    const isEmailMessage = messageType === 'email';
    const badgeContainerClass = isOwnMessage ? 'task-matter-badge task-matter-badge-right' : 'task-matter-badge';
    const emailBadgeHtml = isEmailMessage ? `
        <div class="${badgeContainerClass}">
            <span class="badge matter-priority-badge task-priority-badge priority-medium" style="font-size: 0.625rem; padding: 0.25rem 0.5rem; border-radius: 12px; font-weight: 600; background-color: #e3f2fd; color: #1565c0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;">
                Sent via email
            </span>
        </div>
    ` : '';

    // Format timestamp for hover (only show time)
    const hoverDate = new Date(message.createdAt);
    const hoverHours = hoverDate.getHours();
    const hoverMinutes = hoverDate.getMinutes();
    const hoverAmpm = hoverHours >= 12 ? 'PM' : 'AM';
    const hoverDisplayHours = hoverHours % 12 || 12;
    const hoverDisplayMinutes = hoverMinutes < 10 ? '0' + hoverMinutes : hoverMinutes;
    const hoverTimestamp = `${hoverDisplayHours}:${hoverDisplayMinutes} ${hoverAmpm}`;

    messageElement.innerHTML = `
        <div class="${avatarClass}"${avatarStyle ? ` style="${avatarStyle}"` : ''}>
            <span>${initials}</span>
        </div>
        <div class="message-content">
            ${emailBadgeHtml}
            <div class="message-header">
                <span class="message-author">${escapeHtml(message.senderName)}</span>
                <span class="message-time">
                    ${dateStr ? `<span class="message-date">${dateStr}</span>` : ''}
                    <span class="message-time-value">${timeStr}</span>
                </span>
            </div>
            <p class="message-text">${withBreaks}</p>
            ${actionsHtml}
        </div>
        ${isGrouped ? `<div class="hover-timestamp">${hoverTimestamp}</div>` : ''}
    `;

    messagesContainer.appendChild(messageElement);
}

function showDirectTypingIndicator(userName) {
    // Could add typing indicator in the future
    console.log(`${userName} is typing...`);
}

function hideDirectTypingIndicator() {
    // Could hide typing indicator in the future
}

function updateReadReceipt(userId, readAt) {
    // Update UI to show message was read
    // Could add checkmarks or "Seen" indicators here
    console.log(`User ${userId} read messages up to ${readAt}`);
}

function showDirectMessageError(message) {
    // Show error notification
    if (typeof showNotification === 'function') {
        showNotification(message, 'error');
    } else {
        console.error("DM Error:", message);
    }
}

function formatTimestamp(timestamp) {
    if (!timestamp) return { dateStr: '', timeStr: '' };
    
    const date = new Date(timestamp);
    const now = new Date();
    
    // Check if message is from today
    const isToday = date.toDateString() === now.toDateString();
    
    // Format time
    const hours = date.getHours();
    const minutes = date.getMinutes();
    const ampm = hours >= 12 ? 'PM' : 'AM';
    const displayHours = hours % 12 || 12;
    const displayMinutes = minutes < 10 ? '0' + minutes : minutes;
    const timeStr = `${displayHours}:${displayMinutes} ${ampm}`;
    
    // Format date
    const dateStr = isToday ? '' : date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    
    return { dateStr, timeStr };
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Exit DM mode (called when switching back to channels)
function exitDirectMessageMode() {
    // Store thread ID before clearing it
    const threadIdToLeave = currentDirectThreadId;
    
    // Clear state
    isDirectMessageMode = false;
    currentDirectThreadId = null;
    currentDirectOtherUserId = null;
    currentDirectOtherUserName = null;
    currentDirectOtherUserEmail = null;
    
    // Reset UI to channel mode
    const messageInput = document.getElementById('messageInput');
    const commsMessageInput = document.getElementById('commsMessageInput');
    
    if (messageInput) {
        // Reset placeholder - try to get active channel name
        const channelTitle = document.querySelector('.channel-title');
        if (channelTitle) {
            const channelName = channelTitle.textContent.trim();
            messageInput.placeholder = `Message #${channelName}`;
        } else {
            messageInput.placeholder = 'Type a message...';
        }
    }
    
    if (commsMessageInput) {
        // Reset placeholder for communications sidebar
        const commsChannelTitle = document.getElementById('commsChannelTitle');
        if (commsChannelTitle) {
            const channelName = commsChannelTitle.textContent.trim();
            commsMessageInput.placeholder = `Message #${channelName}`;
        } else {
            commsMessageInput.placeholder = 'Type a message...';
        }
    }
    
    // Leave SignalR thread if connected
    if (directConnection && directConnection.state === signalR.HubConnectionState.Connected && threadIdToLeave) {
        directConnection.invoke("LeaveThread", threadIdToLeave).catch(err => {
            console.error('Error leaving thread:', err);
        });
    }
}

// Export function to check if in DM mode
function isInDirectMessageMode() {
    return isDirectMessageMode;
}

// Export function to send message (used by shared send handler)
function sendCurrentDirectMessage(message) {
    if (isDirectMessageMode && currentDirectThreadId) {
        sendDirectMessage(message);
        return true;
    }
    return false;
}

// Export functions for use in other scripts
window.exitDirectMessageMode = exitDirectMessageMode;
window.openDirectThread = openDirectThread;

// Export getter functions for use in Communications.cshtml
window.getCurrentDirectOtherUserName = () => currentDirectOtherUserName;
window.getCurrentDirectOtherUserEmail = () => currentDirectOtherUserEmail;
window.getCurrentDirectOtherUserId = () => currentDirectOtherUserId;
window.getCurrentDirectThreadId = () => currentDirectThreadId;

