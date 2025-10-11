// Communications Real-Time Chat System
let communicationsConnection = null;
let currentChannelId = null;
let currentUserId = null;
let currentUserName = null;
let communicationsOrganizationId = null;
let typingTimeout = null;
let isTyping = false;

// Initialize communications chat
async function initializeCommunicationsChat(channelId, userId, userName, organizationId) {
    currentChannelId = channelId;
    currentUserId = userId;
    currentUserName = userName;
    communicationsOrganizationId = organizationId;

    // Initialize SignalR connection
    if (typeof signalR === 'undefined') {
        console.warn('SignalR not available, chat will work in limited mode');
        return;
    }

    try {
        communicationsConnection = new signalR.HubConnectionBuilder()
            .withUrl(`/hubs/chat?orgId=${communicationsOrganizationId}`)
            .withAutomaticReconnect()
            .build();

        // Set up event handlers
        setupSignalRHandlers();

        // Start connection
        await communicationsConnection.start();
        console.log("Communications SignalR Connected");

        // Join the current channel
        if (currentChannelId) {
            await joinChannel(currentChannelId);
        }

        // Load channel messages
        await loadChannelMessages(currentChannelId);

    } catch (err) {
        console.error("SignalR Connection Error: ", err);
    }
}

// Setup SignalR event handlers
function setupSignalRHandlers() {
    if (!communicationsConnection) return;

    // Receive channel message
    communicationsConnection.on("ReceiveChannelMessage", function (message) {
        appendMessage(message);
        scrollToBottom();
    });

    // Receive regular message
    communicationsConnection.on("ReceiveMessage", function (message) {
        appendMessage(message);
        scrollToBottom();
    });

    // User typing
    communicationsConnection.on("UserTyping", function (data) {
        if (data.UserId !== currentUserId) {
            showTypingIndicator(data.UserName);
        }
    });

    // User stopped typing
    communicationsConnection.on("UserStoppedTyping", function (data) {
        hideTypingIndicator(data.UserName);
    });

    // User joined channel
    communicationsConnection.on("UserJoinedChannel", function (data) {
        console.log(`${data.UserName} joined the channel`);
        updateOnlineUsers();
    });

    // User left channel
    communicationsConnection.on("UserLeftChannel", function (data) {
        console.log(`${data.UserName} left the channel`);
        updateOnlineUsers();
    });

    // User online
    communicationsConnection.on("UserOnline", function (data) {
        updateUserStatus(data.UserId, 'online');
    });

    // User offline
    communicationsConnection.on("UserOffline", function (data) {
        updateUserStatus(data.UserId, 'offline');
    });

    // Message edited
    communicationsConnection.on("MessageEdited", function (data) {
        updateEditedMessage(data.Id, data.Content, data.EditedAt);
    });

    // Reaction added
    communicationsConnection.on("ReactionAdded", function (data) {
        addReactionToMessage(data.MessageId, data.Emoji, data.UserId);
    });

    // Reaction removed
    communicationsConnection.on("ReactionRemoved", function (data) {
        removeReactionFromMessage(data.MessageId, data.Emoji, data.UserId);
    });

    // Error handling
    communicationsConnection.on("Error", function (error) {
        console.error("SignalR Error:", error);
        showNotification(error, 'error');
    });

    // Connection state handling
    communicationsConnection.onreconnecting((error) => {
        console.log('Reconnecting...', error);
        showNotification('Connection lost, reconnecting...', 'warning');
    });

    communicationsConnection.onreconnected((connectionId) => {
        console.log('Reconnected:', connectionId);
        showNotification('Reconnected successfully', 'success');
        if (currentChannelId) {
            joinChannel(currentChannelId);
        }
    });

    communicationsConnection.onclose((error) => {
        console.log('Connection closed', error);
        showNotification('Connection closed', 'error');
    });
}

// Join a channel
async function joinChannel(channelId) {
    if (!communicationsConnection || communicationsConnection.state !== signalR.HubConnectionState.Connected) {
        console.warn('Cannot join channel, connection not ready');
        return;
    }

    try {
        // Leave current channel if any
        if (currentChannelId && currentChannelId !== channelId) {
            await communicationsConnection.invoke("LeaveChannel", currentChannelId.toString());
        }

        // Join new channel
        await communicationsConnection.invoke("JoinChannel", channelId.toString());
        currentChannelId = channelId;
        console.log(`Joined channel ${channelId}`);

        // Get online users
        await getOnlineUsers(channelId);
    } catch (err) {
        console.error("Error joining channel:", err);
    }
}

// Load channel messages with lazy loading (Discord/Slack style)
let loadingOlderMessages = false;
let hasMoreMessages = true;
let oldestMessageId = null;

