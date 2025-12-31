// Change Control (Change Notices) - AJAX create/send/refresh with Tasks-style modal
(function () {
    'use strict';

    // Modal state
    let currentNoticeId = null;
    let currentNotice = null;
    let recipientEmails = [];
    let availableContacts = [];

    function adjustChangeNoticeModalPosition() {
        const modal = document.getElementById('ccModal');
        if (!modal || modal.style.display === 'none') return;

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

    // Hook into the global adjuster
    (function registerModalAdjuster() {
        const existing = window.adjustTaskDetailsModalPosition;
        if (typeof existing === 'function' && existing.__ccIncludesModal) {
            return;
        }

        window.adjustTaskDetailsModalPosition = function () {
            if (typeof existing === 'function') {
                existing();
            }
            adjustChangeNoticeModalPosition();
        };
        window.adjustTaskDetailsModalPosition.__ccIncludesModal = true;

        window.addEventListener('resize', function () {
            if (typeof window.adjustTaskDetailsModalPosition === 'function') {
                window.adjustTaskDetailsModalPosition();
            }
        });
    })();

    function getAntiForgeryToken() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, options);
        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `HTTP ${response.status}`);
        }
        return await response.json();
    }

    function escapeHtml(str) {
        return String(str)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function getInitials(email) {
        const parts = email.split('@')[0].split(/[._-]/);
        if (parts.length >= 2) {
            return (parts[0][0] + parts[1][0]).toUpperCase();
        }
        return email.substring(0, 2).toUpperCase();
    }

    // Load available contacts for the matter
    async function loadContacts(orgId, matterId) {
        try {
            const response = await fetch(`/Client/${orgId}/Matter/${matterId}/Users`, {
                credentials: 'include'
            });
            if (response.ok) {
                const result = await response.json();
                if (result.success && result.users) {
                    availableContacts = result.users.map(u => ({
                        id: u.id,
                        name: u.name || `${u.firstName || ''} ${u.lastName || ''}`.trim(),
                        initials: u.initials || `${(u.firstName || '')[0] || ''}${(u.lastName || '')[0] || ''}`.toUpperCase(),
                        email: u.email
                    }));
                }
            }
        } catch (error) {
            console.error('Error loading contacts:', error);
        }
        return availableContacts;
    }

    // Render inline recipient avatars
    function renderRecipientAvatars() {
        const container = document.getElementById('ccRecipientsContainer');
        if (!container) return;

        const maxShow = 5;
        const shown = recipientEmails.slice(0, maxShow);
        const remaining = recipientEmails.length - maxShow;

        container.innerHTML = shown.map(email => `
            <div class="member-avatar cc-recipient-avatar" title="${escapeHtml(email)}" data-email="${escapeHtml(email)}">
                ${getInitials(email)}
            </div>
        `).join('');

        if (remaining > 0) {
            container.innerHTML += `<div class="member-avatar cc-recipient-avatar cc-recipient-more" title="${remaining} more">+${remaining}</div>`;
        }

        if (recipientEmails.length === 0) {
            container.innerHTML = '<span class="text-muted" style="font-size: 0.875rem;">No recipients yet</span>';
        }
    }

    // Render recipients list in section
    function renderRecipientsList() {
        const list = document.getElementById('ccRecipientsList');
        if (!list) return;

        if (recipientEmails.length === 0) {
            list.innerHTML = '<p class="text-muted small mb-0">No recipients added yet. Click "Add" or type an email below.</p>';
            return;
        }

        // Get recipient statuses from current notice if editing
        const recipientStatuses = {};
        if (currentNotice && currentNotice.recipients) {
            currentNotice.recipients.forEach(r => {
                recipientStatuses[r.email.toLowerCase()] = r.status;
            });
        }

        list.innerHTML = recipientEmails.map(email => {
            const status = recipientStatuses[email.toLowerCase()] || 'Pending';
            const statusClass = status === 'Acknowledged' ? 'bg-success text-white' :
                               status === 'NeedsClarification' ? 'bg-warning text-dark' :
                               'bg-secondary text-white';
            const statusLabel = status === 'Acknowledged' ? 'Confirmed' :
                               status === 'NeedsClarification' ? 'Response Required' :
                               'Pending';
            return `
                <div class="cc-recipient-item" data-email="${escapeHtml(email)}">
                    <div class="recipient-info">
                        <div class="member-avatar" style="width: 24px; height: 24px; font-size: 0.6rem;">${getInitials(email)}</div>
                        <span class="recipient-email">${escapeHtml(email)}</span>
                    </div>
                    <div class="d-flex align-items-center gap-2">
                        ${currentNotice && currentNotice.status !== 'Draft' ? `<span class="recipient-status ${statusClass}">${statusLabel}</span>` : ''}
                        <i class="fas fa-times recipient-remove" data-email="${escapeHtml(email)}"></i>
                    </div>
                </div>
            `;
        }).join('');

        // Add remove handlers
        list.querySelectorAll('.recipient-remove').forEach(btn => {
            btn.addEventListener('click', function() {
                const email = this.dataset.email;
                recipientEmails = recipientEmails.filter(e => e !== email);
                renderRecipientAvatars();
                renderRecipientsList();
                updateHiddenRecipients();
            });
        });
    }

    function updateHiddenRecipients() {
        const hidden = document.getElementById('ccRecipientEmailsHidden');
        if (hidden) {
            hidden.value = recipientEmails.join(',');
        }
    }

    // Show contact selector popup
    function showContactSelector(anchorElement) {
        const popup = document.getElementById('ccContactSelectorPopup');
        if (!popup) return;

        const rect = anchorElement.getBoundingClientRect();
        popup.style.top = `${rect.bottom + 5}px`;
        popup.style.left = `${rect.left}px`;

        // Adjust if popup would go off-screen
        const popupWidth = 380;
        if (rect.left + popupWidth > window.innerWidth) {
            popup.style.left = `${window.innerWidth - popupWidth - 10}px`;
        }

        popup.innerHTML = `
            <div class="mb-2">
                <div class="d-flex align-items-center justify-content-between mb-2">
                    <div class="d-flex align-items-center">
                        <i class="fas fa-users me-2" style="color: #3d1019; font-size: 1rem;"></i>
                        <div>
                            <h6 class="fw-bold text-dark mb-0" style="font-size: 0.9rem;">Add Recipients</h6>
                            <p class="text-muted mb-0" style="font-size: 0.75rem;">Select contacts or enter email addresses.</p>
                        </div>
                    </div>
                </div>
            </div>
            
            <div class="mb-2">
                <label class="form-label fw-bold text-dark mb-1" style="font-size: 0.85rem;">Search Contacts</label>
                <div class="input-group input-group-sm">
                    <input type="text" class="form-control" id="ccContactSearch" placeholder="Search by name or email..." style="font-size: 0.875rem;">
                    <button class="btn btn-outline-secondary" type="button">
                        <i class="fas fa-search"></i>
                    </button>
                </div>
            </div>
            
            <div class="mb-2">
                <label class="form-label fw-bold text-dark mb-1" style="font-size: 0.85rem;">Available Contacts</label>
                <div id="ccContactList" style="max-height: 200px; overflow-y: auto;">
                    <!-- Contacts will be loaded here -->
                </div>
            </div>
            
            <div class="mb-2">
                <label class="form-label fw-bold text-dark mb-1" style="font-size: 0.85rem;">Or enter email directly</label>
                <div class="input-group input-group-sm">
                    <input type="email" class="form-control" id="ccDirectEmail" placeholder="email@example.com" style="font-size: 0.875rem;">
                    <button class="btn btn-primary" type="button" id="ccAddDirectEmail">
                        <i class="fas fa-plus"></i>
                    </button>
                </div>
            </div>
            
            <div class="d-flex justify-content-end mt-3">
                <button class="btn btn-secondary btn-sm" id="ccContactSelectorClose" style="border-radius: 0.5rem;">Done</button>
            </div>
        `;

        popup.style.display = 'block';

        // Render contacts
        const contactList = popup.querySelector('#ccContactList');
        renderContactList(contactList, '');

        // Search functionality
        const searchInput = popup.querySelector('#ccContactSearch');
        searchInput.addEventListener('input', function() {
            renderContactList(contactList, this.value);
        });

        // Add direct email
        const addDirectBtn = popup.querySelector('#ccAddDirectEmail');
        const directEmailInput = popup.querySelector('#ccDirectEmail');
        addDirectBtn.addEventListener('click', function() {
            const email = directEmailInput.value.trim();
            if (email && isValidEmail(email) && !recipientEmails.includes(email)) {
                recipientEmails.push(email);
                directEmailInput.value = '';
                renderRecipientAvatars();
                renderRecipientsList();
                updateHiddenRecipients();
                renderContactList(contactList, searchInput.value);
            }
        });
        directEmailInput.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                addDirectBtn.click();
            }
        });

        // Close button
        popup.querySelector('#ccContactSelectorClose').addEventListener('click', function() {
            popup.style.display = 'none';
        });

        // Focus search
        searchInput.focus();
    }

    function renderContactList(container, filterText) {
        const filtered = availableContacts.filter(c => 
            !filterText || 
            c.name.toLowerCase().includes(filterText.toLowerCase()) || 
            c.email.toLowerCase().includes(filterText.toLowerCase())
        );

        if (filtered.length === 0) {
            container.innerHTML = '<p class="text-muted small mb-0 p-2">No contacts found.</p>';
            return;
        }

        container.innerHTML = filtered.map(contact => {
            const isSelected = recipientEmails.includes(contact.email);
            return `
                <div class="contact-item ${isSelected ? 'selected' : ''}" data-email="${escapeHtml(contact.email)}">
                    <div class="contact-avatar">${contact.initials}</div>
                    <div class="contact-info">
                        <div class="contact-name">${escapeHtml(contact.name)}</div>
                        <div class="contact-email">${escapeHtml(contact.email)}</div>
                    </div>
                    ${isSelected ? '<i class="fas fa-check text-success"></i>' : ''}
                </div>
            `;
        }).join('');

        // Add click handlers
        container.querySelectorAll('.contact-item').forEach(item => {
            item.addEventListener('click', function() {
                const email = this.dataset.email;
                if (recipientEmails.includes(email)) {
                    recipientEmails = recipientEmails.filter(e => e !== email);
                } else {
                    recipientEmails.push(email);
                }
                renderRecipientAvatars();
                renderRecipientsList();
                updateHiddenRecipients();
                renderContactList(container, document.getElementById('ccContactSearch')?.value || '');
            });
        });
    }

    function isValidEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }

    // Open modal for new draft
    function openNewDraftModal() {
        currentNoticeId = null;
        currentNotice = null;
        recipientEmails = [];

        const modal = document.getElementById('ccModal');
        if (!modal) return;

        // Reset form
        document.getElementById('ccNoticeId').value = '';
        document.getElementById('ccTitleInput').value = '';
        document.getElementById('ccDescription').value = '';
        document.getElementById('ccChangeType').value = 'General';
        document.getElementById('ccPriority').value = 'Medium';
        document.getElementById('ccDueDate').value = '';

        // Update UI for new draft mode
        document.getElementById('ccStatusDropdown').value = 'Draft';
        document.getElementById('ccStatusDropdown').disabled = true;
        document.getElementById('ccHeaderProgress').style.display = 'none';
        document.getElementById('ccDeleteBtn').style.display = 'none';
        document.getElementById('ccSaveBtn').style.display = 'inline-flex';
        document.getElementById('ccSaveBtnText').textContent = 'Create Draft';
        document.getElementById('ccSendBtn').style.display = 'none';
        document.getElementById('ccModalMode').textContent = 'Creating new change notice';

        renderRecipientAvatars();
        renderRecipientsList();

        modal.style.display = 'flex';
        adjustChangeNoticeModalPosition();
        document.getElementById('ccTitleInput').focus();
    }

    // Open modal for existing notice
    async function openNoticeModal(orgId, matterId, noticeId) {
        try {
            // Fetch notice details
            const notice = await fetchJson(`/Client/${orgId}/Matter/${matterId}/ChangeNotices/${noticeId}`, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            currentNoticeId = noticeId;
            currentNotice = notice;
            recipientEmails = notice.recipients?.map(r => r.email) || [];

            const modal = document.getElementById('ccModal');
            if (!modal) return;

            // Populate form
            document.getElementById('ccNoticeId').value = notice.id;
            document.getElementById('ccTitleInput').value = notice.title || '';
            document.getElementById('ccDescription').value = notice.description || '';
            document.getElementById('ccChangeType').value = notice.changeType || 'General';
            document.getElementById('ccPriority').value = notice.priority || 'Medium';
            document.getElementById('ccDueDate').value = notice.acknowledgementDueDate ? notice.acknowledgementDueDate.split('T')[0] : '';

            // Update status dropdown
            document.getElementById('ccStatusDropdown').value = notice.status;
            document.getElementById('ccStatusDropdown').disabled = true;

            // Show progress for sent notices
            const isDraft = notice.status === 'Draft';
            const progressContainer = document.getElementById('ccHeaderProgress');
            if (!isDraft && notice.recipientCount > 0) {
                const percent = Math.round((notice.acknowledgedRecipientCount / notice.recipientCount) * 100);
                document.getElementById('ccProgressBar').style.width = `${percent}%`;
                document.getElementById('ccProgressText').textContent = `${percent}%`;
                progressContainer.style.display = 'flex';
            } else {
                progressContainer.style.display = 'none';
            }

            // Update buttons
            document.getElementById('ccDeleteBtn').style.display = isDraft ? 'inline-flex' : 'none';
            document.getElementById('ccSaveBtn').style.display = isDraft ? 'inline-flex' : 'none';
            document.getElementById('ccSaveBtnText').textContent = 'Save Changes';
            document.getElementById('ccSendBtn').style.display = 'inline-flex';
            document.getElementById('ccSendBtnText').textContent = isDraft ? 'Send' : 'Nudge';
            
            // Disable send if all confirmed
            const allConfirmed = notice.acknowledgedRecipientCount === notice.recipientCount && notice.recipientCount > 0;
            document.getElementById('ccSendBtn').disabled = allConfirmed;

            document.getElementById('ccModalMode').textContent = isDraft ? 'Editing draft' : `Sent ${notice.sendCount || 1} time(s)`;

            renderRecipientAvatars();
            renderRecipientsList();
            renderActivityLog();

            modal.style.display = 'flex';
            adjustChangeNoticeModalPosition();
        } catch (error) {
            console.error('Error loading notice:', error);
            alert('Failed to load change notice details.');
        }
    }

    function formatTimeAgo(dateStr) {
        if (!dateStr) return '';
        const d = new Date(dateStr);
        if (Number.isNaN(d.getTime())) return '';
        try {
            return d.toLocaleString(undefined, { month: 'short', day: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' });
        } catch {
            return d.toISOString();
        }
    }

    function renderActivityLog() {
        const log = document.getElementById('ccActivityLog');
        if (!log) return;

        const notice = currentNotice;
        const recipients = notice?.recipients || [];

        const items = [];

        for (const r of recipients) {
            const email = r.email || '';
            const hasAccount = !!r.userId;
            const avatarHtml = hasAccount ? escapeHtml(getInitials(email)) : `<i class="fas fa-envelope"></i>`;
            const status = r.status || '';
            const respondedAt = r.respondedAt;
            const note = (r.clarificationNote || '').trim();

            if (status === 'NeedsClarification' && note) {
                items.push({
                    avatarHtml,
                    textHtml: `<strong>${escapeHtml(email)}</strong> requested clarification: ${escapeHtml(note)}`,
                    time: formatTimeAgo(respondedAt)
                });
            } else if (status === 'Acknowledged') {
                items.push({
                    avatarHtml,
                    textHtml: `<strong>${escapeHtml(email)}</strong> confirmed.`,
                    time: formatTimeAgo(respondedAt)
                });
            }
        }

        if (items.length === 0) {
            log.innerHTML = `<p class="text-muted small text-center">No activity yet.</p>`;
            return;
        }

        log.innerHTML = items.map(i => `
            <div class="activity-item">
                <div class="activity-avatar">${i.avatarHtml}</div>
                <div class="activity-content">
                    <div class="activity-text">${i.textHtml}</div>
                    ${i.time ? `<div class="activity-time">${escapeHtml(i.time)}</div>` : ``}
                </div>
            </div>
        `).join('');
    }

    function closeModal() {
        const modal = document.getElementById('ccModal');
        if (modal) {
            modal.style.display = 'none';
        }
        const popup = document.getElementById('ccContactSelectorPopup');
        if (popup) {
            popup.style.display = 'none';
        }
        currentNoticeId = null;
        currentNotice = null;
        recipientEmails = [];
    }

    // Render summary (list view)
    function renderSummary(summary) {
        const el = document.getElementById('change-control-summary-json');
        if (!el) return;
        el.textContent = JSON.stringify(summary);

        const counters = [
            ['draft', summary.draftCount],
            ['pending', summary.pendingCount],
            ['clarify', summary.needsClarificationCount],
            ['ack', summary.acknowledgedCount]
        ];
        counters.forEach(([key, value]) => {
            const span = document.querySelector(`[data-cc-count="${key}"]`);
            if (span) span.textContent = value;
        });

        const list = document.getElementById('change-control-notice-list');
        if (!list) return;
        list.innerHTML = '';

        const header = document.getElementById('cc-list-header');
        if (header) {
            header.classList.toggle('d-none', !summary.notices || summary.notices.length === 0);
        }

        if (!summary.notices || summary.notices.length === 0) {
            list.innerHTML = `<div class="text-center py-3"><p class="text-muted small mb-0">No change notices yet</p></div>`;
            return;
        }

        // Limit to 2 visible items initially
        const MAX_VISIBLE = 2;
        const notices = summary.notices;
        const hasMore = notices.length > MAX_VISIBLE;
        let isExpanded = false;

        function renderNoticeList() {
            list.innerHTML = '';
            const visibleCount = isExpanded ? notices.length : Math.min(notices.length, MAX_VISIBLE);
            
            for (let i = 0; i < visibleCount; i++) {
                const n = notices[i];
                list.appendChild(createNoticeRow(n));
            }
            
            if (hasMore) {
                const toggleRow = document.createElement('div');
                toggleRow.className = 'cc-toggle-row text-center py-2';
                toggleRow.style.cssText = 'cursor: pointer; color: #6b7280; font-size: 0.8rem; border-top: 1px solid #e5e7eb;';
                const remainingCount = notices.length - MAX_VISIBLE;
                toggleRow.innerHTML = isExpanded 
                    ? `<i class="fas fa-chevron-up"></i> Show less`
                    : `<i class="fas fa-chevron-down"></i> Show ${remainingCount} more`;
                toggleRow.addEventListener('click', () => {
                    isExpanded = !isExpanded;
                    renderNoticeList();
                });
                list.appendChild(toggleRow);
            }
        }

        function createNoticeRow(n) {
            const badgeClass =
                n.status === 'Acknowledged' ? 'bg-success' :
                n.status === 'NeedsClarification' ? '' :
                (n.status === 'Sent' || n.status === 'PartiallyAcknowledged') ? '' :
                '';

            const isAcknowledged = n.status === 'Acknowledged';
            const canSend = !isAcknowledged;
            const sendLabel = n.status === 'Draft' ? 'Send' : 'Nudge';
            const statusLabel = n.status === 'Acknowledged' ? 'Confirmed'
                : n.status === 'NeedsClarification' ? 'Response Required'
                : n.status === 'PartiallyAcknowledged' ? 'Partial'
                : (n.status || '');
            const statusIcon =
                n.status === 'Acknowledged' ? 'fas fa-check-circle' :
                n.status === 'NeedsClarification' ? 'fas fa-circle-question' :
                (n.status === 'Sent' || n.status === 'PartiallyAcknowledged') ? 'fas fa-paper-plane' :
                'fas fa-pen';

            const row = document.createElement('div');
            row.className = 'cc-row p-3 mb-2';
            row.style.cursor = 'pointer';
            row.dataset.noticeId = n.id;
            row.innerHTML = `
                <div class="cc-grid">
                    <div class="cc-status-col">
                        <span class="cc-status-pill badge ${badgeClass}"><i class="${statusIcon}"></i> ${escapeHtml(statusLabel)}</span>
                        <div class="cc-status-counts">
                            <span class="cc-count-pill" style="background: rgba(var(--bs-success-rgb), 0.12); color: #065f46;">Confirmed ${n.acknowledgedRecipientCount} / ${n.recipientCount}</span>
                            <span class="cc-count-pill" style="background: rgba(var(--bs-warning-rgb), 0.16); color: #92400e;">Response Required ${n.needsClarificationRecipientCount} / ${n.recipientCount}</span>
                            <span class="cc-count-pill" style="background: rgba(var(--bs-primary-rgb), 0.12); color: #1d4ed8;">Pending ${n.pendingRecipientCount} / ${n.recipientCount}</span>
                        </div>
                    </div>
                    <div class="cc-content-col">
                        <div class="cc-title">${escapeHtml(n.title || '')}</div>
                        <div class="cc-meta">
                            <span class="cc-chip"><i class="fas fa-tag" style="opacity:0.75;"></i> ${escapeHtml(n.changeType || '')}</span>
                            <span class="cc-chip"><i class="fas fa-flag" style="opacity:0.75;"></i> ${escapeHtml(n.priority || '')}</span>
                            <span class="cc-chip"><i class="fas fa-users" style="opacity:0.75;"></i> ${n.recipientCount}</span>
                        </div>
                        ${n.description ? `<div class="cc-description" style="font-size: 0.8rem; color: #6b7280; margin-top: 0.25rem; line-height: 1.4;">${escapeHtml(n.description)}</div>` : ''}
                    </div>
                    <div class="cc-col-actions">
                        <button class="btn btn-sm ${sendLabel === 'Nudge' ? 'btn-primary' : 'btn-outline-primary'} cc-send-btn" style="font-weight:600; padding: 0.2rem 0.5rem;" data-notice-id="${n.id}" ${canSend ? '' : 'disabled'}>${sendLabel}</button>
                    </div>
                </div>
            `;
            return row;
        }

        renderNoticeList();
    }

    async function refreshSummary(orgId, matterId) {
        const url = `/Client/${orgId}/Matter/${matterId}/ChangeNotices/Summary`;
        const summary = await fetchJson(url, { method: 'GET', headers: { 'Accept': 'application/json' } });
        renderSummary(summary);
    }

    async function createOrUpdateDraft(orgId, matterId, skipClose = false) {
        const title = document.getElementById('ccTitleInput').value.trim();
        if (!title) {
            alert('Please enter a title.');
            return false;
        }

        const data = new FormData();
        data.append('Title', title);
        data.append('Description', document.getElementById('ccDescription').value);
        data.append('ChangeType', document.getElementById('ccChangeType').value);
        data.append('Priority', document.getElementById('ccPriority').value);
        data.append('AcknowledgementDueDate', document.getElementById('ccDueDate').value);
        data.append('RecipientEmails', recipientEmails.join(','));

        let url, method;
        if (currentNoticeId) {
            url = `/Client/${orgId}/Matter/${matterId}/ChangeNotices/${currentNoticeId}`;
            method = 'PUT';
        } else {
            url = `/Client/${orgId}/Matter/${matterId}/ChangeNotices`;
            method = 'POST';
        }

        const summary = await fetchJson(url, {
            method: method,
            headers: { 'RequestVerificationToken': getAntiForgeryToken() },
            body: data
        });

        renderSummary(summary);
        if (!skipClose) {
            closeModal();
        }
        return true;
    }

    async function sendNotice(orgId, matterId, noticeId) {
        const url = `/Client/${orgId}/Matter/${matterId}/ChangeNotices/${noticeId}/Send`;
        const summary = await fetchJson(url, {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        renderSummary(summary);
        closeModal();
    }

    async function deleteNotice(orgId, matterId, noticeId) {
        if (!confirm('Are you sure you want to delete this draft?')) return;

        const url = `/Client/${orgId}/Matter/${matterId}/ChangeNotices/${noticeId}`;
        await fetchJson(url, {
            method: 'DELETE',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        
        await refreshSummary(orgId, matterId);
        closeModal();
    }

    // Event handlers
    document.addEventListener('click', async function (e) {
        const root = document.getElementById('change-control-card');
        if (!root) return;

        const orgId = root.getAttribute('data-org-id');
        const matterId = root.getAttribute('data-matter-id');
        if (!orgId || !matterId) return;

        const target = e.target;

        // Refresh button
        const refreshBtn = target.closest?.('#cc-refresh-btn');
        if (refreshBtn) {
            e.preventDefault();
            await refreshSummary(orgId, matterId);
            return;
        }

        // Open new draft
        const openComposerBtn = target.closest?.('#cc-open-composer-btn');
        if (openComposerBtn) {
            e.preventDefault();
            await loadContacts(orgId, matterId);
            openNewDraftModal();
            return;
        }

        // Close modal
        const closeBtn = target.closest?.('#cc-close-modal, #cc-cancel-btn');
        if (closeBtn) {
            e.preventDefault();
            closeModal();
            return;
        }

        // Click backdrop to close (but not when clicking inside the dialog)
        const modal = document.getElementById('ccModal');
        if (modal && modal.style.display === 'flex') {
            const modalDialog = modal.querySelector('.modal-dialog');
            // Only close if click was on the backdrop, not inside the dialog
            if (target === modal || (modal.contains(target) && !modalDialog.contains(target))) {
                e.preventDefault();
                closeModal();
                return;
            }
        }

        // Save/Create button
        const saveBtn = target.closest?.('#ccSaveBtn');
        if (saveBtn) {
            e.preventDefault();
            await createOrUpdateDraft(orgId, matterId);
            return;
        }

        // Send button in modal
        const sendModalBtn = target.closest?.('#ccSendBtn');
        if (sendModalBtn) {
            e.preventDefault();
            const noticeId = currentNoticeId || document.getElementById('ccNoticeId').value;
            if (!noticeId) {
                // Need to save first (new draft) - don't close yet
                const saved = await createOrUpdateDraft(orgId, matterId, true);
                if (!saved) return;
                // Get the ID from the summary
                const summary = JSON.parse(document.getElementById('change-control-summary-json').textContent || '{}');
                if (summary.notices && summary.notices.length > 0) {
                    const newNotice = summary.notices[0];
                    await sendNotice(orgId, matterId, newNotice.id);
                }
            } else {
                // Save any edits first (don't close), then send/nudge
                const saved = await createOrUpdateDraft(orgId, matterId, true);
                if (!saved) return;
                await sendNotice(orgId, matterId, noticeId);
            }
            return;
        }

        // Delete button
        const deleteBtn = target.closest?.('#ccDeleteBtn');
        if (deleteBtn && currentNoticeId) {
            e.preventDefault();
            await deleteNotice(orgId, matterId, currentNoticeId);
            return;
        }

        // Add recipient buttons
        const addRecipientBtn = target.closest?.('#ccAddRecipientBtn, #ccAddRecipientBtnAlt');
        if (addRecipientBtn) {
            e.preventDefault();
            await loadContacts(orgId, matterId);
            showContactSelector(addRecipientBtn);
            return;
        }

        // Send button in list (quick send)
        const sendListBtn = target.closest?.('.cc-send-btn');
        if (sendListBtn) {
            e.preventDefault();
            e.stopPropagation();
            const noticeId = sendListBtn.getAttribute('data-notice-id');
            if (noticeId) {
                await sendNotice(orgId, matterId, noticeId);
            }
            return;
        }

        // Click on notice row to open details
        const noticeRow = target.closest?.('.cc-row[data-notice-id]');
        if (noticeRow && !target.closest('.cc-send-btn')) {
            e.preventDefault();
            const noticeId = noticeRow.dataset.noticeId;
            await loadContacts(orgId, matterId);
            await openNoticeModal(orgId, matterId, noticeId);
            return;
        }

        // Close contact selector when clicking outside
        const contactPopup = document.getElementById('ccContactSelectorPopup');
        if (contactPopup && contactPopup.style.display === 'block') {
            if (!contactPopup.contains(target) && !target.closest('#ccAddRecipientBtn, #ccAddRecipientBtnAlt')) {
                contactPopup.style.display = 'none';
            }
        }
    });

    // Enter key to add email in section input
    document.addEventListener('keypress', function(e) {
        if (e.target.id === 'ccAddRecipientEmail' && e.key === 'Enter') {
            e.preventDefault();
            const email = e.target.value.trim();
            if (email && isValidEmail(email) && !recipientEmails.includes(email)) {
                recipientEmails.push(email);
                e.target.value = '';
                renderRecipientAvatars();
                renderRecipientsList();
                updateHiddenRecipients();
            }
        }
    });

    // ESC closes modal
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        const modal = document.getElementById('ccModal');
        if (modal && modal.style.display === 'flex') {
            closeModal();
        }
    });

    // Scroll-to buttons
    document.addEventListener('click', function(e) {
        const scrollBtns = {
            '#cc-scroll-type-btn': '#cc-type-priority-section',
            '#cc-scroll-priority-btn': '#cc-type-priority-section',
            '#cc-scroll-due-btn': '#cc-due-section',
            '#cc-scroll-recipients-btn': '#cc-recipients-section'
        };
        
        for (const [btn, target] of Object.entries(scrollBtns)) {
            if (e.target.closest?.(btn)) {
                const section = document.querySelector(target);
                if (section) {
                    section.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                break;
            }
        }
    });
})();
