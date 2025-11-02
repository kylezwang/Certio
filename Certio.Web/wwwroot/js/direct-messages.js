// Direct Messaging System
let directConnection = null;
let currentDirectThreadId = null;
let currentDirectUserId = null;
let currentDirectOrgId = null;
let currentDirectOtherUserId = null;
let currentDirectOtherUserName = null;
let directTypingTimeout = null;
let isDMTyping = false;
let isDirectMessageMode = false;

// Initialize direct messaging connection
async function initializeDirectMessaging(userId, organizationId) {
    currentDirectUserId = userId;
    currentDirectOrgId = organizationId;

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
        appendDirectMessage(message);
        if (typeof scrollToBottom === 'function') {
            setTimeout(scrollToBottom, 100);
        }
        
        // Auto mark as read if thread is open
        if (message.ThreadId === currentDirectThreadId) {
            markDirectThreadAsRead(message.ThreadId);
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
async function openDirectThread(otherUserId, otherUserName) {
    try {
        isDirectMessageMode = true;
        currentDirectOtherUserId = otherUserId;
        currentDirectOtherUserName = otherUserName;
        
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

    try {
        await directConnection.invoke("SendMessage", currentDirectThreadId, body, messageType);
        
        // Clear input - use the shared message input
        const messageInput = document.getElementById('messageInput');
        if (messageInput) {
            messageInput.value = '';
            messageInput.style.height = 'auto';
        }
        
        // Stop typing indicator
        await sendDirectTypingIndicator(false);
        
    } catch (error) {
        console.error("Error sending direct message:", error);
        showDirectMessageError("Failed to send message");
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
    // Use the existing messages-list container
    const messagesContainer = document.querySelector('.messages-list');
    if (!messagesContainer) return;

    // Check if we should group this message with the previous one
    let isGrouped = false;
    if (previousMessage) {
        const isSameSender = parseInt(message.senderId) === parseInt(previousMessage.senderId);
        if (isSameSender) {
            const currentTime = new Date(message.createdAt);
            const previousTime = new Date(previousMessage.createdAt);
            const diffInMinutes = (currentTime - previousTime) / (1000 * 60);
            isGrouped = diffInMinutes <= 15;
        }
    }

    const isOwnMessage = parseInt(message.senderId) === parseInt(currentDirectUserId);
    const messageElement = document.createElement('div');
    messageElement.className = 'message-item';
    if (isOwnMessage) {
        messageElement.classList.add('current-user-message');
    }
    if (isGrouped) {
        messageElement.classList.add('grouped-message');
    }
    messageElement.dataset.messageId = message.id;

    // Get user initials for avatar
    const initials = message.senderName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();

    // Get sender color and check if external contacts
    const isExternalContacts = message.isExternalContacts || false;
    const senderColor = message.senderColor || (isExternalContacts ? '#9ca3af' : '#3d1019');
    
    // Build avatar style with color - don't apply inline style for current user (let CSS gradient handle it)
    let avatarStyle = '';
    let avatarClass = isExternalContacts ? 'message-avatar external-contacts-avatar' : 'message-avatar';
    
    if (!isOwnMessage) {
        // Only apply inline color for non-current-user messages
        avatarStyle = `background: ${senderColor} !important;`;
    }
    // For current user, CSS will apply the gradient via .message-item.current-user-message .message-avatar

    const timestamp = formatTimestamp(message.createdAt);
    
    messageElement.innerHTML = `
        <div class="${avatarClass}"${avatarStyle ? ` style="${avatarStyle}"` : ''}>
            <span>${initials}</span>
        </div>
        <div class="message-content">
            <div class="message-header">
                <span class="message-author">${escapeHtml(message.senderName)}</span>
                <span class="message-time">${timestamp}</span>
            </div>
            <p class="message-text">${escapeHtml(message.body || '')}</p>
        </div>
        ${isGrouped ? `<div class="hover-timestamp">${timestamp}</div>` : ''}
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
    if (!timestamp) return '';
    
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffMins < 1440) return `${Math.floor(diffMins / 60)}h ago`;
    
    return date.toLocaleDateString();
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
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

