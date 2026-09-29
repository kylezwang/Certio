// Matter Details Tab Management
(function() {
    'use strict';

    // Track which tabs have been loaded
    const loadedTabs = new Set();

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function() {
        initializeTabs();
        handleInitialHash();
    });

    // Handle browser back/forward
    window.addEventListener('hashchange', function() {
        handleHashChange();
    });

    function initializeTabs() {
        const tabLinks = document.querySelectorAll('.matter-tab-link');
        
        tabLinks.forEach(link => {
            link.addEventListener('click', function(e) {
                e.preventDefault();
                const tabName = this.dataset.tab;
                switchTab(tabName);
                
                // Update URL hash
                window.location.hash = tabName;
            });
        });
    }

    function switchTab(tabName) {
        // Hide all tab content sections
        const allTabContents = document.querySelectorAll('.tab-content-section');
        allTabContents.forEach(content => {
            content.style.display = 'none';
            content.classList.remove('active');
        });

        // Remove active class from all tab links
        const allTabLinks = document.querySelectorAll('.matter-tab-link');
        allTabLinks.forEach(link => {
            link.classList.remove('active');
        });

        // Show selected tab content
        const targetContent = document.getElementById('tab-' + tabName);
        if (targetContent) {
            targetContent.style.display = 'block';
            targetContent.classList.add('active');
            
            // Load tab content via AJAX if not already loaded and not the summary tab
            if (!loadedTabs.has(tabName) && tabName !== 'summary') {
                loadTabContent(tabName, targetContent);
            }
        }

        // Add active class to selected tab link
        const targetLink = document.querySelector(`.matter-tab-link[data-tab="${tabName}"]`);
        if (targetLink) {
            targetLink.classList.add('active');
        }

        // Scroll to top of content
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function loadTabContent(tabName, targetElement) {
        // Show loading state
        targetElement.innerHTML = `
            <div class="container-fluid px-4 py-4">
                <div class="text-center py-5">
                    <div class="spinner mb-3"></div>
                    <h4 class="text-dark mb-2">Loading ${tabName}...</h4>
                    <p class="text-muted">Please wait while we load the content.</p>
                </div>
            </div>
        `;

        // Get matter ID and organization ID from the current URL or data attributes
        const matterId = getMatterId();
        const orgId = getOrganizationId();
        
        if (!matterId || !orgId) {
            targetElement.innerHTML = `
                <div class="container-fluid px-4 py-4">
                    <div class="text-center py-5">
                        <i class="fas fa-exclamation-triangle text-warning mb-3" style="font-size: 3rem;"></i>
                        <h4 class="text-dark mb-2">Error Loading Content</h4>
                        <p class="text-muted">Unable to determine matter or organization information.</p>
                    </div>
                </div>
            `;
            return;
        }

        // Load content based on tab type
        let url;
        switch (tabName) {
            case 'contacts':
                url = `/Client/${orgId}/Matter/${matterId}/Contacts`;
                break;
            case 'tasks':
                url = `/Client/${orgId}/Matter/${matterId}/Tasks`;
                break;
            case 'timeline':
                url = `/Client/${orgId}/Matter/${matterId}/Timeline`;
                break;
            case 'calendar':
                url = `/Client/${orgId}/Matter/${matterId}/Calendar`;
                break;
            case 'communications':
                // Use CommunicationsController for matter communications
                url = `/Client/${orgId}/Matter/${matterId}/Communications`;
                break;
            case 'documents':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-folder text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter Documents</h4>
                            <p class="text-muted">This section will display all documents and files associated with this matter.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
            case 'billing':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-credit-card text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter Billing</h4>
                            <p class="text-muted">This section will display billing information and time tracking for this matter.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
            case 'history':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-history text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter History</h4>
                            <p class="text-muted">This section will display the change history and audit log for this matter.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
            default:
                console.warn(`Unknown tab: ${tabName}`);
                return;
        }

        // Make AJAX request to load tab content
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
            console.log('Received HTML content, length:', html.length);
            
            // Set the HTML content
            targetElement.innerHTML = html;
            loadedTabs.add(tabName);
            
            console.log('HTML content set, now executing inline scripts...');
            
            // Manually execute script tags (innerHTML doesn't auto-execute them)
            const scripts = targetElement.querySelectorAll('script');
            console.log('Found', scripts.length, 'script tags to execute');
            
            scripts.forEach((oldScript, index) => {
                console.log(`Executing script ${index + 1}/${scripts.length}`);
                const newScript = document.createElement('script');
                
                // Copy attributes
                Array.from(oldScript.attributes).forEach(attr => {
                    newScript.setAttribute(attr.name, attr.value);
                });
                
                // Copy inline script content or src
                if (oldScript.src) {
                    console.log(`Script ${index + 1} has src:`, oldScript.src);
                    newScript.src = oldScript.src;
                } else {
                    console.log(`Script ${index + 1} is inline, length:`, oldScript.innerHTML.length);
                    newScript.textContent = oldScript.innerHTML;
                }
                
                // Replace old script with new one (this executes it)
                oldScript.parentNode.replaceChild(newScript, oldScript);
                console.log(`Script ${index + 1} executed`);
            });
            
            console.log('All scripts executed, waiting before initialization...');
            
            // Wait a bit for scripts to fully initialize
            setTimeout(() => {
                console.log('Now calling initializeTabScripts...');
                // Initialize any scripts that might be needed for the loaded content
                initializeTabScripts(tabName);
            }, 200);
        })
        .catch(error => {
            console.error('Error loading tab content:', error);
            targetElement.innerHTML = `
                <div class="container-fluid px-4 py-4">
                    <div class="text-center py-5">
                        <i class="fas fa-exclamation-triangle text-danger mb-3" style="font-size: 3rem;"></i>
                        <h4 class="text-dark mb-2">Error Loading Content</h4>
                        <p class="text-muted">There was an error loading the ${tabName} content. Please try again.</p>
                        <button class="btn btn-outline-primary" onclick="location.reload()">
                            <i class="fas fa-refresh"></i>
                            Retry
                        </button>
                    </div>
                </div>
            `;
        });
    }

    function initializeTabScripts(tabName) {
        // Initialize scripts specific to each tab
        console.log('=== initializeTabScripts called for tab:', tabName, '===');
        
        switch (tabName) {
            case 'tasks':
                // Initialize task management scripts if they exist
                console.log('Initializing task scripts for Matter Details Tasks tab...');
                
                // Check what functions are available
                console.log('Checking available functions:');
                console.log('- window.initializeMatterTasks:', typeof window.initializeMatterTasks);
                console.log('- window.initializeTasks:', typeof window.initializeTasks);
                console.log('- window.openTaskModal:', typeof window.openTaskModal);
                console.log('- window.initializeBottomNavbar:', typeof window.initializeBottomNavbar);
                
                // Call the Matter Tasks initialization function
                if (typeof window.initializeMatterTasks === 'function') {
                    console.log('Found initializeMatterTasks, calling it...');
                    try {
                        window.initializeMatterTasks();
                        console.log('initializeMatterTasks completed successfully');
                    } catch (error) {
                        console.error('Error calling initializeMatterTasks:', error);
                    }
                } else {
                    console.error('window.initializeMatterTasks function not available');
                    console.log('Available window functions:', Object.keys(window).filter(k => k.includes('initialize')));
                }
                
                // Also try the generic initialization if available
                if (typeof window.initializeTasks === 'function') {
                    console.log('Found initializeTasks, calling it...');
                    try {
                        window.initializeTasks();
                        console.log('initializeTasks completed successfully');
                    } catch (error) {
                        console.error('Error calling initializeTasks:', error);
                    }
                }
                
                // Verify bottom navbar was initialized
                const navItems = document.querySelectorAll('.bottom-navbar .nav-item');
                console.log('Bottom navbar items found:', navItems.length);
                navItems.forEach((item, i) => {
                    console.log(`  Nav item ${i}:`, item.getAttribute('data-view'), 'listeners:', item.onclick ? 'has onclick' : 'no onclick');
                });
                
                console.log('=== Task scripts initialization complete ===');
                break;
            
            case 'communications':
                // Initialize communications scripts if they exist
                console.log('Initializing communications scripts for Matter Details Communications tab...');
                
                // Check what functions are available
                console.log('Checking available functions:');
                console.log('- window.initializeCommunicationsChat:', typeof window.initializeCommunicationsChat);
                console.log('- window.initializeDirectMessaging:', typeof window.initializeDirectMessaging);
                
                // The communications scripts are already loaded and initialized via inline scripts in the partial view
                // Just verify they loaded correctly
                if (typeof window.initializeCommunicationsChat === 'function') {
                    console.log('Communications chat functions are available');
                } else {
                    console.warn('Communications chat functions may not be fully loaded yet');
                }
                
                console.log('=== Communications scripts initialization complete ===');
                break;
            
            case 'calendar':
                // Initialize calendar scripts if they exist
                console.log('Initializing calendar scripts for Matter Details Calendar tab...');
                
                // Check what functions are available
                console.log('Checking available functions:');
                console.log('- window.initializeEventLocationSearch:', typeof window.initializeEventLocationSearch);
                
                // The calendar scripts are already loaded and initialized via inline scripts in the partial view
                // Just verify they loaded correctly
                if (typeof window.initializeEventLocationSearch === 'function') {
                    console.log('Calendar functions are available');
                } else {
                    console.warn('Calendar functions may not be fully loaded yet');
                }
                
                // Dispatch event to notify calendar that tab is active
                const calendarTabEvent = new CustomEvent('matterTabChanged', { 
                    detail: { tabName: 'calendar' } 
                });
                document.dispatchEvent(calendarTabEvent);
                console.log('Dispatched matterTabChanged event for calendar');
                
                console.log('=== Calendar scripts initialization complete ===');
                break;
            
            case 'timeline':
                // Initialize timeline scripts
                console.log('Initializing timeline scripts for Matter Details Timeline tab...');
                
                // Dispatch event to notify timeline that tab is active
                const timelineTabEvent = new CustomEvent('matterTabChanged', { 
                    detail: { tabName: 'timeline' } 
                });
                document.dispatchEvent(timelineTabEvent);
                console.log('Dispatched matterTabChanged event for timeline');
                
                // Check if timeline initialization function exists and call it
                if (typeof window.initializeTimeline === 'function') {
                    console.log('Found initializeTimeline, calling it...');
                    try {
                        window.initializeTimeline();
                        console.log('initializeTimeline completed successfully');
                    } catch (error) {
                        console.error('Error calling initializeTimeline:', error);
                    }
                } else {
                    console.log('Timeline will be initialized by its own observer');
                }
                
                console.log('=== Timeline scripts initialization complete ===');
                break;
            
            case 'contacts':
                // Initialize contacts tab scripts
                console.log('Initializing contacts scripts for Matter Details Contacts tab...');
                
                // The contacts scripts are already loaded and initialized via inline scripts in the partial view
                // Dispatch event to notify contacts tab is active
                const contactsTabEvent = new CustomEvent('matterTabChanged', { 
                    detail: { tabName: 'contacts' } 
                });
                document.dispatchEvent(contactsTabEvent);
                console.log('Dispatched matterTabChanged event for contacts');
                
                console.log('=== Contacts scripts initialization complete ===');
                break;

            // Add other tab-specific initializations here as needed
        }
    }

    function getMatterId() {
        // Try to get matter ID from URL
        const urlPath = window.location.pathname;
        const matterMatch = urlPath.match(/\/Matter\/(\d+)/);
        if (matterMatch) {
            return matterMatch[1];
        }
        
        // Try to get from data attributes on the page
        const matterElement = document.querySelector('[data-matter-id]');
        if (matterElement) {
            return matterElement.getAttribute('data-matter-id');
        }
        
        // Try to get from the matter details container
        const matterContainer = document.querySelector('.matter-details-container');
        if (matterContainer) {
            const matterIdAttr = matterContainer.getAttribute('data-matter-id');
            if (matterIdAttr) {
                return matterIdAttr;
            }
        }
        
        // Try to extract from the page title or other elements
        const pageTitle = document.title;
        const titleMatch = pageTitle.match(/Matter (\d+)/);
        if (titleMatch) {
            return titleMatch[1];
        }
        
        // Try to get from meta tags as final fallback
        const matterMeta = document.querySelector('meta[name="matter-id"]');
        if (matterMeta) {
            return matterMeta.getAttribute('content');
        }
        
        console.warn('Could not determine matter ID');
        return null;
    }

    function getOrganizationId() {
        // Try to get organization ID from URL
        const urlPath = window.location.pathname;
        const orgMatch = urlPath.match(/\/Client\/(\d+)/);
        if (orgMatch) {
            return orgMatch[1];
        }
        
        // Try to get from ViewBag or data attributes
        const orgElement = document.querySelector('[data-organization-id]');
        if (orgElement) {
            return orgElement.getAttribute('data-organization-id');
        }
        
        // Try to get from the matter details container
        const matterContainer = document.querySelector('.matter-details-container');
        if (matterContainer) {
            const orgIdAttr = matterContainer.getAttribute('data-organization-id');
            if (orgIdAttr) {
                return orgIdAttr;
            }
        }
        
        // Try to get from meta tags
        const orgMeta = document.querySelector('meta[name="organization-id"]');
        if (orgMeta) {
            return orgMeta.getAttribute('content');
        }
        
        // Try to get from matter meta tag as well
        const matterMeta = document.querySelector('meta[name="matter-id"]');
        if (matterMeta) {
            // If we have matter ID but no org ID, we might need to extract from URL or use a default
            const urlPath = window.location.pathname;
            const orgMatch = urlPath.match(/\/Client\/(\d+)/);
            if (orgMatch) {
                return orgMatch[1];
            }
        }
        
        console.warn('Could not determine organization ID');
        return null;
    }

    function handleInitialHash() {
        const hash = window.location.hash.substring(1); // Remove the # character
        
        if (hash && isValidTab(hash)) {
            switchTab(hash);
        } else {
            // Default to summary tab
            switchTab('summary');
        }
    }

    function handleHashChange() {
        const hash = window.location.hash.substring(1);
        
        if (hash && isValidTab(hash)) {
            switchTab(hash);
        }
    }

    function isValidTab(tabName) {
        const validTabs = ['summary', 'tasks', 'timeline', 'calendar', 'communications', 'documents', 'billing', 'history'];
        return validTabs.includes(tabName);
    }

    function getActiveTabName() {
        const activeLink = document.querySelector('.matter-tab-link.active');
        return activeLink?.dataset?.tab || null;
    }

    // Some tab scripts expect their DOM to be measurable (not under display:none).
    // When we "ensure load" a tab in the background, temporarily render it offscreen.
    function prepareTabForBackgroundInit(tabName) {
        const el = document.getElementById('tab-' + tabName);
        if (!el) return null;

        const computed = window.getComputedStyle(el);
        const isHidden = computed.display === 'none' || el.style.display === 'none';
        if (!isHidden) return null;

        // Store current inline style attribute so we can restore exactly.
        el.dataset.bgInitPrevStyle = el.getAttribute('style') || '';

        // Render full-size but invisible so measurements match the real viewport.
        // This avoids initializing Tasks modal logic in a 1px container (which breaks layout).
        el.style.display = 'block';
        el.style.position = 'fixed';
        el.style.left = '0';
        el.style.top = '0';
        el.style.right = '0';
        el.style.bottom = '0';
        el.style.width = '100vw';
        el.style.height = '100vh';
        el.style.overflow = 'auto';
        el.style.visibility = 'hidden';
        el.style.pointerEvents = 'none';
        el.style.opacity = '0';
        el.style.zIndex = '-1';
        return el;
    }

    function restoreTabAfterBackgroundInit(tabName) {
        const el = document.getElementById('tab-' + tabName);
        if (!el || !el.dataset) return;
        if (!('bgInitPrevStyle' in el.dataset)) return;

        const prev = el.dataset.bgInitPrevStyle || '';
        if (prev) {
            el.setAttribute('style', prev);
        } else {
            el.removeAttribute('style');
        }
        delete el.dataset.bgInitPrevStyle;
    }

    // Expose a tiny helper so other tabs (ex: Timeline) can ensure another tab's
    // HTML + inline scripts are loaded before calling its functions (ex: openTaskModal).
    // This avoids requiring the user to click the Tasks tab first.
    window.ensureMatterTabLoaded = function(tabName, options) {
        const opts = options || {};
        const timeoutMs = typeof opts.timeoutMs === 'number' ? opts.timeoutMs : 20000;

        const isReady = () => {
            if (!loadedTabs.has(tabName)) return false;
            if (tabName === 'tasks') return (typeof window.openTaskModal === 'function');
            if (tabName === 'timeline') return (typeof window.initializeTimeline === 'function');
            return true;
        };

        return new Promise((resolve, reject) => {
            if (isReady()) {
                resolve(true);
                return;
            }

            const targetElement = document.getElementById('tab-' + tabName);
            if (!targetElement) {
                reject(new Error(`Tab element not found: tab-${tabName}`));
                return;
            }

            // If the tab content is currently hidden, render it full-size but invisible while scripts initialize.
            // This is critical for Tasks modal logic which measures layout.
            const didPrepare = !!prepareTabForBackgroundInit(tabName);

            // Kick off loading if needed; otherwise retry initialization (scripts may exist but not initialized)
            if (!loadedTabs.has(tabName)) {
                loadTabContent(tabName, targetElement);
            } else {
                try { initializeTabScripts(tabName); } catch (_) { /* ignore */ }
            }

            const start = Date.now();
            const tick = () => {
                if (isReady()) {
                    // If Tasks loaded, make sure the modal can render from other tabs.
                    try { window.portalTaskDetailsModalToBody?.(); } catch (_) { /* ignore */ }
                    // Restore background rendering unless the user actually switched to this tab.
                    if (didPrepare && getActiveTabName() !== tabName) restoreTabAfterBackgroundInit(tabName);
                    resolve(true);
                    return;
                }
                if (Date.now() - start > timeoutMs) {
                    if (didPrepare && getActiveTabName() !== tabName) restoreTabAfterBackgroundInit(tabName);
                    reject(new Error(`Timed out loading tab: ${tabName}`));
                    return;
                }
                setTimeout(tick, 50);
            };
            tick();
        });
    };

    // Move the Tasks modal to <body> once loaded so it can render while other tabs are active.
    window.portalTaskDetailsModalToBody = function() {
        const modal = document.getElementById('taskDetailsModal');
        if (!modal) return false;
        if (modal.dataset && modal.dataset.portaledToBody === 'true') return true;

        const wrapper = document.querySelector('.client-main-content-wrapper');
        if (wrapper && wrapper.parentElement) {
            // Keep modal as a sibling of the main wrapper so existing CSS (~ selectors) continues to work.
            wrapper.insertAdjacentElement('afterend', modal);
            modal.dataset.portaledToBody = 'true';
            return true;
        }

        // Fallback
        document.body.appendChild(modal);
        modal.dataset.portaledToBody = 'true';
        return true;
    };

})();

