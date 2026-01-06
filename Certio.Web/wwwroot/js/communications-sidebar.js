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
    signalRConnection: null, // ChatHub for channels
    directSignalRConnection: null, // DirectHub for DMs
    isInitialized: false
};

// Initialize Communications Sidebar
function initializeCommsSidebar() {
    console.log('Initializing Communications Sidebar...');
    
    // Restore saved width immediately to prevent overflow on reload
    const commsSidebarPanel = document.getElementById('commsSidebarPanel');
    const savedWidth = parseInt(
        localStorage.getItem('rightSidebarWidth') ||
        localStorage.getItem('notificationsPanelWidth') ||
        localStorage.getItem('chatPanelWidth') ||
        '320',
        10
    ) || 320;
    if (commsSidebarPanel) {
        commsSidebarPanel.style.width = savedWidth + 'px';
    }
    
    // If communications sidebar is active, apply sizing immediately
    const activeSidebar = localStorage.getItem('activeSidebar');
    if (activeSidebar === 'comms') {
        const resizeHandle = document.getElementById('resizeHandle');
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        
        if (resizeHandle) {
            resizeHandle.style.right = (savedWidth - 12) + 'px';
            resizeHandle.style.display = 'flex';
        }
        if (mainContentWrapper) {
            mainContentWrapper.style.right = savedWidth + 'px';
        }
        
        // Update floating timer overlay position
        // IMPORTANT: Do NOT set `right` when the timer is positioned via `left/top` (default or user-dragged),
        // otherwise the fixed element becomes over-constrained (left + right) and stretches across the screen,
        // creating an invisible draggable hitbox.
        const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
        if (floatingTimerOverlay) {
            const hasSavedManualPosition = !!localStorage.getItem('timerPosition');
            const hasExplicitLeft = (floatingTimerOverlay.style.left || '').trim().length > 0;

            if (!hasSavedManualPosition && !hasExplicitLeft) {
                floatingTimerOverlay.style.right = (savedWidth + 24) + 'px';
            }
        }
    }
    
    // Get organization and user info from page context
    commsSidebarState.organizationId = getCurrentOrgId();
    commsSidebarState.currentUserId = getCurrentUserId();
    commsSidebarState.currentUserName = getCurrentUserName();
    
    console.log('Comms Sidebar - Org ID:', commsSidebarState.organizationId);
    console.log('Comms Sidebar - User ID:', commsSidebarState.currentUserId);
    console.log('Comms Sidebar - User Name:', commsSidebarState.currentUserName);
    
    // If user ID not found, try again after a delay (page scripts might not be loaded yet)
    if (!commsSidebarState.currentUserId) {
        setTimeout(() => {
            commsSidebarState.currentUserId = getCurrentUserId();
            commsSidebarState.currentUserName = getCurrentUserName();
            console.log('Retried - User ID:', commsSidebarState.currentUserId, 'User Name:', commsSidebarState.currentUserName);
            
            // If user ID or name was found after retry AND team members are already loaded, re-render them
            if ((commsSidebarState.currentUserId || commsSidebarState.currentUserName) && commsSidebarState.teamMembers && commsSidebarState.teamMembers.length > 0) {
                renderCommsTeamMembers(commsSidebarState.teamMembers);
            }
        }, 1000);
    }
    
    // Set up event listeners
    setupCommsSidebarEventListeners();
    
    // Initialize SignalR connections for real-time updates
    initializeCommsSignalR();
    initializeDirectSignalR();
    
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
    
    // Inbox card click listener - Navigate to Communications page and open inbox
    const inboxCard = document.getElementById('commsInboxCard');
    if (inboxCard) {
        inboxCard.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('Inbox card clicked in sidebar - navigating to Communications with inbox open');
            
            // Get organization ID for proper routing
            const orgId = getCurrentOrgId();
            if (orgId) {
                window.location.href = `/Client/${orgId}/Communications?openInbox=true`;
            } else {
                console.error('Could not determine organization ID for Communications navigation');
                // Fallback: try to navigate to HomeController Communications action
                window.location.href = '/Communications?openInbox=true';
            }
        });
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
    const savedWidth = parseInt(
        localStorage.getItem('rightSidebarWidth') ||
        localStorage.getItem('notificationsPanelWidth') ||
        localStorage.getItem('chatPanelWidth') ||
        '320',
        10
    ) || 320;
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
        console.log('Team members with colors:', data.teamMembers?.map(m => ({ name: m.name, color: m.color || m.Color, isExternal: m.isExternalContacts })));
        
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
    
    // Remove any existing Direct Messages section to prevent duplicates
    const existingDMSection = channelsList.querySelector('.comms-dm-section');
    if (existingDMSection) {
        existingDMSection.remove();
    }
    
    // Sort team members with priority:
    // 1. Current user first
    // 2. Current organization users (not from relationships)
    // 3. Relationship users (clients) who are NOT external contacts
    // 4. External contacts
    // Within each group: online users first, then alphabetically
    
    // CRITICAL: Re-fetch currentUserId if it's null (might not have been ready during init)
    let currentUserId = commsSidebarState.currentUserId;
    if (!currentUserId) {
        currentUserId = getCurrentUserId();
        commsSidebarState.currentUserId = currentUserId;
    }
    
    const currentOrgId = commsSidebarState.organizationId;
    
    const sortedMembers = [...teamMembers].sort((a, b) => {
        // Current user always first (ONLY match by userId, not by name to avoid false matches)
        const aIsCurrentUser = a.userId && currentUserId && parseInt(a.userId) === parseInt(currentUserId);
        const bIsCurrentUser = b.userId && currentUserId && parseInt(b.userId) === parseInt(currentUserId);
        if (aIsCurrentUser) return -1;
        if (bIsCurrentUser) return 1;
        
        // Check if external contacts
        const aIsExternal = a.isExternalContacts || a.organizationName?.endsWith("'s External Contacts");
        const bIsExternal = b.isExternalContacts || b.organizationName?.endsWith("'s External Contacts");
        
        // Determine if from current org or relationship
        const aIsCurrentOrg = a.organizationId == currentOrgId && !aIsExternal;
        const bIsCurrentOrg = b.organizationId == currentOrgId && !bIsExternal;
        const aIsRelationship = a.organizationId != currentOrgId && !aIsExternal;
        const bIsRelationship = b.organizationId != currentOrgId && !bIsExternal;
        
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
    
    // Deduplicate members by userId (or name if userId is missing)
    const seenMembers = new Map();
    const uniqueMembers = sortedMembers.filter(member => {
        const key = member.userId ? `id-${member.userId}` : `name-${member.name.trim()}`;
        if (seenMembers.has(key)) {
            return false;
        }
        seenMembers.set(key, true);
        return true;
    });
    
    const onlineCount = uniqueMembers.filter(m => m.status === 'online').length;
    
    let dmHtml = `
        <div class="comms-dm-section">
            <div class="comms-dm-title">
                Direct Messages
                <span class="comms-online-count">${onlineCount} online</span>
            </div>
            <div class="comms-dm-list">
    `;
    
    uniqueMembers.forEach(member => {
        const avatar = member.avatar || member.name.charAt(0);
        const status = member.status || 'offline';
        
        // Check if this is an external contacts user
        const isExternalContacts = member.isExternalContacts || member.organizationName?.endsWith("'s External Contacts");
        
        // Check if this is the current user - ONLY match by userId, not by name
        // Name matching can incorrectly match external users with the same name
        const isCurrentUser = member.userId && currentUserId && parseInt(member.userId) === parseInt(currentUserId);
        const displayName = isCurrentUser ? `${member.name} (You)` : member.name;
        
        // If external contacts, show "External", otherwise show online/offline status
        // BUT: Never show "External" for the current user
        const activity = (isCurrentUser || !isExternalContacts) ? (status === 'online' ? 'Online' : 'Offline') : 'External';
        
        // Get avatar color from member, default to maroon if not set
        // Normalize #9ca3af to #aaaaaa for external contacts
        let avatarColor = member.color || member.Color || (isExternalContacts ? '#aaaaaa' : '#3d1019');
        if (isExternalContacts && avatarColor === '#9ca3af') {
            avatarColor = '#aaaaaa';
        }
        
        // Build avatar class with external contacts support
        // Current user should never be marked as external contacts
        let avatarClass = 'comms-dm-avatar';
        if (isCurrentUser) {
            avatarClass += ' current-user-avatar';
        } else if (isExternalContacts) {
            avatarClass += ' external-contacts-avatar';
        }
        
        // Add data attribute for CSS targeting (but not for current user)
        const dataAttrs = (isExternalContacts && !isCurrentUser) ? ' data-is-external="true"' : '';
        
        // Don't apply inline style for current user (let CSS gradient handle it)
        let avatarStyle = '';
        if (!isCurrentUser) {
            avatarStyle = ` style="background: ${avatarColor} !important;"`;
        }
        
        // Only show status indicator for non-external users
        const statusIndicatorHtml = isExternalContacts ? '' : `<div class="comms-status-indicator ${status}"></div>`;
        
        dmHtml += `
            <div class="comms-dm-item" data-user-id="${member.userId}" data-user-name="${member.name}"${dataAttrs}>
                <div class="${avatarClass}"${avatarStyle}>
                    ${avatar}
                    ${statusIndicatorHtml}
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
        
        // Ensure currentUserId is set before loading messages
        if (!commsSidebarState.currentUserId) {
            commsSidebarState.currentUserId = getCurrentUserId();
            commsSidebarState.currentUserName = getCurrentUserName();
        }
        
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
            // Store thread ID for sending messages (this is a GUID)
            // Keep original format for sending, but normalize for comparison
            const threadIdOriginal = result.thread.id.toString();
            commsSidebarState.currentChannelId = threadIdOriginal; // Store original format
            commsSidebarState.currentChannelType = 'dm';
            console.log('Thread ID stored:', threadIdOriginal);
            
            // Join SignalR thread group (use original format for API call)
            if (commsSidebarState.directSignalRConnection && 
                commsSidebarState.directSignalRConnection.state === signalR.HubConnectionState.Connected) {
                try {
                    console.log('🚪 Joining DirectHub thread group:', threadIdOriginal);
                    await commsSidebarState.directSignalRConnection.invoke("JoinThread", threadIdOriginal);
                    console.log('✅ Successfully joined DirectHub thread group:', threadIdOriginal);
                } catch (err) {
                    console.error('❌ Error joining thread group:', err);
                }
            } else {
                console.warn('⚠️ DirectHub not connected, cannot join thread. State:', 
                    commsSidebarState.directSignalRConnection ? commsSidebarState.directSignalRConnection.state : 'null');
            }
            
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
            
            // Messages come newest first from API, reverse for display (oldest at top, newest at bottom)
            const reversedMessages = [...messages].reverse();
            commsSidebarState.messages = reversedMessages;
            
            renderCommsMessages(reversedMessages);
            
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
    
    // Ensure currentUserId is set before loading messages
    if (!commsSidebarState.currentUserId) {
        commsSidebarState.currentUserId = getCurrentUserId();
        commsSidebarState.currentUserName = getCurrentUserName();
    }
    
    // Exit DM mode if we're switching from DM to channel
    if (commsSidebarState.currentChannelType === 'dm' && typeof exitDirectMessageMode === 'function') {
        exitDirectMessageMode();
    }
    
    // Update state
    commsSidebarState.currentChannelId = channelId;
    commsSidebarState.currentChannelName = channelName;
    commsSidebarState.currentChannelType = channelType;
    
    // Update message input placeholder for channel
    const messageInput = document.getElementById('commsMessageInput');
    if (messageInput) {
        messageInput.placeholder = `Message #${channelName}`;
    }
    
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
    
    // Join SignalR channel group
    if (commsSidebarState.signalRConnection && 
        commsSidebarState.signalRConnection.state === signalR.HubConnectionState.Connected) {
        // Ensure channelId is a string for SignalR
        const channelIdStr = channelId ? channelId.toString() : null;
        if (channelIdStr) {
            commsSidebarState.signalRConnection.invoke("JoinChannel", channelIdStr)
                .then(() => {
                    console.log('Successfully joined channel:', channelIdStr);
                })
                .catch(err => {
                    console.error('Error joining channel:', err);
                });
        }
    }
    
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
    
    // If still null, try again after a short delay (in case page scripts haven't set it yet)
    if (!commsSidebarState.currentUserId) {
        setTimeout(() => {
            commsSidebarState.currentUserId = getCurrentUserId();
            console.log('Retried getting currentUserId:', commsSidebarState.currentUserId);
        }, 500);
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
    const timestamp = message.createdAt || message.CreatedAt || message.Time;
    const { dateStr, timeStr } = formatTime(timestamp);
    // DM messages use 'body', channel messages use 'Content' or 'content'
    // For Change Notice synced "Email" messages, render Confirm/Clarify as buttons and hide raw URLs.
    const messageType = (message.messageType || message.MessageType || '').toLowerCase();
    let bodyText = message.body || message.Content || message.content || '';

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
                continue;
            }

            m = trimmed.match(/^Needs\s+clarification:\s*(\S+)\s*$/i);
            if (m && m[1]) {
                clarifyUrl = m[1];
                continue;
            }

            kept.push(line);
        }

        const looksLikeChangeNotice =
            (confirmUrl && confirmUrl.includes('/public/change-notice/respond')) ||
            (clarifyUrl && clarifyUrl.includes('/public/change-notice/respond'));

        if (!looksLikeChangeNotice) {
            return { confirmUrl: null, clarifyUrl: null, cleanedText: text };
        }

        while (kept.length > 0 && kept[kept.length - 1].trim() === '') {
            kept.pop();
        }

        return { confirmUrl, clarifyUrl, cleanedText: kept.join('\n').trim() };
    }

    function safeUrl(url) {
        if (!url) return null;
        const u = String(url).trim();
        if (!/^https?:\/\//i.test(u)) return null;
        return u;
    }

    const extracted = (messageType === 'email')
        ? extractChangeNoticeActionUrls(bodyText)
        : { confirmUrl: null, clarifyUrl: null, cleanedText: bodyText };

    const normalizedText = String(extracted.cleanedText || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    const escapedText = escapeHtml(normalizedText);
    let withBreaks = escapedText.replace(/\n{2,}/g, '<br><br>');
    withBreaks = withBreaks.replace(/\n/g, '<br>');

    const confirmUrlSafe = safeUrl(extracted.confirmUrl);
    const clarifyUrlSafe = safeUrl(extracted.clarifyUrl);
    const hasActionButtons = !!(confirmUrlSafe || clarifyUrlSafe);
    const actionsHtml = hasActionButtons ? `
        <div class="comms-change-notice-actions" style="margin-top: 10px; display: flex; gap: 8px; flex-wrap: wrap;">
            ${confirmUrlSafe ? `<a class="btn btn-sm btn-primary" href="${escapeHtml(confirmUrlSafe)}" target="_blank" rel="noopener noreferrer" style="border-radius: 8px; font-weight: 600;">Confirm</a>` : ``}
            ${clarifyUrlSafe ? `<a class="btn btn-sm btn-outline-secondary" href="${escapeHtml(clarifyUrlSafe)}" target="_blank" rel="noopener noreferrer" style="border-radius: 8px; font-weight: 600;">Needs clarification</a>` : ``}
        </div>
    ` : '';

    const content = `${withBreaks}${actionsHtml}`;
    
    // Get sender color and check if external contacts
    const isExternalContacts = message.isExternalContacts || message.organizationName?.endsWith("'s External Contacts") || false;
    let senderColor = message.senderColor || (isExternalContacts ? '#aaaaaa' : '#3d1019');
    // Normalize #9ca3af to #aaaaaa for external contacts
    if (isExternalContacts && senderColor === '#9ca3af') {
        senderColor = '#aaaaaa';
    }
    
    // Build avatar style with color - don't apply inline style for current user (let CSS gradient handle it)
    let avatarStyle = '';
    let avatarClass = isExternalContacts ? 'comms-message-avatar external-contacts-avatar' : 'comms-message-avatar';
    const avatarDataAttr = isExternalContacts ? ' data-is-external="true"' : '';
    
    if (!isCurrentUser) {
        // Only apply inline color for non-current-user messages
        avatarStyle = `background: ${senderColor} !important;`;
    }
    // For current user, CSS will apply the gradient via .comms-message-item.current-user-message .comms-message-avatar
    
    const currentUserClass = isCurrentUser ? 'current-user-message' : '';
    const groupedClass = isGrouped ? 'grouped-message' : '';
    
    // Check if message is an email
    const isEmailMessage = messageType === 'email';
    const emailBadgeHtml = isEmailMessage ? `
        <div class="task-matter-badge">
            <span class="badge matter-priority-badge task-priority-badge priority-medium" style="font-size: 0.625rem; padding: 0.25rem 0.5rem; border-radius: 12px; font-weight: 600; background-color: #e3f2fd; color: #1565c0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;">
                Sent via email
            </span>
        </div>
    ` : '';
    
    // Format timestamp for hover (only show time)
    let hoverTimestamp = '';
    if (timestamp) {
        try {
            const hoverDate = new Date(timestamp);
            const hoverHours = hoverDate.getHours();
            const hoverMinutes = hoverDate.getMinutes();
            const hoverAmpm = hoverHours >= 12 ? 'PM' : 'AM';
            const hoverDisplayHours = hoverHours % 12 || 12;
            const hoverDisplayMinutes = hoverMinutes < 10 ? '0' + hoverMinutes : hoverMinutes;
            hoverTimestamp = `${hoverDisplayHours}:${hoverDisplayMinutes} ${hoverAmpm}`;
        } catch (err) {
            hoverTimestamp = timestamp;
        }
    }

    return `
        <div class="comms-message-item ${currentUserClass} ${groupedClass}" data-message-id="${message.Id || message.id}">
            <div class="${avatarClass}"${avatarDataAttr}${avatarStyle ? ` style="${avatarStyle}"` : ''}>${avatar}</div>
            <div class="comms-message-content">
                ${emailBadgeHtml}
                <div class="comms-message-header">
                    <span class="comms-message-author">${escapeHtml(senderName)}</span>
                    <span class="comms-message-time">
                        ${dateStr ? `<span class="message-date">${dateStr}</span>` : ''}
                        <span class="message-time-value">${timeStr}</span>
                    </span>
                </div>
                <div class="comms-message-text">${content}</div>
            </div>
            ${isGrouped ? `<div class="hover-timestamp">${hoverTimestamp}</div>` : ''}
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
    if (!timestamp) return { dateStr: '', timeStr: '' };
    
    try {
        const date = new Date(timestamp);
        
        // Check if date is valid
        if (isNaN(date.getTime())) {
            // If it's already a formatted time string (like "9:42 AM"), return it as-is
            return { dateStr: '', timeStr: timestamp };
        }
        
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
    } catch (err) {
        // If parsing fails, try to return as-is
        return { dateStr: '', timeStr: timestamp };
    }
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
    
    // Ensure we have user ID before sending
    if (!commsSidebarState.currentUserId) {
        commsSidebarState.currentUserId = getCurrentUserId();
    }
    
    if (!commsSidebarState.currentUserId) {
        console.error('User ID not available');
        const messagesList = document.getElementById('commsMessagesList');
        if (messagesList) {
            const errorHtml = `<div style="padding: 0.5rem; margin: 0.5rem; background: #fee; color: #c33; border-radius: 4px; font-size: 0.875rem;">
                Failed to send message: User ID not found. Please refresh the page.
            </div>`;
            messagesList.insertAdjacentHTML('beforeend', errorHtml);
        }
        input.value = message; // Restore message
        return;
    }
    
    console.log('Sending message:', message, 'Type:', commsSidebarState.currentChannelType);
    console.log('User ID:', commsSidebarState.currentUserId, 'Channel ID:', commsSidebarState.currentChannelId);
    
    // Clear input immediately
    input.value = '';
    input.style.height = 'auto';
    
    try {
        // Check if it's a DM
        if (commsSidebarState.currentChannelType === 'dm') {
            // For DMs, currentChannelId is the threadId (GUID)
            const threadId = commsSidebarState.currentChannelId;
            
            if (!commsSidebarState.directSignalRConnection || 
                commsSidebarState.directSignalRConnection.state !== signalR.HubConnectionState.Connected) {
                console.error('Direct SignalR connection not available');
                throw new Error('Not connected to direct messaging');
            }
            
            console.log('📤 Sending DM to thread:', threadId);
            console.log('📤 Current channel type:', commsSidebarState.currentChannelType);
            console.log('📤 DirectHub connection state:', commsSidebarState.directSignalRConnection.state);
            
            // Ensure threadId is in proper format for backend
            const threadIdForSend = threadId ? threadId.toString() : null;
            
            if (!threadIdForSend) {
                throw new Error('Invalid thread ID');
            }
            
            console.log('📤 Normalized threadId for send:', threadIdForSend);
            console.log('📤 Invoking DirectHub.SendMessage with:', { threadId: threadIdForSend, message, messageType: 'Text' });
            
            // Create optimistic message for immediate display
            const optimisticMessage = {
                id: 'temp-' + Date.now(),
                senderId: commsSidebarState.currentUserId,
                SenderId: commsSidebarState.currentUserId,
                senderName: commsSidebarState.currentUserName || 'You',
                SenderName: commsSidebarState.currentUserName || 'You',
                body: message,
                Body: message,
                content: message,
                Content: message,
                createdAt: new Date().toISOString(),
                CreatedAt: new Date().toISOString(),
                Time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            };
            
            // Add optimistic message to UI immediately
            addMessageToUI(optimisticMessage);
            
            // Send via DirectHub SignalR
            await commsSidebarState.directSignalRConnection.invoke("SendMessage", threadIdForSend, message, "Text");
            
            console.log('✅ DM message sent successfully via SignalR');
            console.log('⏳ Waiting for ReceiveDirectMessage event...');
        } else {
            // Send channel message via ChatHub SignalR
            if (!commsSidebarState.signalRConnection || 
                commsSidebarState.signalRConnection.state !== signalR.HubConnectionState.Connected) {
                console.error('Channel SignalR connection not available');
                throw new Error('Not connected to chat');
            }
            
            console.log('Sending channel message to:', commsSidebarState.currentChannelId);
            
            // Ensure we have valid string values
            const channelId = commsSidebarState.currentChannelId ? commsSidebarState.currentChannelId.toString() : null;
            const userId = commsSidebarState.currentUserId ? commsSidebarState.currentUserId.toString() : null;
            
            if (!channelId || !userId) {
                throw new Error('Invalid channel or user ID');
            }
            
            // Send via ChatHub SignalR (same as Communications page)
            await commsSidebarState.signalRConnection.invoke(
                "SendChannelMessage",
                channelId,
                userId,
                "Member", // userType
                message,
                "Text",
                null, // replyToMessageId
                commsSidebarState.currentUserName || "User"
            );
            
            console.log('Channel message sent successfully');
        }
    } catch (error) {
        console.error('Error sending message:', error);
        
        // Remove optimistic message if it exists
        const messagesList = document.getElementById('commsMessagesList');
        if (messagesList) {
            const tempMessage = messagesList.querySelector('[data-message-id^="temp-"]');
            if (tempMessage) {
                tempMessage.remove();
            }
            
            // Show error to user
            const errorHtml = `<div style="padding: 0.5rem; margin: 0.5rem; background: #fee; color: #c33; border-radius: 4px; font-size: 0.875rem;">
                Failed to send message: ${error.message}
            </div>`;
            messagesList.insertAdjacentHTML('beforeend', errorHtml);
        }
        
        // Restore message in input
        input.value = message;
    }
}

// Add message to UI (for real-time updates)
function addMessageToUI(message) {
    const messagesList = document.getElementById('commsMessagesList');
    if (!messagesList) return;
    
    // Prevent duplicate messages - check if message already exists
    const messageId = message.Id || message.id;
    if (messageId) {
        const existingMessage = messagesList.querySelector(`[data-message-id="${messageId}"]`);
        if (existingMessage) {
            console.log('Message already exists in UI, skipping:', messageId);
            return;
        }
    }
    
      // Determine if message is from current user
     const messageUserId = message.senderId || message.SenderId || message.UserId || message.userId;
     const isCurrentUser = messageUserId && commsSidebarState.currentUserId && 
         (parseInt(messageUserId) === parseInt(commsSidebarState.currentUserId) || 
          messageUserId.toString() === commsSidebarState.currentUserId.toString());
    
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
        console.log('Current channel ID:', commsSidebarState.currentChannelId);
        console.log('Current channel type:', commsSidebarState.currentChannelType);
        console.log('Message ConversationId:', message.ConversationId, 'ChannelId:', message.ChannelId);
        
        // Only show channel messages if we're NOT in DM mode
        if (commsSidebarState.currentChannelType === 'dm') {
            console.log('Ignoring channel message - sidebar is in DM mode');
            return;
        }
        
        // Compare channel IDs (handle both string and number)
        // ChatHub sends ConversationId and ChannelId fields
        const messageChannelId = message.ChannelId || message.ConversationId || message.channelId;
        const currentChannelId = commsSidebarState.currentChannelId;
        
        // Normalize both to strings for comparison
        const messageChannelIdStr = messageChannelId ? messageChannelId.toString() : null;
        const currentChannelIdStr = currentChannelId ? currentChannelId.toString() : null;
        
        console.log('Comparing:', messageChannelIdStr, '===', currentChannelIdStr);
        
        if (messageChannelIdStr === currentChannelIdStr) {
            console.log('Message matches current channel, adding to UI');
            
            // Ensure message has the right format
            const formattedMessage = {
                Id: message.Id || message.id,
                UserId: message.UserId || message.userId,
                senderId: message.UserId || message.userId,
                User: message.User || message.SenderName || message.senderName,
                SenderName: message.SenderName || message.User || message.senderName,
                Content: message.Content || message.content,
                content: message.Content || message.content,
                CreatedAt: message.CreatedAt || message.createdAt,
                createdAt: message.CreatedAt || message.createdAt,
                Time: message.Time || (message.CreatedAt ? new Date(message.CreatedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '')
            };
            
            // Add to state (channel messages are oldest-first, so append to end)
            commsSidebarState.messages.push(formattedMessage);
            
            // Add to UI at the bottom
            addMessageToUI(formattedMessage);
            
            // Scroll to bottom
            setTimeout(() => {
                const container = document.getElementById('commsMessagesContainer');
                if (container) {
                    container.scrollTop = container.scrollHeight;
                }
            }, 100);
        } else {
            console.log('Message does not match current channel, ignoring');
        }
        
        // Update unread badge
        updateUnreadBadge(message.ChannelId || message.ConversationId || message.channelId);
    });
    
    // Handle errors from SignalR
    commsSidebarState.signalRConnection.on("Error", function(error) {
        console.error('ChatHub SignalR Error:', error);
    });
    
    // Handle user online/offline status
    commsSidebarState.signalRConnection.on("UserOnline", function(data) {
        const userId = data.UserId || data.userId;
        if (userId) {
            console.log('User came online:', userId);
            updateUserStatus(userId, 'online');
        }
    });
    
    commsSidebarState.signalRConnection.on("UserOffline", function(data) {
        const userId = data.UserId || data.userId;
        if (userId) {
            console.log('User went offline:', userId);
            updateUserStatus(userId, 'offline');
        }
    });
    
    // Start connection
    commsSidebarState.signalRConnection.start()
        .then(function() {
            console.log('Communications SignalR (ChatHub) connected');
            
            // Join current channel if one is selected
            if (commsSidebarState.currentChannelId && commsSidebarState.currentChannelType !== 'dm') {
                commsSidebarState.signalRConnection.invoke("JoinChannel", commsSidebarState.currentChannelId.toString())
                    .catch(err => console.error('Error joining channel:', err));
            }
        })
        .catch(function(err) {
            console.error('Communications SignalR connection error:', err);
        });
}

// Initialize DirectHub SignalR connection for DMs
function initializeDirectSignalR() {
    // Check if SignalR is available
    if (typeof signalR === 'undefined') {
        console.log('SignalR not available for direct messaging');
        return;
    }
    
    if (!commsSidebarState.organizationId) {
        console.log('Organization ID not set, skipping DirectHub connection');
        return;
    }
    
    // Create DirectHub SignalR connection
    commsSidebarState.directSignalRConnection = new signalR.HubConnectionBuilder()
        .withUrl(`/hubs/direct?orgId=${commsSidebarState.organizationId}`)
        .withAutomaticReconnect()
        .build();
    
    // Handle incoming direct messages
    commsSidebarState.directSignalRConnection.on("ReceiveDirectMessage", function(message) {
        console.log('🔔 ReceiveDirectMessage FIRED!', message);
        console.log('🔍 Full message object:', JSON.stringify(message, null, 2));
        console.log('🔍 Message keys:', Object.keys(message));
        console.log('📧 Message ThreadId (capital T):', message.ThreadId);
        console.log('📧 Message threadId (lowercase t):', message.threadId);
        console.log('📍 Current thread ID:', commsSidebarState.currentChannelId);
        console.log('🔖 Current channel type:', commsSidebarState.currentChannelType);
        
        // Remove optimistic message if it exists (by checking if we have a temp message)
        const messagesList = document.getElementById('commsMessagesList');
        if (messagesList) {
            const tempMessage = messagesList.querySelector('[data-message-id^="temp-"]');
            if (tempMessage) {
                console.log('🗑️ Removing optimistic message');
                tempMessage.remove();
            }
        }
        
        // Normalize thread IDs for comparison (GUIDs can come in different formats)
        // SignalR may camelCase properties, so check multiple variations
        // Also check if ThreadId comes from the group name or message payload
        const threadIdFromMessage = message.ThreadId || message.threadId || message.ThreadID || message.threadID;
        
        // If ThreadId is still not found, try to extract from group name if available
        // Or use the threadId parameter passed to SendMessage (which we stored)
        let messageThreadId = normalizeGuid(threadIdFromMessage);
        
        // If message doesn't have ThreadId but we're in the right thread, use current thread ID
        // This handles cases where backend doesn't include ThreadId in broadcast
        if (!messageThreadId && commsSidebarState.currentChannelType === 'dm') {
            console.log('⚠️ Message missing ThreadId, but we are in DM mode. Using current thread ID.');
            messageThreadId = normalizeGuid(commsSidebarState.currentChannelId);
        }
        
        const currentThreadId = normalizeGuid(commsSidebarState.currentChannelId);
        
        console.log('✨ Normalized message ThreadId:', messageThreadId);
        console.log('✨ Normalized current thread ID:', currentThreadId);
        console.log('🔍 Thread IDs match?', messageThreadId === currentThreadId);
        console.log('🔍 Is DM mode?', commsSidebarState.currentChannelType === 'dm');
        
        // Only process if we're in DM mode and thread IDs match
        if (commsSidebarState.currentChannelType === 'dm' && messageThreadId === currentThreadId) {
            console.log('✅ Message matches current thread, adding to UI');
            
            // SignalR may camelCase properties, so check both cases
            const senderId = message.SenderId || message.senderId;
            let senderName = message.SenderName || message.senderName;
            const body = message.Body || message.body;
            const createdAt = message.CreatedAt || message.createdAt;
            
            // If senderName is missing and this is the current user, use current user's name
            if (!senderName || senderName.trim() === '') {
                if (senderId && commsSidebarState.currentUserId && 
                    parseInt(senderId) === parseInt(commsSidebarState.currentUserId)) {
                    senderName = commsSidebarState.currentUserName || 'You';
                } else {
                    senderName = 'Unknown';
                }
            }
            
            // Transform message format to match what renderCommsMessages expects
            const formattedMessage = {
                id: message.Id || message.id,
                Id: message.Id || message.id,
                senderId: senderId,
                SenderId: senderId,
                userId: senderId,
                UserId: senderId,
                senderName: senderName,
                SenderName: senderName,
                body: body,
                Body: body,
                content: body,
                Content: body,
                createdAt: createdAt,
                CreatedAt: createdAt,
                Time: createdAt ? new Date(createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''
            };
            
            // Add to state (newest at end since we reversed the initial load)
            commsSidebarState.messages.push(formattedMessage);
            
            // Add to UI at the bottom
            addMessageToUI(formattedMessage);
            
            // Scroll to bottom
            setTimeout(() => {
                const container = document.getElementById('commsMessagesContainer');
                if (container) {
                    container.scrollTop = container.scrollHeight;
                }
            }, 100);
        } else {
            console.log('Message does not match current thread or not in DM mode. ThreadId:', messageThreadId, 'Current:', currentThreadId, 'Type:', commsSidebarState.currentChannelType);
        }
    });
    
    // Handle typing indicators
    commsSidebarState.directSignalRConnection.on("UserTyping", function(data) {
        // Could add typing indicator here if needed
        console.log('User typing:', data);
    });
    
    // Handle errors
    commsSidebarState.directSignalRConnection.on("Error", function(error) {
        console.error('DirectHub SignalR Error:', error);
    });
    
    // Start connection
    commsSidebarState.directSignalRConnection.start()
        .then(function() {
            console.log('✅ DirectHub SignalR connected successfully');
            console.log('🔌 Connection ID:', commsSidebarState.directSignalRConnection.connectionId);
            
            // Join current thread if one is selected
            if (commsSidebarState.currentChannelId && commsSidebarState.currentChannelType === 'dm') {
                console.log('🚪 Rejoining existing thread:', commsSidebarState.currentChannelId);
                commsSidebarState.directSignalRConnection.invoke("JoinThread", commsSidebarState.currentChannelId.toString())
                    .then(() => console.log('✅ Rejoined thread successfully'))
                    .catch(err => console.error('❌ Error joining thread:', err));
            }
        })
        .catch(function(err) {
            console.error('❌ DirectHub SignalR connection error:', err);
        });
}

// Update user status in sidebar
function updateUserStatus(userId, status) {
    // Update in team members state
    const member = commsSidebarState.teamMembers.find(m => m.userId == userId);
    if (member) {
        member.status = status;
        // Check if external contacts - don't update activity for external users via status updates
        const isExternalContacts = member.isExternalContacts || member.organizationName?.endsWith("'s External Contacts");
        if (!isExternalContacts) {
        member.activity = status === 'online' ? 'Online' : 'Offline';
        }
    }
    
    // Update active members display if currently visible
    if (commsSidebarState.currentView === 'chat') {
        renderActiveMembers();
    }
    
    // Update DM list status indicators and activity text (only for non-external users)
    const dmItem = document.querySelector(`.comms-dm-item[data-user-id="${userId}"]`);
    if (dmItem) {
        const isExternal = dmItem.dataset.isExternal === 'true';
        if (!isExternal) {
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
    // Get from appContext first - THIS IS THE PRIMARY SOURCE
    const appContext = document.getElementById('appContext');
    if (appContext) {
        const userId = appContext.dataset.currentUserId;
        // Don't return "0" which is the default when user is null
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return userId;
        }
    }
    
    // Try from specific data-current-user-id attribute (not data-user-id which could be any user)
    const userIdElement = document.querySelector('[data-current-user-id]');
    if (userIdElement && userIdElement.dataset.currentUserId) {
        const userId = userIdElement.dataset.currentUserId;
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return userId;
        }
    }
    
    // Try from meta tag
    const metaUserId = document.querySelector('meta[name="current-user-id"]');
    if (metaUserId) {
        const userId = metaUserId.getAttribute('content');
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return userId;
        }
    }
    
    // Fallback: try to get from window object (set by page scripts)
    if (window.currentUserId) {
        const userId = window.currentUserId.toString();
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return userId;
        }
    }
    
    // REMOVED DANGEROUS FALLBACK: querySelector('[data-user-id]') was picking up
    // any user element on the page, including client users in scoped views
    
    console.warn('getCurrentUserId: Could not find current user ID from any source');
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

// Normalize GUID for consistent comparison (handles different formats)
function normalizeGuid(guid) {
    if (!guid) return null;
    // Convert to string, lowercase, trim whitespace, remove braces
    return guid.toString().toLowerCase().trim().replace(/[{}]/g, '');
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
                <div class="member-avatar-small current-user-avatar" title="${commsSidebarState.currentUserName || 'You'}">
                    ${currentUserInitials}
                    <div class="status-indicator ${currentUserStatus}"></div>
                </div>
            `;
            
            // Other user
            if (dmUserName) {
                const otherUserInitials = getInitials(dmUserName);
                const otherUserMember = commsSidebarState.teamMembers.find(m => m.userId == dmUserId);
                const otherUserStatus = otherUserMember ? otherUserMember.status : 'offline';
                const isExternalContacts = otherUserMember?.isExternalContacts || otherUserMember?.organizationName?.endsWith("'s External Contacts");
                let avatarColor = otherUserMember?.color || otherUserMember?.Color || (isExternalContacts ? '#aaaaaa' : '#3d1019');
                if (isExternalContacts && avatarColor === '#9ca3af') {
                    avatarColor = '#aaaaaa';
                }
                const avatarClass = isExternalContacts ? 'member-avatar-small external-contacts-avatar' : 'member-avatar-small';
                const dataAttr = isExternalContacts ? ' data-is-external="true"' : '';
                
                // Only show status indicator for non-external users
                const statusIndicatorHtml = isExternalContacts ? '' : `<div class="status-indicator ${otherUserStatus}"></div>`;
                
                html += `
                    <div class="${avatarClass}" title="${dmUserName}"${dataAttr} style="background: ${avatarColor} !important;">
                        ${otherUserInitials}
                        ${statusIndicatorHtml}
                    </div>
                `;
            }
        } else {
            // DM with self - show only one avatar
            const currentUserInitials = getInitials(commsSidebarState.currentUserName || 'You');
            const currentUserMember = commsSidebarState.teamMembers.find(m => m.userId == commsSidebarState.currentUserId);
            const currentUserStatus = currentUserMember ? currentUserMember.status : 'online';
            
            html += `
                <div class="member-avatar-small current-user-avatar" title="${commsSidebarState.currentUserName || 'You'} (Self)">
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
                        const isCurrentUser = member.userId == commsSidebarState.currentUserId;
                        const isExternalContacts = member.isExternalContacts || member.organizationName?.endsWith("'s External Contacts");
                        let avatarColor = member.color || member.Color || (isExternalContacts ? '#aaaaaa' : '#3d1019');
                        if (isExternalContacts && avatarColor === '#9ca3af') {
                            avatarColor = '#aaaaaa';
                        }
                        let avatarClass = 'member-avatar-small';
                        if (isCurrentUser) {
                            avatarClass += ' current-user-avatar';
                        } else if (isExternalContacts) {
                            avatarClass += ' external-contacts-avatar';
                        }
                        const dataAttr = isExternalContacts ? ' data-is-external="true"' : '';
                        // Don't apply inline style for current user (let CSS gradient handle it)
                        let avatarStyle = '';
                        if (!isCurrentUser) {
                            avatarStyle = ` style="background: ${avatarColor} !important;"`;
                        }
                        // Only show status indicator for non-external users
                        const statusIndicatorHtml = isExternalContacts ? '' : `<div class="status-indicator ${status}"></div>`;
                        html += `
                            <div class="${avatarClass}" title="${member.name}"${dataAttr}${avatarStyle}>
                                ${avatar}
                                ${statusIndicatorHtml}
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
                const isCurrentUser = member.userId == commsSidebarState.currentUserId;
                const isExternalContacts = member.isExternalContacts || member.organizationName?.endsWith("'s External Contacts");
                let avatarColor = member.color || member.Color || (isExternalContacts ? '#aaaaaa' : '#3d1019');
                if (isExternalContacts && avatarColor === '#9ca3af') {
                    avatarColor = '#aaaaaa';
                }
                let avatarClass = 'member-avatar-small';
                if (isCurrentUser) {
                    avatarClass += ' current-user-avatar';
                } else if (isExternalContacts) {
                    avatarClass += ' external-contacts-avatar';
                }
                const dataAttr = (isExternalContacts && !isCurrentUser) ? ' data-is-external="true"' : '';
                // Don't apply inline style for current user (let CSS gradient handle it)
                let avatarStyle = '';
                if (!isCurrentUser) {
                    avatarStyle = ` style="background: ${avatarColor} !important;"`;
                }
                html += `
                    <div class="${avatarClass}" title="${member.name}"${dataAttr}${avatarStyle}>
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
    openDM: openCommsDM,
    sendMessage: sendCommsMessage,
    get isInitialized() {
        return commsSidebarState.isInitialized;
    }
};

// Expose state for external access
window.commsSidebarState = commsSidebarState;

