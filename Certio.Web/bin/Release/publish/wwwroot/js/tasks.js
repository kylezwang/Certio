// Tasks Page - Trello-like Functionality
(function() {
    'use strict';

    let sortableInstances = [];
    let currentTaskId = null;

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initializeDragAndDrop();
        initializeSearch();
        initializeInboxCheckboxes();
        initializeCardCheckboxes();
        initializeAddTaskButton();
        initializeTaskForm();
        initializeCalendarToggle();
        initializeColumnAddButtons();
        initializeLocationSection();
        initializeMatterPopupPositioning();
        
        // Location search will be initialized by Google Maps callback or setTimeout
        if (!window.google || !google.maps.places) {
            // Try to initialize location search again after Google Maps loads
            setTimeout(initializeLocationSearch, 2000);
        } else {
            initializeLocationSearch();
        }
    });

    // Initialize Drag and Drop for Kanban columns
    function initializeDragAndDrop() {
        // Destroy existing sortable instances first
        sortableInstances.forEach(instance => {
            if (instance && instance.destroy) {
                instance.destroy();
            }
        });
        sortableInstances = [];
        
        const columns = document.querySelectorAll('.column-cards');
        console.log('Found columns:', columns.length, columns);
        
        columns.forEach((column, index) => {
            console.log(`Initializing column ${index}:`, column.id);
            const sortable = new Sortable(column, {
                group: 'kanban',
                animation: 150,
                ghostClass: 'task-card-ghost',
                dragClass: 'task-card-drag',
                handle: '.task-card',
                scroll: document.querySelector('.kanban-board-container'), // Use our kanban container
                scrollSensitivity: 80,
                scrollSpeed: 15,
                bubbleScroll: true,
                forceFallback: false,
                onStart: function(evt) {
                    console.log('Drag started:', evt.item.id);
                    // Enable auto-scroll when drag starts
                    enableAutoScroll();
                },
                onEnd: function(evt) {
                    console.log('Drag ended:', evt.item.id, 'to', evt.to.id);
                    // Disable auto-scroll when drag ends
                    disableAutoScroll();
                    handleTaskMove(evt);
                }
            });
            sortableInstances.push(sortable);
        });
    }

    // Reinitialize drag and drop when board view becomes visible
    function reinitializeDragAndDropIfNeeded() {
        const boardView = document.getElementById('boardView');
        if (boardView && !boardView.classList.contains('d-none')) {
            console.log('Board view is visible, reinitializing drag and drop');
            // Small delay to ensure DOM is ready
            setTimeout(() => {
                initializeDragAndDrop();
            }, 100);
        }
    }

    // Expose function globally so it can be called from view switching
    window.reinitializeDragAndDropIfNeeded = reinitializeDragAndDropIfNeeded;

    // Auto-scroll functionality for drag operations
    let autoScrollInterval = null;
    let isDragging = false;
    let dragElement = null;
    let currentMousePos = { x: 0, y: 0 };

    function enableAutoScroll() {
        console.log('Auto-scroll enabled');
        isDragging = true;
        
        // Add global mouse position tracker
        document.addEventListener('mousemove', updateMousePosition);
        document.addEventListener('touchmove', updateMousePosition);
        
        // Start auto-scroll check interval
        startAutoScrollCheck();
    }
    
    function updateMousePosition(e) {
        currentMousePos = {
            x: e.clientX || (e.touches && e.touches[0].clientX) || 0,
            y: e.clientY || (e.touches && e.touches[0].clientY) || 0
        };
    }
    
    function startAutoScrollCheck() {
        if (autoScrollInterval) {
            clearInterval(autoScrollInterval);
        }
        
        autoScrollInterval = setInterval(() => {
            if (isDragging) {
                checkAndPerformAutoScroll();
            }
        }, 16); // ~60fps
    }


    function disableAutoScroll() {
        console.log('Auto-scroll disabled');
        isDragging = false;
        
        // Remove mouse position tracker
        document.removeEventListener('mousemove', updateMousePosition);
        document.removeEventListener('touchmove', updateMousePosition);
        
        // Clear any existing scroll interval
        if (autoScrollInterval) {
            clearInterval(autoScrollInterval);
            autoScrollInterval = null;
        }
        
        // Remove visual indicators
        const kanbanContainer = document.querySelector('.kanban-board-container');
        if (kanbanContainer) {
            kanbanContainer.classList.remove('drag-scroll-left', 'drag-scroll-right');
        }
    }
    
    function checkAndPerformAutoScroll() {
        const scrollThreshold = 100; // Distance from edge to start scrolling
        const scrollSpeed = 20; // Pixels to scroll per interval
        
        // Get kanban container and its bounds
        const kanbanContainer = document.querySelector('.kanban-board-container');
        if (!kanbanContainer) return;
        
        const kanbanRect = kanbanContainer.getBoundingClientRect();
        
        // Check if mouse is near left or right edge of kanban container
        const distanceFromLeft = currentMousePos.x - kanbanRect.left;
        const distanceFromRight = kanbanRect.right - currentMousePos.x;
        
        let scrollX = 0;
        
        if (distanceFromLeft < scrollThreshold && distanceFromLeft > 0) {
            scrollX = -scrollSpeed; // Scroll left
        } else if (distanceFromRight < scrollThreshold && distanceFromRight > 0) {
            scrollX = scrollSpeed; // Scroll right
        }
        
        // Perform scroll if needed
        if (scrollX !== 0) {
            const oldScrollLeft = kanbanContainer.scrollLeft;
            const maxScrollLeft = kanbanContainer.scrollWidth - kanbanContainer.clientWidth;
            
            // Only scroll if we haven't reached the limits
            if ((scrollX < 0 && oldScrollLeft > 0) || (scrollX > 0 && oldScrollLeft < maxScrollLeft)) {
                kanbanContainer.scrollLeft += scrollX;
                
                // Add visual indicators
                if (scrollX < 0) {
                    kanbanContainer.classList.add('drag-scroll-left');
                    kanbanContainer.classList.remove('drag-scroll-right');
                } else if (scrollX > 0) {
                    kanbanContainer.classList.add('drag-scroll-right');
                    kanbanContainer.classList.remove('drag-scroll-left');
                }
                
                console.log('Kanban horizontal scroll:', { 
                    oldScrollLeft, 
                    newScrollLeft: kanbanContainer.scrollLeft, 
                    scrollX,
                    maxScrollLeft,
                    mouseX: currentMousePos.x,
                    distanceFromLeft,
                    distanceFromRight
                });
            } else {
                // Remove visual indicators when we can't scroll anymore
                kanbanContainer.classList.remove('drag-scroll-left', 'drag-scroll-right');
            }
        } else {
            // Remove visual indicators when not scrolling
            kanbanContainer.classList.remove('drag-scroll-left', 'drag-scroll-right');
        }
    }


    // Handle task moved between columns or reordered
    function handleTaskMove(evt) {
        console.log('handleTaskMove called with:', evt);
        const taskId = evt.item.getAttribute('data-task-id');
        const newColumn = evt.to.id;
        const newOrder = evt.newIndex;
        
        console.log('Task ID:', taskId, 'New Column:', newColumn, 'New Order:', newOrder);
        
        // Determine new status based on column
        let newStatus = 'Pending';
        if (newColumn === 'column-inprogress-full') newStatus = 'InProgress';
        else if (newColumn === 'column-review-full') newStatus = 'Review';
        else if (newColumn === 'column-completed-full') newStatus = 'Completed';
        
        console.log(`Moving task ${taskId} to ${newColumn} with status ${newStatus}`);
        
        // Update task status via API
        updateTaskStatus(taskId, newStatus, newOrder);
    }

    // Update task status via AJAX
    function updateTaskStatus(taskId, status, order) {
        fetch('/Tasks/UpdateStatus', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({
                taskId: parseInt(taskId),
                status: status,
                order: order
            })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                console.log('Task status updated successfully');
                updateColumnCounts();
                
                // Update inbox checkbox and visual state
                const inboxCheckbox = document.querySelector(`#inbox-task-${taskId}`);
                if (inboxCheckbox) {
                    const isCompleted = (status === 'Completed');
                    inboxCheckbox.checked = isCompleted;
                    const inboxItem = inboxCheckbox.closest('.task-card-item');
                    if (inboxItem) {
                        inboxItem.classList.toggle('completed', isCompleted);
                        
                        // Update the task's data-status attribute
                        inboxItem.setAttribute('data-status', status);
                        
                        // Move the task to the correct inbox section if needed
                        moveTaskToCorrectInboxSection(taskId, status);
                    }
                }
            } else {
                console.error('Failed to update task status:', data.message);
                showToast('Error', 'Failed to update task status', 'error');
            }
        })
        .catch(error => {
            console.error('Error updating task status:', error);
            showToast('Error', 'An error occurred while updating the task', 'error');
        });
    }

    // Move task to correct inbox section based on status
    function moveTaskToCorrectInboxSection(taskId, status) {
        const taskItem = document.querySelector(`[data-task-id="${taskId}"]`);
        if (!taskItem) return;
        
        // Find the correct inbox section based on status
        let targetSection = null;
        const sections = document.querySelectorAll('.inbox-status-section');
        sections.forEach(section => {
            const badge = section.querySelector('.badge');
            if (badge) {
                const badgeText = badge.textContent.toLowerCase();
                if ((status === 'Pending' && badgeText.includes('planned')) ||
                    (status === 'InProgress' && badgeText.includes('progress')) ||
                    (status === 'Review' && badgeText.includes('review')) ||
                    (status === 'Completed' && badgeText.includes('completed'))) {
                    targetSection = section;
                }
            }
        });
        
        if (targetSection) {
            const tasksContainer = targetSection.querySelector('.inbox-status-tasks');
            if (tasksContainer) {
                // Remove from current location
                taskItem.remove();
                // Add to new location
                tasksContainer.appendChild(taskItem);
                console.log(`Moved task ${taskId} to ${status} section in inbox`);
            }
        }
    }
    function initializeSearch() {
        const searchInput = document.getElementById('taskSearchInput');
        if (!searchInput) return;

        searchInput.addEventListener('input', function(e) {
            const searchTerm = e.target.value.toLowerCase();
            filterTasks(searchTerm);
        });
    }

    // Filter tasks based on search term
    function filterTasks(searchTerm) {
        const allCards = document.querySelectorAll('.task-card');
        const inboxItems = document.querySelectorAll('.task-card-item');
        
        allCards.forEach(card => {
            const title = card.querySelector('.task-card-title')?.textContent.toLowerCase() || '';
            const description = card.querySelector('.task-card-description')?.textContent.toLowerCase() || '';
            const shouldShow = title.includes(searchTerm) || description.includes(searchTerm);
            card.style.display = shouldShow ? 'block' : 'none';
        });

        inboxItems.forEach(item => {
            const title = item.querySelector('.task-title')?.textContent.toLowerCase() || '';
            const shouldShow = title.includes(searchTerm);
            item.style.display = shouldShow ? 'flex' : 'none';
        });
    }

    // Initialize inbox task checkboxes
    function initializeInboxCheckboxes() {
        const checkboxes = document.querySelectorAll('.task-card-item .card-checkbox');
        
        checkboxes.forEach(checkbox => {
            checkbox.addEventListener('change', function(e) {
                const taskId = this.getAttribute('data-task-id');
                const isCompleted = this.checked;
                const newStatus = isCompleted ? 'Completed' : 'Pending';
                
                // Update visual state
                const inboxItem = this.closest('.task-card-item');
                inboxItem.classList.toggle('completed', isCompleted);
                
                // Update task status
                updateTaskStatus(taskId, newStatus, 0);
                
                // Update corresponding card checkbox
                const cardCheckbox = document.querySelector(`#card-task-${taskId}`);
                if (cardCheckbox) {
                    cardCheckbox.checked = isCompleted;
                }
            });
        });
    }

    // Initialize card checkboxes
    function initializeCardCheckboxes() {
        const checkboxes = document.querySelectorAll('.task-card .card-checkbox');
        
        checkboxes.forEach(checkbox => {
            checkbox.addEventListener('change', function(e) {
                e.stopPropagation(); // Prevent opening modal
                const taskId = this.getAttribute('data-task-id');
                const isCompleted = this.checked;
                const newStatus = isCompleted ? 'Completed' : 'InProgress';
                
                // Update task status
                updateTaskStatus(taskId, newStatus, 0);
                
                // Update corresponding inbox checkbox
                const inboxCheckbox = document.querySelector(`#inbox-task-${taskId}`);
                if (inboxCheckbox) {
                    inboxCheckbox.checked = isCompleted;
                    const inboxItem = inboxCheckbox.closest('.task-card-item');
                    if (inboxItem) {
                        inboxItem.classList.toggle('completed', isCompleted);
                    }
                }
            });
        });
    }

    // Initialize Add Task button
    function initializeAddTaskButton() {
        const addTaskBtn = document.getElementById('addTaskBtn');
        if (!addTaskBtn) return;

        addTaskBtn.addEventListener('click', function() {
            openTaskFormModal();
        });
    }

    // Initialize column add buttons
    function initializeColumnAddButtons() {
        const addButtons = document.querySelectorAll('.column-add-btn');
        
        addButtons.forEach(button => {
            button.addEventListener('click', function() {
                const status = this.getAttribute('data-status');
                openTaskFormModal(null, status);
            });
        });
    }

    // Note: openTaskModal function is already defined in the Views and handles task loading correctly

    // Initialize task form submission (now handled by existing saveTask function)
    function initializeTaskForm() {
        // Form submission is now handled by the existing saveTask function in the Views
        // This function is kept for compatibility but does nothing
    }

    // Load task detail into modal
    window.loadTaskDetail = function(taskId) {
        currentTaskId = taskId;
        const content = document.getElementById('taskDetailContent');
        
        // Get task data from the card
        const card = document.querySelector(`[data-task-id="${taskId}"]`);
        if (!card) return;
        
        const title = card.querySelector('.task-card-title')?.textContent || 'Task';
        const description = card.querySelector('.task-card-description')?.textContent || 'No description';
        
        // Set modal title
        document.getElementById('taskDetailTitle').textContent = title;
        
        // Build detail view
        let html = `
            <div class="task-detail">
                <div class="mb-4">
                    <h6 class="text-muted mb-2">Description</h6>
                    <p>${description}</p>
                </div>
                
                <div class="mb-4">
                    <h6 class="text-muted mb-2">Checklist</h6>
                    <div class="alert alert-info mb-0">
                        <i class="bi bi-info-circle"></i> Subtasks feature coming soon
                    </div>
                </div>
                
                <div class="mb-4">
                    <h6 class="text-muted mb-2">Comments</h6>
                    <div id="taskComments">
                        <div class="alert alert-info mb-0">
                            <i class="bi bi-info-circle"></i> Comments feature coming soon
                        </div>
                    </div>
                </div>
                
                <div class="mb-4">
                    <h6 class="text-muted mb-2">Assigned Members</h6>
                    <div class="alert alert-info mb-0">
                        <i class="bi bi-info-circle"></i> Member assignments coming soon
                    </div>
                </div>
                
                <div class="mt-4">
                    <button class="btn btn-sm btn-primary" onclick="editTask(${taskId})">
                        <i class="bi bi-pencil"></i> Edit
                    </button>
                    <button class="btn btn-sm btn-danger" onclick="deleteTask(${taskId})">
                        <i class="bi bi-trash"></i> Delete
                    </button>
                </div>
            </div>
        `;
        
        content.innerHTML = html;
    };

    // Edit task
    window.editTask = function(taskId) {
        bootstrap.Modal.getInstance(document.getElementById('taskDetailModal')).hide();
        openTaskFormModal(taskId);
    };

    // Delete task
    window.deleteTask = function(taskId) {
        if (!confirm('Are you sure you want to delete this task?')) return;
        
        fetch(`/Tasks/Delete/${taskId}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiForgeryToken()
            }
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('Success', 'Task deleted successfully', 'success');
                bootstrap.Modal.getInstance(document.getElementById('taskDetailModal')).hide();
                // Remove task from DOM
                document.querySelector(`[data-task-id="${taskId}"]`)?.remove();
                updateColumnCounts();
            } else {
                showToast('Error', data.message || 'Failed to delete task', 'error');
            }
        })
        .catch(error => {
            console.error('Error deleting task:', error);
            showToast('Error', 'An error occurred while deleting the task', 'error');
        });
    };

    // Initialize calendar toggle
    function initializeCalendarToggle() {
        const toggleBtn = document.getElementById('toggleCalendarBtn');
        const calendarView = document.getElementById('calendarView');
        const kanbanBoard = document.querySelector('.kanban-board-container');
        
        if (!toggleBtn || !calendarView || !kanbanBoard) return;
        
        toggleBtn.addEventListener('click', function() {
            const isCalendarVisible = !calendarView.classList.contains('d-none');
            
            if (isCalendarVisible) {
                calendarView.classList.add('d-none');
                kanbanBoard.classList.remove('d-none');
                this.innerHTML = '<i class="fas fa-calendar"></i> Calendar';
            } else {
                calendarView.classList.remove('d-none');
                kanbanBoard.classList.add('d-none');
                this.innerHTML = '<i class="fa-solid fa-chart-simple" style="transform: rotate(180deg);"></i> Board';
            }
        });
    }

    // Update column badge counts
    function updateColumnCounts() {
        document.querySelectorAll('.kanban-column').forEach(column => {
            const status = column.getAttribute('data-status');
            const count = column.querySelectorAll('.task-card').length;
            const badge = column.querySelector('.badge');
            if (badge) {
                badge.textContent = count;
            }
        });
    }

    // Get anti-forgery token
    function getAntiForgeryToken() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    // Show toast notification
    function showToast(title, message, type = 'info') {
        // Simple console log for now - can be replaced with actual toast library
        console.log(`[${type.toUpperCase()}] ${title}: ${message}`);
        
        // If you have a toast library, implement it here
        // For now, use alert for errors
        if (type === 'error') {
            alert(`${title}: ${message}`);
        }
    }

    let autocomplete = null; // Deprecated widget; no longer used for new customers
    let currentLocationInput = null;
    let placesSessionToken = null;

    // Initialize Google Places Autocomplete
    function initializeLocationSearch() {
        console.log('Initializing location search...');
        const searchInput = document.getElementById('locationSearchInput');
        
        if (!searchInput) {
            console.error('Location search input not found');
            return;
        }
        
        if (!window.google || !google.maps.places) {
            console.warn('Google Maps API not loaded yet, retrying...');
            setTimeout(initializeLocationSearch, 1000);
            return;
        }

        console.log('Google Maps API is ready, setting up location search');
        currentLocationInput = searchInput;

        // Do NOT initialize google.maps.places.Autocomplete widget.
        // New customers cannot use it; rely on manual predictions + PlacesService instead.
        // Ensure input is enabled and placeholder is correct in case a previous attempt disabled it
        searchInput.disabled = false;
        searchInput.placeholder = 'Start typing a location...';

        // Handle typing and show custom dropdown
        let typingTimeout = null;
        searchInput.addEventListener('input', function() {
            clearTimeout(typingTimeout);
            const query = this.value.trim();
            console.log('Location search input:', query);
            
            if (query.length > 2) {
                if (!placesSessionToken || query.length === 3) {
                    placesSessionToken = new google.maps.places.AutocompleteSessionToken();
                }
                typingTimeout = setTimeout(() => {
                    console.log('Triggering autocomplete for:', query);
                    showManualAutocomplete(query);
                }, 300); // Delay to avoid too many requests
            } else {
                hideAutocompleteDropdown();
            }
        });

        // Handle keyboard navigation
        searchInput.addEventListener('keydown', handleKeyboardNavigation);

        // Click outside to close dropdown
        document.addEventListener('click', function(e) {
            if (!e.target.closest('.location-search')) {
                hideAutocompleteDropdown();
            }
        });

        // Initialize location section scroll button functionality
        initializeLocationScrollButton();
    }

    // Show manual autocomplete dropdown
    function showManualAutocomplete(query) {
        console.log('showManualAutocomplete called with query:', query);
        const dropdown = document.getElementById('autocompleteDropdown');
        const list = document.getElementById('autocompleteList');
        const loadingIndicator = document.getElementById('searchLoadingIndicator');
        
        console.log('Dropdown element:', dropdown);
        console.log('List element:', list);
        
        if (!dropdown || !list || !query) {
            console.warn('Missing autocomplete elements or empty query');
            return;
        }

        loadingIndicator.style.display = 'block';

        // Use Places API to get suggestions
        const service = new google.maps.places.AutocompleteService();
        console.log('AutocompleteService created, making request...');
        
        service.getPlacePredictions({
            input: query,
            types: ['establishment', 'geocode'],
            sessionToken: placesSessionToken
        }, function(predictions, status) {
            console.log('Places API response:', {predictions: predictions, status: status});
            console.log('Full status code:', status);
            loadingIndicator.style.display = 'none';
            
            if (status === google.maps.places.PlacesServiceStatus.OK && predictions) {
                console.log('Got predictions, populating dropdown...');
                populateAutocompleteDropdown(predictions);
                showAutocompleteDropdown();
            } else if (status === google.maps.places.PlacesServiceStatus.REQUEST_DENIED) {
                console.error('Places API request denied. Ensure Places API is enabled for your API key.');
                showToast('Error', 'Places API not enabled for your key. Enable "Places API" in Google Cloud.', 'error');
                hideAutocompleteDropdown();
            } else if (status === google.maps.places.PlacesServiceStatus.UNKNOWN_ERROR) {
                console.error('Unknown error from Places API. The Maps JavaScript API may not be enabled.');
                showToast('Error', 'Maps API error. Enable "Maps JavaScript API" in Google Cloud Console.', 'error');
                hideAutocompleteDropdown();
            } else {
                console.log('No predictions or error. Status:', status);
                hideAutocompleteDropdown();
            }
        });
    }

    // Populate autocomplete dropdown with predictions
    function populateAutocompleteDropdown(predictions) {
        console.log('populateAutocompleteDropdown called with predictions:', predictions);
        const list = document.getElementById('autocompleteList');
        console.log('Autocomplete list element:', list);
        if (!list) {
            console.error('Autocomplete list not found!');
            return;
        }

        list.innerHTML = '';
        console.log('Creating', predictions.length, 'prediction items');
        
        predictions.slice(0, 5).forEach((prediction, index) => {
            const item = document.createElement('li');
            item.className = 'autocomplete-item';
            item.dataset.placeId = prediction.place_id;
            item.dataset.prediction = prediction.description;
            
            // Extract primary and secondary text
            const description = prediction.description;
            const commaIndex = description.indexOf(', ');
            const primaryText = commaIndex > -1 ? description.substring(0, commaIndex) : description;
            const secondaryText = commaIndex > -1 ? description.substring(commaIndex + 2) : '';
            
            item.innerHTML = `
                <div class="autocomplete-icon">
                    <i class="fas fa-map-marker-alt"></i>
                </div>
                <div class="autocomplete-text">
                    <div class="autocomplete-primary-text">${primaryText}</div>
                    ${secondaryText ? `<div class="autocomplete-secondary-text">${secondaryText}</div>` : ''}
                </div>
            `;
            
            // Handle click selection
            item.addEventListener('click', function() {
                selectLocationFromDropdown(prediction);
            });
            
            // Handle keyboard navigation
            item.addEventListener('mouseenter', function() {
                clearActiveAutocompleteItem();
                this.classList.add('active');
            });
            
            list.appendChild(item);
            console.log('Added prediction item:', prediction.description);
        });
        console.log('Total items in list:', list.children.length);
    }

    // Handle autocomplete selection from dropdown
    function selectLocationFromDropdown(prediction) {
        const searchInput = document.getElementById('locationSearchInput');
        searchInput.value = prediction.description;
        
        // Get place details
        const service = new google.maps.places.PlacesService(document.createElement('div'));
        service.getDetails({
            placeId: prediction.place_id,
            fields: ['name', 'formatted_address', 'geometry'],
            sessionToken: placesSessionToken
        }, function(place, status) {
            hideAutocompleteDropdown();
            
            if (status === google.maps.places.PlacesServiceStatus.OK) {
                displayLocationFromPlace(place);
                placesSessionToken = null; // reset after successful selection
            } else {
                showToast('Error', 'Could not load location details', 'error');
            }
        });
    }

    // Handle keyboard navigation in autocomplete
    function handleKeyboardNavigation(e) {
        const dropdown = document.getElementById('autocompleteDropdown');
        const list = document.getElementById('autocompleteList');
        
        if (!dropdown || !list || dropdown.style.display === 'none') return;

        const items = list.querySelectorAll('.autocomplete-item');
        const activeItem = list.querySelector('.autocomplete-item.active');
        let nextActiveIndex = -1;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                nextActiveIndex = activeItem ? Array.from(items).indexOf(activeItem) + 1 : 0;
                break;
            case 'ArrowUp':
                e.preventDefault();
                nextActiveIndex = activeItem ? Array.from(items).indexOf(activeItem) - 1 : items.length - 1;
                break;
            case 'Enter':
                e.preventDefault();
                if (activeItem) {
                    activeItem.click();
                }
                return;
            case 'Escape':
                e.preventDefault();
                hideAutocompleteDropdown();
                return;
            default:
                return;
        }

        if (nextActiveIndex >= 0 && nextActiveIndex < items.length) {
            clearActiveAutocompleteItem();
            items[nextActiveIndex].classList.add('active');
        }
    }

    // Clear active autocomplete item
    function clearActiveAutocompleteItem() {
        const activeItem = document.querySelector('.autocomplete-item.active');
        if (activeItem) {
            activeItem.classList.remove('active');
        }
    }

    // Show autocomplete dropdown
    function showAutocompleteDropdown() {
        const dropdown = document.getElementById('autocompleteDropdown');
        console.log('showAutocompleteDropdown called, dropdown element:', dropdown);
        if (dropdown) {
            dropdown.style.display = 'block';
            console.log('Dropdown display set to block. Current style:', dropdown.style.display);
            console.log('Dropdown computed style:', window.getComputedStyle(dropdown).display);
        } else {
            console.error('Dropdown element not found!');
        }
    }

    // Hide autocomplete dropdown
    function hideAutocompleteDropdown() {
        const dropdown = document.getElementById('autocompleteDropdown');
        if (dropdown) {
            dropdown.style.display = 'none';
        }
        clearActiveAutocompleteItem();
    }

    // Initialize location scroll button
    function initializeLocationScrollButton() {
        const locationScrollBtn = document.querySelector('#scroll-location-btn');
        if (locationScrollBtn) {
            locationScrollBtn.addEventListener('click', function() {
                document.getElementById('location-section').scrollIntoView({
                    behavior: 'smooth'
                });
            });
        }
    }

    // Display location from Google Place object
    function displayLocationFromPlace(place) {
        const locationDisplay = document.getElementById('locationDisplay');
        const locationName = document.getElementById('locationName');
        const locationAddress = document.getElementById('locationAddress');

        // Update location info
        locationName.textContent = place.name || 'Selected Location';
        locationAddress.textContent = place.formatted_address;

        // Show location display
        locationDisplay.style.display = 'flex';

        // Store the full location data globally for saving
        window.currentLocationData = {
            name: place.name || 'Selected Location',
            address: place.formatted_address,
            formatted_address: place.formatted_address,
            place_id: place.place_id,
            geometry: {
                location: {
                    lat: place.geometry.location.lat(),
                    lng: place.geometry.location.lng()
                }
            }
        };

        // Load embedded map
        loadEmbeddedMap(place.geometry.location, place.name || 'Location', place.formatted_address);

        // Update task location field
        updateTaskLocationField(place.name + ', ' + place.formatted_address);
        
        showToast('Success', 'Location selected successfully', 'success');
    }


    // Load embedded Google Map
    function loadEmbeddedMap(location, name, address) {
        const mapContainer = document.getElementById('mapContainer');
        
        // Create map URL for embedded display
        const locationStr = location.lat() + ',' + location.lng();
        const encodedName = encodeURIComponent(name);
        const encodedAddress = encodeURIComponent(address);
        
        const mapUrl = `https://maps.google.com/maps?q=${encodedName}|${encodedAddress}&t=&z=15&ie=UTF8&iwloc=&output=embed`;
        
        // Create iframe for embedded map
        mapContainer.innerHTML = `
            <iframe 
                class="embedded-map loaded"
                src="${mapUrl}"
                frameborder="0"
                allowfullscreen>
            </iframe>
        `;
    }

    // Update task location field (if editing)
    function updateTaskLocationField(locationText) {
        const locationInput = document.querySelector('input[name="location"], input[id*="location"]');
        if (locationInput) {
            locationInput.value = locationText;
        }
    }

    // Initialize location section functionality
    function initializeLocationSection() {
        // Remove location button
        const removeLocationBtn = document.querySelector('.location-actions .btn.text-danger');
        if (removeLocationBtn) {
            removeLocationBtn.addEventListener('click', function() {
                removeLocation();
            });
        }

        // Get directions button
        const directionsBtn = document.querySelector('.location-actions .btn:first-child');
        if (directionsBtn) {
            directionsBtn.addEventListener('click', function() {
                const locationName = document.getElementById('locationName')?.textContent;
                const locationAddress = document.getElementById('locationAddress')?.textContent;
                
                if (locationName && locationAddress) {
                    const searchQuery = encodeURIComponent(`${locationName}, ${locationAddress}`);
                    window.open(`https://www.google.com/maps/dir/?api=1&destination=${searchQuery}`, '_blank');
                }
            });
        }

        // Initialize location action button in toolbar (avoid :contains selector)
        (function() {
            const candidates = document.querySelectorAll('.task-action-buttons button, .task-action-btn');
            for (const btn of candidates) {
                if (btn.textContent && btn.textContent.trim().toLowerCase().includes('location')) {
                    btn.addEventListener('click', function() {
                        const section = document.getElementById('location-section');
                        if (section) {
                            section.scrollIntoView({ behavior: 'smooth', block: 'start' });
                        }
                    });
                    break;
                }
            }
        })();
    }

    // Remove location
    function removeLocation() {
        if (!confirm('Are you sure you want to remove this location?')) return;

        const locationDisplay = document.getElementById('locationDisplay');
        const mapContainer = document.getElementById('mapContainer');
        
        // Hide location display
        locationDisplay.style.display = 'none';
        
        // Reset map container
        mapContainer.innerHTML = `
            <div class="map-placeholder">
                <i class="fas fa-map-marked-alt" style="font-size: 3rem; color: #d1d5db;"></i>
                <p class="text-muted mt-2">No location selected</p>
                <small class="text-muted">Search for a location to display it on the map</small>
            </div>
        `;
        
        // Clear location field if editing
        updateTaskLocationField('');
        
        showToast('Success', 'Location removed successfully', 'success');
    }

    // Sample function to simulate loading different attachment types
    window.simulateDocumentUpload = function(fileName, fileType, fileSize) {
        const attachmentsContainer = document.querySelector('.attachments-container');
        const attachmentItem = document.createElement('div');
        attachmentItem.className = 'attachment-item';
        
        // Determine icon based on file type
        let iconClass = 'fas fa-file';
        if (fileName.includes('.pdf')) iconClass = 'fas fa-file-pdf';
        else if (fileName.includes('.doc') || fileName.includes('.docx')) iconClass = 'fas fa-file-word';
        else if (fileName.includes('.xls') || fileName.includes('.xlsx')) iconClass = 'fas fa-file-excel';
        else if (fileName.includes('.ppt') || fileName.includes('.pptx')) iconClass = 'fas fa-file-powerpoint';
        else if (fileName.match(/\.(jpg|jpeg|png|gif)$/)) iconClass = 'fas fa-image';
        else if (fileName.match(/\.(zip|rar|7z)$/)) iconClass = 'fas fa-file-archive';
        
        attachmentItem.innerHTML = `
            <div class="attachment-icon">
                <i class="${iconClass}"></i>
            </div>
            <div class="attachment-info">
                <div class="attachment-name">${fileName}</div>
                <div class="attachment-meta">
                    <span class="attachment-size">${fileSize}</span>
                    <span class="attachment-date">Uploaded just now</span>
                </div>
            </div>
            <div class="attachment-actions">
                <button class="btn btn-sm btn-link p-0" title="Download">
                    <i class="fas fa-download"></i>
                </button>
                <button class="btn btn-sm btn-link p-0 text-danger" title="Remove">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `;
        
        // Insert before the upload button
        const uploadBtn = attachmentsContainer.querySelector('.attachment-upload-btn');
        attachmentsContainer.insertBefore(attachmentItem, uploadBtn);
        
        showToast('Success', 'File uploaded successfully', 'success');
    };

    // Initialize matter popup positioning to adjust with main content width changes
    function initializeMatterPopupPositioning() {
        const matterPopup = document.getElementById('matterPopup');
        if (!matterPopup) return;

        // Function to update matter popup position based on main content width
        function updateMatterPopupPosition() {
            if (!matterPopup || matterPopup.style.display === 'none') return;

            // Get the main content container
            const mainContent = document.querySelector('.main-content, .content-wrapper, main');
            const tasksPage = document.querySelector('.tasks-page');
            
            if (!mainContent && !tasksPage) return;

            // Use tasks page if available, otherwise fall back to main content
            const contentElement = tasksPage || mainContent;
            const contentRect = contentElement.getBoundingClientRect();
            const viewportWidth = window.innerWidth;
            
            // Calculate the center of the visible content area
            const contentCenter = contentRect.left + (contentRect.width / 2);
            
            // Ensure the popup stays within viewport bounds
            const popupWidth = 360; // Fixed width from CSS
            const minLeft = popupWidth / 2;
            const maxLeft = viewportWidth - (popupWidth / 2);
            
            let targetLeft = Math.max(minLeft, Math.min(maxLeft, contentCenter));
            
            // Check if this is a top-positioned popup (from top matter selector)
            const isTopPositioned = matterPopup.classList.contains('top-positioned');
            
            if (isTopPositioned) {
                // For top popup, position it relative to the top matter selector button
                const topMatterSelectorBtn = document.getElementById('topMatterSelectorBtn');
                if (topMatterSelectorBtn) {
                    const buttonRect = topMatterSelectorBtn.getBoundingClientRect();
                    const buttonCenter = buttonRect.left + (buttonRect.width / 2);
                    targetLeft = Math.max(minLeft, Math.min(maxLeft, buttonCenter));
                    console.log('Top popup positioning:', {
                        buttonRect: buttonRect,
                        buttonCenter: buttonCenter,
                        targetLeft: targetLeft
                    });
                } else {
                    console.warn('Top matter selector button not found, using content center');
                }
            }
            
            // Apply the positioning
            matterPopup.style.left = targetLeft + 'px';
            matterPopup.style.transform = 'translateX(-50%)';
            
            console.log('Matter popup repositioned:', {
                isTopPositioned: isTopPositioned,
                contentCenter: contentCenter,
                targetLeft: targetLeft,
                contentWidth: contentRect.width,
                viewportWidth: viewportWidth
            });
        }

        // Set up ResizeObserver to watch for main content width changes
        if (window.ResizeObserver) {
            const resizeObserver = new ResizeObserver(function(entries) {
                // Debounce the position update to avoid excessive calls
                clearTimeout(window.matterPopupPositionTimeout);
                window.matterPopupPositionTimeout = setTimeout(updateMatterPopupPosition, 100);
            });

            // Observe the main content container
            const mainContent = document.querySelector('.main-content, .content-wrapper, main');
            const tasksPage = document.querySelector('.tasks-page');
            
            if (mainContent) {
                resizeObserver.observe(mainContent);
            }
            if (tasksPage) {
                resizeObserver.observe(tasksPage);
            }

            // Also observe the body for global layout changes
            resizeObserver.observe(document.body);
        }

        // Listen for custom popup shown events
        document.addEventListener('matterPopupShown', function(event) {
            const source = event.detail?.source || 'bottom';
            console.log('Matter popup shown from:', source);
            // Small delay to ensure the popup is rendered before positioning
            setTimeout(updateMatterPopupPosition, 10);
        });

        // Also listen for class changes on the matter popup
        if (window.MutationObserver) {
            const mutationObserver = new MutationObserver(function(mutations) {
                mutations.forEach(function(mutation) {
                    if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                        const target = mutation.target;
                        if (target === matterPopup && target.style.display !== 'none') {
                            console.log('Matter popup class changed, repositioning...');
                            setTimeout(updateMatterPopupPosition, 10);
                        }
                    }
                });
            });
            
            mutationObserver.observe(matterPopup, {
                attributes: true,
                attributeFilter: ['class']
            });
        }

        // Update position on window resize
        window.addEventListener('resize', function() {
            clearTimeout(window.matterPopupResizeTimeout);
            window.matterPopupResizeTimeout = setTimeout(updateMatterPopupPosition, 100);
        });

        // Update position when sidebar states change (if there are sidebar toggle events)
        document.addEventListener('sidebarToggle', updateMatterPopupPosition);
        document.addEventListener('chatToggle', updateMatterPopupPosition);
    }

    // Expose functions globally for Google Maps callback
    window.initializeLocationSearch = initializeLocationSearch;
    window.displayLocationFromPlace = displayLocationFromPlace;
    
    // Expose initialization functions globally
    window.initializeInboxCheckboxes = initializeInboxCheckboxes;
    window.initializeCardCheckboxes = initializeCardCheckboxes;
    window.initializeDragAndDrop = initializeDragAndDrop;

    // View switching functionality for bottom navbar (reusable for both Tasks page and Matter Tasks tab)
    function initializeBottomNavbar() {
        console.log('*** initializeBottomNavbar() called from tasks.js ***');
        
        const navItems = document.querySelectorAll('.bottom-navbar .nav-item');
        const viewContainers = document.querySelectorAll('.view-container');
        const tasksContent = document.querySelector('.tasks-content, .matter-tasks-content');
        
        console.log('Bottom navbar elements check:', {
            navItems: navItems.length,
            viewContainers: viewContainers.length,
            tasksContent: !!tasksContent
        });
        
        if (!navItems.length || !viewContainers.length || !tasksContent) {
            console.warn('✗ Bottom navbar elements not found, skipping initialization');
            console.warn('Missing:', {
                navItems: !navItems.length,
                viewContainers: !viewContainers.length,
                tasksContent: !tasksContent
            });
            return;
        }
        
        console.log('✓ All required elements found, setting up view switching...');
        
        // Track active views
        let activeViews = new Set(['inbox']); // Default to inbox only
        
        // Initialize the layout
        updateViewLayout();

        navItems.forEach((item, index) => {
            console.log(`Adding click listener to nav item ${index}:`, item.getAttribute('data-view'));
            item.addEventListener('click', function() {
                const view = this.getAttribute('data-view');
                console.log('*** Nav item clicked:', view);
                
                // Skip switch matters
                if (view === 'switch') {
                    console.log('Skipping switch matters button');
                    return;
                }
                
                // Toggle the view
                if (activeViews.has(view)) {
                    // If it's the only active view, don't remove it
                    if (activeViews.size === 1) {
                        console.log('Cannot remove last active view');
                        return;
                    }
                    console.log('Removing view:', view);
                    activeViews.delete(view);
                } else {
                    console.log('Adding view:', view);
                    activeViews.add(view);
                }
                
                updateViewLayout();
            });
        });
        
        console.log('✓ Bottom navbar initialization complete, active views:', Array.from(activeViews));

        // Update the view layout based on active views
        function updateViewLayout() {
            // Convert Set to Array for easier manipulation
            const activeViewsArray = Array.from(activeViews);
            const viewCount = activeViewsArray.length;
            
            console.log('Updating view layout. Active views:', activeViewsArray);
            
            // Hide all views and resizers first
            viewContainers.forEach(container => {
                container.classList.add('d-none');
                container.style.width = '';
            });
            
            const resizers = document.querySelectorAll('.view-resizer');
            resizers.forEach(resizer => {
                resizer.classList.add('d-none');
            });
            
            // Update nav items active state
            navItems.forEach(item => {
                const view = item.getAttribute('data-view');
                if (view === 'switch') return; // Skip switch matters
                
                if (activeViews.has(view)) {
                    item.classList.add('active');
                } else {
                    item.classList.remove('active');
                }
            });
            
            // Show active views and configure layout
            if (viewCount === 1) {
                // Single view mode
                tasksContent.classList.remove('multi-view', 'all-views');
                const viewId = activeViewsArray[0] + 'View';
                const viewElement = document.getElementById(viewId);
                if (viewElement) {
                    viewElement.classList.remove('d-none');
                    viewElement.style.width = '100%';
                }
            } else if (viewCount === 2) {
                // Two view mode
                tasksContent.classList.add('multi-view');
                tasksContent.classList.remove('all-views');
                
                const view1 = activeViewsArray[0] + 'View';
                const view2 = activeViewsArray[1] + 'View';
                const view1Element = document.getElementById(view1);
                const view2Element = document.getElementById(view2);
                
                if (view1Element && view2Element) {
                    view1Element.classList.remove('d-none');
                    view2Element.classList.remove('d-none');
                    view1Element.style.width = '50%';
                    view2Element.style.width = '50%';
                    
                    // Show resizer between the two views
                    const resizerId = view1.replace('View', '') + view2.replace('View', '').charAt(0).toUpperCase() + view2.replace('View', '').slice(1) + 'Resizer';
                    const resizerElement = document.getElementById(resizerId);
                    if (resizerElement) {
                        resizerElement.classList.remove('d-none');
                    }
                }
            } else if (viewCount === 3) {
                // Three view mode
                tasksContent.classList.add('multi-view', 'all-views');
                
                const view1Element = document.getElementById('inboxView');
                const view2Element = document.getElementById('plannerView');
                const view3Element = document.getElementById('boardView');
                
                if (view1Element && view2Element && view3Element) {
                    view1Element.classList.remove('d-none');
                    view2Element.classList.remove('d-none');
                    view3Element.classList.remove('d-none');
                    view1Element.style.width = '33.33%';
                    view2Element.style.width = '33.33%';
                    view3Element.style.width = '33.33%';
                    
                    // Show both resizers
                    const resizer1 = document.getElementById('inboxPlannerResizer');
                    const resizer2 = document.getElementById('plannerBoardResizer');
                    if (resizer1) resizer1.classList.remove('d-none');
                    if (resizer2) resizer2.classList.remove('d-none');
                }
            }
            
            // Reinitialize drag and drop if board view is visible
            if (activeViews.has('board') && typeof reinitializeDragAndDropIfNeeded === 'function') {
                reinitializeDragAndDropIfNeeded();
            }
            
            // Update inbox title widths if needed
            if (activeViews.has('inbox') && typeof updateInboxTitleWidths === 'function') {
                setTimeout(() => {
                    updateInboxTitleWidths();
                }, 100);
            }
        }
    }

    // Expose bottom navbar initialization globally
    console.log('*** Exposing initializeBottomNavbar globally from tasks.js ***');
    window.initializeBottomNavbar = initializeBottomNavbar;
    console.log('✓ window.initializeBottomNavbar =', typeof window.initializeBottomNavbar);

})();

console.log('*** tasks.js loaded and initialized ***');

