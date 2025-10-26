// Communications Sidebar JavaScript
// Handles sidebar toggle, channel loading, and messaging functionality

let commsSidebarState = {
    isOpen: false,
    currentView: 'channels', // 'channels' or 'chat'
    currentChannelId: null,
    currentChannelName: null,
    currentChannelType: null,
    channels: [],
    channelCategories: [],
    teamMembers: [],
    messages: [],
    organizationId: null,
    currentUserId: null,
    currentUserName: null,
    signalRConnection: null,
    isInitialized: false
};

// Initialize Communications Sidebar
function initializeCommsSidebar() {
    console.log('Initializing Communications Sidebar...');
    
    // Restore saved width immediately to prevent overflow on reload
    const commsSidebarPanel = document.getElementById('commsSidebarPanel');
    const savedWidth = localStorage.getItem('notificationsPanelWidth') || '320';
    if (commsSidebarPanel) {
        commsSidebarPanel.style.width = savedWidth + 'px';
    }
    
    // If communications sidebar is active, apply sizing immediately
    const activeSidebar = localStorage.getItem('activeSidebar');
    if (activeSidebar === 'comms') {
        const resizeHandle = document.getElementById('resizeHandle');
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        
        if (resizeHandle) {
            resizeHandle.style.right = (parseInt(savedWidth) - 12) + 'px';
            resizeHandle.style.display = 'flex';
        }
        if (mainContentWrapper) {
            mainContentWrapper.style.right = savedWidth + 'px';
        }
        
        // Update floating timer overlay position
        const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
        if (floatingTimerOverlay && !localStorage.getItem('timerPosition')) {
            floatingTimerOverlay.style.right = (parseInt(savedWidth) + 24) + 'px';
        }
    }
    
    // Get organization and user info from page context
    commsSidebarState.organizationId = getCurrentOrgId();
    commsSidebarState.currentUserId = getCurrentUserId();
    commsSidebarState.currentUserName = getCurrentUserName();
    
    console.log('Comms Sidebar - Org ID:', commsSidebarState.organizationId);
    console.log('Comms Sidebar - User ID:', commsSidebarState.currentUserId);
    
    // Set up event listeners
    setupCommsSidebarEventListeners();
    
    // Initialize SignalR connection for real-time updates
    initializeCommsSignalR();
    
    // Mark as initialized
    commsSidebarState.isInitialized = true;
    
    console.log('Communications Sidebar initialized successfully');
}

// Setup Event Listeners
function setupCommsSidebarEventListeners() {
    // Toggle button
    const toggleBtn = document.getElementById('commsToggleBtn');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleCommsSidebar);
    }
    
    // Close button
    const closeBtn = document.getElementById('commsCloseBtn');
    if (closeBtn) {
        closeBtn.addEventListener('click', closeCommsSidebar);
    }
    
    // Back button (to channels list)
    const backBtn = document.getElementById('commsBackBtn');
    if (backBtn) {
        backBtn.addEventListener('click', showChannelsView);
    }
    
    // Send message button
    const sendBtn = document.getElementById('commsSendButton');
    if (sendBtn) {
        sendBtn.addEventListener('click', sendCommsMessage);
    }
    
    // Message input (Enter to send)
    const messageInput = document.getElementById('commsMessageInput');
    if (messageInput) {
        messageInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendCommsMessage();
            }
        });
        
        // Auto-resize textarea
        messageInput.addEventListener('input', function() {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 120) + 'px';
        });
    }
    
    // Search input
    const searchInput = document.getElementById('commsSearchInput');
    if (searchInput) {
        searchInput.addEventListener('input', function(e) {
            filterCommsChannels(e.target.value);
        });
    }
    
    // Click outside to close
    document.addEventListener('click', function(e) {
        const panel = document.getElementById('commsSidebarPanel');
        const toggleBtn = document.getElementById('commsToggleBtn');
        
        if (panel && toggleBtn && 
            commsSidebarState.isOpen && 
            !panel.contains(e.target) && 
            !toggleBtn.contains(e.target)) {
            closeCommsSidebar();
        }
    });
}

// Toggle Sidebar Open/Close (handled by _ClientLayout.cshtml)
function toggleCommsSidebar() {
    // This is now handled by the header button logic
    // which calls showCommsSidebar() from _ClientLayout
}

// Open Sidebar - Called when switching to comms sidebar
function openCommsSidebar() {
    commsSidebarState.isOpen = true;
    
    // Load channels if not already loaded
    if (commsSidebarState.channels.length === 0) {
        loadCommsChannels();
    }
    
    // Set the width from localStorage or default (shares width with notifications)
    const savedWidth = localStorage.getItem('notificationsPanelWidth') || '320';
    const panel = document.getElementById('commsSidebarPanel');
    if (panel) {
        panel.style.width = savedWidth + 'px';
    }
}

