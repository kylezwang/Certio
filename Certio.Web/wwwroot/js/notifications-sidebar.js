// Notifications Sidebar JavaScript

(function() {
    'use strict';

    // Initialize when DOM is loaded
    document.addEventListener('DOMContentLoaded', function() {
        initializeNotificationsSidebar();
        renderCalendar();
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
        
        // Apply saved width or default (use same key as other sidebars for consistency)
        const savedWidth = localStorage.getItem('chatPanelWidth') || '320';
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
        
        // Don't reposition action buttons when just showing/hiding - only when resizing
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

    // Expose functions globally
    window.showNotificationsSidebar = showNotificationsSidebar;
    window.hideNotificationsSidebar = hideNotificationsSidebar;
})();