async function loadChannelMessages(channelId, loadOlder = false) {
    if (!channelId) return;
    
    // Prevent duplicate requests
    if (loadOlder && loadingOlderMessages) return;

    try {
        if (loadOlder) {
            loadingOlderMessages = true;
        }
        
        // Build URL with pagination if loading older messages
        let url = `/api/chat/channel/${channelId}/messages`;
        if (loadOlder && oldestMessageId) {
            url += `?before=${oldestMessageId}&limit=50`;
        }
        
        const response = await fetch(url);
        
        if (!response.ok) {
            throw new Error('Failed to load messages');
        }

        const messages = await response.json();
        
        // Check if we have more messages to load
        hasMoreMessages = messages.length >= 50;
        
        if (loadOlder) {
            // Prepend older messages to existing ones
            prependMessages(messages);
        } else {
            // Initial load - display all messages
            displayMessages(messages);
        }
        
        // Update oldest message ID for pagination
        if (messages.length > 0) {
            oldestMessageId = messages[0].Id || messages[0].id;
        }
        
    } catch (err) {
        console.error("Error loading messages:", err);
    } finally {
        if (loadOlder) {
            loadingOlderMessages = false;
        }
    }
}

// Prepend older messages (for lazy loading)
function prependMessages(messages) {
    const messagesContainer = document.querySelector('.messages-list');
    if (!messagesContainer) return;
    
    // Save current scroll position
    const scrollContainer = document.querySelector('.messages-container');
    const oldScrollHeight = scrollContainer?.scrollHeight || 0;
    
    messages.forEach(message => {
        const messageElement = createMessageElement(message);
        messagesContainer.insertBefore(messageElement, messagesContainer.firstChild);
    });
    
    // Restore scroll position (keep user at same visual position)
    if (scrollContainer) {
        const newScrollHeight = scrollContainer.scrollHeight;
        scrollContainer.scrollTop = newScrollHeight - oldScrollHeight;
    }
}

// Display messages
function displayMessages(messages) {
    const messagesContainer = document.querySelector('.messages-list');
    if (!messagesContainer) {
        console.error('Messages container not found in displayMessages');
        return;
    }

    // Don't clear if we're showing sample messages and no real messages are loaded
    if (messages && messages.length > 0) {
        // Check if we already have messages displayed (from server-side rendering)
        const existingMessages = messagesContainer.querySelectorAll('.message-item');
        const hasExistingMessages = existingMessages.length > 0;
        
        if (hasExistingMessages) {
            // Only clear if we have more messages from API than from server-side rendering
            if (messages.length > existingMessages.length) {
                messagesContainer.innerHTML = '';
            } else {
                return;
            }
        } else {
            messagesContainer.innerHTML = '';
        }
        
        messages.forEach(message => {
            appendMessage(message, false);
        });
        scrollToBottom();
    }
}

// Append a single message
function appendMessage(message, animate = true) {
    const messagesContainer = document.querySelector('.messages-list');
    if (!messagesContainer) {
        console.error('Messages container not found');
        return;
    }

    const messageElement = createMessageElement(message);
    if (animate) {
        messageElement.style.opacity = '0';
        messageElement.style.transform = 'translateY(10px)';
    }

    messagesContainer.appendChild(messageElement);

    if (animate) {
        setTimeout(() => {
            messageElement.style.transition = 'opacity 0.3s, transform 0.3s';
            messageElement.style.opacity = '1';
            messageElement.style.transform = 'translateY(0)';
        }, 10);
    }
}

// Create message element
function createMessageElement(message) {
    const messageDiv = document.createElement('div');
    messageDiv.className = 'message-item';
    messageDiv.dataset.messageId = message.Id || message.id;

    // Better sender name handling with more fallbacks
    let senderName = 'Unknown';
    // Check both casing variants for property names (SignalR uses camelCase by default)
    if (message.senderName && message.senderName.trim() !== '') {
        senderName = message.senderName.trim();
    } else if (message.SenderName && message.SenderName.trim() !== '') {
        senderName = message.SenderName.trim();
    } else if (message.user && message.user.trim() !== '') {
        senderName = message.user.trim();
    } else if (message.User && message.User.trim() !== '') {
        senderName = message.User.trim();
    } else if (message.sender && message.sender.trim() !== '') {
        senderName = message.sender.trim();
    } else if (message.Sender && message.Sender.trim() !== '') {
        senderName = message.Sender.trim();
    } else if (message.userId && message.userId !== 'system') {
        senderName = `User ${message.userId}`;
    } else if (message.UserId && message.UserId !== 'system') {
        senderName = `User ${message.UserId}`;
    } else if (message.isFromAI || message.IsFromAI) {
        senderName = 'AI Assistant';
    }

    const avatar = getInitials(senderName);
    const time = formatTime(message.CreatedAt || message.createdAt || message.Time);
    // Check both camelCase and PascalCase for content
    const content = escapeHtml(message.Content || message.content || '');

    messageDiv.innerHTML = `
        <div class="message-avatar">${avatar}</div>
        <div class="message-content">
            <div class="message-header">
                <span class="message-author">${escapeHtml(senderName)}</span>
                <span class="message-time">${time}</span>
                ${message.IsEdited || message.isEdited ? '<span class="edited-badge">(edited)</span>' : ''}
            </div>
            <div class="message-text">${content}</div>
            ${message.Reactions || message.reactions ? createReactionsHTML(message.Reactions || message.reactions) : ''}
        </div>
    `;

    return messageDiv;
}

