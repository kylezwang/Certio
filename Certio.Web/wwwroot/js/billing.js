// Billing Page Tab Management
(function() {
    'use strict';

    // Track which tabs have been loaded
    const loadedTabs = new Set();

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function() {
        initializeTabs();
        handleInitialHash();
        initializeNewTimeEntryButton();
        
        // Hide right sidebar on page load
        if (typeof window.hideBothSidebars === 'function') {
            window.hideBothSidebars();
        }
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
            
            // Load tab content via AJAX if not already loaded
            if (!loadedTabs.has(tabName) && (tabName === 'time-entries' || tabName === 'expenses' || tabName === 'overview')) {
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
                    <h4 class="text-dark mb-2">Loading ${formatTabName(tabName)}...</h4>
                    <p class="text-muted">Please wait while we load the content.</p>
                </div>
            </div>
        `;

        // Get organization ID from the page
        const orgId = getOrganizationId();
        
        if (!orgId) {
            targetElement.innerHTML = `
                <div class="container-fluid px-4 py-4">
                    <div class="text-center py-5">
                        <i class="fas fa-exclamation-triangle text-warning mb-3" style="font-size: 3rem;"></i>
                        <h4 class="text-dark mb-2">Error Loading Content</h4>
                        <p class="text-muted">Unable to determine organization information.</p>
                    </div>
                </div>
            `;
            return;
        }

        // Load content based on tab type
        let url;
        switch (tabName) {
            case 'time-entries':
                url = `/Client/${orgId}/Billing/TimeEntries`;
                break;
            case 'expenses':
                url = `/Client/${orgId}/Billing/Expenses`;
                break;
            case 'overview':
                url = `/Client/${orgId}/Billing/Overview`;
                break;
            default:
                // Tab not implemented yet
                loadedTabs.add(tabName);
                return;
        }

        // Fetch the content via AJAX
        fetch(url)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.text();
            })
            .then(html => {
                targetElement.innerHTML = html;
                loadedTabs.add(tabName);
                
                // Initialize any scripts needed for the loaded content
                initializeTabContent(tabName, targetElement);
            })
            .catch(error => {
                console.error('Error loading tab content:', error);
                targetElement.innerHTML = `
                    <div class="container-fluid px-4 py-4">
                        <div class="text-center py-5">
                            <i class="fas fa-exclamation-triangle text-danger mb-3" style="font-size: 3rem;"></i>
                            <h4 class="text-dark mb-2">Error Loading Content</h4>
                            <p class="text-muted">Failed to load ${formatTabName(tabName)}. Please try again later.</p>
                            <button class="btn btn-primary mt-3" onclick="location.reload()">
                                <i class="fas fa-redo me-2"></i>Reload Page
                            </button>
                        </div>
                    </div>
                `;
            });
    }

    function initializeTabContent(tabName, targetElement) {
        // Initialize any specific functionality for loaded tabs
        switch (tabName) {
            case 'time-entries':
                // Initialize time entries specific functionality
                // (e.g., edit buttons, delete buttons, filters)
                break;
            case 'expenses':
                // Initialize expenses specific functionality
                // (e.g., edit buttons, delete buttons, filters)
                break;
            case 'overview':
                // Initialize overview specific functionality
                // (e.g., charts, graphs)
                break;
        }
    }

    function handleInitialHash() {
        const hash = window.location.hash.substring(1);
        if (hash) {
            switchTab(hash);
        } else {
            // Load the default tab (overview)
            switchTab('overview');
        }
    }

    function handleHashChange() {
        const hash = window.location.hash.substring(1);
        if (hash) {
            switchTab(hash);
        }
    }

    function getOrganizationId() {
        // Try to get from meta tag
        const metaTag = document.querySelector('meta[name="organization-id"]');
        if (metaTag) {
            return metaTag.getAttribute('content');
        }
        
        // Try to get from URL
        const pathMatch = window.location.pathname.match(/\/Client\/(\d+)/);
        if (pathMatch) {
            return pathMatch[1];
        }
        
        // Try to get from ViewBag (if available in a data attribute)
        const container = document.querySelector('.billing-container');
        if (container && container.dataset.organizationId) {
            return container.dataset.organizationId;
        }
        
        return null;
    }

    function formatTabName(tabName) {
        // Convert kebab-case to Title Case
        return tabName
            .split('-')
            .map(word => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
    }

    function initializeNewTimeEntryButton() {
        const newTimeEntryBtn = document.getElementById('newTimeEntryBtn');
        if (newTimeEntryBtn) {
            newTimeEntryBtn.addEventListener('click', function() {
                // TODO: Implement new time entry modal/form
                alert('New Time Entry functionality will be implemented in a future phase.');
            });
        }
    }

    // Export functions for external use if needed
    window.BillingPage = {
        switchTab: switchTab,
        reloadTab: function(tabName) {
            loadedTabs.delete(tabName);
            const targetContent = document.getElementById('tab-' + tabName);
            if (targetContent) {
                loadTabContent(tabName, targetContent);
            }
        }
    };
})();

