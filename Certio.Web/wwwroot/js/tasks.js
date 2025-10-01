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
                this.innerHTML = '<i class="bi bi-calendar3"></i> Calendar';
            } else {
                calendarView.classList.remove('d-none');
                kanbanBoard.classList.add('d-none');
                this.innerHTML = '<i class="bi bi-kanban"></i> Board';
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

})();