// Send message
async function sendChannelMessage(content) {
    if (!content || !content.trim()) return;
    
    // Clear input immediately for better UX
    const messageInput = document.getElementById('messageInput');
    if (messageInput) {
        messageInput.value = '';
        messageInput.style.height = 'auto';
    }
    
    if (!communicationsConnection || communicationsConnection.state !== signalR.HubConnectionState.Connected) {
        console.warn('SignalR not connected, adding message locally');
        // Add message locally as fallback
        const localMessage = {
            Id: Date.now(),
            Content: content.trim(),
            SenderName: currentUserName || 'You',
            CreatedAt: new Date().toISOString(),
            UserId: currentUserId
        };
        appendMessage(localMessage);
        scrollToBottom();
        showNotification('Not connected to chat server - message saved locally', 'warning');
        return;
    }

    try {
        
        await communicationsConnection.invoke(
            "SendChannelMessage",
            currentChannelId.toString(),
            currentUserId.toString(),
            "Member", // userType
            content.trim(),
            "Text",
            null, // replyToMessageId
            currentUserName // Pass user name directly
        );

        // Stop typing indicator
        await stopTyping();
    } catch (err) {
        console.error("Error sending message:", err);
        showNotification('Failed to send message', 'error');
    }
}

// Start typing indicator
async function startTyping() {
    if (isTyping || !communicationsConnection) return;

    try {
        await communicationsConnection.invoke("StartTyping", currentChannelId.toString());
        isTyping = true;

        // Clear existing timeout
        if (typingTimeout) {
            clearTimeout(typingTimeout);
        }

        // Auto-stop typing after 3 seconds
        typingTimeout = setTimeout(async () => {
            await stopTyping();
        }, 3000);
    } catch (err) {
        console.error("Error starting typing:", err);
    }
}

// Stop typing indicator
async function stopTyping() {
    if (!isTyping || !communicationsConnection) return;

    try {
        await communicationsConnection.invoke("StopTyping", currentChannelId.toString());
        isTyping = false;

        if (typingTimeout) {
            clearTimeout(typingTimeout);
            typingTimeout = null;
        }
    } catch (err) {
        console.error("Error stopping typing:", err);
    }
}

// Show typing indicator
function showTypingIndicator(userName) {
    let typingDiv = document.querySelector('.typing-indicator');
    if (!typingDiv) {
        typingDiv = document.createElement('div');
        typingDiv.className = 'typing-indicator';
        const messagesContainer = document.querySelector('.messages-list');
        if (messagesContainer) {
            messagesContainer.appendChild(typingDiv);
        }
    }
    typingDiv.textContent = `${userName} is typing...`;
    typingDiv.style.display = 'block';
    scrollToBottom();
}

// Hide typing indicator
function hideTypingIndicator(userName) {
    const typingDiv = document.querySelector('.typing-indicator');
    if (typingDiv) {
        typingDiv.style.display = 'none';
    }
}

// Get online users
async function getOnlineUsers(channelId) {
    if (!communicationsConnection) return;

    try {
        await communicationsConnection.invoke("GetOnlineUsers", channelId.toString());
    } catch (err) {
        console.error("Error getting online users:", err);
    }
}

// Update online users list
function updateOnlineUsers() {
    // This will be called when we receive online user updates
    if (currentChannelId) {
        getOnlineUsers(currentChannelId);
    }
}

// Update user status
function updateUserStatus(userId, status) {
    const memberElements = document.querySelectorAll(`[data-user-id="${userId}"]`);
    memberElements.forEach(element => {
        const statusIndicator = element.querySelector('.status-indicator');
        if (statusIndicator) {
            statusIndicator.className = `status-indicator ${status}`;
        }
    });
}