// Close Sidebar - Called when switching away from comms sidebar
function closeCommsSidebar() {
    commsSidebarState.isOpen = false;
}

// Show Channels View
function showChannelsView() {
    const channelsView = document.getElementById('commsChannelsView');
    const chatView = document.getElementById('commsChatView');
    
    if (channelsView && chatView) {
        channelsView.style.display = 'flex';
        chatView.style.display = 'none';
        commsSidebarState.currentView = 'channels';
    }
}

// Show Chat View
function showChatView() {
    const channelsView = document.getElementById('commsChannelsView');
    const chatView = document.getElementById('commsChatView');
    
    if (channelsView && chatView) {
        channelsView.style.display = 'none';
        chatView.style.display = 'flex';
        commsSidebarState.currentView = 'chat';
    }
}

// Load Channels from Server
async function loadCommsChannels() {
    console.log('Loading communications channels...');
    
    if (!commsSidebarState.organizationId) {
        console.error('Organization ID not found');
        return;
    }
    
    try {
        const response = await fetch(`/Client/${commsSidebarState.organizationId}/Communications/GetChannelsJson`);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const data = await response.json();
        
        if (!data.success) {
            throw new Error(data.message || 'Failed to load channels');
        }
        
        console.log('Loaded channel data:', data);
        
        // Store channel categories and team members
        commsSidebarState.channelCategories = data.channelCategories;
        commsSidebarState.teamMembers = data.teamMembers;
        
        // Flatten channels for easy access
        const channels = [];
        data.channelCategories.forEach(category => {
            // Add channels from category
            if (category.channels) {
                channels.push(...category.channels);
            }
            // Add channels from subcategories
            if (category.subcategories) {
                category.subcategories.forEach(subcat => {
                    if (subcat.channels) {
                        channels.push(...subcat.channels);
                    }
                });
            }
        });
        
        commsSidebarState.channels = channels;
        console.log('Total channels loaded:', channels.length);
        
        // Render channels in sidebar
        renderCommsChannelCategories(data.channelCategories);
        
        // Render team members
        renderCommsTeamMembers(data.teamMembers);
        
    } catch (error) {
        console.error('Error loading channels:', error);
        showCommsError('Failed to load channels');
    }
}

// Get channel icon from DOM element
function getChannelIcon(element) {
    const icon = element.querySelector('.channel-icon');
    if (!icon) return 'fa-hashtag';
    
    if (icon.classList.contains('fa-folder')) return 'fa-folder';
    if (icon.classList.contains('fa-lock')) return 'fa-lock';
    if (icon.classList.contains('fa-volume-up')) return 'fa-volume-up';
    return 'fa-hashtag';
}

// Render Channel Categories in Sidebar (matches Communications page structure)
function renderCommsChannelCategories(channelCategories) {
    const channelsList = document.getElementById('commsChannelsList');
    if (!channelsList) return;
    
    let html = '';
    
    channelCategories.forEach(category => {
        // Skip voice channels for now
        if (category.name === 'VOICE CHANNELS') return;
        
        html += `
            <div class="comms-channel-category">
                <button class="comms-category-toggle" data-category="${category.name}">
                    <i class="fas fa-chevron-down comms-category-icon"></i>
                    ${category.name}
                </button>
                <div class="comms-channels-container" data-category="${category.name}">
        `;
        
        // Render category-level channels
        if (category.channels && category.channels.length > 0) {
            category.channels.forEach(channel => {
                html += renderChannelItem(channel);
            });
        }
        
        // Render subcategories
        if (category.subcategories && category.subcategories.length > 0) {
            category.subcategories.forEach(subcat => {
                html += `
                    <div class="comms-subcategory-section">
                        <button class="comms-subcategory-toggle" data-subcategory="${subcat.name}">
                            <i class="fas fa-chevron-down comms-subcategory-icon"></i>
                            ${subcat.name}
                        </button>
                        <div class="comms-subcategory-channels" data-subcategory="${subcat.name}">
                `;
                
                subcat.channels.forEach(channel => {
                    html += renderChannelItem(channel);
                });
                
                html += `
                        </div>
                    </div>
                `;
            });
        }
        
        html += `
                </div>
            </div>
        `;
    });
    
    channelsList.innerHTML = html;
    
    // Setup category toggle listeners
    setupCategoryToggles();
    
    // Setup subcategory toggle listeners
    setupSubcategoryToggles();
    
    // Setup channel click listeners
    setupChannelClickListeners();
}

