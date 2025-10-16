// Matter Details Tab Management
(function() {
    'use strict';

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
        }

        // Add active class to selected tab link
        const targetLink = document.querySelector(`.matter-tab-link[data-tab="${tabName}"]`);
        if (targetLink) {
            targetLink.classList.add('active');
        }

        // Scroll to top of content
        window.scrollTo({ top: 0, behavior: 'smooth' });
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

