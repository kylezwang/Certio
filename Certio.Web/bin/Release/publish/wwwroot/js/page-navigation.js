// AJAX Page Navigation System
(function() {
    'use strict';

    // Configuration - can be disabled if needed
    const config = {
        enabled: false, // Set to false to disable AJAX navigation - DISABLED UNTIL PAGES ARE AJAX-READY
        debug: false, // Set to false to reduce console logging
        fallbackOnError: true // Automatically fallback to full page load on errors
    };

    // Track loaded pages to avoid reloading
    const loadedPages = new Map();
    let isNavigating = false;

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function() {
        if (!config.enabled) {
            console.log('AJAX navigation is disabled');
            return;
        }
        
        try {
            initializeNavigation();
        } catch (error) {
            console.error('Error initializing AJAX navigation:', error);
            console.log('Falling back to traditional navigation');
        }
    });

    // Handle browser back/forward
    window.addEventListener('popstate', function(event) {
        if (event.state && event.state.url) {
            loadPage(event.state.url, false); // false = don't push to history
        }
    });

    function initializeNavigation() {
        // Get all sidebar navigation links (excluding logout and external links)
        const navLinks = document.querySelectorAll('.sidebar-navigation .nav-link');
        
        if (!navLinks || navLinks.length === 0) {
            if (config.debug) {
                console.warn('No navigation links found with selector: .sidebar-navigation .nav-link');
            }
            // Don't throw error, just silently skip if not found
            return;
        }
        
        if (config.debug) {
            console.log(`Initializing AJAX navigation for ${navLinks.length} links`);
        }
        
        navLinks.forEach(link => {
            // Skip if it's a logout link or form submission
            const href = link.getAttribute('href');
            if (!href || href === '#' || href.includes('Logout') || link.closest('form')) {
                return;
            }
            
            // Validate URL is safe for AJAX navigation
            if (!isValidNavigationUrl(href)) {
                console.warn('Skipping invalid navigation URL:', href);
                return;
            }
            
            link.addEventListener('click', function(e) {
                e.preventDefault();
                const url = this.getAttribute('href');
                
                if (url && url !== '#' && !isNavigating) {
                    loadPage(url, true);
                }
            });
        });

        // Store initial page in history
        const currentUrl = window.location.pathname;
        history.replaceState({ url: currentUrl }, '', currentUrl);
    }

    function isValidNavigationUrl(url) {
        // Security check: Only allow relative URLs within the application
        if (!url || url === '#') return false;
        
        // Block absolute URLs to other domains
        if (url.startsWith('http://') || url.startsWith('https://')) {
            try {
                const urlObj = new URL(url);
                if (urlObj.hostname !== window.location.hostname) {
                    return false;
                }
            } catch (e) {
                return false;
            }
        }
        
        // Block javascript: and data: URLs (XSS protection)
        if (url.toLowerCase().startsWith('javascript:') || url.toLowerCase().startsWith('data:')) {
            return false;
        }
        
        // Allow /Client/* URLs
        if (url.startsWith('/Client/')) {
            return true;
        }
        
        return false;
    }

    function loadPage(url, pushHistory = true) {
        if (isNavigating) {
            console.log('Navigation already in progress, skipping');
            return;
        }
        
        // Validate URL before loading
        if (!isValidNavigationUrl(url)) {
            console.warn('Invalid navigation URL, falling back to full page load:', url);
            window.location.href = url;
            return;
        }
        
        isNavigating = true;
        
        if (config.debug) {
            console.log('Loading page via AJAX:', url);
        }

        // Update active state immediately for better UX
        updateActiveNavLink(url);

        // Find main content area with multiple fallbacks
        const mainContent = getMainContentElement();
        const originalContent = mainContent ? mainContent.innerHTML : '';
        
        if (!mainContent) {
            console.error('Could not find main content area, falling back to full page load');
            if (config.fallbackOnError) {
                window.location.href = url;
            }
            isNavigating = false;
            return;
        }
        
        // Show loading state with white background and burgundy spinner
        mainContent.innerHTML = `
            <div class="text-center py-5" style="background: white; min-height: 300px; display: flex; flex-direction: column; justify-content: center; align-items: center;">
                <div class="spinner-border mb-3" role="status" style="color: #3d1019; width: 3rem; height: 3rem; border-width: 0.3rem;">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <h4 class="text-dark mb-2">Loading...</h4>
                <p class="text-muted">Please wait while we load the page.</p>
            </div>
        `;

        // Make AJAX request
        fetch(url, {
            method: 'GET',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'text/html'
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.text();
        })
        .then(html => {
            // Extract just the main content from the response
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');
            
            // Find the main content area in the response - try multiple selectors
            let newContent = doc.querySelector('.client-main-content-wrapper .container-fluid');
            if (!newContent) {
                newContent = doc.querySelector('.client-main-content-wrapper > div');
            }
            if (!newContent) {
                newContent = doc.querySelector('.client-main-content-wrapper');
            }
            if (!newContent) {
                // Fallback to body content if specific selectors don't work
                newContent = doc.querySelector('body');
            }
            
            if (newContent && mainContent) {
                // Replace content
                mainContent.innerHTML = newContent.innerHTML;
                
                // Execute scripts in the new content
                executeScripts(mainContent);
                
                // Update page title
                const newTitle = doc.querySelector('title');
                if (newTitle) {
                    document.title = newTitle.textContent;
                }
                
                // Update ViewBag values if present
                updateViewBagValues(doc);
                
                // Scroll to top
                window.scrollTo({ top: 0, behavior: 'smooth' });
                
                // Update browser history
                if (pushHistory) {
                    history.pushState({ url: url }, '', url);
                }
                
                // Store in loaded pages cache
                loadedPages.set(url, {
                    content: mainContent.innerHTML,
                    timestamp: Date.now()
                });
                
                // Initialize page-specific functionality
                initializePageScripts(url);
                
                // Preserve sidebar state
                restoreSidebarState();
                
            } else {
                throw new Error('Could not find main content in response');
            }
            
            isNavigating = false;
        })
        .catch(error => {
            console.error('Error loading page:', error);
            
            // Restore original content
            if (mainContent && originalContent) {
                mainContent.innerHTML = originalContent;
            }
            
            // Show error message with fallback
            if (mainContent) {
                const errorMsg = document.createElement('div');
                errorMsg.className = 'alert alert-danger alert-dismissible fade show mt-3';
                errorMsg.innerHTML = `
                    <strong>Navigation Error:</strong> Could not load page via AJAX. 
                    <button type="button" class="btn btn-sm btn-outline-danger ms-2" onclick="location.href='${escapeHtml(url)}'">
                        Try Full Page Load
                    </button>
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                `;
                mainContent.insertBefore(errorMsg, mainContent.firstChild);
            } else {
                // If we can't show error, just do full reload
                window.location.href = url;
            }
            
            isNavigating = false;
        });
    }

    function getMainContentElement() {
        // Try multiple selectors in order of preference
        const selectors = [
            '.client-main-content-wrapper > .container-fluid',
            '.client-main-content-wrapper > div',
            '.client-main-content-wrapper',
            '.main-content',
            'main',
            '#main-content',
            '[role="main"]',
            '.content-wrapper'
        ];
        
        for (const selector of selectors) {
            const element = document.querySelector(selector);
            if (element) {
                if (config.debug) {
                    console.log('Found main content with selector:', selector);
                }
                return element;
            }
        }
        
        console.error('Could not find main content with any known selector');
        return null;
    }

    function escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function executeScripts(container) {
        // Find and execute all script tags
        const scripts = container.querySelectorAll('script');
        
        scripts.forEach((oldScript) => {
            const newScript = document.createElement('script');
            
            // Copy attributes
            Array.from(oldScript.attributes).forEach(attr => {
                newScript.setAttribute(attr.name, attr.value);
            });
            
            // Copy inline script content or src
            if (oldScript.src) {
                newScript.src = oldScript.src;
            } else {
                newScript.textContent = oldScript.innerHTML;
            }
            
            // Replace old script with new one (this executes it)
            oldScript.parentNode.replaceChild(newScript, oldScript);
        });
    }

    function updateActiveNavLink(url) {
        // Remove active class from all nav links
        const allNavLinks = document.querySelectorAll('.sidebar-navigation .nav-link');
        allNavLinks.forEach(link => {
            link.classList.remove('active');
        });
        
        // Add active class to the matching link
        const matchingLink = document.querySelector(`.sidebar-navigation .nav-link[href="${url}"]`);
        if (matchingLink) {
            matchingLink.classList.add('active');
        } else {
            // Try to match by comparing paths
            allNavLinks.forEach(link => {
                const linkHref = link.getAttribute('href');
                if (linkHref && url.includes(linkHref)) {
                    link.classList.add('active');
                }
            });
        }
    }

    function updateViewBagValues(doc) {
        // Extract organization ID and name from the new page if available
        const orgIdMeta = doc.querySelector('meta[name="organization-id"]');
        const orgNameMeta = doc.querySelector('meta[name="organization-name"]');
        
        if (orgIdMeta) {
            const orgId = orgIdMeta.getAttribute('content');
            if (window.ViewBag) {
                window.ViewBag.OrganizationId = orgId;
            }
        }
        
        if (orgNameMeta) {
            const orgName = orgNameMeta.getAttribute('content');
            if (window.ViewBag) {
                window.ViewBag.OrganizationName = orgName;
            }
        }
    }

    function initializePageScripts(url) {
        // Initialize page-specific scripts based on the URL
        console.log('Initializing page scripts for:', url);
        
        // Determine page type from URL
        if (url.includes('/Dashboard')) {
            initializeDashboardScripts();
        } else if (url.includes('/Matter') && !url.includes('/Matter/')) {
            initializeMatterListScripts();
        } else if (url.includes('/Tasks')) {
            initializeTasksScripts();
        } else if (url.includes('/Communications')) {
            initializeCommunicationsScripts();
        } else if (url.includes('/Calendar')) {
            initializeCalendarScripts();
        } else if (url.includes('/Teams')) {
            initializeTeamsScripts();
        } else if (url.includes('/Settings')) {
            initializeSettingsScripts();
        } else if (url.includes('/Documents')) {
            initializeDocumentsScripts();
        }
        
        // Re-initialize chat if it's available (for AI conversations)
        if (typeof initializeChat === 'function') {
            try {
                initializeChat();
            } catch (e) {
                console.log('Chat already initialized or not available');
            }
        }
    }

    function initializeDashboardScripts() {
        console.log('Initializing Dashboard scripts');
        // Dashboard-specific initialization
        // The dashboard scripts should already be loaded from inline scripts
    }

    function initializeMatterListScripts() {
        console.log('Initializing Matter List scripts');
        // Matter list initialization
        if (typeof window.initializeMatterCarousel === 'function') {
            window.initializeMatterCarousel();
        }
    }

    function initializeTasksScripts() {
        console.log('Initializing Tasks scripts');
        // Tasks initialization
        if (typeof window.initializeTasks === 'function') {
            window.initializeTasks();
        }
    }

    function initializeCommunicationsScripts() {
        console.log('Initializing Communications scripts');
        // Communications initialization
        if (typeof window.initializeCommunicationsPage === 'function') {
            window.initializeCommunicationsPage();
        }
    }

    function initializeCalendarScripts() {
        console.log('Initializing Calendar scripts');
        // Calendar initialization
    }

    function initializeTeamsScripts() {
        console.log('Initializing Teams scripts');
        // Teams initialization
    }

    function initializeSettingsScripts() {
        console.log('Initializing Settings scripts');
        // Settings initialization
    }

    function initializeDocumentsScripts() {
        console.log('Initializing Documents scripts');
        // Documents initialization
    }

    function restoreSidebarState() {
        // Restore the active sidebar (AI, Communications, or Notifications)
        const activeSidebar = localStorage.getItem('activeSidebar');
        
        if (!activeSidebar || activeSidebar === 'none') {
            // If no sidebar is active, just call hideBothSidebars to ensure clean state
            if (typeof window.hideBothSidebars === 'function') {
                setTimeout(() => {
                    window.hideBothSidebars();
                }, 100);
            }
            return;
        }
        
        // Make sure the appropriate sidebar is shown
        console.log('Restoring sidebar state:', activeSidebar);
        
        if (activeSidebar === 'ai' && typeof window.showAIChatPanel === 'function') {
            // Delay slightly to ensure DOM is ready
            setTimeout(() => {
                window.showAIChatPanel();
            }, 100);
        } else if (activeSidebar === 'comms' && typeof window.showCommsSidebar === 'function') {
            setTimeout(() => {
                window.showCommsSidebar();
            }, 100);
        } else if (activeSidebar === 'notifications' && typeof window.showNotificationsSidebar === 'function') {
            setTimeout(() => {
                window.showNotificationsSidebar();
            }, 100);
        }
    }

    // Expose functions globally
    window.navigateToPage = function(url) {
        if (config.enabled) {
            loadPage(url, true);
        } else {
            window.location.href = url;
        }
    };

    // Allow disabling AJAX navigation at runtime
    window.disableAjaxNavigation = function() {
        config.enabled = false;
        console.log('AJAX navigation disabled');
    };

    // Allow enabling AJAX navigation at runtime
    window.enableAjaxNavigation = function() {
        config.enabled = true;
        console.log('AJAX navigation enabled');
        initializeNavigation();
    };

})();

