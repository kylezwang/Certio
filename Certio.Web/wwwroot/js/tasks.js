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
        const columns = document.querySelectorAll('.column-cards');
        
        columns.forEach(column => {
            const sortable = new Sortable(column, {
                group: 'kanban',
                animation: 150,
                ghostClass: 'task-card-ghost',
                dragClass: 'task-card-drag',
                handle: '.task-card',
                onEnd: function(evt) {
                    handleTaskMove(evt);
                }
            });
            sortableInstances.push(sortable);
        });
    }

    // Handle task moved between columns or reordered
    function handleTaskMove(evt) {
        const taskId = evt.item.getAttribute('data-task-id');
        const newColumn = evt.to.id;
        const newOrder = evt.newIndex;
        
        // Determine new status based on column
        let newStatus = 'Pending';
        if (newColumn === 'column-inprogress') newStatus = 'InProgress';
        else if (newColumn === 'column-review') newStatus = 'Review';
        else if (newColumn === 'column-completed') newStatus = 'Completed';
        
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
                // Update inbox checkbox if exists
                const inboxCheckbox = document.querySelector(`#inbox-task-${taskId}`);
                if (inboxCheckbox) {
                    inboxCheckbox.checked = (status === 'Completed');
                    const inboxItem = inboxCheckbox.closest('.inbox-task-item');
                    if (inboxItem) {
                        inboxItem.classList.toggle('completed', status === 'Completed');
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

    // Initialize search functionality
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
        const inboxItems = document.querySelectorAll('.inbox-task-item');
        
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
        const checkboxes = document.querySelectorAll('.inbox-task-item .task-checkbox');
        
        checkboxes.forEach(checkbox => {
            checkbox.addEventListener('change', function(e) {
                const taskId = this.getAttribute('data-task-id');
                const isCompleted = this.checked;
                const newStatus = isCompleted ? 'Completed' : 'Pending';
                
                // Update visual state
                const inboxItem = this.closest('.inbox-task-item');
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
                    const inboxItem = inboxCheckbox.closest('.inbox-task-item');
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

    // Open task form modal
    function openTaskFormModal(taskId = null, defaultStatus = 'Pending') {
        const modal = new bootstrap.Modal(document.getElementById('taskFormModal'));
        const form = document.getElementById('taskForm');
        const title = document.getElementById('taskFormModalTitle');
        
        // Reset form
        form.reset();
        
        if (taskId) {
            title.textContent = 'Edit Task';
            document.getElementById('taskId').value = taskId;
            // Load task data here if editing
        } else {
            title.textContent = 'Add Task';
            document.getElementById('taskId').value = '';
            document.getElementById('taskStatus').value = defaultStatus;
        }
        
        modal.show();
    }

    // Initialize task form submission
    function initializeTaskForm() {
        const form = document.getElementById('taskForm');
        if (!form) return;

        form.addEventListener('submit', function(e) {
            e.preventDefault();
            
            const taskId = document.getElementById('taskId').value;
            const formData = {
                matterId: parseInt(document.getElementById('taskMatter').value),
                title: document.getElementById('taskTitle').value,
                description: document.getElementById('taskDescription').value,
                priority: document.getElementById('taskPriority').value,
                status: document.getElementById('taskStatus').value,
                dueDate: document.getElementById('taskDueDate').value || null,
                order: 0
            };

            const url = taskId ? '/Tasks/Update' : '/Tasks/Create';
            if (taskId) {
                formData.id = parseInt(taskId);
            }

            fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(formData)
            })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    showToast('Success', 'Task saved successfully', 'success');
                    bootstrap.Modal.getInstance(document.getElementById('taskFormModal')).hide();
                    // Reload page to show new task
                    setTimeout(() => window.location.reload(), 500);
                } else {
                    showToast('Error', data.message || 'Failed to save task', 'error');
                }
            })
            .catch(error => {
                console.error('Error saving task:', error);
                showToast('Error', 'An error occurred while saving the task', 'error');
            });
        });
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

    // Expose functions globally for Google Maps callback
    window.initializeLocationSearch = initializeLocationSearch;
    window.displayLocationFromPlace = displayLocationFromPlace;

})();

