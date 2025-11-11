// Matter Creation Form JavaScript
document.addEventListener('DOMContentLoaded', function() {
    const form = document.querySelector('.needs-validation');
    const progressSteps = document.querySelectorAll('.progress-step');
    const progressLabels = document.querySelectorAll('.progress-step-label');
    
    // Form validation
    if (form) {
        form.addEventListener('submit', function(event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        });
    }
    
    // Real-time validation for required fields
    const requiredFields = form.querySelectorAll('[required]');
    requiredFields.forEach(field => {
        field.addEventListener('blur', function() {
            validateField(this);
        });
        
        field.addEventListener('input', function() {
            if (this.classList.contains('is-invalid')) {
                validateField(this);
            }
        });
    });
    
    function validateField(field) {
        const isValid = field.checkValidity();
        if (isValid) {
            field.classList.remove('is-invalid');
            field.classList.add('is-valid');
        } else {
            field.classList.remove('is-valid');
            field.classList.add('is-invalid');
        }
    }
    
    // Date validation
    const startDateField = document.querySelector('input[name="StartDate"]');
    const dueDateField = document.querySelector('input[name="DueDate"]');
    
    if (startDateField && dueDateField) {
        startDateField.addEventListener('change', function() {
            if (this.value && dueDateField.value) {
                if (new Date(this.value) > new Date(dueDateField.value)) {
                    dueDateField.setCustomValidity('Due date must be after start date');
                } else {
                    dueDateField.setCustomValidity('');
                }
            }
        });
        
        dueDateField.addEventListener('change', function() {
            if (this.value && startDateField.value) {
                if (new Date(this.value) < new Date(startDateField.value)) {
                    this.setCustomValidity('Due date must be after start date');
                } else {
                    this.setCustomValidity('');
                }
            }
        });
    }
    
    // Auto-save form data to localStorage
    const formData = {};
    const inputs = form.querySelectorAll('input, select, textarea');
    
    // Load saved data
    const savedData = localStorage.getItem('matterFormData');
    if (savedData) {
        try {
            const parsedData = JSON.parse(savedData);
            Object.keys(parsedData).forEach(key => {
                const field = form.querySelector(`[name="${key}"]`);
                if (field && field.type !== 'hidden') {
                    field.value = parsedData[key];
                }
            });
        } catch (e) {
            console.warn('Could not load saved form data');
        }
    }
    
    // Save data on input
    inputs.forEach(input => {
        if (input.type !== 'hidden') {
            input.addEventListener('input', function() {
                formData[this.name] = this.value;
                localStorage.setItem('matterFormData', JSON.stringify(formData));
            });
        }
    });
    
    // Clear saved data on successful submission
    form.addEventListener('submit', function(event) {
        const action = event.submitter?.value;
        if (action === 'next' && form.checkValidity()) {
            // Don't clear data yet, user might go back
        } else if (action === 'create') {
            // Clear saved data on final submission
            localStorage.removeItem('matterFormData');
        }
    });
    
    // Enhanced progress indicator animation
    function updateProgressIndicator(currentStep) {
        progressSteps.forEach((step, index) => {
            if (index + 1 <= currentStep) {
                step.classList.add('active');
                progressLabels[index].classList.add('active');
            } else {
                step.classList.remove('active');
                progressLabels[index].classList.remove('active');
            }
        });
    }
    
    // Initialize progress indicator
    const currentStep = parseInt(document.querySelector('input[name="Step"]').value);
    updateProgressIndicator(currentStep);
    
    // Smooth scroll to form on page load
    const formElement = document.querySelector('.card');
    if (formElement) {
        formElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    
    // Add loading state to buttons
    const submitButtons = form.querySelectorAll('button[type="submit"]');
    submitButtons.forEach(button => {
        button.addEventListener('click', function() {
            if (form.checkValidity()) {
                this.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Processing...';
                this.disabled = true;
            }
        });
    });
    
    // Character counter for description field
    const descriptionField = document.querySelector('textarea[name="Description"]');
    if (descriptionField) {
        const maxLength = 1000;
        const counter = document.createElement('div');
        counter.className = 'form-text text-muted text-end';
        counter.innerHTML = `<span class="char-count">0</span>/${maxLength} characters`;
        descriptionField.parentNode.appendChild(counter);
        
        const charCount = counter.querySelector('.char-count');
        
        descriptionField.addEventListener('input', function() {
            const length = this.value.length;
            charCount.textContent = length;
            
            if (length > maxLength * 0.9) {
                counter.classList.add('text-warning');
            } else {
                counter.classList.remove('text-warning');
            }
            
            if (length > maxLength) {
                counter.classList.add('text-danger');
                counter.classList.remove('text-warning');
            } else {
                counter.classList.remove('text-danger');
            }
        });
        
        // Initialize counter
        charCount.textContent = descriptionField.value.length;
    }
    
    // Matter preview updates
    function updateMatterPreview() {
        const title = document.querySelector('input[name="Title"]')?.value || 'Matter Title';
        const description = document.querySelector('textarea[name="Description"]')?.value || 'Matter description will appear here...';
        const category = document.querySelector('select[name="Category"]')?.value || 'Category';
        const status = document.querySelector('select[name="Status"]')?.value || 'Status';
        const priority = document.querySelector('select[name="Priority"]')?.value || 'Priority';
        
        // Update preview elements if they exist
        const previewTitle = document.querySelector('.matter-preview .h-4');
        const previewDescription = document.querySelector('.matter-preview .h-3');
        const previewCategory = document.querySelector('.matter-preview .bg-accent');
        const previewStatus = document.querySelector('.matter-preview .bg-secondary');
        
        if (previewTitle) {
            previewTitle.textContent = title;
        }
        if (previewDescription) {
            previewDescription.textContent = description.length > 50 ? description.substring(0, 50) + '...' : description;
        }
        if (previewCategory) {
            previewCategory.textContent = category;
        }
        if (previewStatus) {
            previewStatus.textContent = status;
        }
    }
    
    // Update preview on input changes
    inputs.forEach(input => {
        if (['Title', 'Description', 'Category', 'Status', 'Priority'].includes(input.name)) {
            input.addEventListener('input', updateMatterPreview);
        }
    });
    
    // Initialize preview
    updateMatterPreview();
});
