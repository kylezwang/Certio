// Notifications Sidebar JavaScript

(function() {
    'use strict';

    // Initialize when DOM is loaded
    document.addEventListener('DOMContentLoaded', function() {
        initializeNotificationsSidebar();
        loadRecentMessages();
        loadDeadlines();
    });

    function initializeNotificationsSidebar() {
        const notificationsPanel = document.getElementById('notificationsSidebarPanel');
        const notificationsCloseBtn = document.getElementById('notificationsCloseBtn');
        const resizeHandle = document.getElementById('resizeHandle');
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        
        if (!notificationsPanel) return;

        // Restore saved width immediately to prevent overflow on reload
        const savedWidth = localStorage.getItem('notificationsPanelWidth') || '320';
        notificationsPanel.style.width = savedWidth + 'px';
        
        // If notifications sidebar is active, apply sizing immediately
        const activeSidebar = localStorage.getItem('activeSidebar');
        if (activeSidebar === 'notifications') {
            // Apply saved width to all relevant elements
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
        } else {
            notificationsPanel.style.display = 'none';
        }

        // Close button handler
        if (notificationsCloseBtn) {
            notificationsCloseBtn.addEventListener('click', function() {
                hideNotificationsSidebar();
            });
        }
    }

    function showNotificationsSidebar() {
        const notificationsPanel = document.getElementById('notificationsSidebarPanel');
        const chatPanel = document.getElementById('chatPanel');
        const commsSidebarPanel = document.getElementById('commsSidebarPanel');
        const resizeHandle = document.getElementById('resizeHandle');
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        const notificationsBtn = document.getElementById('notificationsBtn');
        const aiToggle = document.getElementById('aiToggle');
        const communicationsBtn = document.getElementById('communicationsBtn');
        
        if (!notificationsPanel) return;

        // Hide chat and comms panels explicitly
        document.body.classList.add('chat-hidden');
        if (chatPanel) {
            chatPanel.style.display = 'none';
        }
        if (commsSidebarPanel) {
            commsSidebarPanel.style.display = 'none';
        }

        // Show notifications panel
        notificationsPanel.style.display = 'flex';
        
        // Apply saved width or default (notifications shares width key with communications)
        const savedWidth = localStorage.getItem('notificationsPanelWidth') || '320';
        notificationsPanel.style.width = savedWidth + 'px';
        
        // Position resize handle
        if (resizeHandle) {
            resizeHandle.style.right = (parseInt(savedWidth) - 12) + 'px';
            resizeHandle.style.display = 'flex';
        }
        
        // Adjust main content wrapper
        if (mainContentWrapper) {
            mainContentWrapper.style.right = savedWidth + 'px';
        }

        // Update floating timer overlay position - only restore if manually positioned
        const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
        if (floatingTimerOverlay) {
            const savedTimerPosition = localStorage.getItem('timerPosition');
            if (savedTimerPosition) {
                // Restore saved manual position
                const position = JSON.parse(savedTimerPosition);
                floatingTimerOverlay.style.left = position.left;
                floatingTimerOverlay.style.top = position.top;
                floatingTimerOverlay.style.right = ''; // Clear right
                floatingTimerOverlay.style.bottom = ''; // Clear bottom
                floatingTimerOverlay.style.transform = ''; // Clear transform
            }
            // If no saved position, leave timer at default position (center of left sidebar area)
        }

        // Update button states - only notifications button should be active
        if (notificationsBtn) {
            notificationsBtn.classList.add('active');
        }
        if (aiToggle) {
            aiToggle.classList.remove('active');
        }
        if (communicationsBtn) {
            communicationsBtn.classList.remove('active');
        }

        // Mark current sidebar target
        window.currentSidebarTarget = 'notifications';

        // Remove chat-hidden class
        document.body.classList.remove('chat-hidden');
        document.body.classList.remove('no-sidebars');
        
        // Save to localStorage
        localStorage.setItem('activeSidebar', 'notifications');

        // Update Notal logo - ensure it's not inverted when notifications is active
        // Call immediately after removing active class from AI toggle
        if (typeof window.updateNotalLogo === 'function') {
            window.updateNotalLogo();
        }
        
        // Also use setTimeout as backup to ensure DOM updates have completed
        setTimeout(() => {
            if (typeof window.updateNotalLogo === 'function') {
                window.updateNotalLogo();
            }
        }, 10);
        
        // Recalculate and save action buttons position after transition completes
        setTimeout(() => {
            if (typeof window.centerSearchBar === 'function') {
                window.centerSearchBar();
            }
        }, 350);
        
        // Reload deadlines when sidebar is shown
        loadDeadlines();
        
        // Load recent messages when sidebar is shown
        loadRecentMessages();
    }

    function hideNotificationsSidebar() {
        const notificationsPanel = document.getElementById('notificationsSidebarPanel');
        const resizeHandle = document.getElementById('resizeHandle');
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        const notificationsBtn = document.getElementById('notificationsBtn');
        const aiToggle = document.getElementById('aiToggle');
        const communicationsBtn = document.getElementById('communicationsBtn');
        
        if (!notificationsPanel) return;

        // Hide notifications panel
        notificationsPanel.style.display = 'none';
        
        // Hide resize handle
        if (resizeHandle) {
            resizeHandle.style.display = 'none';
        }
        
        // Reset main content wrapper
        if (mainContentWrapper) {
            mainContentWrapper.style.right = '1rem';
        }

        // Reset floating timer overlay position
        const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
        if (floatingTimerOverlay) {
            floatingTimerOverlay.style.right = '1.5rem';
        }

        // Update button states - remove active from all
        if (notificationsBtn) {
            notificationsBtn.classList.remove('active');
        }
        if (aiToggle) {
            aiToggle.classList.remove('active');
        }
        if (communicationsBtn) {
            communicationsBtn.classList.remove('active');
        }

        // Update Notal logo immediately after removing active classes
        if (typeof window.updateNotalLogo === 'function') {
            window.updateNotalLogo();
        }
        
        // Also use setTimeout as backup to ensure DOM updates have completed
        setTimeout(() => {
            if (typeof window.updateNotalLogo === 'function') {
                window.updateNotalLogo();
            }
        }, 10);

        // Add chat-hidden class
        document.body.classList.add('chat-hidden');
        document.body.classList.add('no-sidebars');
        
        // Update sidebar target
        window.currentSidebarTarget = 'none';
        
        // Save to localStorage
        localStorage.setItem('activeSidebar', 'none');
        
        // Recenter search bar after transition completes (action buttons will stay at saved position)
        setTimeout(() => {
            if (typeof window.centerSearchBar === 'function') {
                window.centerSearchBar();
            }
        }, 350);
    }

    function renderCalendar() {
        const calendarBody = document.getElementById('calendarBody');
        const calendarMonth = document.getElementById('calendarMonth');
        
        if (!calendarBody) return;

        const now = new Date();
        const year = now.getFullYear();
        const month = now.getMonth();
        const today = now.getDate();

        // Update month title
        if (calendarMonth) {
            const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
                'July', 'August', 'September', 'October', 'November', 'December'];
            calendarMonth.textContent = `${monthNames[month]} ${year}`;
        }

        // Get first day of month and calculate start date
        const firstDay = new Date(year, month, 1);
        const startDay = new Date(firstDay);
        startDay.setDate(startDay.getDate() - firstDay.getDay());

        // Clear existing calendar
        calendarBody.innerHTML = '';

        // Generate 6 weeks
        for (let week = 0; week < 6; week++) {
            const weekDiv = document.createElement('div');
            weekDiv.className = 'calendar-week';

            for (let day = 0; day < 7; day++) {
                const currentDate = new Date(startDay);
                currentDate.setDate(currentDate.getDate() + (week * 7 + day));

                const isCurrentMonth = currentDate.getMonth() === month;
                const isToday = currentDate.getDate() === today && 
                               currentDate.getMonth() === month && 
                               currentDate.getFullYear() === year;

                const dayDiv = document.createElement('div');
                dayDiv.className = 'calendar-day';
                if (isCurrentMonth) dayDiv.classList.add('current-month');
                if (!isCurrentMonth) dayDiv.classList.add('other-month');
                if (isToday) dayDiv.classList.add('today');
                dayDiv.textContent = currentDate.getDate();

                // Add click handler
                dayDiv.addEventListener('click', function() {
                    // Remove selected from all days
                    document.querySelectorAll('.calendar-day.selected').forEach(el => {
                        el.classList.remove('selected');
                    });
                    // Remove today class if selecting a different day
                    if (!this.classList.contains('today')) {
                        document.querySelectorAll('.calendar-day.today').forEach(el => {
                            el.classList.remove('today');
                        });
                    }
                    // Add selected to clicked day
                    this.classList.add('selected');
                });

                weekDiv.appendChild(dayDiv);
            }

            calendarBody.appendChild(weekDiv);
        }
    }

    // Load recent messages from API
    async function loadRecentMessages() {
        const recentMessagesList = document.getElementById('recentMessagesList');
        if (!recentMessagesList) return;

        // Get organization ID from the page
        let orgId = null;
        
        // Try to get from window object (set by page)
        if (window.currentOrganizationId) {
            orgId = window.currentOrganizationId;
        }
        // Try to get from URL path
        else {
            const pathMatch = window.location.pathname.match(/\/Client\/(\d+)/);
            if (pathMatch) {
                orgId = parseInt(pathMatch[1]);
            }
        }

        if (!orgId) {
            recentMessagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">Organization ID not found</div>';
            return;
        }

        try {
            const response = await fetch(`/api/communications/recent-messages?orgId=${orgId}&take=10`);
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const result = await response.json();
            
            if (result.success && result.messages && result.messages.length > 0) {
                renderRecentMessages(result.messages);
                updateRecentMessagesBadge(result.messages.length);
            } else {
                recentMessagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">No recent messages</div>';
                updateRecentMessagesBadge(0);
            }
        } catch (error) {
            console.error('Error loading recent messages:', error);
            recentMessagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">Failed to load messages</div>';
        }
    }

    // Render recent messages
    function renderRecentMessages(messages) {
        const recentMessagesList = document.getElementById('recentMessagesList');
        if (!recentMessagesList) return;

        if (messages.length === 0) {
            recentMessagesList.innerHTML = '<div style="text-align: center; padding: 2rem; color: #9ca3af;">No recent messages</div>';
            return;
        }

        let html = '';
        
        messages.forEach(message => {
            const senderName = message.senderName || 'Unknown';
            const avatar = getInitials(senderName);
            const content = escapeHtml(message.content || '').substring(0, 100);
            const channelName = message.channelName || 'Unknown';
            const time = message.time || formatTime(message.createdAt);
            const isChannel = message.type === 'channel';
            const isMatterChannel = isChannel && message.matterId != null;
            
            // Get sender color and check if external contacts
            const isExternalContacts = message.isExternalContacts || false;
            let senderColor = message.senderColor || (isExternalContacts ? '#aaaaaa' : '#3d1019');
            // Normalize #9ca3af to #aaaaaa for external contacts
            if (isExternalContacts && senderColor === '#9ca3af') {
                senderColor = '#aaaaaa';
            }
            
            // Build avatar style with color
            const avatarStyle = `background: ${senderColor} !important;`;
            const avatarClass = isExternalContacts ? 'recent-message-avatar external-contacts-avatar' : 'recent-message-avatar';
            const avatarDataAttr = isExternalContacts ? ' data-is-external="true"' : '';
            
            // Determine icon based on message type and channel type
            let channelIconHtml = '';
            if (isChannel) {
                if (isMatterChannel) {
                    channelIconHtml = '<i class="fas fa-folder" style="font-size: 0.875rem; color: #6b7280;"></i>';
                } else {
                    channelIconHtml = '#';
                }
            } else {
                channelIconHtml = '<i class="fas fa-comments" style="font-size: 0.875rem; color: #6b7280;"></i>';
            }

            html += `
                <div class="recent-message-item" 
                     data-message-id="${message.id}" 
                     data-type="${message.type}" 
                     ${message.channelId ? `data-channel-id="${message.channelId}"` : ''} 
                     ${message.channelName ? `data-channel-name="${escapeHtml(channelName)}"` : ''}
                     ${message.threadId ? `data-thread-id="${message.threadId}"` : ''}
                     ${message.senderId ? `data-sender-id="${message.senderId}"` : ''}>
                    <div class="${avatarClass}"${avatarDataAttr} style="${avatarStyle}">${avatar}</div>
                    <div class="recent-message-content">
                        <div class="recent-message-header">
                            <span class="recent-message-channel">
                                <span class="recent-message-channel-icon">${channelIconHtml}</span>
                                <span class="recent-message-channel-name">${escapeHtml(channelName)}</span>
                            </span>
                            <span class="recent-message-time">${time}</span>
                        </div>
                        <div class="recent-message-author">${escapeHtml(senderName)}</div>
                        <div class="recent-message-text">${content}${message.content && message.content.length > 100 ? '...' : ''}</div>
                    </div>
                </div>
            `;
        });

        recentMessagesList.innerHTML = html;

        // Add click handlers to navigate to Communications
        recentMessagesList.querySelectorAll('.recent-message-item').forEach(item => {
            item.addEventListener('click', function() {
                const type = this.dataset.type;
                const channelId = this.dataset.channelId;
                const channelName = this.dataset.channelName || '';
                const threadId = this.dataset.threadId;
                const senderId = this.dataset.senderId;

                // Open Communications sidebar and navigate to the message
                if (typeof window.showCommsSidebar === 'function') {
                    window.showCommsSidebar();
                    
                    // Small delay to ensure sidebar is open
                    setTimeout(() => {
                        if (type === 'channel' && channelId) {
                            // Navigate to channel - use channelName from data attribute
                            if (window.commsSidebar && typeof window.commsSidebar.selectChannel === 'function') {
                                window.commsSidebar.selectChannel(channelId, channelName, 'channel', '#');
                            } else if (typeof window.selectCommsChannel === 'function') {
                                window.selectCommsChannel(channelId, channelName, 'channel', '#');
                            }
                            // Reload recent messages after opening channel to refresh badge
                            setTimeout(() => {
                                loadRecentMessages();
                            }, 500);
                        } else if (type === 'dm' && threadId && channelName) {
                            // Navigate to DM thread - use channelName (other user's name) and find their ID
                            // First try to find user ID from team members list
                            if (window.commsSidebar && typeof window.openCommsDMByThreadId === 'function') {
                                window.openCommsDMByThreadId(threadId, channelName);
                            } else {
                                // Fallback: find user by name from team members
                                findAndOpenDMThread(threadId, channelName, senderId);
                            }
                            // Reload recent messages after opening DM to refresh badge
                            setTimeout(() => {
                                loadRecentMessages();
                            }, 500);
                        }
                    }, 300);
                }
            });
        });
    }

    // Helper function to find and open DM thread
    async function findAndOpenDMThread(threadId, otherUserName, senderId) {
        try {
            // Try to find user ID from team members in communications sidebar
            let otherUserId = null;
            
            // Check if communications sidebar is initialized and has team members
            if (window.commsSidebar && window.commsSidebarState && window.commsSidebarState.teamMembers) {
                const teamMember = window.commsSidebarState.teamMembers.find(member => 
                    member.name && member.name.toLowerCase() === otherUserName.toLowerCase()
                );
                if (teamMember) {
                    otherUserId = teamMember.userId || teamMember.id;
                }
            }
            
            // If not found and we have senderId, determine if sender is the other user or current user
            if (!otherUserId && senderId) {
                const currentUserId = getCurrentUserId();
                // If senderId is not the current user, then senderId is the other user
                if (currentUserId && parseInt(senderId) !== parseInt(currentUserId)) {
                    otherUserId = senderId;
                } else {
                    // Sender is current user, need to find other user from thread
                    // Fetch thread info to get other user ID
                    const orgId = window.currentOrganizationId || (window.location.pathname.match(/\/Client\/(\d+)/)?.[1]);
                    if (orgId) {
                        try {
                            const response = await fetch(`/api/dm/threads/${threadId}/info?orgId=${orgId}`);
                            if (response.ok) {
                                const result = await response.json();
                                if (result.success && result.otherUserId) {
                                    otherUserId = result.otherUserId;
                                }
                            }
                        } catch (e) {
                            console.error('Error fetching thread info:', e);
                        }
                    }
                }
            }
            
            // If we found otherUserId, open DM
            if (otherUserId) {
                if (window.commsSidebar && typeof window.commsSidebar.openDM === 'function') {
                    window.commsSidebar.openDM(otherUserId, otherUserName);
                } else if (typeof window.openCommsDM === 'function') {
                    window.openCommsDM(otherUserId, otherUserName);
                } else {
                    console.error('Cannot open DM: openCommsDM function not found');
                }
            } else {
                // Fallback: try to open by threadId directly if function exists
                if (window.commsSidebar && typeof window.commsSidebar.openDMByThreadId === 'function') {
                    window.commsSidebar.openDMByThreadId(threadId, otherUserName);
                } else {
                    console.error('Cannot open DM: unable to determine other user ID');
                }
            }
        } catch (error) {
            console.error('Error opening DM thread:', error);
        }
    }
    
    function getCurrentUserId() {
        const appContext = document.getElementById('appContext');
        if (appContext && appContext.dataset.currentUserId) {
            const userId = appContext.dataset.currentUserId;
            if (userId && userId !== "0" && userId !== "null" && userId !== "") {
                return userId;
            }
        }
        if (window.currentUserId) {
            return window.currentUserId.toString();
        }
        return null;
    }

    // Load upcoming deadlines - matches Dashboard functionality
    async function loadDeadlines() {
        const deadlinesList = document.getElementById('deadlinesList');
        if (!deadlinesList) return;
        
        // Show loading state
        deadlinesList.innerHTML = '<p class="text-muted small" style="padding: 0.5rem;">Loading deadlines...</p>';
        
        try {
            // Get organization ID from the page
            let orgId = null;
            
            // Try to get from window object (set by page)
            if (window.currentOrganizationId) {
                orgId = window.currentOrganizationId;
            }
            // Try to get from URL path
            else {
                const pathMatch = window.location.pathname.match(/\/Client\/(\d+)/);
                if (pathMatch) {
                    orgId = parseInt(pathMatch[1]);
                }
            }

            if (!orgId) {
                deadlinesList.innerHTML = '<p class="text-muted small" style="padding: 0.5rem;">Unable to load deadlines.</p>';
                return;
            }
            
            // Fetch upcoming calendar events (deadlines) - next 30 days
            const endDate = new Date();
            endDate.setDate(endDate.getDate() + 30);
            
            const response = await fetch(`/Client/${orgId}/Calendar/Events?start=${new Date().toISOString()}&end=${endDate.toISOString()}`, {
                credentials: 'include'
            });
            
            if (!response.ok) {
                throw new Error('Failed to fetch deadlines');
            }
            
            const result = await response.json();
            
            if (!result.success || !result.events || result.events.length === 0) {
                deadlinesList.innerHTML = '<p class="text-muted small" style="padding: 0.5rem;">No upcoming deadlines.</p>';
                return;
            }
            
            // Filter and sort events as deadlines
            const deadlines = result.events
                .filter(e => new Date(e.startDateTime) > new Date())
                .sort((a, b) => new Date(a.startDateTime) - new Date(b.startDateTime))
                .slice(0, 5); // Show top 5
            
            if (deadlines.length === 0) {
                deadlinesList.innerHTML = '<p class="text-muted small" style="padding: 0.5rem;">No upcoming deadlines.</p>';
                return;
            }
            
            // Render deadlines with same styling as Dashboard
            deadlinesList.innerHTML = deadlines.map(deadline => {
                const date = new Date(deadline.startDateTime);
                const isToday = date.toDateString() === new Date().toDateString();
                const daysDiff = Math.ceil((date - new Date()) / (1000 * 60 * 60 * 24));
                
                // Determine urgency class (border color)
                let urgencyClass = '';
                if (daysDiff <= 1) {
                    urgencyClass = 'border-danger';
                } else if (daysDiff <= 3) {
                    urgencyClass = 'border-warning';
                } else {
                    urgencyClass = 'border-primary';
                }
                
                // Format date
                const dateStr = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
                const timeStr = date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
                
                return `
                    <div class="deadline-item ${urgencyClass} border-start border-3 ps-2 mb-2">
                        <div class="d-flex justify-content-between align-items-start">
                            <div style="flex: 1; min-width: 0;">
                                <div class="fw-semibold text-truncate" style="font-size: 0.85rem;">${escapeHtml(deadline.title)}</div>
                                <div class="text-muted small">${dateStr}${isToday ? ' (Today)' : ''} • ${timeStr}</div>
                            </div>
                        </div>
                    </div>
                `;
            }).join('');
            
        } catch (error) {
            console.error('Error loading deadlines:', error);
            deadlinesList.innerHTML = '<p class="text-muted small" style="padding: 0.5rem;">Error loading deadlines.</p>';
        }
    }

    // Helper functions
    function getInitials(name) {
        if (!name || name === 'Unknown') return '?';
        const parts = name.trim().split(' ');
        if (parts.length >= 2) {
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        }
        return name.substring(0, 2).toUpperCase();
    }

    function formatTime(timestamp) {
        if (!timestamp) return '';
        const date = new Date(timestamp);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'Just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;
        
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Update recent messages badge
    function updateRecentMessagesBadge(count) {
        const badge = document.getElementById('recentMessagesBadge');
        if (badge) {
            if (count > 0) {
                badge.textContent = count;
                badge.style.display = 'block';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    // Expose functions globally
    window.showNotificationsSidebar = showNotificationsSidebar;
    window.hideNotificationsSidebar = hideNotificationsSidebar;
})();