// Update edited message
function updateEditedMessage(messageId, newContent, editedAt) {
    const messageElement = document.querySelector(`[data-message-id="${messageId}"]`);
    if (messageElement) {
        const textElement = messageElement.querySelector('.message-text');
        if (textElement) {
            textElement.textContent = newContent;
        }

        const headerElement = messageElement.querySelector('.message-header');
        if (headerElement && !headerElement.querySelector('.edited-badge')) {
            const editedBadge = document.createElement('span');
            editedBadge.className = 'edited-badge';
            editedBadge.textContent = '(edited)';
            headerElement.appendChild(editedBadge);
        }
    }
}

// Add reaction to message
function addReactionToMessage(messageId, emoji, userId) {
    const messageElement = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!messageElement) return;

    let reactionsContainer = messageElement.querySelector('.message-reactions');
    if (!reactionsContainer) {
        reactionsContainer = document.createElement('div');
        reactionsContainer.className = 'message-reactions';
        const contentElement = messageElement.querySelector('.message-content');
        if (contentElement) {
            contentElement.appendChild(reactionsContainer);
        }
    }

    // Update or create reaction button
    let reactionButton = reactionsContainer.querySelector(`[data-emoji="${emoji}"]`);
    if (reactionButton) {
        const count = parseInt(reactionButton.dataset.count || 0) + 1;
        reactionButton.dataset.count = count;
        reactionButton.textContent = `${emoji} ${count}`;
    } else {
        reactionButton = document.createElement('button');
        reactionButton.className = 'reaction-button';
        reactionButton.dataset.emoji = emoji;
        reactionButton.dataset.count = 1;
        reactionButton.textContent = `${emoji} 1`;
        reactionsContainer.appendChild(reactionButton);
    }
}

// Remove reaction from message
function removeReactionFromMessage(messageId, emoji, userId) {
    const messageElement = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!messageElement) return;

    const reactionsContainer = messageElement.querySelector('.message-reactions');
    if (!reactionsContainer) return;

    const reactionButton = reactionsContainer.querySelector(`[data-emoji="${emoji}"]`);
    if (reactionButton) {
        const count = parseInt(reactionButton.dataset.count || 0) - 1;
        if (count <= 0) {
            reactionButton.remove();
        } else {
            reactionButton.dataset.count = count;
            reactionButton.textContent = `${emoji} ${count}`;
        }
    }
}

// Create reactions HTML
function createReactionsHTML(reactions) {
    if (!reactions) return '';
    
    try {
        const reactionsObj = typeof reactions === 'string' ? JSON.parse(reactions) : reactions;
        if (!reactionsObj || Object.keys(reactionsObj).length === 0) return '';

        let html = '<div class="message-reactions">';
        for (const [emoji, userIds] of Object.entries(reactionsObj)) {
            const count = Array.isArray(userIds) ? userIds.length : 0;
            if (count > 0) {
                html += `<button class="reaction-button" data-emoji="${emoji}" data-count="${count}">${emoji} ${count}</button>`;
            }
        }
        html += '</div>';
        return html;
    } catch (err) {
        console.error('Error parsing reactions:', err);
        return '';
    }
}

// Switch channel
async function switchChannel(channelId, channelName) {
    // Update UI
    document.querySelectorAll('.channel-item').forEach(item => {
        item.classList.remove('active');
    });
    const channelButton = document.querySelector(`[data-channel-id="${channelId}"]`);
    if (channelButton) {
        channelButton.classList.add('active');
    }

    // Update channel header
    const channelTitle = document.querySelector('.channel-title');
    if (channelTitle) {
        channelTitle.textContent = `# ${channelName}`;
    }

    // Join new channel
    await joinChannel(channelId);

    // Load messages
    await loadChannelMessages(channelId);
}

// Helper functions
function getInitials(name) {
    if (!name) return '??';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
        return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
}

function formatTime(dateString) {
    if (!dateString) return '';
    
    try {
        const date = new Date(dateString);
        const hours = date.getHours();
        const minutes = date.getMinutes();
        const ampm = hours >= 12 ? 'PM' : 'AM';
        const displayHours = hours % 12 || 12;
        const displayMinutes = minutes < 10 ? '0' + minutes : minutes;
        return `${displayHours}:${displayMinutes} ${ampm}`;
    } catch (err) {
        return dateString;
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function scrollToBottom() {
    const messagesContainer = document.querySelector('.messages-container');
    if (messagesContainer) {
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }
}

function showNotification(message, type = 'info') {
    console.log(`[${type.toUpperCase()}] ${message}`);
    // You can implement a toast notification system here
}

// Export for global access
window.initializeCommunicationsChat = initializeCommunicationsChat;
window.sendChannelMessage = sendChannelMessage;
window.switchChannel = switchChannel;
window.startTyping = startTyping;
window.stopTyping = stopTyping;