// Render a single channel item
function renderChannelItem(channel) {
    const unreadBadge = channel.unread > 0 ? 
        `<span class="comms-unread-badge">${channel.unread}</span>` : '';
    
    // Determine icon based on channel properties
    let icon = 'fa-hashtag';
    if (channel.matterId) {
        icon = 'fa-folder';
    } else if (channel.isPrivate) {
        icon = 'fa-lock';
    } else if (channel.type === 'Voice' || channel.type === 1) {
        icon = 'fa-volume-up';
    }
    
    return `
        <button class="comms-channel-item" 
                data-channel-id="${channel.id}"
                data-channel-name="${channel.name}"
                data-channel-type="${channel.type}"
                data-channel-icon="${icon}">
            <div class="comms-channel-info-left">
                <i class="fas ${icon} comms-channel-icon-left"></i>
                <span class="comms-channel-name">${channel.name}</span>
            </div>
            <div class="comms-channel-meta">
                ${unreadBadge}
            </div>
        </button>
    `;
}

// Setup Category Toggle Listeners
function setupCategoryToggles() {
    const categoryToggles = document.querySelectorAll('.comms-category-toggle');
    categoryToggles.forEach(toggle => {
        toggle.addEventListener('click', function() {
            const category = this.dataset.category;
            const container = document.querySelector(`.comms-channels-container[data-category="${category}"]`);
            const icon = this.querySelector('.comms-category-icon');
            
            if (container) {
                container.classList.toggle('collapsed');
                this.classList.toggle('collapsed');
            }
        });
    });
}

// Setup Subcategory Toggle Listeners
function setupSubcategoryToggles() {
    const subcategoryToggles = document.querySelectorAll('.comms-subcategory-toggle');
    subcategoryToggles.forEach(toggle => {
        toggle.addEventListener('click', function() {
            const subcategory = this.dataset.subcategory;
            const container = document.querySelector(`.comms-subcategory-channels[data-subcategory="${subcategory}"]`);
            const icon = this.querySelector('.comms-subcategory-icon');
            
            if (container) {
                container.classList.toggle('collapsed');
                this.classList.toggle('collapsed');
            }
        });
    });
}

// Setup Channel Click Listeners
function setupChannelClickListeners() {
    const channelItems = document.querySelectorAll('.comms-channel-item');
    channelItems.forEach(item => {
        item.addEventListener('click', function() {
            const channelId = this.dataset.channelId;
            const channelName = this.dataset.channelName;
            const channelType = this.dataset.channelType;
            const channelIcon = this.dataset.channelIcon;
            
            selectCommsChannel(channelId, channelName, channelType, channelIcon);
        });
    });
}

// Render Team Members for Direct Messages
function renderCommsTeamMembers(teamMembers) {
    const channelsList = document.getElementById('commsChannelsList');
    if (!channelsList || !teamMembers || teamMembers.length === 0) return;
    
    // Sort team members: current user first, then online users, then offline
    const currentUserId = commsSidebarState.currentUserId;
    const sortedMembers = [...teamMembers].sort((a, b) => {
        // Current user always first
        if (a.userId == currentUserId) return -1;
        if (b.userId == currentUserId) return 1;
        
        // Then by online status
        if (a.status === 'online' && b.status !== 'online') return -1;
        if (a.status !== 'online' && b.status === 'online') return 1;
        
        // Then alphabetically by name
        return a.name.localeCompare(b.name);
    });
    
    const onlineCount = sortedMembers.filter(m => m.status === 'online').length;
    
    let dmHtml = `
        <div class="comms-dm-section">
            <div class="comms-dm-title">
                Direct Messages
                <span class="comms-online-count">${onlineCount} online</span>
            </div>
            <div class="comms-dm-list">
    `;
    
    sortedMembers.forEach(member => {
        const avatar = member.avatar || member.name.charAt(0);
        const status = member.status || 'offline';
        // If status is online, show "Online", otherwise show "Offline"
        const activity = status === 'online' ? 'Online' : 'Offline';
        
        // Check if this is the current user
        const isCurrentUser = member.userId == currentUserId;
        const displayName = isCurrentUser ? `${member.name} (You)` : member.name;
        const avatarClass = isCurrentUser ? 'comms-dm-avatar current-user-avatar' : 'comms-dm-avatar';
        
        dmHtml += `
            <div class="comms-dm-item" data-user-id="${member.userId}" data-user-name="${member.name}">
                <div class="${avatarClass}">
                    ${avatar}
                    <div class="comms-status-indicator ${status}"></div>
                </div>
                <div class="comms-dm-info">
                    <div class="comms-dm-name">${displayName}</div>
                    <div class="comms-dm-activity">${activity}</div>
                </div>
            </div>
        `;
    });
    
    dmHtml += `
            </div>
        </div>
    `;
    
    channelsList.insertAdjacentHTML('beforeend', dmHtml);
    
    // Setup DM click listeners
    setupDMClickListeners();
}

// Setup DM Click Listeners
function setupDMClickListeners() {
    const dmItems = document.querySelectorAll('.comms-dm-item');
    dmItems.forEach(item => {
        item.addEventListener('click', function() {
            const userId = this.dataset.userId;
            const userName = this.dataset.userName;
            
            console.log('Opening DM with:', userName, 'ID:', userId);
            
            // Open direct message thread
            openCommsDM(userId, userName);
        });
    });
}

