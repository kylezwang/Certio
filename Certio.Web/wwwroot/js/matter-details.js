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
                    <div class="spinner-border text-primary mb-3" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
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
            case 'tasks':
                url = `/Client/${orgId}/Matter/${matterId}/Tasks`;
                break;
            case 'timeline':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-stream text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter Timeline</h4>
                            <p class="text-muted">This section will display a timeline of events and milestones for this matter.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
            case 'calendar':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-calendar text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter Calendar</h4>
                            <p class="text-muted">This section will display calendar events related to this matter's due dates and milestones.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
            case 'communications':
                // Placeholder for future implementation
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-comments text-muted mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Matter Communications</h4>
                            <p class="text-muted">This section will display all communications, messages, and discussions related to this matter.</p>
                            <p class="text-muted small">(To be implemented in a future phase)</p>
                        </div>
                    </div>
                `;
                loadedTabs.add(tabName);
                return;
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
                    console.log('✓ Found initializeMatterTasks, calling it...');
                    try {
                        window.initializeMatterTasks();
                        console.log('✓ initializeMatterTasks completed successfully');
                    } catch (error) {
                        console.error('✗ Error calling initializeMatterTasks:', error);
                    }
                } else {
                    console.error('✗ window.initializeMatterTasks function not available');
                    console.log('Available window functions:', Object.keys(window).filter(k => k.includes('initialize')));
                }
                
                // Also try the generic initialization if available
                if (typeof window.initializeTasks === 'function') {
                    console.log('✓ Found initializeTasks, calling it...');
                    try {
                        window.initializeTasks();
                        console.log('✓ initializeTasks completed successfully');
                    } catch (error) {
                        console.error('✗ Error calling initializeTasks:', error);
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

})();

