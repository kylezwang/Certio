// Billing Page Tab Management
(function() {
    'use strict';

    const billingFilterLabels = {
        'all': 'All Records',
        'time-entries': 'Time Entries',
        'expenses': 'Expenses',
        'invoices': 'Invoices',
        'trusts': 'Trust Activity'
    };

    const billingDateLabels = {
        'last-30-days': 'Last 30 Days',
        'last-90-days': 'Last 90 Days',
        'this-year': 'This Year',
        'all-time': 'All Time'
    };

    // Button text and icons for each tab
    const newButtonConfig = {
        'overview': { text: 'New', icon: 'fa-plus' },
        'time-entries': { text: 'New Time Entry', icon: 'fa-plus' },
        'expenses': { text: 'New Expense', icon: 'fa-plus' },
        'invoices': { text: 'New Invoice', icon: 'fa-plus' },
        'trusts': { text: 'New Retainer', icon: 'fa-plus' }
    };

    let currentBillingFilter = 'all';
    let currentBillingDateRange = 'last-30-days';
    let currentActiveTab = 'overview';

    // Track which tabs have been loaded
    const loadedTabs = new Set();
    
    // Store loaded assignees for popup
    let billingAssignees = [];
    // Store loaded clients for popup
    let billingClients = [];
    
    // Track selected assignees per modal
    const modalAssignees = {
        'timeEntryModal': null,
        'expenseModal': null,
        'invoiceModal': null,
        'retainerModal': null
    };

    // Track selected clients per modal
    const modalClients = {
        'timeEntryModal': null,
        'expenseModal': null,
        'invoiceModal': null,
        'retainerModal': null
    };
    
    // Get current user info from the page (set by _ClientLayout)
    function getCurrentUser() {
        // Try to get from window.currentUser (set by tasks page)
        if (window.currentUser) {
            return window.currentUser;
        }
        
        // Try to get from ViewBag data attributes or meta tags
        const userIdMeta = document.querySelector('meta[name="current-user-id"]');
        const userNameMeta = document.querySelector('meta[name="current-user-name"]');
        const userInitialsMeta = document.querySelector('meta[name="current-user-initials"]');
        
        if (userIdMeta) {
            return {
                id: parseInt(userIdMeta.content),
                name: userNameMeta?.content || 'Current User',
                initials: userInitialsMeta?.content || 'CU'
            };
        }
        
        // Try to find from billing assignees list (current user should be there)
        // We'll match by checking if any loaded assignee matches the DOM indicators
        return null;
    }

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function() {
        console.log('[Billing] Initializing billing page...');
        
        initializeTabs();
        handleInitialHash();
        initializeNewTimeEntryButton();
        initializeFilters();
        initializeBillingModalCloseHandlers();
        initializeBillingSaveHandlers();
        initializeBillingAssigneeButtons();
        initializeBillingClientButtons();
        initializeMatterClientAutoFill();
        loadBillingFormData();
        
        console.log('[Billing] Initialization complete. Current tab:', currentActiveTab);
        
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
            if (!loadedTabs.has(tabName) && (tabName === 'time-entries' || tabName === 'expenses' || tabName === 'overview' || tabName === 'trusts' || tabName === 'invoices')) {
                loadTabContent(tabName, targetContent);
            }
        }

        // Add active class to selected tab link
        const targetLink = document.querySelector(`.matter-tab-link[data-tab="${tabName}"]`);
        if (targetLink) {
            targetLink.classList.add('active');
        }

        // Update the new button text based on active tab
        currentActiveTab = tabName;
        updateNewButtonText(tabName);

        // Scroll to top of content
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function updateNewButtonText(tabName) {
        const newBtn = document.getElementById('newTimeEntryBtn');
        if (newBtn) {
            const config = newButtonConfig[tabName] || newButtonConfig['overview'];
            console.log('[Billing] Updating button text for tab:', tabName, 'to:', config.text);
            // Update the entire button content
            newBtn.innerHTML = `<i class="fas ${config.icon}"></i> ${config.text}`;
        } else {
            console.warn('[Billing] New button not found');
        }
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
            case 'trusts':
                url = `/Client/${orgId}/Billing/Trusts`;
                break;
            case 'invoices':
                url = `/Client/${orgId}/Billing/Invoices`;
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
                loadRecentActivity();
                break;
            case 'trusts':
                // Initialize trusts specific functionality
                // (e.g., deposit/withdraw buttons, filters)
                break;
            case 'invoices':
                // Initialize invoices specific functionality
                // (e.g., view/payment/download buttons, filters)
                break;
        }
    }

    function handleInitialHash() {
        const hash = window.location.hash.substring(1);
        if (hash && ['overview', 'time-entries', 'expenses', 'invoices', 'trusts'].includes(hash)) {
            switchTab(hash);
        } else {
            // Load the default tab (overview)
            switchTab('overview');
        }
        // Ensure button text is updated after page load
        setTimeout(() => updateNewButtonText(currentActiveTab), 100);
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

    function initializeFilters() {
        const filterLabel = document.getElementById('billingFilterLabel');
        const dateLabel = document.getElementById('billingDateFilterLabel');

        if (!filterLabel || !dateLabel) {
            return;
        }

        document.querySelectorAll('.billing-filter-option').forEach(option => {
            option.addEventListener('click', function(e) {
                e.preventDefault();
                const filter = this.dataset.filter || 'all';
                currentBillingFilter = filter;
                filterLabel.textContent = billingFilterLabels[filter] || 'Filter';

                document.dispatchEvent(new CustomEvent('billing:filterChanged', {
                    detail: { filter }
                }));
            });
        });

        document.querySelectorAll('.billing-date-option').forEach(option => {
            option.addEventListener('click', function(e) {
                e.preventDefault();
                const range = this.dataset.dateRange || 'last-30-days';
                currentBillingDateRange = range;
                dateLabel.textContent = billingDateLabels[range] || 'Date Range';

                document.dispatchEvent(new CustomEvent('billing:dateRangeChanged', {
                    detail: { range }
                }));
            });
        });

        filterLabel.textContent = billingFilterLabels[currentBillingFilter];
        dateLabel.textContent = billingDateLabels[currentBillingDateRange];
    }

    function initializeNewTimeEntryButton() {
        const newTimeEntryBtn = document.getElementById('newTimeEntryBtn');
        if (newTimeEntryBtn) {
            console.log('[Billing] Attaching click handler to New button');
            newTimeEntryBtn.addEventListener('click', function(e) {
                e.preventDefault();
                console.log('[Billing] New button clicked, current tab:', currentActiveTab);
                openBillingModal(currentActiveTab);
            });
        } else {
            console.warn('[Billing] New Time Entry button not found');
        }
    }

    // Open the appropriate billing modal based on the current tab
    function openBillingModal(tabName) {
        console.log('[Billing] Opening modal for tab:', tabName);
        
        let modalId;
        switch (tabName) {
            case 'time-entries':
                modalId = 'timeEntryModal';
                break;
            case 'expenses':
                modalId = 'expenseModal';
                break;
            case 'invoices':
                modalId = 'invoiceModal';
                break;
            case 'trusts':
                modalId = 'retainerModal';
                break;
            case 'overview':
            default:
                // On overview, show a dropdown or default to time entry
                console.log('[Billing] Overview tab - showing dropdown');
                showNewItemDropdown();
                return;
        }
        
        console.log('[Billing] Looking for modal:', modalId);
        const modal = document.getElementById(modalId);
        if (modal) {
            console.log('[Billing] Found modal, displaying it');
            resetBillingModal(modalId);
            
            // Ensure matters dropdown is populated
            populateMatterDropdowns();
            
            // Auto-assign current user if this modal has assignee (Time Entry, Expense)
            if (modalId === 'timeEntryModal' || modalId === 'expenseModal') {
                autoAssignCurrentUser(modalId);
            }
            
            // Reinitialize assignee button click handlers
            initializeBillingAssigneeButtons();
            initializeBillingClientButtons();
            
            modal.style.display = 'flex';
            adjustBillingModalPosition(modal);
            
            // Focus on first select (matter dropdown)
            const matterSelect = modal.querySelector('.billing-matter-select');
            if (matterSelect) {
                setTimeout(() => matterSelect.focus(), 100);
            }
        } else {
            console.error('[Billing] Modal not found:', modalId);
        }
    }
    
    // Auto-assign current user to modal
    function autoAssignCurrentUser(modalId) {
        // Skip if already has an assignee
        if (modalAssignees[modalId]) {
            console.log('[Billing] Modal already has assignee, skipping auto-assign');
            return;
        }
        
        // Try to find current user from loaded assignees
        if (billingAssignees && billingAssignees.length > 0) {
            // Try to get current user ID from meta tag
            const userIdMeta = document.querySelector('meta[name="current-user-id"]');
            if (userIdMeta) {
                const currentUserId = parseInt(userIdMeta.content);
                // Handle both camelCase and PascalCase
                const currentUser = billingAssignees.find(a => 
                    (a.id ?? a.Id) === currentUserId
                );
                
                if (currentUser) {
                    const userId = currentUser.id ?? currentUser.Id;
                    const userName = currentUser.name ?? currentUser.Name;
                    const userInitials = currentUser.initials ?? currentUser.Initials ?? 
                        (userName ? userName.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2) : 'U');
                    
                    modalAssignees[modalId] = { 
                        id: userId, 
                        name: userName,
                        initials: userInitials
                    };
                    renderBillingAssignee(modalId);
                    updateAssigneeHiddenInput(modalId);
                    console.log('[Billing] Auto-assigned current user:', userName);
                    return;
                }
            }
            
            // Fallback: assign first user in list
            const firstUser = billingAssignees[0];
            const userId = firstUser.id ?? firstUser.Id;
            const userName = firstUser.name ?? firstUser.Name;
            const userInitials = firstUser.initials ?? firstUser.Initials ?? 
                (userName ? userName.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2) : 'U');
            
            modalAssignees[modalId] = { 
                id: userId, 
                name: userName,
                initials: userInitials
            };
            renderBillingAssignee(modalId);
            updateAssigneeHiddenInput(modalId);
            console.log('[Billing] Auto-assigned first user (fallback):', userName);
        } else {
            console.warn('[Billing] No assignees loaded, cannot auto-assign');
        }
    }

    // Show dropdown menu for overview tab "New" button
    function showNewItemDropdown() {
        // Check if dropdown already exists
        let dropdown = document.getElementById('newItemDropdown');
        if (dropdown) {
            dropdown.style.display = dropdown.style.display === 'none' ? 'block' : 'none';
            return;
        }

        const newBtn = document.getElementById('newTimeEntryBtn');
        if (!newBtn) return;

        dropdown = document.createElement('div');
        dropdown.id = 'newItemDropdown';
        dropdown.className = 'new-item-dropdown';
        dropdown.innerHTML = `
            <div class="dropdown-item" data-type="time-entries">
                <i class="fas fa-stopwatch"></i> New Time Entry
            </div>
            <div class="dropdown-item" data-type="expenses">
                <i class="fas fa-receipt"></i> New Expense
            </div>
            <div class="dropdown-item" data-type="invoices">
                <i class="fas fa-file-invoice"></i> New Invoice
            </div>
            <div class="dropdown-item" data-type="trusts">
                <i class="fas fa-hand-holding-usd"></i> New Retainer
            </div>
        `;
        
        // Position dropdown below button
        const btnRect = newBtn.getBoundingClientRect();
        dropdown.style.cssText = `
            position: fixed;
            top: ${btnRect.bottom + 5}px;
            right: ${window.innerWidth - btnRect.right}px;
            background: white;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            z-index: 1000;
            min-width: 180px;
            padding: 0.5rem 0;
        `;

        // Add click handlers to dropdown items
        dropdown.querySelectorAll('.dropdown-item').forEach(item => {
            item.style.cssText = `
                padding: 0.75rem 1rem;
                cursor: pointer;
                display: flex;
                align-items: center;
                gap: 0.75rem;
                font-size: 0.875rem;
                color: #1e293b;
                transition: background-color 0.2s;
            `;
            item.addEventListener('mouseenter', () => item.style.backgroundColor = '#f3f4f6');
            item.addEventListener('mouseleave', () => item.style.backgroundColor = 'transparent');
            item.addEventListener('click', (e) => {
                e.stopPropagation();
                const type = item.dataset.type;
                dropdown.style.display = 'none';
                openBillingModal(type);
            });
        });

        document.body.appendChild(dropdown);

        // Close dropdown when clicking outside
        setTimeout(() => {
            document.addEventListener('click', function closeDropdown(e) {
                if (!dropdown.contains(e.target) && e.target !== newBtn) {
                    dropdown.style.display = 'none';
                    document.removeEventListener('click', closeDropdown);
                }
            });
        }, 10);
    }

    // Reset a billing modal to its initial state
    function resetBillingModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        // Clear all form inputs
        modal.querySelectorAll('input:not([type="hidden"]):not([type="checkbox"])').forEach(input => {
            input.value = '';
        });
        modal.querySelectorAll('input[type="checkbox"]').forEach(input => {
            input.checked = true; // Default checkboxes to checked (billable)
        });
        modal.querySelectorAll('select').forEach(select => {
            select.selectedIndex = 0;
        });
        modal.querySelectorAll('textarea').forEach(textarea => {
            textarea.value = '';
        });

        // Set default date to today
        const today = new Date().toISOString().split('T')[0];
        modal.querySelectorAll('input[type="date"]').forEach(dateInput => {
            dateInput.value = today;
        });

        // Reset modal title to "New" mode
        const title = modal.querySelector('.billing-modal-title');
        if (title) {
            const modalTitles = {
                'timeEntryModal': 'New Time Entry',
                'expenseModal': 'New Expense',
                'invoiceModal': 'New Invoice',
                'retainerModal': 'New Retainer'
            };
            title.textContent = modalTitles[modalId] || 'New Item';
        }

        // Clear assignee for this modal
        if (modalAssignees[modalId] !== undefined) {
            clearModalAssignee(modalId);
        }

        // Clear client for this modal
        if (modalClients[modalId] !== undefined) {
            clearModalClient(modalId);
        }

        // Clear hidden ID field (indicates new vs edit mode)
        const idField = modal.querySelector('[data-billing-id]');
        if (idField) {
            idField.value = '';
        }
    }

    // Adjust modal position to match Tasks modal behavior
    function adjustBillingModalPosition(modal) {
        const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
        if (!mainContentWrapper) return;

        const wrapperStyle = window.getComputedStyle(mainContentWrapper);
        const wrapperRight = wrapperStyle.right;

        if (document.body.classList.contains('chat-hidden')) {
            modal.style.right = '1rem';
        } else if (wrapperRight && wrapperRight !== 'auto') {
            modal.style.right = wrapperRight;
        }
    }

    // Close billing modal
    function closeBillingModal(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.style.display = 'none';
        }
    }

    // Initialize billing modal close handlers
    function initializeBillingModalCloseHandlers() {
        const modals = ['timeEntryModal', 'expenseModal', 'invoiceModal', 'retainerModal'];
        
        modals.forEach(modalId => {
            const modal = document.getElementById(modalId);
            if (!modal) return;

            // Close button handlers (both X button and Cancel button)
            modal.querySelectorAll('[data-close-modal], .billing-modal-close').forEach(closeBtn => {
                closeBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    closeBillingModal(modalId);
                });
            });

            // Backdrop click handler - close when clicking on the backdrop itself
            modal.addEventListener('click', (e) => {
                // Close if clicking on backdrop or container (not the content)
                if (e.target === modal || e.target.classList.contains('billing-modal-container')) {
                    closeBillingModal(modalId);
                }
            });

            // Prevent clicks inside the content from bubbling up
            const content = modal.querySelector('.billing-modal-content');
            if (content) {
                content.addEventListener('click', (e) => {
                    e.stopPropagation();
                });
            }
        });
    }

    // Save billing item (generic handler)
    async function saveBillingItem(modalId, endpoint, data, id) {
        const orgId = getOrganizationId();
        if (!orgId) {
            alert('Unable to determine organization.');
            return false;
        }

        try {
            const hasId = !!id;
            const url = hasId
                ? `/api/billing/${orgId}/${endpoint}/${id}`
                : `/api/billing/${orgId}/${endpoint}`;

            const response = await fetch(url, {
                method: hasId ? 'PUT' : 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(data)
            });

            const result = await response.json();
            if (result.success) {
                closeBillingModal(modalId);
                // Reload the current tab to show new data
                const tabName = endpoint === 'time-entries' ? 'time-entries' : 
                               endpoint === 'expenses' ? 'expenses' :
                               endpoint === 'invoices' ? 'invoices' :
                               endpoint === 'retainers' ? 'trusts' : currentActiveTab;
                loadedTabs.delete(tabName);
                const targetContent = document.getElementById('tab-' + tabName);
                if (targetContent) {
                    loadTabContent(tabName, targetContent);
                }

                // Keep Overview (Recent Activity) in sync
                loadedTabs.delete('overview');
                const overviewContent = document.getElementById('tab-overview');
                if (overviewContent) {
                    loadTabContent('overview', overviewContent);
                }
                return true;
            } else {
                alert('Error saving: ' + (result.error || 'Unknown error'));
                return false;
            }
        } catch (error) {
            console.error('Error saving billing item:', error);
            alert('Error saving item. Please try again.');
            return false;
        }
    }

    // Initialize billing modal save handlers
    function initializeBillingSaveHandlers() {
        // Time Entry Save
        const timeEntrySaveBtn = document.getElementById('saveTimeEntryBtn');
        if (timeEntrySaveBtn) {
            timeEntrySaveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const modal = document.getElementById('timeEntryModal');
                const id = parseInt(modal?.querySelector('#timeEntryId')?.value || '');
                const data = {
                    matterId: getSelectValue(modal, '#timeEntryMatter'),
                    clientId: modalClients['timeEntryModal']?.id || null,
                    assigneeId: modalAssignees['timeEntryModal']?.id || null,
                    date: getInputValue(modal, '#timeEntryDate') || null,
                    description: getInputValue(modal, '#timeEntryDescription'),
                    hours: parseFloat(getInputValue(modal, '#timeEntryHours')) || 0,
                    rate: parseFloat(getInputValue(modal, '#timeEntryRate')) || 0,
                    isBillable: modal.querySelector('#timeEntryBillable')?.checked ?? true,
                    status: getSelectValue(modal, '#timeEntryStatus')
                };
                await saveBillingItem('timeEntryModal', 'time-entries', data, isNaN(id) ? null : id);
            });
        }

        // Expense Save
        const expenseSaveBtn = document.getElementById('saveExpenseBtn');
        if (expenseSaveBtn) {
            expenseSaveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const modal = document.getElementById('expenseModal');
                const id = parseInt(modal?.querySelector('#expenseId')?.value || '');
                const data = {
                    matterId: getSelectValue(modal, '#expenseMatter'),
                    clientId: modalClients['expenseModal']?.id || null,
                    assigneeId: modalAssignees['expenseModal']?.id || null,
                    date: getInputValue(modal, '#expenseDate') || null,
                    description: getInputValue(modal, '#expenseDescription'),
                    category: getInputValue(modal, '#expenseCategory'), // Category is a string, not int
                    amount: parseFloat(getInputValue(modal, '#expenseAmount')) || 0,
                    isBillable: modal.querySelector('#expenseBillable')?.checked ?? true,
                    status: getSelectValue(modal, '#expenseStatus')
                };
                await saveBillingItem('expenseModal', 'expenses', data, isNaN(id) ? null : id);
            });
        }

        // Invoice Save
        const invoiceSaveBtn = document.getElementById('saveInvoiceBtn');
        if (invoiceSaveBtn) {
            invoiceSaveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const modal = document.getElementById('invoiceModal');
                const id = parseInt(modal?.querySelector('#invoiceId')?.value || '');
                const data = {
                    matterId: getSelectValue(modal, '#invoiceMatter'),
                    clientId: modalClients['invoiceModal']?.id || null,
                    invoiceDate: getInputValue(modal, '#invoiceDate') || null,
                    dueDate: getInputValue(modal, '#invoiceDueDate') || null,
                    notes: getInputValue(modal, '#invoiceNotes'),
                    terms: getInputValue(modal, '#invoiceTerms'),
                    taxAmount: parseFloat(getInputValue(modal, '#invoiceTax')) || 0,
                    status: getSelectValue(modal, '#invoiceStatus')
                };
                await saveBillingItem('invoiceModal', 'invoices', data, isNaN(id) ? null : id);
            });
        }

        // Retainer Save
        const retainerSaveBtn = document.getElementById('saveRetainerBtn');
        if (retainerSaveBtn) {
            retainerSaveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const modal = document.getElementById('retainerModal');
                const id = parseInt(modal?.querySelector('#retainerId')?.value || '');
                const data = {
                    matterId: getSelectValue(modal, '#retainerMatter'),
                    clientId: modalClients['retainerModal']?.id || null,
                    initialAmount: parseFloat(getInputValue(modal, '#retainerAmount')) || 0,
                    notes: getInputValue(modal, '#retainerNotes'),
                    status: getSelectValue(modal, '#retainerStatus')
                };
                await saveBillingItem('retainerModal', 'retainers', data, isNaN(id) ? null : id);
            });
        }
    }

    // Helper functions for form values
    function getInputValue(modal, selector) {
        const input = modal?.querySelector(selector);
        return input?.value || '';
    }

    function getSelectValue(modal, selector) {
        const select = modal?.querySelector(selector);
        const value = select?.value;
        if (!value) return null;
        // Return as integer if it's a number, otherwise return as string (for categories)
        const parsed = parseInt(value);
        return isNaN(parsed) ? value : parsed;
    }

    // Store loaded matters globally so they can be re-applied when modals open
    let billingMatters = [];
    
    // Load matters and assignees for modal dropdowns
    async function loadBillingFormData() {
        const orgId = getOrganizationId();
        console.log('[Billing] Loading form data for org:', orgId);
        if (!orgId) {
            console.warn('[Billing] No organization ID found, skipping form data load');
            return;
        }

        try {
            // Load matters
            console.log('[Billing] Fetching matters from:', `/api/billing/${orgId}/matters`);
            const mattersResponse = await fetch(`/api/billing/${orgId}/matters`);
            if (!mattersResponse.ok) {
                console.error('[Billing] Matters API failed with status:', mattersResponse.status);
                return;
            }
            const mattersResult = await mattersResponse.json();
            console.log('[Billing] Matters API response:', mattersResult);
            
            if (mattersResult.success && mattersResult.data && mattersResult.data.length > 0) {
                billingMatters = mattersResult.data;
                console.log('[Billing] First matter sample:', mattersResult.data[0]);
                const selectCount = document.querySelectorAll('.billing-matter-select').length;
                console.log('[Billing] Found', selectCount, 'select elements with .billing-matter-select');
                populateMatterDropdowns();
                console.log('[Billing] Populated matter dropdowns with', mattersResult.data.length, 'items');
            } else {
                console.warn('[Billing] No matters returned from API or empty array');
            }

            // Load clients and store globally for popup
            console.log('[Billing] Fetching clients...');
            const clientsResponse = await fetch(`/api/billing/${orgId}/clients`);
            const clientsResult = await clientsResponse.json();
            console.log('[Billing] Clients result:', clientsResult);
            if (clientsResult.success && clientsResult.data) {
                billingClients = clientsResult.data;
                console.log('[Billing] Loaded', billingClients.length, 'clients for popup');
            }

            // Load assignees and store globally for popup
            console.log('[Billing] Fetching assignees...');
            const assigneesResponse = await fetch(`/api/billing/${orgId}/assignees`);
            const assigneesResult = await assigneesResponse.json();
            console.log('[Billing] Assignees result:', assigneesResult);
            if (assigneesResult.success && assigneesResult.data) {
                billingAssignees = assigneesResult.data;
                console.log('[Billing] Loaded', billingAssignees.length, 'assignees for popup');
            }

            // Load expense categories
            console.log('[Billing] Fetching expense categories...');
            const categoriesResponse = await fetch(`/api/billing/${orgId}/expense-categories`);
            const categoriesResult = await categoriesResponse.json();
            console.log('[Billing] Categories result:', categoriesResult);
            if (categoriesResult.success && categoriesResult.data) {
                populateSelectOptionsSimple('.billing-category-select', categoriesResult.data);
                console.log('[Billing] Populated category dropdowns with', categoriesResult.data.length, 'items');
            }
        } catch (error) {
            console.error('[Billing] Error loading billing form data:', error);
        }
    }
    
    // Populate matter dropdowns with stored data
    function populateMatterDropdowns() {
        if (!billingMatters || billingMatters.length === 0) {
            console.warn('[Billing] No matters to populate');
            return;
        }
        
        const selects = document.querySelectorAll('.billing-matter-select');
        console.log('[Billing] Populating', selects.length, 'matter dropdowns');
        
        selects.forEach((select, idx) => {
            // Preserve the placeholder
            const placeholderText = select.querySelector('option[value=""]')?.textContent || 'Select Event...';
            select.innerHTML = '';
            
            // Add placeholder option
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = placeholderText;
            select.appendChild(placeholder);
            
            // Add matter options
            billingMatters.forEach(matter => {
                const opt = document.createElement('option');
                // Handle both camelCase (from JSON) and PascalCase (from C#)
                opt.value = matter.id ?? matter.Id ?? '';
                opt.textContent = matter.title ?? matter.Title ?? 'Unknown';
                select.appendChild(opt);
            });
            
            console.log(`[Billing] Select #${idx} now has ${select.options.length} options`);
        });
    }

    function populateSelectOptions(selector, data, valueKey, labelKey) {
        document.querySelectorAll(selector).forEach(select => {
            const placeholder = select.querySelector('option[value=""]');
            select.innerHTML = '';
            if (placeholder) {
                select.appendChild(placeholder);
            } else {
                const opt = document.createElement('option');
                opt.value = '';
                opt.textContent = 'Select...';
                select.appendChild(opt);
            }
            data.forEach(item => {
                const opt = document.createElement('option');
                // Handle both camelCase and PascalCase property names (C# serialization)
                opt.value = item[valueKey] ?? item[valueKey.charAt(0).toUpperCase() + valueKey.slice(1)] ?? '';
                opt.textContent = item[labelKey] ?? item[labelKey.charAt(0).toUpperCase() + labelKey.slice(1)] ?? '';
                select.appendChild(opt);
            });
        });
    }

    function populateSelectOptionsSimple(selector, data) {
        document.querySelectorAll(selector).forEach(select => {
            const placeholder = select.querySelector('option[value=""]');
            select.innerHTML = '';
            if (placeholder) {
                select.appendChild(placeholder);
            } else {
                const opt = document.createElement('option');
                opt.value = '';
                opt.textContent = 'Select...';
                select.appendChild(opt);
            }
            data.forEach(item => {
                const opt = document.createElement('option');
                opt.value = item;
                opt.textContent = item;
                select.appendChild(opt);
            });
        });
    }

    // Initialize assignee buttons with popup functionality
    function initializeBillingAssigneeButtons() {
        const assigneeButtons = document.querySelectorAll('.billing-add-assignee-btn');
        console.log('[Billing] Initializing', assigneeButtons.length, 'assignee buttons');
        
        assigneeButtons.forEach(btn => {
            // Remove existing listeners by cloning and replacing
            const newBtn = btn.cloneNode(true);
            btn.parentNode.replaceChild(newBtn, btn);
            
            newBtn.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                const modalId = this.dataset.modal;
                console.log('[Billing] Assignee button clicked for modal:', modalId);
                showBillingAssigneePopup(modalId, this);
            });
        });
    }

    // Initialize client buttons with popup functionality
    function initializeBillingClientButtons() {
        const clientButtons = document.querySelectorAll('.billing-add-client-btn');
        console.log('[Billing] Initializing', clientButtons.length, 'client buttons');

        clientButtons.forEach(btn => {
            // Remove existing listeners by cloning and replacing
            const newBtn = btn.cloneNode(true);
            btn.parentNode.replaceChild(newBtn, btn);

            newBtn.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                const modalId = this.dataset.modal;
                console.log('[Billing] Client button clicked for modal:', modalId);
                showBillingClientPopup(modalId, this);
            });
        });
    }

    // Show client selection popup (same UI/behavior as assignee popup)
    function showBillingClientPopup(modalId, buttonElement) {
        console.log('[Billing] showBillingClientPopup called for:', modalId);
        console.log('[Billing] billingClients available:', billingClients?.length || 0);

        // Remove any existing popup
        const existingPopup = document.querySelector('.billing-assignment-popup');
        if (existingPopup) {
            existingPopup.remove();
        }

        if (!billingClients || billingClients.length === 0) {
            console.warn('[Billing] No clients loaded, attempting to reload...');
            loadBillingFormData().then(() => {
                if (billingClients && billingClients.length > 0) {
                    showBillingClientPopup(modalId, buttonElement);
                } else {
                    alert('No clients available. Please try again.');
                }
            });
            return;
        }

        const popup = document.createElement('div');
        popup.className = 'billing-assignment-popup';

        const btnRect = buttonElement.getBoundingClientRect();
        const spaceOnRight = window.innerWidth - btnRect.right;
        let top, left;

        if (spaceOnRight > 370) {
            top = btnRect.top;
            left = btnRect.right + 10;
        } else {
            top = btnRect.bottom + 5;
            left = Math.max(10, btnRect.left - 150);
        }

        popup.style.cssText = `
            position: fixed;
            top: ${top}px;
            left: ${left}px;
            z-index: 1200;
        `;

        const currentClient = modalClients[modalId];

        popup.innerHTML = `
            <input type="text" class="assignee-search" placeholder="Search clients..." />
            <div class="assignee-list">
                ${billingClients.map(client => {
                    const id = client.id ?? client.Id;
                    const name = client.name ?? client.Name ?? 'Unknown';
                    const initials = (client.initials ?? client.Initials) || calcInitials(name);
                    const isSelected = currentClient && currentClient.id === id;
                    return `
                        <div class="assignee-item ${isSelected ? 'selected' : ''}" data-id="${id}" data-name="${name}" data-initials="${initials}">
                            <div class="assignee-avatar">${initials}</div>
                            <div>
                                <div class="assignee-name">${name}</div>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;

        document.body.appendChild(popup);

        const searchInput = popup.querySelector('.assignee-search');
        searchInput.addEventListener('input', function() {
            const query = this.value.toLowerCase();
            popup.querySelectorAll('.assignee-item').forEach(item => {
                const name = item.dataset.name.toLowerCase();
                item.style.display = name.includes(query) ? 'flex' : 'none';
            });
        });

        popup.querySelectorAll('.assignee-item').forEach(item => {
            item.addEventListener('click', function() {
                const clientId = parseInt(this.dataset.id);
                const clientName = this.dataset.name;
                const clientInitials = this.dataset.initials;

                console.log('[Billing] Client selected:', clientName, 'ID:', clientId);

                if (modalClients[modalId] && modalClients[modalId].id === clientId) {
                    modalClients[modalId] = null;
                } else {
                    modalClients[modalId] = { id: clientId, name: clientName, initials: clientInitials };
                }

                updateClientHiddenInput(modalId);
                renderBillingClient(modalId);
                popup.remove();
            });
        });

        setTimeout(() => {
            document.addEventListener('click', function closePopup(e) {
                if (!popup.contains(e.target) && e.target !== buttonElement) {
                    popup.remove();
                    document.removeEventListener('click', closePopup);
                }
            });
        }, 10);

        searchInput.focus();
    }

    // Show assignee selection popup
    function showBillingAssigneePopup(modalId, buttonElement) {
        console.log('[Billing] showBillingAssigneePopup called for:', modalId);
        console.log('[Billing] billingAssignees available:', billingAssignees?.length || 0);
        
        // Remove any existing popup
        const existingPopup = document.querySelector('.billing-assignment-popup');
        if (existingPopup) {
            existingPopup.remove();
        }

        if (!billingAssignees || billingAssignees.length === 0) {
            console.warn('[Billing] No assignees loaded, attempting to reload...');
            // Try to load assignees
            loadBillingFormData().then(() => {
                if (billingAssignees && billingAssignees.length > 0) {
                    showBillingAssigneePopup(modalId, buttonElement);
                } else {
                    alert('No assignees available. Please try again.');
                }
            });
            return;
        }

        // Create popup
        const popup = document.createElement('div');
        popup.className = 'billing-assignment-popup';
        
        // Position popup relative to button - position it to the right of button
        const btnRect = buttonElement.getBoundingClientRect();
        const modalRect = buttonElement.closest('.billing-modal')?.getBoundingClientRect();
        
        // Check if there's room on the right, otherwise position below
        const spaceOnRight = window.innerWidth - btnRect.right;
        let top, left;
        
        if (spaceOnRight > 370) {
            // Position to the right
            top = btnRect.top;
            left = btnRect.right + 10;
        } else {
            // Position below
            top = btnRect.bottom + 5;
            left = Math.max(10, btnRect.left - 150);
        }
        
        popup.style.cssText = `
            position: fixed;
            top: ${top}px;
            left: ${left}px;
            z-index: 1200;
        `;

        // Get current selection for this modal
        const currentAssignee = modalAssignees[modalId];

        // Build popup content - handle both camelCase and PascalCase
        popup.innerHTML = `
            <input type="text" class="assignee-search" placeholder="Search assignees..." />
            <div class="assignee-list">
                ${billingAssignees.map(assignee => {
                    const id = assignee.id ?? assignee.Id;
                    const name = assignee.name ?? assignee.Name ?? 'Unknown';
                    const email = assignee.email ?? assignee.Email ?? '';
                    const initials = (assignee.initials ?? assignee.Initials) || 
                        name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
                    const isSelected = currentAssignee && currentAssignee.id === id;
                    return `
                        <div class="assignee-item ${isSelected ? 'selected' : ''}" data-id="${id}" data-name="${name}" data-initials="${initials}">
                            <div class="assignee-avatar">${initials}</div>
                            <div>
                                <div class="assignee-name">${name}</div>
                                ${email ? `<div class="assignee-email">${email}</div>` : ''}
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;

        document.body.appendChild(popup);
        console.log('[Billing] Popup created and appended to body');

        // Search functionality
        const searchInput = popup.querySelector('.assignee-search');
        searchInput.addEventListener('input', function() {
            const query = this.value.toLowerCase();
            popup.querySelectorAll('.assignee-item').forEach(item => {
                const name = item.dataset.name.toLowerCase();
                item.style.display = name.includes(query) ? 'flex' : 'none';
            });
        });

        // Click on assignee to select
        popup.querySelectorAll('.assignee-item').forEach(item => {
            item.addEventListener('click', function() {
                const assigneeId = parseInt(this.dataset.id);
                const assigneeName = this.dataset.name;
                const assigneeInitials = this.dataset.initials;
                
                console.log('[Billing] Assignee selected:', assigneeName, 'ID:', assigneeId);
                
                // Toggle selection
                if (modalAssignees[modalId] && modalAssignees[modalId].id === assigneeId) {
                    // Deselect
                    modalAssignees[modalId] = null;
                    console.log('[Billing] Deselected assignee');
                } else {
                    // Select
                    modalAssignees[modalId] = { id: assigneeId, name: assigneeName, initials: assigneeInitials };
                    console.log('[Billing] Selected assignee:', modalAssignees[modalId]);
                }

                // Update hidden input
                updateAssigneeHiddenInput(modalId);
                
                // Update visual display
                renderBillingAssignee(modalId);
                
                // Close popup
                popup.remove();
            });
        });

        // Close popup when clicking outside
        setTimeout(() => {
            document.addEventListener('click', function closePopup(e) {
                if (!popup.contains(e.target) && e.target !== buttonElement) {
                    popup.remove();
                    document.removeEventListener('click', closePopup);
                }
            });
        }, 10);

        // Focus search
        searchInput.focus();
    }

    // Update the hidden input with assignee ID
    function updateAssigneeHiddenInput(modalId) {
        const inputMap = {
            'timeEntryModal': '#timeEntryAssignee',
            'expenseModal': '#expenseAssignee'
        };
        
        const selector = inputMap[modalId];
        if (selector) {
            const input = document.querySelector(selector);
            if (input) {
                input.value = modalAssignees[modalId] ? modalAssignees[modalId].id : '';
            }
        }
    }

    function updateClientHiddenInput(modalId) {
        const inputMap = {
            'timeEntryModal': '#timeEntryClient',
            'expenseModal': '#expenseClient',
            'invoiceModal': '#invoiceClient',
            'retainerModal': '#retainerClient'
        };

        const selector = inputMap[modalId];
        if (!selector) return;

        const input = document.querySelector(selector);
        if (input) {
            input.value = modalClients[modalId] ? modalClients[modalId].id : '';
        }
    }

    // Render the selected assignee avatar
    function renderBillingAssignee(modalId) {
        const containerMap = {
            'timeEntryModal': '#timeEntryAssigneesContainer',
            'expenseModal': '#expenseAssigneesContainer'
        };
        
        const selector = containerMap[modalId];
        if (!selector) {
            console.warn('[Billing] No container selector for modal:', modalId);
            return;
        }
        
        const container = document.querySelector(selector);
        if (!container) {
            console.warn('[Billing] Container not found:', selector);
            return;
        }

        const assignee = modalAssignees[modalId];
        console.log('[Billing] Rendering assignee for', modalId, ':', assignee);
        
        if (assignee) {
            // Use stored initials or calculate from name
            const initials = assignee.initials || 
                (assignee.name ? assignee.name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2) : 'U');
            container.innerHTML = `
                <div class="member-avatar" title="${assignee.name || 'Assignee'}">
                    ${initials}
                </div>
            `;
        } else {
            container.innerHTML = '';
        }
    }

    function renderBillingClient(modalId) {
        const containerMap = {
            'timeEntryModal': '#timeEntryClientsContainer',
            'expenseModal': '#expenseClientsContainer',
            'invoiceModal': '#invoiceClientsContainer',
            'retainerModal': '#retainerClientsContainer'
        };

        const selector = containerMap[modalId];
        if (!selector) {
            console.warn('[Billing] No client container selector for modal:', modalId);
            return;
        }

        const container = document.querySelector(selector);
        if (!container) {
            console.warn('[Billing] Client container not found:', selector);
            return;
        }

        const client = modalClients[modalId];
        console.log('[Billing] Rendering client for', modalId, ':', client);

        if (client) {
            const initials = client.initials || calcInitials(client.name || 'Client');
            container.innerHTML = `
                <div class="member-avatar" title="${client.name || 'Client'}">
                    ${initials}
                </div>
            `;
        } else {
            container.innerHTML = '';
        }
    }

    // Clear assignee for a modal
    function clearModalAssignee(modalId) {
        modalAssignees[modalId] = null;
        updateAssigneeHiddenInput(modalId);
        renderBillingAssignee(modalId);
    }

    function clearModalClient(modalId) {
        modalClients[modalId] = null;
        updateClientHiddenInput(modalId);
        renderBillingClient(modalId);
    }

    function calcInitials(name) {
        if (!name) return 'C';
        const parts = name.trim().split(/\s+/).filter(Boolean);
        const first = parts[0]?.[0] ?? 'C';
        const second = parts.length > 1 ? (parts[1]?.[0] ?? '') : (parts[0]?.[1] ?? '');
        return (first + second).toUpperCase();
    }

    // Auto-assign client when a matter/event is selected
    function initializeMatterClientAutoFill() {
        document.querySelectorAll('.billing-matter-select').forEach(select => {
            select.addEventListener('change', function() {
                const modal = this.closest('.billing-modal');
                const modalId = modal?.id;
                if (!modalId || !(modalId in modalClients)) {
                    return;
                }

                const selectedId = parseInt(this.value);
                if (!selectedId || isNaN(selectedId)) {
                    clearModalClient(modalId);
                    return;
                }

                const matter = billingMatters.find(m => (m.id ?? m.Id) === selectedId);
                const clientId = matter?.clientOrganizationId ?? matter?.ClientOrganizationId ?? null;
                const clientName = matter?.clientOrganizationName ?? matter?.ClientOrganizationName ?? null;

                if (!clientId) {
                    clearModalClient(modalId);
                    return;
                }

                const client = billingClients.find(c => (c.id ?? c.Id) === clientId);
                const name = client?.name ?? client?.Name ?? clientName ?? 'Client';
                const initials = client?.initials ?? client?.Initials ?? calcInitials(name);

                modalClients[modalId] = { id: clientId, name, initials };
                updateClientHiddenInput(modalId);
                renderBillingClient(modalId);
            });
        });
    }

    function formatStatusLabel(value) {
        if (value == null) return '—';
        const s = String(value);
        return s
            .replace(/([a-z])([A-Z])/g, '$1 $2')
            .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2');
    }

    function setModalTitle(modalId, text) {
        const modal = document.getElementById(modalId);
        const title = modal?.querySelector('.billing-modal-title');
        if (title && text) title.textContent = text;
    }

    function setSelectValue(modal, selector, value) {
        const el = modal?.querySelector(selector);
        if (!el) return;
        el.value = value == null ? '' : String(value);
    }

    function setInputValue(modal, selector, value) {
        const el = modal?.querySelector(selector);
        if (!el) return;
        el.value = value == null ? '' : String(value);
    }

    function setCheckboxValue(modal, selector, checked) {
        const el = modal?.querySelector(selector);
        if (!el) return;
        el.checked = !!checked;
    }

    function setModalClientById(modalId, clientId) {
        if (!clientId) {
            clearModalClient(modalId);
            return;
        }
        const client = billingClients.find(c => (c.id ?? c.Id) === clientId);
        const name = client?.name ?? client?.Name ?? 'Client';
        const initials = client?.initials ?? client?.Initials ?? calcInitials(name);
        modalClients[modalId] = { id: clientId, name, initials };
        updateClientHiddenInput(modalId);
        renderBillingClient(modalId);
    }

    function setModalAssigneeById(modalId, assigneeId) {
        if (!assigneeId) {
            clearModalAssignee(modalId);
            return;
        }
        const a = billingAssignees.find(x => (x.id ?? x.Id) === assigneeId);
        const name = a?.name ?? a?.Name ?? 'Assignee';
        const initials = a?.initials ?? a?.Initials ?? calcInitials(name);
        modalAssignees[modalId] = { id: assigneeId, name, initials };
        updateAssigneeHiddenInput(modalId);
        renderBillingAssignee(modalId);
    }

    function openBillingDetailsFromRow(rowEl) {
        const d = rowEl?.dataset || {};
        const type = d.billingItem;
        const id = parseInt(d.id);
        if (!type || isNaN(id)) return;

        let modalId;
        switch (type) {
            case 'time-entry': modalId = 'timeEntryModal'; break;
            case 'expense': modalId = 'expenseModal'; break;
            case 'invoice': modalId = 'invoiceModal'; break;
            case 'retainer': modalId = 'retainerModal'; break;
            default: return;
        }

        const modal = document.getElementById(modalId);
        if (!modal) return;

        resetBillingModal(modalId);
        populateMatterDropdowns();

        // id fields
        if (modalId === 'timeEntryModal') setInputValue(modal, '#timeEntryId', id);
        if (modalId === 'expenseModal') setInputValue(modal, '#expenseId', id);
        if (modalId === 'invoiceModal') setInputValue(modal, '#invoiceId', id);
        if (modalId === 'retainerModal') setInputValue(modal, '#retainerId', id);

        // common
        const matterId = parseInt(d.matterId || '');
        const clientId = parseInt(d.clientId || '');
        const assigneeId = parseInt(d.assigneeId || '');
        const status = parseInt(d.status || '');

        setModalClientById(modalId, isNaN(clientId) ? null : clientId);
        if (modalId === 'timeEntryModal' || modalId === 'expenseModal') {
            setModalAssigneeById(modalId, isNaN(assigneeId) ? null : assigneeId);
        }

        if (modalId === 'timeEntryModal') {
            setModalTitle(modalId, 'Time Entry');
            setSelectValue(modal, '#timeEntryMatter', isNaN(matterId) ? '' : matterId);
            setInputValue(modal, '#timeEntryDate', d.date || '');
            setInputValue(modal, '#timeEntryHours', d.hours || '');
            setInputValue(modal, '#timeEntryRate', d.rate || '');
            setInputValue(modal, '#timeEntryDescription', d.description || '');
            setCheckboxValue(modal, '#timeEntryBillable', d.isBillable === 'true');
            setSelectValue(modal, '#timeEntryStatus', isNaN(status) ? '' : status);
        } else if (modalId === 'expenseModal') {
            setModalTitle(modalId, 'Expense');
            setSelectValue(modal, '#expenseMatter', isNaN(matterId) ? '' : matterId);
            setInputValue(modal, '#expenseDate', d.date || '');
            setSelectValue(modal, '#expenseCategory', d.category || '');
            setInputValue(modal, '#expenseAmount', d.amount || '');
            setInputValue(modal, '#expenseDescription', d.description || '');
            setCheckboxValue(modal, '#expenseBillable', d.isBillable === 'true');
            setSelectValue(modal, '#expenseStatus', isNaN(status) ? '' : status);
        } else if (modalId === 'invoiceModal') {
            setModalTitle(modalId, 'Invoice');
            setSelectValue(modal, '#invoiceMatter', isNaN(matterId) ? '' : matterId);
            setInputValue(modal, '#invoiceDate', d.invoiceDate || '');
            setInputValue(modal, '#invoiceDueDate', d.dueDate || '');
            setInputValue(modal, '#invoiceTax', d.taxAmount || '');
            setInputValue(modal, '#invoiceNotes', d.notes || '');
            setInputValue(modal, '#invoiceTerms', d.terms || '');
            setSelectValue(modal, '#invoiceStatus', isNaN(status) ? '' : status);
        } else if (modalId === 'retainerModal') {
            setModalTitle(modalId, 'Retainer');
            setSelectValue(modal, '#retainerMatter', isNaN(matterId) ? '' : matterId);
            setInputValue(modal, '#retainerAmount', d.initialAmount || '');
            setInputValue(modal, '#retainerNotes', d.notes || '');
            setSelectValue(modal, '#retainerStatus', isNaN(status) ? '' : status);
        }

        initializeBillingAssigneeButtons();
        initializeBillingClientButtons();
        modal.style.display = 'flex';
        adjustBillingModalPosition(modal);
    }

    // Click anywhere on a row to open details (Task modal behavior)
    document.addEventListener('click', function(e) {
        const row = e.target.closest('.billing-card-item[data-billing-item]');
        if (!row) return;
        if (row.classList.contains('billing-column-header-card')) return;
        if (e.target.closest('button, a, input, select, textarea, .billing-description-btn, .billing-description-btn-overview')) return;
        openBillingDetailsFromRow(row);
    });

    // Make Edit buttons open the same details modal
    document.addEventListener('click', function(e) {
        const btn = e.target.closest('.billing-action-btn.edit-btn');
        if (!btn) return;
        const row = btn.closest('.billing-card-item[data-billing-item]');
        if (!row) return;
        e.preventDefault();
        e.stopPropagation();
        openBillingDetailsFromRow(row);
    });

    // Overview Recent Activity loader
    async function loadRecentActivity() {
        const container = document.getElementById('billingOverviewCardsContainer');
        if (!container) return;

        const orgId = getOrganizationId();
        if (!orgId) return;

        try {
            const resp = await fetch(`/api/billing/${orgId}/recent-activity`);
            const json = await resp.json();
            if (!json.success) {
                throw new Error(json.error || 'Failed to load recent activity');
            }

            const items = json.data || [];
            if (items.length === 0) {
                container.innerHTML = `
                    <div class="text-center py-5 text-muted" style="border: 1px dashed #e5e7eb; border-radius: 12px; background: #fff;">
                        <i class="fas fa-inbox mb-2" style="font-size: 1.5rem;"></i>
                        <div style="font-weight: 600;">No recent activity</div>
                        <div style="font-size: 0.875rem;">Time entries, expenses, invoices, and retainers will appear here.</div>
                    </div>
                `;
                return;
            }

            container.innerHTML = items.map(item => {
                const statusText = formatStatusLabel(item.statusText);
                const statusClass = (item.statusClass || '').toLowerCase();

                return `
                    <div class="billing-card-item"
                         data-billing-item="${item.type}"
                         data-id="${item.id || ''}"
                         data-matter-id="${item.matterId || 0}"
                         data-client-id="${item.clientId || 0}"
                         data-assignee-id="${item.assigneeId || 0}"
                         data-date="${item.date || ''}"
                         data-hours="${item.hours ?? ''}"
                         data-rate="${item.rate ?? ''}"
                         data-description="${(item.description || '').replace(/"/g, '&quot;')}"
                         data-is-billable="${item.isBillable ? 'true' : 'false'}"
                         data-status="${item.status ?? ''}"
                         data-category="${(item.category || '').replace(/"/g, '&quot;')}"
                         data-amount="${item.amount ?? ''}"
                         data-invoice-date="${item.invoiceDate || ''}"
                         data-due-date="${item.dueDate || ''}"
                         data-tax-amount="${item.taxAmount ?? ''}"
                         data-notes="${(item.notes || '').replace(/"/g, '&quot;')}"
                         data-terms="${(item.terms || '').replace(/"/g, '&quot;')}"
                         data-initial-amount="${item.initialAmount ?? ''}">
                        <div class="billing-date-column">${item.dateDisplay || '—'}</div>
                        <div class="billing-type-column">${item.typeDisplay || '—'}</div>
                        <div class="billing-client-column"><span style="color: #1e293b;">${item.clientName || '—'}</span></div>
                        <div class="billing-matter-column"><span style="color: #1e293b;">${item.matterTitle || '—'}</span></div>
                        <div class="billing-attorney-column">
                            <div class="billing-user-avatar">${item.assigneeInitials || '—'}</div>
                            <span class="billing-user-name">${item.assigneeName || '—'}</span>
                        </div>
                        <div class="billing-description-column-overview">
                            <button class="billing-description-btn-overview" title="${(item.description || '').replace(/"/g, '&quot;')}" onclick="event.stopPropagation();">
                                <i class="fa-solid fa-align-left" style="transform: scaleY(-1);"></i>
                            </button>
                        </div>
                        <div class="billing-amount-column">${item.amountDisplay || '—'}</div>
                        <div class="billing-status-column">
                            <span class="billing-status-badge ${statusClass}">${statusText}</span>
                        </div>
                    </div>
                `;
            }).join('');
        } catch (err) {
            console.error('[Billing] Failed to load recent activity:', err);
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
        },
        openModal: openBillingModal,
        closeModal: closeBillingModal,
        loadFormData: loadBillingFormData
    };
})();