// Open Direct Message Thread - Match direct-messages.js exactly
async function openCommsDM(otherUserId, otherUserName) {
    try {
        console.log('Opening DM thread with:', otherUserName, 'userId:', otherUserId);
        
        // Update state FIRST
        commsSidebarState.currentChannelType = 'dm';
        commsSidebarState.currentChannelName = otherUserName;
        
        // Switch to chat view immediately
        showChatView();
        
        // Update chat header
        const channelTitle = document.getElementById('commsChannelTitle');
        if (channelTitle) {
            channelTitle.textContent = otherUserName;
        }
        
        // Render active members (just the two DM participants)
        renderActiveMembers(otherUserId, otherUserName);
        
        // Create or get DM thread - exact same endpoint as Communications page
        console.log('Creating/fetching DM thread...');
        const response = await fetch(`/api/dm/threads?orgId=${commsSidebarState.organizationId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ OtherUserId: parseInt(otherUserId) })
        });

        if (!response.ok) {
            console.error('Failed to create/fetch thread:', response.status);
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('DM thread response:', result);
        
        if (result.success && result.thread) {
            // Store thread ID for sending messages
            commsSidebarState.currentChannelId = result.thread.id;
            console.log('Thread ID stored:', result.thread.id);
            
            // Load DM messages
            await loadDMMessages(result.thread.id);
        } else {
            console.error("Failed to open direct thread:", result.error);
            const messagesList = document.getElementById('commsMessagesList');
            if (messagesList) {
                messagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">Failed to open conversation. Please try again.</div>';
            }
        }
    } catch (error) {
        console.error("Error opening direct thread:", error);
        const messagesList = document.getElementById('commsMessagesList');
        if (messagesList) {
            messagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">Error loading conversation. Please try again.</div>';
        }
    }
}

// Load DM Messages - Match direct-messages.js exactly
async function loadDMMessages(threadId) {
    console.log('Loading DM messages for thread:', threadId);
    
    const messagesList = document.getElementById('commsMessagesList');
    if (!messagesList) return;
    
    // Show loading state
    messagesList.innerHTML = '<div class="comms-loading"><div class="spinner"></div><span>Loading messages...</span></div>';
    
    try {
        // Use exact same endpoint as Communications page
        const url = `/api/dm/threads/${threadId}/messages?orgId=${commsSidebarState.organizationId}&take=50`;
        console.log(`Loading DM messages from: ${url}`);
        
        const response = await fetch(url);
        
        if (!response.ok) {
            console.error(`Failed to load DM messages: ${response.status}`);
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const result = await response.json();
        console.log('DM API response:', result);
        
        if (result.success && result.messages && result.messages.length > 0) {
            const messages = result.messages;
            console.log(`Loaded ${messages.length} DM messages`, messages);
            commsSidebarState.messages = messages;
            
            renderCommsMessages(messages);
            
            // Scroll to bottom
            setTimeout(() => {
                const container = document.getElementById('commsMessagesContainer');
                if (container) {
                    container.scrollTop = container.scrollHeight;
                }
            }, 100);
        } else {
            console.log('No DM messages found or empty response');
            messagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">No messages yet. Start the conversation!</div>';
        }
    } catch (error) {
        console.error('Error loading DM messages:', error);
        messagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">Failed to load messages. Please try again.</div>';
    }
}

// Select a Channel
function selectCommsChannel(channelId, channelName, channelType, channelIcon) {
    console.log('Selecting channel:', channelName, 'ID:', channelId);
    
    // Update state
    commsSidebarState.currentChannelId = channelId;
    commsSidebarState.currentChannelName = channelName;
    commsSidebarState.currentChannelType = channelType;
    
    // Update active state in channels list
    document.querySelectorAll('.comms-channel-item').forEach(item => {
        item.classList.remove('active');
    });
    document.querySelector(`.comms-channel-item[data-channel-id="${channelId}"]`)?.classList.add('active');
    
    // Update chat header
    const channelTitle = document.getElementById('commsChannelTitle');
    
    if (channelTitle) {
        channelTitle.textContent = channelName;
    }
    
    // Render active members for this channel
    renderActiveMembers();
    
    // Load messages for this channel
    loadCommsMessages(channelId);
    
    // Switch to chat view
    showChatView();
}

// Load Messages for Channel - Use Same Endpoint as Communications Page
async function loadCommsMessages(channelId) {
    console.log('Loading messages for channel:', channelId);
    
    const messagesList = document.getElementById('commsMessagesList');
    if (!messagesList) return;
    
    // Show loading state
    messagesList.innerHTML = '<div class="comms-loading"><div class="spinner"></div><span>Loading messages...</span></div>';
    
    if (!commsSidebarState.organizationId) {
        console.error('Organization ID not set');
        renderDemoMessages();
        return;
    }
    
    try {
        // Use the same endpoint as Communications page
        const url = `/Client/${commsSidebarState.organizationId}/Chat/channel/${channelId}/messages`;
        console.log(`Loading messages from: ${url}`);
        
        const response = await fetch(url);
        
        if (!response.ok) {
            console.error(`Failed to load messages: ${response.status} ${response.statusText}`);
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const contentType = response.headers.get('content-type');
        if (!contentType || !contentType.includes('application/json')) {
            console.error(`Expected JSON but got: ${contentType}`);
            throw new Error('Invalid response type');
        }
        
        const messages = await response.json();
        console.log(`Loaded ${messages.length} messages`);
        commsSidebarState.messages = messages;
        
        renderCommsMessages(messages);
        
        // Scroll to bottom
        setTimeout(() => {
            const container = document.getElementById('commsMessagesContainer');
            if (container) {
                container.scrollTop = container.scrollHeight;
            }
        }, 100);
        
    } catch (error) {
        console.error('Error loading messages:', error);
        // Show demo messages if API fails
        renderDemoMessages();
    }
}

// Render Messages
function renderCommsMessages(messages) {
    const messagesList = document.getElementById('commsMessagesList');
    if (!messagesList) return;
    
    if (messages.length === 0) {
        messagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">No messages yet. Start the conversation!</div>';
        return;
    }
    
    // Make sure we have the current user ID
    if (!commsSidebarState.currentUserId) {
        commsSidebarState.currentUserId = getCurrentUserId();
    }
    
    console.log('Rendering messages with currentUserId:', commsSidebarState.currentUserId);
    
    let html = '';
    let previousMessage = null;
    
    messages.forEach(message => {
        // Check if this message is from the current user
        // DM messages use 'senderId', channel messages use 'UserId' or 'userId'
        const messageUserId = message.senderId || message.UserId || message.userId;
        const isCurrentUser = messageUserId && commsSidebarState.currentUserId && 
            (parseInt(messageUserId) === parseInt(commsSidebarState.currentUserId) || 
             messageUserId.toString() === commsSidebarState.currentUserId.toString());
        
        console.log('Message from userId:', messageUserId, 'isCurrentUser:', isCurrentUser);
        
        // Check if we should group this message with the previous one
        let isGrouped = false;
        if (previousMessage) {
            const prevUserId = previousMessage.senderId || previousMessage.UserId || previousMessage.userId;
            const isSameSender = messageUserId && prevUserId && 
                (parseInt(messageUserId) === parseInt(prevUserId) || 
                 messageUserId.toString() === prevUserId.toString());
            
            if (isSameSender) {
                // DM messages use 'createdAt', channel messages use 'CreatedAt'
                const currentTime = new Date(message.createdAt || message.CreatedAt);
                const previousTime = new Date(previousMessage.createdAt || previousMessage.CreatedAt);
                const diffInMinutes = (currentTime - previousTime) / (1000 * 60);
                isGrouped = diffInMinutes <= 15;
            }
        }
        
        html += renderMessageItem(message, isGrouped, isCurrentUser);
        previousMessage = message;
    });
    
    messagesList.innerHTML = html;
}

// Render a single message item - Match Communications Page Format
function renderMessageItem(message, isGrouped, isCurrentUser) {
    // Better sender name handling with more fallbacks
    // DM messages use 'senderName', channel messages use 'SenderName' or 'User'
    let senderName = 'Unknown';
    if (message.senderName && message.senderName.trim() !== '') {
        senderName = message.senderName.trim();
    } else if (message.SenderName && message.SenderName.trim() !== '') {
        senderName = message.SenderName.trim();
    } else if (message.user && message.user.trim() !== '') {
        senderName = message.user.trim();
    } else if (message.User && message.User.trim() !== '') {
        senderName = message.User.trim();
    } else if (message.userName && message.userName.trim() !== '') {
        senderName = message.userName.trim();
    }
    
    const avatar = getInitials(senderName);
    // DM messages use 'createdAt', channel messages use 'CreatedAt'
    const time = formatTime(message.createdAt || message.CreatedAt || message.Time);
    // DM messages use 'body', channel messages use 'Content' or 'content'
    const content = escapeHtml(message.body || message.Content || message.content || '');
    
    const currentUserClass = isCurrentUser ? 'current-user-message' : '';
    const groupedClass = isGrouped ? 'grouped-message' : '';
    
    return `
        <div class="comms-message-item ${currentUserClass} ${groupedClass}" data-message-id="${message.Id || message.id}">
            <div class="comms-message-avatar">${avatar}</div>
            <div class="comms-message-content">
                <div class="comms-message-header">
                    <span class="comms-message-author">${escapeHtml(senderName)}</span>
                    <span class="comms-message-time">${time}</span>
                </div>
                <div class="comms-message-text">${content}</div>
            </div>
            ${isGrouped ? `<div class="hover-timestamp">${time}</div>` : ''}
        </div>
    `;
}

// Get initials from name
function getInitials(name) {
    if (!name || name === 'Unknown') return '?';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
}

// Format time
function formatTime(timestamp) {
    if (!timestamp) return '';
    const date = new Date(timestamp);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

// Render Demo Messages (fallback)
function renderDemoMessages() {
    const demoMessages = [
        {
            userId: '1',
            userName: 'Sarah Chen',
            userAvatar: 'SC',
            content: 'Hey! Can you review the latest design mockups? I\'ve uploaded them to the project folder.',
            createdAt: new Date(Date.now() - 3600000).toISOString()
        },
        {
            userId: commsSidebarState.currentUserId || '2',
            userName: commsSidebarState.currentUserName || 'You',
            userAvatar: 'JD',
            content: 'Sure! I\'ll take a look at them right now. The homepage design looks great so far.',
            createdAt: new Date(Date.now() - 1800000).toISOString()
        },
        {
            userId: '3',
            userName: 'Mike Johnson',
            userAvatar: 'MJ',
            content: 'Just finished the API integration. Everything should be working now.',
            createdAt: new Date(Date.now() - 300000).toISOString()
        }
    ];
    
    renderCommsMessages(demoMessages);
}

// Send Message (handles both channels and DMs)
async function sendCommsMessage() {
    const input = document.getElementById('commsMessageInput');
    if (!input) return;
    
    const message = input.value.trim();
    if (!message) return;
    
    if (!commsSidebarState.currentChannelId) {
        console.error('No channel selected');
        return;
    }
    
    console.log('Sending message:', message, 'Type:', commsSidebarState.currentChannelType);
    
    // Clear input immediately
    input.value = '';
    input.style.height = 'auto';
    
    try {
        // Check if it's a DM
        if (commsSidebarState.currentChannelType === 'dm') {
            // Extract thread ID from channel ID (format: dm_{userId} -> we need to get actual threadId)
            // For now, reload the messages after send
            const threadId = commsSidebarState.currentChannelId.replace('dm_', '');
            
            // Send DM via API
            // Note: You'll need to implement this endpoint or use SignalR
            console.log('Sending DM to thread:', threadId);
            
            // Add message to UI immediately for better UX
            addMessageToUI({
                UserId: commsSidebarState.currentUserId,
                SenderName: commsSidebarState.currentUserName,
                Content: message,
                CreatedAt: new Date().toISOString()
            });
        } else {
            // Send channel message
            const response = await fetch(`/api/communications/channels/${commsSidebarState.currentChannelId}/messages`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    content: message,
                    channelId: commsSidebarState.currentChannelId
                })
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            // Reload messages to get the actual server response
            setTimeout(() => {
                if (commsSidebarState.currentChannelType === 'dm') {
                    const threadId = commsSidebarState.currentChannelId.replace('dm_', '');
                    loadDMMessages(threadId);
                } else {
                    loadCommsMessages(commsSidebarState.currentChannelId);
                }
            }, 500);
        }
    } catch (error) {
        console.error('Error sending message:', error);
        
        // Add message to UI anyway for demo purposes
        addMessageToUI({
            UserId: commsSidebarState.currentUserId,
            SenderName: commsSidebarState.currentUserName,
            Content: message,
            CreatedAt: new Date().toISOString()
        });
    }
}

// Add message to UI (for real-time updates)
function addMessageToUI(message) {
    const messagesList = document.getElementById('commsMessagesList');
    if (!messagesList) return;
    
    const isCurrentUser = message.userId === commsSidebarState.currentUserId;
    const html = renderMessageItem(message, false, isCurrentUser);
    
    messagesList.insertAdjacentHTML('beforeend', html);
    
    // Scroll to bottom
    const container = document.getElementById('commsMessagesContainer');
    if (container) {
        container.scrollTop = container.scrollHeight;
    }
}

// Initialize SignalR for real-time updates
function initializeCommsSignalR() {
    // Check if SignalR is available
    if (typeof signalR === 'undefined') {
        console.log('SignalR not available for communications sidebar');
        return;
    }
    
    // Create SignalR connection
    commsSidebarState.signalRConnection = new signalR.HubConnectionBuilder()
        .withUrl(`/hubs/chat?orgId=${commsSidebarState.organizationId}`)
        .withAutomaticReconnect()
        .build();
    
    // Handle incoming channel messages
    commsSidebarState.signalRConnection.on("ReceiveChannelMessage", function(message) {
        console.log('Received channel message via SignalR:', message);
        
        if (message.channelId === commsSidebarState.currentChannelId) {
            addMessageToUI(message);
        }
        
        // Update unread badge
        updateUnreadBadge(message.channelId);
    });
    
    // Handle user online/offline status
    commsSidebarState.signalRConnection.on("UserOnline", function(data) {
        console.log('User came online:', data.UserId);
        updateUserStatus(data.UserId, 'online');
    });
    
    commsSidebarState.signalRConnection.on("UserOffline", function(data) {
        console.log('User went offline:', data.UserId);
        updateUserStatus(data.UserId, 'offline');
    });
    
    // Start connection
    commsSidebarState.signalRConnection.start()
        .then(function() {
            console.log('Communications SignalR connected');
        })
        .catch(function(err) {
            console.error('Communications SignalR connection error:', err);
        });
}

// Update user status in sidebar
function updateUserStatus(userId, status) {
    // Update in team members state
    const member = commsSidebarState.teamMembers.find(m => m.userId == userId);
    if (member) {
        member.status = status;
        member.activity = status === 'online' ? 'Online' : 'Offline';
    }
    
    // Update active members display if currently visible
    if (commsSidebarState.currentView === 'chat') {
        renderActiveMembers();
    }
    
    // Update DM list status indicators and activity text
    const dmItem = document.querySelector(`.comms-dm-item[data-user-id="${userId}"]`);
    if (dmItem) {
        const statusIndicator = dmItem.querySelector('.comms-status-indicator');
        if (statusIndicator) {
            statusIndicator.className = `comms-status-indicator ${status}`;
        }
        const activityText = dmItem.querySelector('.comms-dm-activity');
        if (activityText) {
            activityText.textContent = status === 'online' ? 'Online' : 'Offline';
        }
    }
}

// Update unread badge
function updateUnreadBadge(channelId) {
    const badge = document.getElementById('commsBadge');
    if (badge) {
        // Get total unread count
        const unreadCount = commsSidebarState.channels.reduce((sum, ch) => sum + ch.unread, 0);
        
        if (unreadCount > 0) {
            badge.textContent = unreadCount;
            badge.style.display = 'block';
        } else {
            badge.style.display = 'none';
        }
    }
}

// Show error message
function showCommsError(message) {
    const channelsList = document.getElementById('commsChannelsList');
    if (channelsList) {
        channelsList.innerHTML = `
            <div style="padding: 2rem; text-align: center; color: #e74c3c;">
                <i class="fas fa-exclamation-triangle" style="font-size: 2rem; margin-bottom: 1rem;"></i>
                <p>${message}</p>
            </div>
        `;
    }
}

// Helper Functions
function getCurrentOrgId() {
    // Get from appContext first
    const appContext = document.getElementById('appContext');
    if (appContext && appContext.dataset.organizationId) {
        return appContext.dataset.organizationId;
    }
    
    // Try from other elements
    const orgIdElement = document.querySelector('[data-organization-id]');
    if (orgIdElement) {
        return orgIdElement.dataset.organizationId;
    }
    
    // Try from URL
    const match = window.location.pathname.match(/\/Client\/(\d+)/);
    if (match) {
        return match[1];
    }
    
    return null;
}

function getCurrentUserId() {
    // Get from appContext first
    const appContext = document.getElementById('appContext');
    if (appContext && appContext.dataset.currentUserId) {
        return appContext.dataset.currentUserId;
    }
    
    // Fallback to other methods
    const userIdElement = document.querySelector('[data-current-user-id]');
    if (userIdElement) {
        return userIdElement.dataset.currentUserId;
    }
    return null;
}

function getCurrentUserName() {
    // Get from appContext first
    const appContext = document.getElementById('appContext');
    if (appContext && appContext.dataset.currentUserName) {
        return appContext.dataset.currentUserName;
    }
    
    // Fallback to other methods
    const userNameElement = document.querySelector('[data-current-user-name]');
    if (userNameElement) {
        return userNameElement.dataset.currentUserName;
    }
    return 'User';
}

function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, m => map[m]);
}

// Render Active Members (shown when viewing a channel or DM)
async function renderActiveMembers(dmUserId, dmUserName) {
    const activeMembersSection = document.getElementById('commsActiveMembers');
    if (!activeMembersSection) return;
    
    let html = '<div class="members-label">ACTIVE MEMBERS</div><div class="members-avatars">';
    
    if (commsSidebarState.currentChannelType === 'dm') {
        // For DMs, show just the two participants
        // Don't duplicate if DM with self
        if (dmUserId && dmUserId != commsSidebarState.currentUserId) {
            // Current user
            const currentUserInitials = getInitials(commsSidebarState.currentUserName || 'You');
            const currentUserMember = commsSidebarState.teamMembers.find(m => m.userId == commsSidebarState.currentUserId);
            const currentUserStatus = currentUserMember ? currentUserMember.status : 'online';
            
            html += `
                <div class="member-avatar-small" title="${commsSidebarState.currentUserName || 'You'}">
                    ${currentUserInitials}
                    <div class="status-indicator ${currentUserStatus}"></div>
                </div>
            `;
            
            // Other user
            if (dmUserName) {
                const otherUserInitials = getInitials(dmUserName);
                const otherUserMember = commsSidebarState.teamMembers.find(m => m.userId == dmUserId);
                const otherUserStatus = otherUserMember ? otherUserMember.status : 'offline';
                
                html += `
                    <div class="member-avatar-small" title="${dmUserName}">
                        ${otherUserInitials}
                        <div class="status-indicator ${otherUserStatus}"></div>
                    </div>
                `;
            }
        } else {
            // DM with self - show only one avatar
            const currentUserInitials = getInitials(commsSidebarState.currentUserName || 'You');
            const currentUserMember = commsSidebarState.teamMembers.find(m => m.userId == commsSidebarState.currentUserId);
            const currentUserStatus = currentUserMember ? currentUserMember.status : 'online';
            
            html += `
                <div class="member-avatar-small" title="${commsSidebarState.currentUserName || 'You'} (Self)">
                    ${currentUserInitials}
                    <div class="status-indicator ${currentUserStatus}"></div>
                </div>
            `;
        }
    } else {
        // For channels, get actual channel members
        try {
            const response = await fetch(`/Client/${commsSidebarState.organizationId}/Communications/GetChannelMembers?orgId=${commsSidebarState.organizationId}&channelId=${commsSidebarState.currentChannelId}`);
            
            if (response.ok) {
                const result = await response.json();
                
                if (result.success && result.members) {
                    const members = result.members.slice(0, 5);
                    members.forEach(member => {
                        const avatar = member.avatar || getInitials(member.name);
                        const status = member.status || 'offline';
                        html += `
                            <div class="member-avatar-small" title="${member.name}">
                                ${avatar}
                                <div class="status-indicator ${status}"></div>
                            </div>
                        `;
                    });
                    
                    // Show count if there are more
                    if (result.members.length > 5) {
                        const remaining = result.members.length - 5;
                        html += `
                            <div class="member-avatar-small" title="${remaining} more members">
                                +${remaining}
                            </div>
                        `;
                    }
                }
            }
        } catch (error) {
            console.error('Error fetching channel members:', error);
            // Fallback to showing first 5 team members
            const membersToShow = commsSidebarState.teamMembers.slice(0, 5);
            membersToShow.forEach(member => {
                const avatar = member.avatar || getInitials(member.name);
                const status = member.status || 'offline';
                html += `
                    <div class="member-avatar-small" title="${member.name}">
                        ${avatar}
                        <div class="status-indicator ${status}"></div>
                    </div>
                `;
            });
        }
    }
    
    html += '</div>';
    activeMembersSection.innerHTML = html;
}

// Filter channels and team members based on search query
function filterCommsChannels(query) {
    const searchQuery = query.toLowerCase().trim();
    
    // Get all channel items and team member items
    const channelItems = document.querySelectorAll('.comms-channel-item');
    const dmItems = document.querySelectorAll('.comms-dm-item');
    const categories = document.querySelectorAll('.comms-category');
    const subcategories = document.querySelectorAll('.comms-subcategory-section');
    
    if (!searchQuery) {
        // Show everything if search is empty
        channelItems.forEach(item => item.style.display = '');
        dmItems.forEach(item => item.style.display = '');
        categories.forEach(cat => cat.style.display = '');
        subcategories.forEach(subcat => subcat.style.display = '');
        return;
    }
    
    // Filter channels
    channelItems.forEach(item => {
        const channelName = item.textContent.toLowerCase();
        if (channelName.includes(searchQuery)) {
            item.style.display = '';
        } else {
            item.style.display = 'none';
        }
    });
    
    // Filter direct messages
    dmItems.forEach(item => {
        const userName = item.querySelector('.comms-dm-name')?.textContent.toLowerCase() || '';
        if (userName.includes(searchQuery)) {
            item.style.display = '';
        } else {
            item.style.display = 'none';
        }
    });
    
    // Hide empty categories
    categories.forEach(category => {
        const visibleChannels = category.querySelectorAll('.comms-channel-item:not([style*="display: none"])');
        const visibleSubcats = category.querySelectorAll('.comms-subcategory-section');
        let hasVisibleContent = visibleChannels.length > 0;
        
        // Check subcategories
        visibleSubcats.forEach(subcat => {
            const subcatVisibleChannels = subcat.querySelectorAll('.comms-channel-item:not([style*="display: none"])');
            if (subcatVisibleChannels.length > 0) {
                hasVisibleContent = true;
                subcat.style.display = '';
            } else {
                subcat.style.display = 'none';
            }
        });
        
        category.style.display = hasVisibleContent ? '' : 'none';
    });
}

// Initialize on DOM load
document.addEventListener('DOMContentLoaded', function() {
    // Small delay to ensure other scripts are loaded
    setTimeout(() => {
        initializeCommsSidebar();
    }, 500);
});

// Export functions for external access
window.commsSidebar = {
    open: openCommsSidebar,
    close: closeCommsSidebar,
    toggle: toggleCommsSidebar,
    selectChannel: selectCommsChannel,
    sendMessage: sendCommsMessage,
    get isInitialized() {
        return commsSidebarState.isInitialized;
    }
};

