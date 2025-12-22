let connection;
let currentConversationId = null;
let currentOrganizationId = null;
let currentMessages = [];
let aiThinkingInterval = null;
let lastMessageCount = 0;
let aiInsights = null;
let insightsVisible = false;

// Sticky message state
let stickyMessageIndex = -1; // Index of the message currently shown in sticky overlay (from end)
let messageElements = []; // Array to store references to message DOM elements
let userMessageElements = []; // Array to store only user message elements

// Initialize SignalR connection
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, initializing chat...');
    
    // Initialize chat layout immediately (synchronous, fast)
    initializeChatLayout();
    
    // Load conversations immediately after DOM is ready
    // No delay needed since we're using Redis caching for fast responses
    window.addEventListener('load', () => {
        console.log('Page fully loaded, now loading AI conversations...');
        console.log('AI chat panel exists:', document.getElementById('chatPanel') !== null);
        console.log('Tab list exists:', document.querySelector('.tab-list') !== null);
        loadAIConversationsForPanel();
    });
    
    // Initialize SignalR connection with significant delay (non-critical)
    setTimeout(() => {
        if (typeof signalR !== 'undefined') {
            if (!connection || connection.state === signalR.HubConnectionState.Disconnected) {
                connection = new signalR.HubConnectionBuilder()
                    .withUrl("/hubs/chat")
                    .withAutomaticReconnect()
                    .build();

                // Start connection
                connection.start().then(function () {
                    console.log("SignalR Connected");
                }).catch(function (err) {
                    console.error("SignalR Connection Error: ", err.toString());
                });
            }
        } else {
            console.log("SignalR not available, using fallback communication");
        }
    }, 1000); // 1 second delay for SignalR

    // Event listeners
    // Only attach send button handler if NOT on Communications page (to prevent conflict)
    if (!window.communicationsPageActive) {
        document.getElementById('sendButton')?.addEventListener('click', sendMessage);
    }
    // Note: Enter key handling is now in _ClientLayout.cshtml to support Shift+Enter for new lines
    document.getElementById('clarityButton')?.addEventListener('click', requestClarity);
    document.getElementById('requestClarity')?.addEventListener('click', processClarityRequest);
    document.getElementById('toggleInsights')?.addEventListener('click', toggleAIInsights);
    document.getElementById('backButton')?.addEventListener('click', toggleSidebar);
    document.getElementById('loadAIInsights')?.addEventListener('click', loadAIInsights);
    document.getElementById('menuButton')?.addEventListener('click', toggleSidebar);
    
    // Handle NEW conversation button (Plus button) - returns to landing state
    document.getElementById('newConversationBtn')?.addEventListener('click', function(e) {
        e.preventDefault();
        console.log('Returning to landing state...');
        
        // Clear current conversation ID (new conversation will be created on first message)
        currentConversationId = null;
        
        // Deactivate all tabs
        document.querySelectorAll('.conversation-tab').forEach(tab => {
            tab.classList.remove('active');
        });
        
        // Enter landing mode
        const chatContent = document.getElementById('chatContent');
        if (chatContent) {
            chatContent.classList.add('landing-mode');
            // Remove sticky message class to remove unnecessary padding
            chatContent.classList.remove('has-sticky-message');
        }
        
        // Show welcome message
        const chatMessages = document.getElementById('chatMessages');
        if (chatMessages) {
            // Remove sticky message class from chat messages too
            chatMessages.classList.remove('has-sticky-message');
            chatMessages.innerHTML = `
                <div class="welcome-message" id="welcomeMessage">
                    <div class="ai-message-bubble">
                        <div class="message-content">
                            <div class="message-header">
                                <img src="/images/Notal_Banner_Logo.png" alt="Notal AI" class="ai-message-logo" />
                                <span class="timestamp">Just now</span>
                            </div>
                            <div class="message-text">
                                Hi! I'm Notal, your AI assistant. Leave me an actionable note or select a chat above to get started!
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }
        
        // Hide conversation header
        const conversationHeader = document.getElementById('notalConversationHeader');
        if (conversationHeader) {
            conversationHeader.style.display = 'none';
        }
        
        // Hide sticky last message overlay
        const stickyLastMessage = document.getElementById('stickyLastMessage');
        if (stickyLastMessage) {
            stickyLastMessage.style.display = 'none';
        }
        
        // Reset sticky message state
        stickyMessageIndex = -1;
        messageElements = [];
        userMessageElements = [];
        
        // Clear message input
        const messageInput = document.getElementById('messageInput');
        if (messageInput) {
            messageInput.value = '';
            messageInput.focus();
        }
        
        console.log('Landing state ready - conversation will be created on first message');
    });
    
    // Handle conversation creation form (from modal - kept for backward compatibility)
    document.getElementById('createConversationForm')?.addEventListener('submit', function(e) {
        e.preventDefault();
        console.log('Form submitted, creating conversation...');
        const formData = new FormData(this);
        
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        console.log('Creating conversation with orgId:', orgId);
        fetch(`/Client/${orgId}/Chat/CreateConversation`, {
            method: 'POST',
            body: formData
        })
        .then(response => {
            console.log('Response received:', response.status);
            if (response.ok) {
                // Close modal
                const modal = bootstrap.Modal.getInstance(document.getElementById('newConversationModal'));
                if (modal) {
                    modal.hide();
                }
                
                // Clear form
                this.reset();
                
                // Refresh conversations instead of reloading page
                console.log('Refreshing conversations...');
                refreshConversations();
            } else {
                console.error('Failed to create conversation:', response.statusText);
            }
        })
        .catch(error => {
            console.error('Error creating conversation:', error);
        });
    });

    // Conversation selection - use event delegation
    document.addEventListener('click', function(event) {
        console.log('Click detected on:', event.target);
        const conversationItem = event.target.closest('.conversation-tab');
        console.log('Conversation item found:', conversationItem);
        
        if (conversationItem) {
            const conversationId = conversationItem.dataset.conversationId;
            console.log('Conversation ID:', conversationId);
            if (conversationId) {
                console.log('Loading conversation:', conversationId);
                loadConversation(conversationId);
            } else {
                console.error('No conversation ID found in dataset');
            }
        }
    });

    // Hide context menu when clicking elsewhere
    document.addEventListener('click', function(event) {
        const contextMenu = document.getElementById('conversationContextMenu');
        if (contextMenu && !event.target.closest('#conversationContextMenu')) {
            contextMenu.remove();
        }
    });
    
});

// Initialize chat for specific conversation
function initializeChat(conversationId) {
    currentConversationId = conversationId;
    if (connection && typeof signalR !== 'undefined' && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("JoinConversation", conversationId.toString());
    }
}


// Load conversation messages
async function loadConversationMessages(conversationId) {
    console.log('Loading messages for conversation:', conversationId);
    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GetMessages/${conversationId}`);
        console.log('Response status:', response.status);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const messages = await response.json();
        console.log('Loaded messages:', messages);
        currentMessages = messages;
        lastMessageCount = messages.length;
        
        const chatMessages = document.getElementById('chatMessages');
        if (chatMessages) {
            // Clear only messages, preserve the headers (welcome-message and notal-conversation-header)
            const messagesToRemove = chatMessages.querySelectorAll('.ai-message-bubble:not(.welcome-message .ai-message-bubble):not(.notal-conversation-header .ai-message-bubble), .user-message-bubble');
            messagesToRemove.forEach(msg => msg.remove());
            
            messageElements = []; // Reset message elements array
            userMessageElements = []; // Reset user message elements array
            
            messages.forEach(message => {
                addMessageToChat(message);
            });
            
            // Process AI insights
            processAIInsights(messages);
            
            // Initialize sticky message feature after messages are loaded
            initializeStickyMessage();
        } else {
            console.error('Chat messages container not found');
        }
        
    } catch (error) {
        console.error('Error loading messages:', error);
    }
}

// Generate a smart title from a message using keywords
function generateTitleFromMessage(message) {
    // Remove common stop words and extract meaningful keywords
    const stopWords = ['the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 'of', 'with', 'by', 'from', 'as', 'is', 'was', 'are', 'were', 'be', 'been', 'being', 'have', 'has', 'had', 'do', 'does', 'did', 'will', 'would', 'should', 'could', 'may', 'might', 'can', 'about', 'into', 'through', 'during', 'before', 'after', 'above', 'below', 'up', 'down', 'out', 'off', 'over', 'under', 'again', 'further', 'then', 'once', 'here', 'there', 'when', 'where', 'why', 'how', 'all', 'both', 'each', 'few', 'more', 'most', 'other', 'some', 'such', 'no', 'nor', 'not', 'only', 'own', 'same', 'so', 'than', 'too', 'very', 'just', 'please', 'help', 'me', 'my', 'i', 'you', 'your', 'what', 'need'];
    
    // Clean the message and get words
    const words = message.toLowerCase()
        .replace(/[^\w\s]/g, ' ') // Remove punctuation
        .split(/\s+/)
        .filter(word => word.length > 2 && !stopWords.includes(word));
    
    // Take first 3-4 meaningful words
    const keywords = words.slice(0, 4);
    
    if (keywords.length === 0) {
        // Fallback to first part of message if no keywords found
        return message.length > 30 ? message.substring(0, 30) + '...' : message;
    }
    
    // Capitalize first letter of each keyword
    const title = keywords
        .map(word => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');
    
    // Limit title length
    return title.length > 40 ? title.substring(0, 40) + '...' : title;
}

// Create a new conversation from a message
async function createNewConversationFromMessage(message, fileInfo = null, capturedFile = null) {
    const orgId = getCurrentOrganizationId();
    if (!orgId) {
        console.error('Organization ID not found');
        return;
    }
    
    try {
        // Generate smart title from message keywords
        const title = generateTitleFromMessage(message);
        const response = await fetch(`/Client/${orgId}/Chat/CreateConversation`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `title=${encodeURIComponent(title)}&description=${encodeURIComponent('New conversation started')}`
        });
        
        if (response.ok) {
            const data = await response.json();
            if (data.success && data.conversationId) {
                // Update the current conversation ID
                currentConversationId = data.conversationId;
                
                // Hide welcome message and show conversation header
                const welcomeMessage = document.getElementById('welcomeMessage');
                if (welcomeMessage) {
                    welcomeMessage.style.display = 'none';
                }
                
                const conversationHeader = document.getElementById('notalConversationHeader');
                if (conversationHeader) {
                    conversationHeader.style.display = 'block';
                }
                
                // Exit landing mode
                const chatContent = document.getElementById('chatContent');
                if (chatContent) {
                    chatContent.classList.remove('landing-mode');
                }
                
                // Create a new tab for this conversation
                await addConversationTab(data.conversationId, title);
                
                // Now send the message to the new conversation (pass fileInfo and capturedFile for background processing)
                await sendMessageInternal(message, data.conversationId, fileInfo, capturedFile);

                // Safeguard: ensure the input is cleared after send
                const messageInput = document.getElementById('messageInput');
                if (messageInput) messageInput.value = '';
            } else {
                console.error('Failed to create conversation:', data.error);
            }
        } else {
            console.error('Failed to create conversation:', response.statusText);
        }
    } catch (error) {
        console.error('Error creating conversation:', error);
    }
}

// Add a conversation tab to the tab list
async function addConversationTab(conversationId, title) {
    const tabList = document.querySelector('.tab-list');
    if (!tabList) return;
    
    // Create the tab element
    const tabHtml = `
        <div class="conversation-tab" data-conversation-id="${conversationId}">
            <div class="tab-content">
                <div class="tab-title">${title}</div>
            </div>
            <button class="tab-close" data-conversation-id="${conversationId}" title="Delete conversation">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;
    
    // Find the add button
    const addButton = tabList.querySelector('.add-conversation-btn');
    if (addButton) {
        // Insert before the add button
        addButton.insertAdjacentHTML('beforebegin', tabHtml);
    } else {
        // If no add button, just append
        tabList.insertAdjacentHTML('beforeend', tabHtml);
    }
    
    // Get the newly created tab
    const newTab = tabList.querySelector(`.conversation-tab[data-conversation-id="${conversationId}"]`);
    if (newTab) {
        // Set it as active
        document.querySelectorAll('.conversation-tab').forEach(tab => {
            tab.classList.remove('active');
        });
        newTab.classList.add('active');
        
        // Add click handler
        newTab.addEventListener('click', function() {
            if (!this.classList.contains('active')) {
                loadConversation(conversationId);
            }
        });
        
        // Add delete handler
        const deleteBtn = newTab.querySelector('.tab-close');
        if (deleteBtn) {
            deleteBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                e.preventDefault();
                deleteConversation(conversationId.toString());
            });
        }
    }
}

// Send message to a specific conversation
async function sendMessageToConversation(message, conversationId) {
    return await sendMessageInternal(message, conversationId);
}

// Pending attachment state
let pendingAttachment = null;

// Upload and extract content from attachment
async function uploadAttachment(file) {
    const orgId = getCurrentOrganizationId();
    if (!orgId) {
        console.error('Organization ID not found');
        return null;
    }
    
    // Set uploading flag
    window._uploadingAttachment = true;
    
    const formData = new FormData();
    formData.append('file', file);
    
    try {
        const response = await fetch(`/Client/${orgId}/Chat/UploadAttachment`, {
            method: 'POST',
            body: formData
        });
        
        const data = await response.json();
        
        if (data.success) {
            console.log(`Attachment processed: ${data.fileName} (${data.contentLength} chars extracted)`);
            window._uploadingAttachment = false;
            return {
                fileName: data.fileName,
                fileSize: data.fileSize,
                extractedContent: data.extractedContent
            };
        } else {
            console.error('Failed to process attachment:', data.error);
            window._uploadingAttachment = false;
            alert('Failed to process attachment: ' + data.error);
            return null;
        }
    } catch (error) {
        console.error('Error uploading attachment:', error);
        window._uploadingAttachment = false;
        alert('Error uploading attachment. Please try again.');
        return null;
    }
}

// Send message
async function sendMessage() {
    const messageInput = document.getElementById('messageInput');
    const fileInput = document.getElementById('fileInput');
    const attachmentPreview = document.getElementById('attachmentPreview');
    const attachFileBtn = document.getElementById('attachFileBtn');
    const message = messageInput.value.trim();
    
    // Check if there's a file attached
    const hasAttachment = fileInput && fileInput.files && fileInput.files.length > 0;
    
    if (!message && !hasAttachment) return;
    
    // Capture file and file info before clearing
    let capturedFile = null;
    let fileInfo = null;
    if (hasAttachment) {
        capturedFile = fileInput.files[0];
        fileInfo = {
            name: capturedFile.name,
            size: capturedFile.size
        };
    }
    
    // Clear input immediately to avoid duplicate text lingering
    messageInput.value = '';
    
    // Reset textarea height
    messageInput.style.height = 'auto';
    
    // Reset character counter
    const charCounter = document.getElementById('charCounter');
    if (charCounter) {
        charCounter.textContent = '0 / 2000';
        charCounter.classList.remove('warning', 'limit');
    }
    
    // Disable send button
    const sendButton = document.getElementById('sendButton');
    if (sendButton) {
        sendButton.disabled = true;
    }
    
    // Clear attachment preview immediately
    if (attachmentPreview) {
        attachmentPreview.classList.remove('active');
    }
    if (attachFileBtn) {
        attachFileBtn.classList.remove('active');
    }
    
    // Clear the file input immediately
    if (fileInput) {
        fileInput.value = '';
    }
    
    // If we don't have a conversation ID, create a new one first
    if (!currentConversationId) {
        await createNewConversationFromMessage(message || 'Analyzing attached document...', fileInfo, capturedFile);
        return;
    }
    
    await sendMessageInternal(message || 'Please analyze this document.', currentConversationId, fileInfo, capturedFile);
}

// Generate AI response with streaming
async function generateAIResponse(userMessage) {
    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        // Keep "AI is thinking..." visible until first chunk arrives
        // Don't create streaming placeholder yet
        
        let aiMessageDiv = null;
        let messageTextDiv = null;
        let isFirstChunk = true;
        
        let fullContent = '';
        let plainTextCache = '';
        let messageId = null;
        
        // Track if user has manually scrolled up
        let userHasScrolledUp = false;
        let lastScrollHeight = 0;
        
        const chatMessages = document.getElementById('chatMessages');
        
        // Detect manual scroll by user
        const handleScroll = () => {
            if (!chatMessages) return;
            
            const isAtBottom = chatMessages.scrollHeight - chatMessages.scrollTop - chatMessages.clientHeight < 50;
            
            // If user scrolls back to bottom, re-enable auto-scroll
            if (isAtBottom) {
                userHasScrolledUp = false;
            } 
            // If scroll position changed and we're not at bottom, user scrolled up
            else if (chatMessages.scrollHeight === lastScrollHeight) {
                userHasScrolledUp = true;
            }
            
            lastScrollHeight = chatMessages.scrollHeight;
        };
        
        // Add scroll listener
        if (chatMessages) {
            chatMessages.addEventListener('scroll', handleScroll);
        }
        
        // Build request body with optional attachment content
        const requestBody = {
            conversationId: parseInt(currentConversationId),
            userMessage: userMessage
        };
        
        // Include AI mode and model if AgentActions is available
        if (typeof window.AgentActions !== 'undefined') {
            requestBody.aiMode = window.AgentActions.getMode();
            requestBody.aiModel = window.AgentActions.getModel();
            console.log('[AgentActions] Sending with mode:', requestBody.aiMode, 'model:', requestBody.aiModel);
        }
        
        // Wait for pendingAttachment if it's still being processed (max 30 seconds)
        if (pendingAttachment === null && window._uploadingAttachment) {
            console.log('Waiting for attachment upload to complete...');
            const maxWait = 30000; // 30 seconds
            const startWait = Date.now();
            while (pendingAttachment === null && window._uploadingAttachment && (Date.now() - startWait) < maxWait) {
                await new Promise(resolve => setTimeout(resolve, 100));
            }
        }
        
        // Include attachment content if present
        if (pendingAttachment) {
            requestBody.attachmentContent = pendingAttachment.extractedContent;
            requestBody.attachmentFileName = pendingAttachment.fileName;
            console.log(`Including attachment in AI request: ${pendingAttachment.fileName}`);
            // Clear pending attachment after including it
            pendingAttachment = null;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GenerateAIResponseStream`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(requestBody)
        });
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        // Handle Server-Sent Events stream
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        
        // While streaming, never show raw [ACTION:...] text. We render action cards instead (Cursor-style).
        
        while (true) {
            const { done, value } = await reader.read();
            
            if (done) {
                // Stream ended without an explicit { done: true } event.
                // Finalize the UI so the user doesn't get stuck on "AI loading".
                console.log('⚠️ Stream reader ended (no explicit done event). Finalizing...');

                // If we never got any content chunks, show a friendly error
                if (!aiMessageDiv) {
                    hideAIThinkingIndicator();
                    aiMessageDiv = createStreamingAIMessagePlaceholder();
                    messageTextDiv = aiMessageDiv.querySelector('.message-text');
                    messageTextDiv.innerHTML = 'Sorry, I was unable to generate a response. The AI stream ended unexpectedly. Please try again.';
                    aiMessageDiv.classList.remove('streaming');
                    return;
                }

                // Remove streaming state and render whatever we collected
                aiMessageDiv.classList.remove('streaming');

                let displayContent = fullContent;
                if (typeof window.AgentActions !== 'undefined') {
                    if (window.AgentActions.replaceWithCards) {
                        displayContent = window.AgentActions.replaceWithCards(fullContent);
                    } else if (window.AgentActions.stripActionCommands) {
                        displayContent = window.AgentActions.stripActionCommands(fullContent);
                    }
                }
                messageTextDiv.innerHTML = displayContent || 'No response generated.';

                // Remove scroll listener
                if (chatMessages) {
                    chatMessages.removeEventListener('scroll', handleScroll);
                }

                return;
            }
            
            // Decode the chunk and add to buffer
            buffer += decoder.decode(value, { stream: true });
            
            // Process complete SSE messages (separated by \n\n)
            const messages = buffer.split('\n\n');
            buffer = messages.pop() || ''; // Keep incomplete message in buffer
            
            for (const message of messages) {
                if (message.startsWith('data: ')) {
                    const jsonData = message.substring(6);
                    
                    try {
                        const eventData = JSON.parse(jsonData);
                        console.log('📨 Chunk received:', eventData.content?.substring(0, 20), 'at', new Date().getMilliseconds());
                        console.log('🔍 eventData:', eventData);
                        console.log('🔍 eventData.content truthy?', !!eventData.content);
                        console.log('🔍 messageTextDiv exists?', !!messageTextDiv);
                        
                        // Add delay between chunks for smooth visual streaming (30ms per chunk)
                        await new Promise(resolve => setTimeout(resolve, 30));
                        
                        if (eventData.error) {
                            console.log('❌ Error in event data');
                            // Error occurred - ensure we have a message div to show error
                            if (!aiMessageDiv) {
                                hideAIThinkingIndicator();
                                aiMessageDiv = createStreamingAIMessagePlaceholder();
                                messageTextDiv = aiMessageDiv.querySelector('.message-text');
                            }
                            messageTextDiv.innerHTML = eventData.content || 'An error occurred while generating the response.';
                            // Remove streaming class
                            aiMessageDiv.classList.remove('streaming');
                            return;
                        }
                        
                        if (eventData.done) {
                            console.log('✅ Stream done signal received');
                            // Always clear the "thinking" UI on done, even if we never received content chunks.
                            hideAIThinkingIndicator();
                            
                            // Ensure messageTextDiv exists if aiMessageDiv exists
                            if (aiMessageDiv && !messageTextDiv) {
                                messageTextDiv = aiMessageDiv.querySelector('.message-text');
                            }
                            
                            // If stream completed without any content, create error message
                            if (!aiMessageDiv) {
                                console.log('⚠️ Stream ended without content - showing error');
                                aiMessageDiv = createStreamingAIMessagePlaceholder();
                                messageTextDiv = aiMessageDiv.querySelector('.message-text');
                                messageTextDiv.innerHTML = 'Sorry, I was unable to generate a response. The AI model may be unavailable. Please try again or select a different model.';
                                aiMessageDiv.classList.remove('streaming');
                                return;
                            }
                            
                            // Stream complete - remove streaming class and cursor
                            aiMessageDiv.classList.remove('streaming');
                            
                            // Clean the content for display - replace action commands with nice action cards
                            let displayContent = fullContent;
                            if (typeof window.AgentActions !== 'undefined') {
                                // Use replaceWithCards to show animated action cards instead of raw commands
                                if (window.AgentActions.replaceWithCards) {
                                    displayContent = window.AgentActions.replaceWithCards(fullContent);
                                } else if (window.AgentActions.stripActionCommands) {
                                    // Fallback: strip if replaceWithCards not available
                                    displayContent = window.AgentActions.stripActionCommands(fullContent);
                                }
                            }
                            messageTextDiv.innerHTML = displayContent || 'No response generated.';
                            
                            // Create a proper message object for tracking
                            const aiMessage = {
                                id: messageId || Date.now(),
                                conversationId: parseInt(currentConversationId),
                                content: fullContent,
                                createdAt: new Date().toISOString(),
                                isFromAI: true,
                                messageType: 'AI_Response',
                                userType: 'AI'
                            };
                            
                            currentMessages.push(aiMessage);
                            
                            // Reinitialize sticky message after streaming is complete
                            initializeStickyMessage();
                            
                            // 🤖 Agent Actions: Check for action commands in AI response
                            if (typeof window.AgentActions !== 'undefined') {
                                try {
                                    const detectedActions = window.AgentActions.parseResponse(fullContent);
                                    if (detectedActions && detectedActions.length > 0) {
                                        console.log('[AgentActions] Detected actions in AI response:', detectedActions);
                                        window.AgentActions.processActions(detectedActions, currentConversationId);
                                    }
                                } catch (agentError) {
                                    console.error('[AgentActions] Error processing actions:', agentError);
                                }
                            }
                            
                            // Load AI insights after a short delay
                            setTimeout(() => {
                                if (insightsVisible) {
                                    loadAIInsights();
                                }
                            }, 2000);
                            
                            // Remove scroll listener
                            if (chatMessages) {
                                chatMessages.removeEventListener('scroll', handleScroll);
                            }
                            
                            return;
                        }
                        
                        if (eventData.content) {
                            console.log('🎯 ENTERING DOM UPDATE BLOCK');
                            
                            // On first chunk: hide thinking indicator and create streaming placeholder
                            if (isFirstChunk) {
                                hideAIThinkingIndicator();
                                aiMessageDiv = createStreamingAIMessagePlaceholder();
                                messageTextDiv = aiMessageDiv.querySelector('.message-text');
                                
                                // Initialize empty message text (no cursor/dots needed)
                                messageTextDiv.textContent = '';
                                
                                isFirstChunk = false;
                            }
                            
                            try {
                                // Append chunk to full content
                                fullContent += eventData.content;
                                console.log('📝 fullContent length:', fullContent.length);
                                
                                // During streaming: keep HTML structure, only remove incomplete tags
                                let displayText = fullContent
                                    .replace(/<[^>]*$/g, '')  // Remove incomplete tag at the end (e.g., "<p" or "<stro")
                                    .trim();

                                // Cursor-style streaming:
                                // - Replace complete action blocks with animated action cards
                                // - Replace an in-progress trailing action block with a placeholder action card
                                if (typeof window.AgentActions !== 'undefined') {
                                    if (window.AgentActions.replaceWithCardsStreaming) {
                                        displayText = window.AgentActions.replaceWithCardsStreaming(displayText);
                                    } else if (window.AgentActions.replaceWithCards) {
                                        displayText = window.AgentActions.replaceWithCards(displayText);
                                        displayText = displayText.replace(/\[ACTION:[\s\S]*$/g, '').trim();
                                    } else if (window.AgentActions.stripActionCommands) {
                                        displayText = window.AgentActions.stripActionCommands(displayText);
                                        displayText = displayText.replace(/\[ACTION:[\s\S]*$/g, '').trim();
                                    }
                                } else {
                                    // Fallback safety: hide any trailing action command text
                                    displayText = displayText.replace(/\[ACTION:[\s\S]*$/g, '').trim();
                                }
                                
                                console.log('🔤 displayText:', displayText.substring(0, 50));
                                console.log('🎯 About to update DOM...');
                                
                                // Update using innerHTML to preserve HTML formatting during streaming
                                const tempDiv = document.createElement('div');
                                tempDiv.innerHTML = displayText;
                                
                                // Clear messageTextDiv and rebuild with formatted content
                                messageTextDiv.innerHTML = '';
                                while (tempDiv.firstChild) {
                                    messageTextDiv.appendChild(tempDiv.firstChild);
                                }
                                
                                console.log('✅ Text node updated!');
                                
                                // Auto-scroll only if user hasn't manually scrolled up
                                if (chatMessages && !userHasScrolledUp) {
                                    chatMessages.scrollTop = chatMessages.scrollHeight;
                                    lastScrollHeight = chatMessages.scrollHeight;
                                }
                                console.log('✅ DOM update complete');
                            } catch (domError) {
                                console.error('💥 ERROR in DOM update:', domError);
                            }
                        } else {
                            console.log('⚠️ No content in eventData');
                        }
                    } catch (e) {
                        console.error('💥 ERROR parsing SSE:', e);
                    }
                }
            }
        }
        
    } catch (error) {
        console.error('Error generating streaming AI response:', error);
        hideAIThinkingIndicator();
        
        // Remove scroll listener on error
        const chatMessagesDiv = document.getElementById('chatMessages');
        if (chatMessagesDiv && typeof handleScroll !== 'undefined') {
            chatMessagesDiv.removeEventListener('scroll', handleScroll);
        }
        
        // Show error message in chat
        const errorDiv = document.createElement('div');
        errorDiv.className = 'ai-message-bubble error-message';
        errorDiv.innerHTML = `
            <div class="message-content">
                <div class="message-header">
                    <img src="/images/Notal_Banner_Logo.png" alt="Notal AI" class="ai-message-logo" />
                    <span class="timestamp">${new Date().toLocaleTimeString()}</span>
                </div>
                <div class="message-text">
                    I apologize, but I'm having trouble generating a response right now. Please try again in a moment.
                </div>
            </div>
        `;
        if (chatMessagesDiv) {
            chatMessagesDiv.appendChild(errorDiv);
            chatMessagesDiv.scrollTo({ top: chatMessagesDiv.scrollHeight, behavior: 'smooth' });
        }
    }
}

// Create a placeholder for streaming AI message
function createStreamingAIMessagePlaceholder() {
    const chatMessages = document.getElementById('chatMessages');
    if (!chatMessages) return null;
    
    const messageDiv = document.createElement('div');
    messageDiv.className = 'message ai-message ai-message-bubble streaming';
    messageDiv.innerHTML = `
        <div class="message-content">
            <div class="message-header">
                <img src="/images/Notal_Banner_Logo.png" alt="Notal AI" class="ai-message-logo" />
                <span class="timestamp">${new Date().toLocaleTimeString()}</span>
            </div>
            <div class="message-text"></div>
        </div>
    `;
    
    chatMessages.appendChild(messageDiv);
    messageElements.push(messageDiv);
    
    return messageDiv;
}

// Internal function to handle message sending logic
async function sendMessageInternal(message, conversationId, fileInfo = null, capturedFile = null) {
    try {
        // Build attachment info from pendingAttachment or fileInfo
        const attachmentInfo = pendingAttachment ? {
            fileName: pendingAttachment.fileName,
            fileSize: pendingAttachment.fileSize
        } : (fileInfo ? {
            fileName: fileInfo.name,
            fileSize: fileInfo.size || fileInfo.fileSize
        } : null);
        
        // Add user message to chat immediately (with attachment indicator if present)
        addUserMessageToChat(message, attachmentInfo);
        
        // Show intelligent AI thinking indicator immediately
        showIntelligentAIThinkingIndicator(message);
        
        // Send to server
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            hideAIThinkingIndicator();
            return false;
        }
        
        // If we have a file to process and haven't processed it yet, do it in background
        if (capturedFile && !pendingAttachment) {
            console.log('Processing attachment in background:', capturedFile.name);
            // Start upload but don't await - let it run in parallel with message send
            uploadAttachment(capturedFile).then(result => {
                if (result) {
                    pendingAttachment = result;
                    console.log('Background file processing complete');
                }
            }).catch(err => {
                console.error('Background file processing failed:', err);
            });
        }
        
        // Build request body with attachment metadata if present
        let requestBody = `conversationId=${conversationId}&content=${encodeURIComponent(message)}&messageType=Text`;
        
        if (attachmentInfo) {
            requestBody += `&attachmentFileName=${encodeURIComponent(attachmentInfo.fileName)}&attachmentFileSize=${attachmentInfo.fileSize}`;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/SendMessage`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: requestBody
        });
        
        const data = await response.json();
        
        if (data.success) {
            console.log('Message sent successfully');
            // Generate intelligent AI response (will include attachment content if pendingAttachment is set)
            await generateAIResponse(message);
            return true;
        } else {
            console.error('Failed to send message:', data.error);
            hideAIThinkingIndicator();
            return false;
        }
    } catch (error) {
        console.error('Error sending message:', error);
        hideAIThinkingIndicator();
        return false;
    }
}

// Check if a message is meaningful enough to trigger AI processing
function isMeaningfulMessage(content) {
    if (!content || content.trim().length < 10) return false;
    
    const trimmedContent = content.trim().toLowerCase();
    
    // Skip simple greetings and short responses
    const simpleGreetings = [
        'hello', 'hi', 'hey', 'good morning', 'good afternoon', 'good evening',
        'thanks', 'thank you', 'ok', 'okay', 'yes', 'no', 'sure', 'alright',
        'bye', 'goodbye', 'see you', 'later', 'ok bye', 'thanks bye'
    ];
    
    if (simpleGreetings.includes(trimmedContent)) return false;
    
    // Skip messages that are just punctuation or numbers
    if (trimmedContent.split('').every(c => /[^\w\s]/.test(c) || /\d/.test(c))) return false;
    
    return true;
}

// Add user message to chat (with optional attachment indicator)
function addUserMessageToChat(message, attachmentInfo = null) {
    const chatMessages = document.getElementById('chatMessages');
    const welcomeMessage = document.getElementById('welcomeMessage');
    
    // Hide welcome message when first real message arrives
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
    }
    
    // Build attachment HTML if present
    let attachmentHtml = '';
    if (attachmentInfo) {
        const fileSizeKb = (attachmentInfo.fileSize / 1024).toFixed(1);
        attachmentHtml = `
            <div class="message-attachment" style="
                display: flex;
                align-items: center;
                gap: 0.5rem;
                padding: 0.5rem 0.75rem;
                background: rgba(199, 183, 163, 0.15);
                border-radius: 8px;
                margin-bottom: 0.5rem;
                border: 1px solid rgba(199, 183, 163, 0.3);
                max-width: 100%;
                box-sizing: border-box;
                overflow: hidden;
            ">
                <i class="fas fa-file-alt" style="color: #c7b7a3; flex-shrink: 0;"></i>
                <div style="flex: 1; min-width: 0; overflow: hidden;">
                    <div style="font-weight: 500; font-size: 0.875rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;">${attachmentInfo.fileName}</div>
                    <div style="font-size: 0.75rem; color: #6c757d;">${fileSizeKb} KB</div>
                </div>
                <i class="fas fa-check-circle" style="color: #28a745; font-size: 0.875rem; flex-shrink: 0;" title="Content extracted"></i>
            </div>
        `;
    }
    
    const messageDiv = document.createElement('div');
    messageDiv.className = 'user-message-bubble';
    messageDiv.dataset.messageId = messageElements.length;
    messageDiv.innerHTML = `
        <div class="message-content">
            <div class="message-header">
                <span class="timestamp">${new Date().toLocaleTimeString()}</span>
            </div>
            ${attachmentHtml}
            <div class="message-text">${message}</div>
        </div>
    `;
    
    chatMessages.appendChild(messageDiv);
    messageElements.push(messageDiv); // Track all message elements
    userMessageElements.push(messageDiv); // Track only user messages for sticky overlay
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}

// Request clarity
function requestClarity() {
    const messageInput = document.getElementById('messageInput');
    const text = messageInput.value.trim();
    
    if (text && currentConversationId) {
        connection.invoke("RequestClarity", currentConversationId.toString(), text, getCurrentUserType())
            .catch(function (err) {
                console.error("Request Clarity Error: ", err.toString());
            });
    }
}

// Get current user ID (you'll need to implement this based on your auth system)
function getCurrentUserId() {
    // Get from appContext first - THIS IS THE PRIMARY SOURCE
    const appContext = document.getElementById('appContext');
    if (appContext) {
        const userId = appContext.dataset.currentUserId;
        // Don't return "0" which is the default when user is null
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return parseInt(userId);
        }
    }
    
    // Try from specific data-current-user-id attribute (not data-user-id which could be any user)
    const userIdElement = document.querySelector('[data-current-user-id]');
    if (userIdElement && userIdElement.dataset.currentUserId) {
        const userId = userIdElement.dataset.currentUserId;
        if (userId && userId !== "0" && userId !== "null" && userId !== "") {
            return parseInt(userId);
        }
    }
    
    // Fallback to window object
    if (window.currentUserId) {
        return parseInt(window.currentUserId);
    }
    
    // REMOVED DANGEROUS FALLBACK: querySelector('[data-user-id]') was picking up
    // any user element on the page, including client users in scoped views
    
    console.warn('getCurrentUserId: Could not find current user ID from any source');
    return null;
}

// Get current user type
function getCurrentUserType() {
    // This should return the actual user type from your authentication system
    return document.querySelector('[data-user-type]')?.dataset.userType || 'Client';
}

// Get current organization ID from URL or data attribute
function getCurrentOrganizationId() {
    console.log('getCurrentOrganizationId() called');
    console.log('currentOrganizationId cached:', currentOrganizationId);
    
    if (currentOrganizationId) {
        console.log('Returning cached orgId:', currentOrganizationId);
        return currentOrganizationId;
    }
    
    // Try to get from URL path (e.g., /Client/123/Chat/... or /Client/123/Matter)
    console.log('Checking URL path:', window.location.pathname);
    const pathMatch = window.location.pathname.match(/\/Client\/(\d+)\/(?:Dashboard|Chat|Matter|Services|Documents|Teams|Settings|Tasks|Calendar|Communications|History)/);
    if (pathMatch) {
        currentOrganizationId = parseInt(pathMatch[1]);
        console.log('Found orgId from URL:', currentOrganizationId);
        return currentOrganizationId;
    }
    
    // Try to get from /Home/Communications or similar paths - look for organization in ViewBag/ViewContext
    console.log('Checking data-organization-id element...');
    const orgIdElement = document.querySelector('[data-organization-id]');
    if (orgIdElement) {
        const orgId = orgIdElement.dataset.organizationId;
        console.log('Found data-organization-id:', orgId);
        if (orgId && orgId !== '') {
            currentOrganizationId = parseInt(orgId);
            console.log('Parsed orgId from data attribute:', currentOrganizationId);
            return currentOrganizationId;
        }
    }
    
    // Try to get from meta tag
    const metaOrgId = document.querySelector('meta[name="organization-id"]');
    if (metaOrgId) {
        const orgId = metaOrgId.getAttribute('content');
        if (orgId && orgId !== '') {
            currentOrganizationId = parseInt(orgId);
            return currentOrganizationId;
        }
    }
    
    console.error('Could not determine organization ID');
    return null;
}

// SignalR event handlers (if connection exists)
if (connection) {
    connection.on("ReceiveMessage", function (message) {
        addMessageToChat(message);
    });

    connection.on("ReceiveClarity", function (clarity) {
        addClarityToChat(clarity);
    });

    connection.on("Error", function (error) {
        showError(error);
    });
}

// Add message to chat
function addMessageToChat(message) {
    const chatMessages = document.getElementById('chatMessages');
    const welcomeMessage = document.getElementById('welcomeMessage');
    
    // Hide welcome message when first real message arrives
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
    }
    
    // Suppress internal/non-user-facing AI analysis artifacts from the chat stream.
    // These message types are stored for potential future "Insights" use, but should
    // not render as user-visible chat bubbles.
    const suppressedAiMessageTypes = new Set(['AI_Summary', 'AI_Goal', 'AI_Reply']);
    if (message?.isFromAI && suppressedAiMessageTypes.has(message?.messageType)) {
        return;
    }

    const messageDiv = document.createElement('div');
    messageDiv.dataset.messageId = message.id || messageElements.length;
    
    // Get current user name from context
    const appContext = document.getElementById('appContext');
    const currentUserName = appContext?.getAttribute('data-current-user-name') || 'User';
    
    if (message.isFromAI) {
        messageDiv.className = 'ai-message-bubble';
        messageDiv.innerHTML = `
            <div class="message-content">
                <div class="message-header">
                    <img src="/images/Notal_Banner_Logo.png" alt="Notal AI" class="ai-message-logo" />
                    <span class="timestamp">${new Date(message.createdAt).toLocaleTimeString()}</span>
                </div>
                <div class="message-text">
                    ${formatAIMessage(message)}
                </div>
                ${'' /* Sources reserved for future use (see generateSourcesSection) */}
            </div>
        `;
    } else {
        // Parse attachment info from message metadata if present (check both camelCase and PascalCase)
        let attachmentHtml = '';
        const metadataStr = message.metadata || message.Metadata;
        if (metadataStr) {
            try {
                const metadata = JSON.parse(metadataStr);
                if (metadata.attachmentFileName && metadata.attachmentFileSize) {
                    const fileSizeKb = (metadata.attachmentFileSize / 1024).toFixed(1);
                    attachmentHtml = `
                        <div class="message-attachment" style="
                            display: flex;
                            align-items: center;
                            gap: 0.5rem;
                            padding: 0.5rem 0.75rem;
                            background: rgba(61, 16, 25, 0.05);
                            border-radius: 8px;
                            margin-bottom: 0.5rem;
                            border: 1px solid rgba(61, 16, 25, 0.1);
                        ">
                            <i class="fas fa-paperclip" style="color: #3d1019; font-size: 0.875rem;"></i>
                            <div style="flex: 1; min-width: 0; overflow: hidden;">
                                <div style="font-weight: 500; font-size: 0.875rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;">${metadata.attachmentFileName}</div>
                                <div style="font-size: 0.75rem; color: #6b7280;">${fileSizeKb} KB</div>
                            </div>
                        </div>
                    `;
                }
            } catch (e) {
                // Ignore JSON parse errors
            }
        }
        
        messageDiv.className = 'user-message-bubble';
        messageDiv.innerHTML = `
            <div class="message-content">
                <div class="message-header">
                    <span class="sender-name">${currentUserName} <span class="you-label">(You)</span></span>
                    <span class="timestamp">${new Date(message.createdAt).toLocaleTimeString()}</span>
                </div>
                ${attachmentHtml}
                <div class="message-text">${message.content}</div>
            </div>
        `;
        // Track only user messages for sticky overlay
        userMessageElements.push(messageDiv);
    }
    
    chatMessages.appendChild(messageDiv);
    messageElements.push(messageDiv); // Track all message elements
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}

// Initialize sticky message feature
function initializeStickyMessage() {
    const chatMessages = document.getElementById('chatMessages');
    const stickyOverlay = document.getElementById('stickyLastMessage');
    
    console.log('initializeStickyMessage called', {
        chatMessages: !!chatMessages,
        stickyOverlay: !!stickyOverlay,
        totalMessages: messageElements.length,
        userMessages: userMessageElements.length
    });
    
    if (!chatMessages || !stickyOverlay) {
        console.warn('Missing required elements for sticky message');
        return;
    }
    
    if (userMessageElements.length === 0) {
        console.log('No user messages yet, skipping sticky message initialization');
        // Hide the overlay if no user messages
        stickyOverlay.style.display = 'none';
        return;
    }
    
    // Don't show sticky on initial load (user is at bottom)
    // It will appear when they start scrolling up
    stickyMessageIndex = -1;
    stickyOverlay.style.display = 'none';
    
    // Remove any existing scroll listener
    chatMessages.removeEventListener('scroll', handleStickyMessageScroll);
    
    // Add scroll listener for dynamic updates
    chatMessages.addEventListener('scroll', handleStickyMessageScroll);
    
    console.log('✅ Sticky message initialized with', userMessageElements.length, 'user messages');
}

// Update the sticky message overlay content
function updateStickyMessage() {
    const stickyOverlay = document.getElementById('stickyLastMessage');
    const stickyContent = stickyOverlay?.querySelector('.sticky-message-content');
    const chatMessages = document.getElementById('chatMessages');
    
    console.log('updateStickyMessage called', {
        hasOverlay: !!stickyOverlay,
        hasContent: !!stickyContent,
        userMessageCount: userMessageElements.length,
        stickyIndex: stickyMessageIndex
    });
    
    if (!stickyOverlay || !stickyContent || userMessageElements.length === 0) {
        console.warn('Cannot update sticky message - missing elements or no user messages');
        return;
    }
    
    // Calculate actual index (from the end of user messages only)
    const actualIndex = userMessageElements.length - 1 - stickyMessageIndex;
    
    console.log('Calculated actualIndex:', actualIndex, 'from userMessageElements.length:', userMessageElements.length, 'and stickyMessageIndex:', stickyMessageIndex);
    
    // If we've reached beyond the first user message, hide the sticky overlay
    if (actualIndex < 0 || stickyMessageIndex >= userMessageElements.length) {
        console.log('Hiding sticky overlay - out of bounds');
        stickyOverlay.style.display = 'none';
        stickyOverlay.classList.remove('visible');
        return;
    }
    
    const messageElement = userMessageElements[actualIndex];
    console.log('User message element:', messageElement);
    
    // Clone the message content for the sticky overlay
    const clonedMessage = messageElement.cloneNode(true);
    stickyContent.innerHTML = '';
    stickyContent.appendChild(clonedMessage);
    
    // Show the sticky overlay
    console.log('Showing sticky overlay...');
    stickyOverlay.style.display = 'block';
    // Use setTimeout to ensure display change is applied before adding visible class
    setTimeout(() => {
        stickyOverlay.classList.add('visible');
        console.log('✅ Sticky overlay now visible with class');
    }, 10);
    
    console.log('✅ Sticky message updated to show user message at index', actualIndex);
}

// Handle scroll events to update sticky message
function handleStickyMessageScroll() {
    const chatMessages = document.getElementById('chatMessages');
    const stickyOverlay = document.getElementById('stickyLastMessage');
    
    if (!chatMessages || userMessageElements.length === 0) {
        return;
    }
    
    // Find the last user message that is currently scrolled out of view (above viewport)
    let newStickyIndex = -1;
    let messageToShow = null;
    
    for (let i = userMessageElements.length - 1; i >= 0; i--) {
        const userMessage = userMessageElements[i];
        
        if (isMessageAboveViewport(userMessage, chatMessages)) {
            // This message is above the viewport (scrolled past)
            newStickyIndex = userMessageElements.length - 1 - i;
            messageToShow = userMessage;
            break;
        }
    }
    
    // If no message is above viewport, hide sticky
    if (newStickyIndex === -1) {
        if (stickyOverlay) {
            stickyOverlay.style.display = 'none';
            stickyOverlay.classList.remove('visible');
        }
        stickyMessageIndex = -1;
        return;
    }
    
    // IMPORTANT: Hide sticky if the actual message is visible in the sticky area
    // This prevents showing duplicate content when scrolling
    if (messageToShow && isMessageInStickyArea(messageToShow, chatMessages)) {
        if (stickyOverlay) {
            stickyOverlay.style.display = 'none';
            stickyOverlay.classList.remove('visible');
        }
        return;
    }
    
    // Update sticky message if index changed
    if (newStickyIndex !== stickyMessageIndex) {
        stickyMessageIndex = newStickyIndex;
        console.log('Scroll detected, updating sticky to index:', stickyMessageIndex);
        updateStickyMessage();
    }
}

// Check if an element is visible in the viewport of its container
function isElementInViewport(element, container) {
    if (!element || !container) return false;
    
    const elementRect = element.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    
    // Check if the top of the element is visible within the container
    // We add a buffer of 150px to account for the sticky overlay height
    return (
        elementRect.top >= containerRect.top + 150 &&
        elementRect.top <= containerRect.bottom
    );
}

// Check if an element is above (scrolled past) the viewport
function isMessageAboveViewport(element, container) {
    if (!element || !container) return false;
    
    const elementRect = element.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    
    // Message is above viewport if its bottom is above the container's top
    // Add a small buffer (80px) so it switches slightly before completely out of view
    return elementRect.bottom < containerRect.top + 80;
}

// Check if a message is visible in the sticky overlay area (top portion of viewport)
function isMessageInStickyArea(element, container) {
    if (!element || !container) return false;
    
    const elementRect = element.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    
    // Sticky area is roughly the top 100px of the container
    const stickyAreaBottom = containerRect.top + 100;
    
    // Message is in sticky area if any part of it overlaps with this zone
    return elementRect.top < stickyAreaBottom && elementRect.bottom > containerRect.top;
}

// Format AI message based on message type
function formatAIMessage(message) {
    const messageType = message?.messageType || '';
    const rawContent = message?.content || '';

    // Never show internal analysis artifacts as standalone chat messages.
    // (These are not part of the conversational response the user expects.)
    if (messageType === 'AI_Summary' || messageType === 'AI_Goal' || messageType === 'AI_Reply') {
        return '';
    }
    
    // IMPORTANT: Regardless of message type, AI content may contain [ACTION:...] blocks.
    // We always route through formatIntelligentResponse() so action blocks are replaced/stripped
    // during history render and after navigation.
    if (!messageType.startsWith('AI_')) {
        return formatIntelligentResponse(rawContent);
    }
    
    // Handle AI_Response type directly (it's plain text, not JSON)
    if (messageType === 'AI_Response') {
        return formatIntelligentResponse(rawContent);
    }
    
    try {
        const aiData = JSON.parse(rawContent);
        
        let formatted = '';
        switch (messageType) {
            case 'AI_Summary':
                formatted = formatSummaryMessage(aiData);
                break;
            case 'AI_Goal':
                formatted = formatGoalMessage(aiData);
                break;
            case 'AI_Reply':
                formatted = formatReplyMessage(aiData);
                break;
            case 'AI_Clarity':
                formatted = formatClarityMessage(aiData);
                break;
            default:
                formatted = `<div class="ai-result"><pre>${cleanNewlines(rawContent)}</pre></div>`;
                break;
        }
        
        // Sanitize/replace any action blocks that might have been included
        return formatIntelligentResponse(formatted);
    } catch (e) {
        // If JSON parsing fails, try to format as intelligent response
        return formatIntelligentResponse(rawContent);
    }
}

// Format conversation summary
function formatSummaryMessage(data) {
    // Removed from user-visible chat/UX (not currently functional).
    return '';
}

// Format client goals
function formatGoalMessage(data) {
    // Removed from user-visible chat/UX (not currently functional).
    return '';
}

// Format reply suggestions
function formatReplyMessage(data) {
    // Removed from user-visible chat/UX (not currently functional).
    return '';
}

// Format clarity explanation
function formatClarityMessage(data) {
    return `
        <div class="ai-clarity">
            <h6><i class="fas fa-question-circle"></i> Clarity Explanation</h6>
            <p><strong>Original:</strong> ${data.OriginalText || 'No text provided'}</p>
            <p><strong>Simplified:</strong> ${data.SimplifiedExplanation || 'No explanation available'}</p>
            ${data.KeyTerms && data.KeyTerms.length > 0 ? `
                <div class="key-terms">
                    <strong>Key Terms:</strong>
                    ${data.KeyTerms.map(term => `<span class="badge bg-info me-1">${term}</span>`).join('')}
                </div>
            ` : ''}
            ${data.Implications && data.Implications.length > 0 ? `
                <div class="implications">
                    <strong>Implications:</strong>
                    <ul>
                        ${data.Implications.map(impl => `<li>${impl}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
            <p><strong>Risk Level:</strong> <span class="badge bg-${getRiskColor(data.RiskLevel)}">${data.RiskLevel || 'Unknown'}</span></p>
        </div>
    `;
}

// Format intelligent AI response
function formatIntelligentResponse(content) {
    if (!content) return content;

    // Render markdown first (unless already HTML), then ALWAYS sanitize/replace any [ACTION:...] blocks.
    // This ensures raw commands never appear:
    // - during reload/history render
    // - during navigation
    // - even if agent-actions.js hasn't initialized yet
    const hasHtmlTags = /<[^>]+>/.test(content);
    let html = hasHtmlTags ? content : convertMarkdownToHtml(content);

    // Replace complete action blocks with an in-message action container (Cursor-style) if available,
    // otherwise strip them entirely. Also strip any trailing partial block as a safety net.
    if (typeof window.AgentActions !== 'undefined' && window.AgentActions.replaceWithCards) {
        html = window.AgentActions.replaceWithCards(html);
    } else {
        html = html.replace(/\[ACTION:\w+\][\s\S]*?\[\/ACTION\]/g, '');
    }
    html = html.replace(/\[ACTION:[\s\S]*$/g, '').trim();

    // Check if this is a service unavailable message
    if (!hasHtmlTags && content.includes("AI Services Temporarily Unavailable")) {
        return html;
    }

    return html;
}

// Convert markdown to HTML
function convertMarkdownToHtml(text) {
    if (!text) return text;
    
    // First, remove code block wrappers if the entire content is wrapped in one
    let cleanedText = text.trim();
    
    // Try to match and remove wrapping code blocks (```html, ```text, ```markdown, or just ```)
    const codeBlockMatch = cleanedText.match(/^```(?:html|text|markdown|xml)?\s*\n?([\s\S]*?)\n?```$/);
    if (codeBlockMatch) {
        cleanedText = codeBlockMatch[1].trim();
    }
    
    // Store code blocks temporarily to prevent processing their contents
    const codeBlocks = [];
    let codeBlockIndex = 0;
    
    // Replace code blocks with placeholders
    cleanedText = cleanedText.replace(/```(\w+)?\n?([\s\S]*?)```/g, (match, lang, code) => {
        const placeholder = `__CODE_BLOCK_${codeBlockIndex}__`;
        codeBlocks.push({ lang: lang || 'text', code: code.trim() });
        codeBlockIndex++;
        return placeholder;
    });
    
    // Replace inline code with placeholders
    const inlineCodes = [];
    let inlineCodeIndex = 0;
    cleanedText = cleanedText.replace(/`([^`]+)`/g, (match, code) => {
        const placeholder = `__INLINE_CODE_${inlineCodeIndex}__`;
        inlineCodes.push(code);
        inlineCodeIndex++;
        return placeholder;
    });
    
    // Process the rest of the markdown
    let processed = cleanedText
        // First, aggressively handle all forms of newlines
        .replace(/\\n/g, '\n')  // Convert \n to actual newlines
        .replace(/\\r\\n/g, '\n')  // Convert \r\n to newlines
        .replace(/\\r/g, '\n')  // Convert \r to newlines
        // Convert **bold** to <strong>bold</strong>
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        // Convert *italic* to <em>italic</em>
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        // Convert escaped quotes
        .replace(/\\"/g, '"');
    
    // Process lists separately to avoid br tags interfering
    // Split by double newlines to identify blocks
    const lines = processed.split('\n');
    const processedLines = [];
    let inList = false;
    let listItems = [];
    let listType = null; // 'ul' or 'ol'
    
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i].trim();
        const bulletMatch = line.match(/^[•\-]\s+(.*)$/);
        const numberMatch = line.match(/^\d+\.\s+(.*)$/);
        
        if (bulletMatch) {
            if (!inList || listType !== 'ul') {
                // Starting a new unordered list
                if (inList) {
                    // Close previous list
                    processedLines.push(`<${listType}>${listItems.join('')}</${listType}>`);
                    listItems = [];
                }
                inList = true;
                listType = 'ul';
            }
            listItems.push(`<li>${bulletMatch[1]}</li>`);
        } else if (numberMatch) {
            if (!inList || listType !== 'ol') {
                // Starting a new ordered list
                if (inList) {
                    // Close previous list
                    processedLines.push(`<${listType}>${listItems.join('')}</${listType}>`);
                    listItems = [];
                }
                inList = true;
                listType = 'ol';
            }
            listItems.push(`<li>${numberMatch[1]}</li>`);
        } else {
            // Not a list item
            if (inList) {
                // Close the list
                processedLines.push(`<${listType}>${listItems.join('')}</${listType}>`);
                listItems = [];
                inList = false;
                listType = null;
            }
            // Add the line as-is (will be converted to br later if not empty)
            if (line) {
                processedLines.push(line);
            } else if (processedLines.length > 0) {
                // Empty line becomes a break
                processedLines.push('<br>');
            }
        }
    }
    
    // Close any remaining list
    if (inList && listItems.length > 0) {
        processedLines.push(`<${listType}>${listItems.join('')}</${listType}>`);
    }
    
    // Join lines and clean up
    processed = processedLines.join('\n')
        // Clean up multiple consecutive br tags
        .replace(/(<br\s*\/?>){3,}/g, '<br><br>')
        // Convert double line breaks to paragraph breaks
        .replace(/(<br\s*\/?>){2,}/g, '</p><p>')
        // Wrap in paragraph tags
        .replace(/^(.+)$/s, '<p>$1</p>')
        // Clean up empty paragraphs
        .replace(/<p><br\s*\/?><\/p>/g, '')
        .replace(/<p><\/p>/g, '')
        // Remove br tags immediately before/after lists
        .replace(/<br\s*\/?>\s*<(ul|ol)>/g, '<$1>')
        .replace(/<\/(ul|ol)>\s*<br\s*\/?>/g, '</$1>');
    
    // Restore inline code
    inlineCodes.forEach((code, index) => {
        processed = processed.replace(`__INLINE_CODE_${index}__`, `<code>${escapeHtml(code)}</code>`);
    });
    
    // Restore code blocks
    codeBlocks.forEach((block, index) => {
        const codeHtml = `<pre><code class="language-${escapeHtml(block.lang)}">${escapeHtml(block.code)}</code></pre>`;
        processed = processed.replace(`__CODE_BLOCK_${index}__`, codeHtml);
    });
    
    return processed;
}

// Helper function to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Generate sources section for AI responses
function generateSourcesSection() {
    const sources = [
        'Legal Documentation',
        'Help Center: Legal Services',
        'FAQs: Legal Questions'
    ];
    
    // Keep for future use, but do not show to users yet.
    // Returning an HTML comment ensures it is not visible and does not affect layout.
    return `<!-- Sources (reserved for future use)
${sources.map(s => `- ${s}`).join('\n')}
-->`;
}

// Add clarity to chat
function addClarityToChat(clarity) {
    const chatMessages = document.getElementById('chatMessages');
    const welcomeMessage = document.getElementById('welcomeMessage');
    
    // Hide welcome message when first real message arrives
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
    }
    
    const clarityDiv = document.createElement('div');
    clarityDiv.className = 'ai-message-bubble clarity-message';
    clarityDiv.innerHTML = `
        <div class="message-content">
            <div class="message-header">
                <span class="timestamp">${new Date().toLocaleTimeString()}</span>
            </div>
            <div class="message-text">
                <div class="clarity-explanation">
                    <h6>Clarity Explanation:</h6>
                    <p><strong>Original:</strong> ${clarity.originalText}</p>
                    <p><strong>Explanation:</strong> ${clarity.explanation}</p>
                </div>
            </div>
        </div>
    `;
    
    chatMessages.appendChild(clarityDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}

// Show error
function showError(error) {
    const errorDiv = document.createElement('div');
    errorDiv.className = 'alert alert-danger alert-dismissible fade show';
    errorDiv.innerHTML = `
        ${error}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    document.querySelector('.chat-container').insertBefore(errorDiv, document.getElementById('chatMessages'));
}

// AI Agent interactions
function showAIAgent(agentType) {
    const agentCard = document.getElementById(agentType + 'Agent');
    if (agentCard) {
        agentCard.classList.add('active');
        // Add logic to show agent results or trigger agent processing
    }
}

// Export chat
document.getElementById('exportChat')?.addEventListener('click', function() {
    const messages = document.querySelectorAll('.message');
    let exportText = 'Chat Export\n==========\n\n';
    
    messages.forEach(message => {
        const userType = message.querySelector('.user-type').textContent;
        const timestamp = message.querySelector('.timestamp').textContent;
        const content = message.querySelector('.message-content').textContent;
        
        exportText += `[${timestamp}] ${userType}: ${content}\n\n`;
    });
    
    const blob = new Blob([exportText], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `chat-export-${currentConversationId}-${new Date().toISOString().split('T')[0]}.txt`;
    a.click();
    window.URL.revokeObjectURL(url);
});

// AI Functionality
function processAIInsights(messages) {
    const aiInsights = {
        summary: null,
        goals: null,
        suggestions: null
    };
    
    // Extract AI messages
    messages.forEach(message => {
        if (message.isFromAI) {
            try {
                const aiData = JSON.parse(message.content);
                switch (message.messageType) {
                    case 'AI_Summary':
                        aiInsights.summary = aiData;
                        break;
                    case 'AI_Goal':
                        aiInsights.goals = aiData;
                        break;
                    case 'AI_Reply':
                        aiInsights.suggestions = aiData;
                        break;
                }
            } catch (e) {
                console.error('Error parsing AI data:', e);
            }
        }
    });
    
    displayAIInsights(aiInsights);
}

function displayAIInsights(insights) {
    const insightsPanel = document.getElementById('aiInsightsPanel');
    const insightsContent = document.getElementById('aiInsightsContent');
    
    // Removed from user-visible UX (not currently functional).
    if (insightsPanel) insightsPanel.style.display = 'none';
    if (insightsContent) insightsContent.innerHTML = '';
}

function getSentimentColor(sentiment) {
    switch (sentiment.toLowerCase()) {
        case 'positive': return 'success';
        case 'negative': return 'danger';
        case 'neutral': return 'secondary';
        default: return 'secondary';
    }
}

function getUrgencyColor(urgency) {
    switch (urgency.toLowerCase()) {
        case 'urgent': return 'danger';
        case 'high': return 'warning';
        case 'medium': return 'info';
        case 'low': return 'success';
        default: return 'secondary';
    }
}

function toggleAIInsights() {
    const insightsPanel = document.getElementById('aiInsightsPanel');
    const mainChatArea = document.getElementById('mainChatArea');
    const button = document.getElementById('toggleInsights');
    
    if (insightsPanel && mainChatArea) {
        insightsPanel.classList.toggle('d-none');
        insightsVisible = !insightsVisible;
        
        // Adjust main chat area width
        if (insightsVisible) {
            mainChatArea.classList.remove('col-md-9');
            mainChatArea.classList.add('col-md-6');
            button.innerHTML = '<i class="fas fa-brain"></i> Hide Insights';
        } else {
            mainChatArea.classList.remove('col-md-6');
            mainChatArea.classList.add('col-md-9');
            button.innerHTML = '<i class="fas fa-brain"></i> AI Insights';
        }
        
        // Load insights if showing for the first time
        if (insightsVisible && !aiInsights) {
            loadAIInsights();
        }
    }
}

function openClarityModal() {
    const modal = new bootstrap.Modal(document.getElementById('clarityModal'));
    modal.show();
}

function openSuggestionsModal() {
    const modal = new bootstrap.Modal(document.getElementById('suggestionsModal'));
    modal.show();
    
    // Load suggestions
    loadReplySuggestions();
}

async function processClarityRequest() {
    const text = document.getElementById('clarityText').value.trim();
    if (!text) return;
    
    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/RequestClarity`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                conversationId: parseInt(currentConversationId),
                text: text,
                userType: getCurrentUserType()
            })
        });
        
        const result = await response.json();
        displayClarityResult(result);
        
    } catch (error) {
        console.error('Error requesting clarity:', error);
    }
}

function displayClarityResult(result) {
    document.getElementById('clarityExplanation').textContent = result.simplifiedExplanation;
    document.getElementById('clarityKeyTerms').innerHTML = result.keyTerms.map(term => `<span class="badge bg-info me-1">${term}</span>`).join('');
    document.getElementById('clarityImplications').innerHTML = result.implications.map(impl => `<li>${impl}</li>`).join('');
    
    const riskBadge = document.getElementById('clarityRiskLevel');
    riskBadge.textContent = result.riskLevel;
    riskBadge.className = `badge bg-${getRiskColor(result.riskLevel)}`;
    
    document.getElementById('clarityResult').style.display = 'block';
}

function getRiskColor(risk) {
    switch (risk.toLowerCase()) {
        case 'high': return 'danger';
        case 'medium': return 'warning';
        case 'low': return 'success';
        default: return 'secondary';
    }
}

async function loadReplySuggestions() {
    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GetSuggestions`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                conversationId: parseInt(currentConversationId),
                userType: getCurrentUserType()
            })
        });
        
        const suggestions = await response.json();
        displayReplySuggestions(suggestions);
        
    } catch (error) {
        console.error('Error loading suggestions:', error);
        document.getElementById('suggestionsContent').innerHTML = '<div class="alert alert-danger">Error loading suggestions</div>';
    }
}

function displayReplySuggestions(suggestions) {
    const content = document.getElementById('suggestionsContent');
    
    content.innerHTML = `
        <div class="suggestion-card">
            <h6><i class="fas fa-lightbulb"></i> Suggested Reply</h6>
            <div class="suggestion-meta">
                <span class="badge bg-${getToneColor(suggestions.tone)}">${suggestions.tone}</span>
                <span class="badge bg-info">${suggestions.purpose}</span>
                ${suggestions.requiresLegalReview ? '<span class="badge bg-warning">Requires Legal Review</span>' : ''}
            </div>
            <div class="suggestion-text">${suggestions.suggestedReply}</div>
            <div class="suggestion-keypoints">
                <h6>Key Points:</h6>
                <ul>
                    ${suggestions.keyPoints.map(point => `<li>${point}</li>`).join('')}
                </ul>
            </div>
        </div>
    `;
    
    document.getElementById('useSuggestion').style.display = 'inline-block';
    document.getElementById('useSuggestion').onclick = () => useSuggestion(suggestions.suggestedReply);
}

function getToneColor(tone) {
    switch (tone.toLowerCase()) {
        case 'professional': return 'primary';
        case 'friendly': return 'success';
        case 'formal': return 'dark';
        case 'casual': return 'secondary';
        default: return 'secondary';
    }
}

function useSuggestion(suggestion) {
    document.getElementById('messageInput').value = suggestion;
    bootstrap.Modal.getInstance(document.getElementById('suggestionsModal')).hide();
}

// Mobile Navigation
function toggleSidebar() {
    const sidebar = document.querySelector('.sidebar');
    if (sidebar) {
        sidebar.classList.toggle('show');
    }
}

// Close sidebar when clicking outside on mobile
document.addEventListener('click', function(event) {
    const sidebar = document.querySelector('.sidebar');
    const menuButton = document.getElementById('menuButton');
    
    if (window.innerWidth <= 768 && sidebar && sidebar.classList.contains('show')) {
        if (!sidebar.contains(event.target) && !menuButton.contains(event.target)) {
            sidebar.classList.remove('show');
        }
    }
});

// Handle window resize
window.addEventListener('resize', function() {
    const sidebar = document.querySelector('.sidebar');
    if (window.innerWidth > 768 && sidebar) {
        sidebar.classList.remove('show');
    }
});

// AI Thinking Indicator Functions
function showAIThinkingIndicator() {
    const chatMessages = document.getElementById('chatMessages');
    const thinkingDiv = document.createElement('div');
    thinkingDiv.id = 'ai-thinking-indicator';
    thinkingDiv.className = 'ai-message-bubble thinking-message';
    thinkingDiv.innerHTML = `
        <div class="message-avatar">
            <div class="avatar-icon">C</div>
        </div>
        <div class="message-content">
            <div class="message-header">
                <span class="sender-name">Certio</span>
                <span class="ai-badge">AI Agent</span>
                <span class="timestamp">${new Date().toLocaleTimeString()}</span>
            </div>
            <div class="message-text">
                <div class="thinking-indicator">
                    <div class="thinking-dots">
                        <span></span>
                        <span></span>
                        <span></span>
                    </div>
                    <span class="thinking-text">AI is thinking...</span>
                </div>
            </div>
        </div>
    `;
    
    chatMessages.appendChild(thinkingDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}

function hideAIThinkingIndicator() {
    const thinkingDiv = document.getElementById('ai-thinking-indicator');
    if (thinkingDiv) {
        thinkingDiv.remove();
    }
}

// Removed polling mechanism - now using direct response handling

// Process AI insights from messages
function processAIInsights(messages) {
    const aiMessages = messages.filter(msg => msg.isFromAI && msg.messageType.startsWith('AI_'));
    if (aiMessages.length > 0) {
        // Update insights if panel is visible
        if (insightsVisible) {
            loadAIInsights();
        }
    }
}

// Load AI insights for current conversation
async function loadAIInsights() {
    if (!currentConversationId) {
        console.log('No conversation selected');
        return;
    }

    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GetAIInsights?conversationId=${currentConversationId}`);
        const data = await response.json();
        
        if (data.success) {
            aiInsights = data.insights;
            displayAIInsights(aiInsights);
        } else {
            console.error('Failed to load AI insights:', data.error);
            showNotification('Failed to load AI insights', 'error');
        }
    } catch (error) {
        console.error('Error loading AI insights:', error);
        showNotification('Error loading AI insights', 'error');
    }
}

// Display AI insights in the panel
function displayAIInsights(insights) {
    const insightsContainer = document.getElementById('aiInsightsContent');
    if (!insightsContainer) return;

    // Removed from user-visible UX (not currently functional).
    const insightsPanel = document.getElementById('aiInsightsPanel');
    if (insightsPanel) insightsPanel.style.display = 'none';
    insightsContainer.innerHTML = '';
}

// Helper functions for styling
function getSentimentColor(sentiment) {
    switch (sentiment?.toLowerCase()) {
        case 'positive': return 'success';
        case 'negative': return 'danger';
        case 'urgent': return 'warning';
        default: return 'secondary';
    }
}

function getUrgencyColor(urgency) {
    switch (urgency?.toLowerCase()) {
        case 'urgent': return 'danger';
        case 'high': return 'warning';
        case 'medium': return 'info';
        case 'low': return 'success';
        default: return 'secondary';
    }
}

// Enhanced AI thinking indicator with intelligent messages
function showIntelligentAIThinkingIndicator(userMessage) {
    const chatMessages = document.getElementById('chatMessages');
    if (!chatMessages) return;
    
    // Determine thinking message based on user input
    let thinkingMessage = "AI is thinking...";
    const messageLower = userMessage.toLowerCase();
    
    // Check if there's a pending attachment or if message is about documents/files
    if (pendingAttachment || window._uploadingAttachment || 
        messageLower.includes("document") || messageLower.includes("file") || 
        messageLower.includes("summarize") || messageLower.includes("analyze") ||
        messageLower.includes("pdf") || messageLower.includes("attachment")) {
        thinkingMessage = "Analyzing document...";
    } else if (messageLower.includes("document review") || messageLower.includes("review")) {
        thinkingMessage = "Analyzing document requirements...";
    } else if (messageLower.includes("contract") || messageLower.includes("agreement")) {
        thinkingMessage = "Reviewing contract details...";
    } else if (messageLower.includes("hello") || messageLower.includes("hi") || messageLower.includes("hey")) {
        thinkingMessage = "Preparing personalized greeting...";
    } else if (messageLower.includes("help") || messageLower.includes("assistance")) {
        thinkingMessage = "Identifying best assistance approach...";
    } else if (messageLower.includes("legal") || messageLower.includes("law")) {
        thinkingMessage = "Processing legal inquiry...";
    } else if (messageLower.includes("question") || messageLower.includes("?")) {
        thinkingMessage = "Formulating detailed response...";
    }
    
    const thinkingDiv = document.createElement('div');
    thinkingDiv.id = 'ai-thinking-indicator';
    thinkingDiv.className = 'message ai-message';
    thinkingDiv.innerHTML = `
        <div class="message-header">
            <img src="/images/Notal_Banner_Logo.png" alt="Notal AI" class="ai-message-logo" />
            <span class="timestamp">${new Date().toLocaleTimeString()}</span>
        </div>
        <div class="message-content">
            <div class="ai-thinking">
                <div class="thinking-spinner">
                    <div class="spinner-border spinner-border-sm" role="status">
                        <span class="sr-only">Loading...</span>
                    </div>
                </div>
                <span class="thinking-text" id="thinkingText">${thinkingMessage}</span>
            </div>
        </div>
    `;
    
    chatMessages.appendChild(thinkingDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
    
    // Apply wave animation to thinking text
    const thinkingTextElement = thinkingDiv.querySelector('#thinkingText');
    if (thinkingTextElement) {
        const text = thinkingTextElement.textContent;
        thinkingTextElement.innerHTML = text.split('').map((char, index) => {
            const delay = index * 0.05; // 50ms delay between each character
            return `<span style="animation-delay: ${delay}s;">${char === ' ' ? '&nbsp;' : char}</span>`;
        }).join('');
    }
}

// Enhanced message display for intelligent AI responses
function addIntelligentMessageToChat(message) {
    const chatMessages = document.getElementById('chatMessages');
    if (!chatMessages) return;
    
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${message.isFromAI ? 'ai-message intelligent-ai' : 'user-message'}`;
    messageDiv.setAttribute('data-message-type', message.messageType);
    
    let content = message.content;
    
    // Enhance AI messages with better formatting
    if (message.isFromAI && message.aiAgentType === 'Notal AI') {
        // Add intelligent AI styling
        messageDiv.classList.add('intelligent-response');
        
        // Format content with better structure
        content = formatIntelligentResponse(content);
    }
    
    messageDiv.innerHTML = `
        <div class="message-header">
            <span class="user-type">${message.userType}</span>
            ${message.isFromAI ? `<span class="ai-agent">${message.aiAgentType}</span>` : ''}
            <span class="timestamp">${new Date(message.createdAt).toLocaleTimeString()}</span>
        </div>
        <div class="message-content">
            ${content}
        </div>
    `;
    
    chatMessages.appendChild(messageDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}


// Function to properly convert newline characters for display
function cleanNewlines(text) {
    if (!text) return text;
    
    // Convert literal newline characters to actual newlines
    return text
        .replace(/\\n/g, '\n')  // Convert \n to actual newlines
        .replace(/\\r\\n/g, '\n')  // Convert \r\n to newlines
        .replace(/\\r/g, '\n')  // Convert \r to newlines
        .replace(/\n\n+/g, '\n\n')  // Clean up multiple consecutive newlines
        .trim();  // Remove leading/trailing whitespace
}

// Nuclear option - properly convert newline artifacts to actual newlines
function nuclearCleanText(text) {
    if (!text) return text;
    
    return text
        .replace(/\\n/g, '\n')  // Convert \n to actual newlines
        .replace(/\\r\\n/g, '\n')  // Convert \r\n to newlines
        .replace(/\\r/g, '\n')  // Convert \r to newlines
        .replace(/\n\n+/g, '\n\n')  // Clean up multiple consecutive newlines
        .trim();
}

// Debug function to help troubleshoot formatting issues
function debugMessageFormatting(content) {
    console.log('Original content:', content);
    console.log('Has \\n:', content.includes('\\n'));
    console.log('Has actual newlines:', content.includes('\n'));
    
    const converted = convertMarkdownToHtml(content);
    console.log('Converted content:', converted);
    
    return converted;
}

// Test function you can call from browser console
function testNewlineFix() {
    const testText = "Hello! I understand your concerns regarding the audit and whether it might be a precursor to your termination. While I cannot speculate on your employer's intentions, I can provide some insights that may help you navigate this situation.\\n\\n\\nThe purpose of a SOC certification audit is primarily to evaluate a company's adherence to data security and compliance standards. It is not inherently linked to employee performance or terminations. However, there are a few considerations to keep in mind:\\n\\n\\n\\n• Performance Evaluation: Sometimes, audits can lead to a broader review of an employee's performance and adherence to company policies. If there are areas of concern identified during the audit related to your work, it could prompt further evaluation.\\n\\n• Compliance Issues: If the audit uncovers compliance violations or data security risks associated with your actions, it may...";
    
    console.log('Testing newline fix...');
    console.log('Original text:', testText);
    const result = formatIntelligentResponse(testText);
    console.log('Formatted result:', result);
    return result;
}


// Function to center action buttons within chat panel
function centerActionButtons(chatPanelWidth) {
    const actionButtonsContainer = document.getElementById('actionButtonsContainer');
    if (actionButtonsContainer) {
        // Calculate center position: chat panel width / 2 - container width / 2
        const containerWidth = actionButtonsContainer.offsetWidth;
        const centerPosition = chatPanelWidth - (chatPanelWidth / 2) - (containerWidth / 2);
        actionButtonsContainer.style.right = centerPosition + 'px';
    }
}

// Right sidebar width (AI / Comms / Notifications) - keep ONE shared width so the resize handle
// stays consistent when switching between the 3 sidebars.
const RIGHT_SIDEBAR_WIDTH_KEY = 'rightSidebarWidth';

function getSavedRightSidebarWidth() {
    // Back-compat: older builds stored AI chat as chatPanelWidth and comms/notifications as notificationsPanelWidth
    const raw =
        localStorage.getItem(RIGHT_SIDEBAR_WIDTH_KEY) ||
        localStorage.getItem('chatPanelWidth') ||
        localStorage.getItem('notificationsPanelWidth') ||
        '320';

    const n = parseInt(raw, 10);
    return Number.isFinite(n) ? n : 320;
}

function setSavedRightSidebarWidth(width) {
    localStorage.setItem(RIGHT_SIDEBAR_WIDTH_KEY, String(width));
    // Also write legacy keys so any older code paths still behave predictably
    localStorage.setItem('chatPanelWidth', String(width));
    localStorage.setItem('notificationsPanelWidth', String(width));
}

// Chat Layout Functions
function initializeChatLayout() {
    // Initialize resize functionality (this restores resize handle position based on active sidebar)
    initializeResizeHandle();
    
    // Only apply AI chat-specific sizing if AI chat is the active sidebar
    const activeSidebar = localStorage.getItem('activeSidebar');
    if (activeSidebar !== 'ai') {
        // Don't apply AI chat sizing if another sidebar is active
        return;
    }
    
    // Set initial chat panel width from localStorage or default
    const chatPanelWidth = getSavedRightSidebarWidth();
    
    const chatPanel = document.getElementById('chatPanel');
    const mainContent = document.querySelector('.main-content');
    const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
    const resizeHandle = document.getElementById('resizeHandle');
    
    if (chatPanel) {
        chatPanel.style.width = chatPanelWidth + 'px';
    }
    
    if (resizeHandle) {
        resizeHandle.style.right = (chatPanelWidth - 12) + 'px';
    }
    
    // Adjust main content wrapper to account for chat panel
    if (mainContentWrapper) {
        mainContentWrapper.style.right = chatPanelWidth + 'px';
    }
    
    // Adjust floating timer overlay position.
    // IMPORTANT: Do NOT set `right` when the timer is positioned via `left/top` (default or user-dragged),
    // otherwise the fixed element becomes over-constrained (left + right) and stretches across the screen,
    // creating an invisible draggable hitbox.
    const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
    if (floatingTimerOverlay) {
        const hasSavedManualPosition = !!localStorage.getItem('timerPosition');
        const hasExplicitLeft = (floatingTimerOverlay.style.left || '').trim().length > 0;

        if (!hasSavedManualPosition && !hasExplicitLeft) {
            floatingTimerOverlay.style.right = (chatPanelWidth + 24) + 'px'; // Add 24px for spacing
        }
    }
    
    // Center action buttons within chat panel
    centerActionButtons(chatPanelWidth);
    
    // Also set the initial header width
    const topHeader = document.querySelector('.top-header');
    if (topHeader) {
        topHeader.style.marginRight = chatPanelWidth + 'px';
    }
}

// Load AI-only conversations for the global chat panel
async function loadAIConversationsForPanel(forceReload = false) {
    console.log('Loading conversations for panel...', { forceReload });
    const tabList = document.querySelector('.tab-list');
    if (!tabList) {
        console.error('Tab list not found');
        return;
    }
    
    // Check if we have actual conversation tabs (not just the loading indicator)
    // Skip this check if tabList is explicitly empty (cleared for refresh) or if we're forcing a reload
    const hasConversationTabs = tabList.querySelector('.conversation-tab') !== null;
    const isEmpty = tabList.innerHTML.trim() === '';
    if (hasConversationTabs && !isEmpty && !forceReload) {
        console.log('Conversations already loaded, skipping reload');
        return;
    }
    
    console.log('Loading AI conversations...');

    try {
        const orgId = getCurrentOrganizationId();
        console.log('getCurrentOrganizationId() returned:', orgId);
        console.log('Current URL:', window.location.pathname);
        console.log('data-organization-id element:', document.querySelector('[data-organization-id]'));
        
        if (!orgId) {
            console.error('Organization ID not found');
            // Clear loading indicator and show message
            tabList.innerHTML = '<div style="padding: 1rem; text-align: center; color: #9ca3af; font-size: 0.875rem;">No organization selected</div>';
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GetAIConversations`);
        if (!response.ok) {
            console.log('No conversations endpoint available, using fallback');
            // Clear loading indicator
            tabList.innerHTML = '<div style="padding: 1rem; text-align: center; color: #9ca3af; font-size: 0.875rem;">No AI conversations yet</div>';
            return;
        }
        
        const conversations = await response.json();
        console.log('Loaded conversations:', conversations);
        
        // Clear loading indicator and populate conversations
        tabList.innerHTML = '';
        
        if (conversations && conversations.length > 0) {
            conversations.forEach(conversation => {
                const tab = document.createElement('div');
                tab.className = 'conversation-tab';
                tab.dataset.conversationId = conversation.id.toString();
                tab.title = conversation.title;
                tab.innerHTML = `
                    <span class="tab-title">${conversation.title}</span>
                    <button class="tab-close" title="Delete conversation">
                        <i class="fas fa-times"></i>
                    </button>
                `;
                
                // Add delete handler
                const deleteBtn = tab.querySelector('.tab-close');
                if (deleteBtn) {
                    deleteBtn.addEventListener('click', function(e) {
                        e.stopPropagation();
                        e.preventDefault();
                        console.log('Delete button clicked for conversation:', conversation.id);
                        deleteConversation(conversation.id.toString());
                    });
                }
                
                tabList.appendChild(tab);
            });
            
            // Restore the last selected conversation after loading
            restoreSelectedConversation();
        } else {
            // No conversations found
            tabList.innerHTML = '<div style="padding: 1rem; text-align: center; color: #9ca3af; font-size: 0.875rem;">No AI conversations yet</div>';
        }
    } catch (error) {
        console.error('Error loading conversations:', error);
        // Clear loading indicator and show error
        tabList.innerHTML = '<div style="padding: 1rem; text-align: center; color: #ef4444; font-size: 0.875rem;">Failed to load conversations</div>';
    }
}

// Note: creation of new conversations should be explicit (e.g., via modal) and not auto-run globally

// Force refresh conversations (useful after creating new conversations)
function refreshConversations() {
    const tabList = document.querySelector('.tab-list');
    if (tabList) {
        // Clear all tabs to force reload
        tabList.innerHTML = '';
    }
    // Call the correct function to reload AI conversations
    loadAIConversationsForPanel();
}

function initializeResizeHandle() {
    const resizeHandle = document.getElementById('resizeHandle');
    const chatPanel = document.getElementById('chatPanel');
    const commsSidebarPanel = document.getElementById('commsSidebarPanel');
    const notificationsPanel = document.getElementById('notificationsSidebarPanel');
    const mainContent = document.querySelector('.main-content');
    const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
    const topHeader = document.querySelector('.top-header');
    
    if (!resizeHandle || !mainContentWrapper) return;
    
    // Restore resize handle position on page load based on active sidebar
    const activeSidebar = localStorage.getItem('activeSidebar');
    if (activeSidebar === 'ai') {
        const savedWidth = getSavedRightSidebarWidth();
        resizeHandle.style.right = (savedWidth - 12) + 'px';
        resizeHandle.style.display = 'flex';
    } else if (activeSidebar === 'notifications') {
        const savedWidth = getSavedRightSidebarWidth();
        resizeHandle.style.right = (savedWidth - 12) + 'px';
        resizeHandle.style.display = 'flex';
    } else if (activeSidebar === 'comms') {
        const savedWidth = getSavedRightSidebarWidth();
        resizeHandle.style.right = (savedWidth - 12) + 'px';
        resizeHandle.style.display = 'flex';
    }
    
    let isResizing = false;
    let startX = 0;
    let startWidth = 0;
    
    resizeHandle.addEventListener('mousedown', function(e) {
        isResizing = true;
        startX = e.clientX;
        
        // Disable transitions during resize for smooth performance
        if (mainContentWrapper) {
            mainContentWrapper.style.transition = 'none';
        }
        
        // Determine which panel is active
        const sidebarTarget = window.currentSidebarTarget;
        let activePanel;
        if (sidebarTarget === 'comms') {
            activePanel = commsSidebarPanel;
        } else if (sidebarTarget === 'notifications') {
            activePanel = notificationsPanel;
        } else {
            activePanel = chatPanel;
        }
        
        if (activePanel) {
            startWidth = parseInt(window.getComputedStyle(activePanel).width, 10);
            activePanel.style.transition = 'none';
        }
        
        if (resizeHandle) {
            resizeHandle.style.transition = 'none';
        }
        
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        
        e.preventDefault();
    });
    
    document.addEventListener('mousemove', function(e) {
        if (!isResizing) return;
        
        const newWidth = startWidth - (e.clientX - startX);
        const minWidth = 300;
        const maxWidth = 600;
        
        if (newWidth >= minWidth && newWidth <= maxWidth) {
            // Determine which panel is active
            const sidebarTarget = window.currentSidebarTarget;
            let activePanel;
            
            if (sidebarTarget === 'comms') {
                activePanel = commsSidebarPanel;
            } else if (sidebarTarget === 'notifications') {
                activePanel = notificationsPanel;
            } else {
                activePanel = chatPanel;
            }
            
            if (activePanel) {
                activePanel.style.width = newWidth + 'px';
            }
            
            // Update resize handle position (left edge aligns with main content right edge)
            resizeHandle.style.right = (newWidth - 12) + 'px';
            
            // Update main content wrapper to make space for sidebar
            if (mainContentWrapper) {
                mainContentWrapper.style.right = newWidth + 'px';
            }
            
            // Keep modals perfectly aligned with the live main content edge (Cursor-like snapping)
            if (typeof window.adjustTaskDetailsModalPosition === 'function') {
                window.adjustTaskDetailsModalPosition();
            }
            if (typeof window.adjustEmailModalPosition === 'function') {
                window.adjustEmailModalPosition();
            }
            
            // Update floating timer overlay position (see note in initializeChatLayout()).
            const floatingTimerOverlay = document.getElementById('floatingTimerOverlay');
            if (floatingTimerOverlay) {
                const hasSavedManualPosition = !!localStorage.getItem('timerPosition');
                const hasExplicitLeft = (floatingTimerOverlay.style.left || '').trim().length > 0;

                if (!hasSavedManualPosition && !hasExplicitLeft) {
                    floatingTimerOverlay.style.right = (newWidth + 24) + 'px'; // Add 24px for spacing
                }
            }
            
            // Center action buttons within chat panel (only for AI chat, not for comms or notifications)
            if (sidebarTarget !== 'notifications' && sidebarTarget !== 'comms') {
                centerActionButtons(newWidth);
            }
            
            // Center search bar during resize
            if (typeof window.centerSearchBar === 'function') {
                window.centerSearchBar();
            }
            
            // Save ONE shared width for all 3 sidebars
            setSavedRightSidebarWidth(newWidth);
        }
    });
    
    document.addEventListener('mouseup', function() {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            
            // Re-enable transitions after resize is complete (not for mainContentWrapper to prevent animations on page navigation)
            if (resizeHandle) {
                resizeHandle.style.transition = 'background-color 0.2s ease';
            }
            
            // Re-enable transition for the active panel
            const sidebarTarget = window.currentSidebarTarget;
            let activePanel;
            if (sidebarTarget === 'comms') {
                activePanel = commsSidebarPanel;
            } else if (sidebarTarget === 'notifications') {
                activePanel = notificationsPanel;
            } else {
                activePanel = chatPanel;
            }
            
            if (activePanel) {
                activePanel.style.transition = '';
            }
            
            // Center search bar after resize completes
            if (typeof window.centerSearchBar === 'function') {
                window.centerSearchBar();
            }

            // Final snap alignment for modals after resize ends
            if (typeof window.adjustTaskDetailsModalPosition === 'function') {
                window.adjustTaskDetailsModalPosition();
            }
            if (typeof window.adjustEmailModalPosition === 'function') {
                window.adjustEmailModalPosition();
            }
        }
    });
    
    // Prevent text selection while resizing
    resizeHandle.addEventListener('selectstart', function(e) {
        e.preventDefault();
    });
}

// Update conversation selection to work with new tab layout
function loadConversation(conversationId) {
    console.log('loadConversation called with ID:', conversationId);
    currentConversationId = parseInt(conversationId);
    
    // Save the selected conversation to localStorage for persistence
    saveSelectedConversation(conversationId);
    
    // Update conversation selection - only target sidebar tabs, not dashboard items
    document.querySelectorAll('.conversation-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    const selectedTab = document.querySelector(`.conversation-tab[data-conversation-id="${conversationId}"]`);
    if (selectedTab) {
        selectedTab.classList.add('active');
        console.log('Selected conversation tab:', selectedTab);
    } else {
        console.error('Could not find conversation tab with ID:', conversationId);
    }
    
    // Update chat header
    const chatHeader = document.getElementById('chatHeader');
    if (chatHeader) chatHeader.textContent = 'AI Assistant';
    
    // Hide welcome message and show conversation header
    const welcomeMessage = document.getElementById('welcomeMessage');
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
    }
    
    const conversationHeader = document.getElementById('notalConversationHeader');
    if (conversationHeader) {
        conversationHeader.style.display = 'block';
    }
    
    // Exit landing mode
    const chatContent = document.getElementById('chatContent');
    if (chatContent) {
        chatContent.classList.remove('landing-mode');
        // Remove sticky message class - it will be re-added if needed after messages load
        chatContent.classList.remove('has-sticky-message');
    }
    
    // Clear sticky message class from chat messages too
    const chatMessages = document.getElementById('chatMessages');
    if (chatMessages) {
        chatMessages.classList.remove('has-sticky-message');
    }
    
    // Load messages for this conversation
    loadConversationMessages(conversationId);
    
    // Initialize SignalR for this conversation
    initializeChat(conversationId);
}

// Update context menu to work with new tab layout
document.addEventListener('contextmenu', function(event) {
    const conversationItem = event.target.closest('.conversation-tab');
    if (conversationItem) {
        event.preventDefault();
        showConversationContextMenu(event, conversationItem);
    }
});

// Update context menu positioning for tabs
function showConversationContextMenu(event, conversationItem) {
    // Remove existing context menu if any
    const existingMenu = document.getElementById('conversationContextMenu');
    if (existingMenu) {
        existingMenu.remove();
    }

    const conversationId = conversationItem.dataset.conversationId;
    const conversationTitle = conversationItem.querySelector('.tab-title')?.textContent || 'Conversation';

    // Create context menu
    const contextMenu = document.createElement('div');
    contextMenu.id = 'conversationContextMenu';
    contextMenu.className = 'conversation-context-menu';
    contextMenu.innerHTML = `
        <div class="context-menu-item" onclick="renameConversation('${conversationId}', '${conversationTitle}')">
            <i class="fas fa-edit"></i> Rename Note
        </div>
        <div class="context-menu-item delete-item" onclick="deleteConversation('${conversationId}')">
            <i class="fas fa-trash"></i> Delete Note
        </div>
    `;

    // Position the menu
    contextMenu.style.left = event.pageX + 'px';
    contextMenu.style.top = event.pageY + 'px';

    document.body.appendChild(contextMenu);
}

// Update delete conversation to work with tabs and dashboard
function deleteConversation(conversationId) {
    console.log('deleteConversation called with ID:', conversationId, 'type:', typeof conversationId);
    
    if (!confirm('Are you sure you want to delete this note? This action cannot be undone.')) {
        return;
    }
    
    const orgId = getCurrentOrganizationId();
    if (!orgId) {
        console.error('Organization ID not found');
        return;
    }
    
    const convId = parseInt(conversationId);
    const convIdStr = String(conversationId);
    console.log('Deleting conversation:', convId, 'for org:', orgId);
    
    fetch(`/Client/${orgId}/Chat/DeleteConversation`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({
            conversationId: convId
        })
    })
        .then(response => {
            console.log('Delete response status:', response.status);
            return response.json();
        })
        .then(data => {
            console.log('Delete response data:', data);
            if (data.success) {
                // Remove the conversation from the UI - check both sidebar and dashboard
                // Try sidebar first (.conversation-tab)
                let conversationElement = document.querySelector(`.conversation-tab[data-conversation-id="${convIdStr}"]`);
                if (!conversationElement) {
                    // Try dashboard (.conversation-item)
                    conversationElement = document.querySelector(`.conversation-item[data-conversation-id="${convIdStr}"]`);
                }
                if (conversationElement) {
                    conversationElement.remove();
                }
                
                // Reload conversations list to ensure UI is in sync with database
                // This handles cases where cache invalidation might not have taken effect yet
                if (typeof loadAIConversationsForPanel === 'function') {
                    loadAIConversationsForPanel(true);
                }
                // Also reload dashboard conversations if on dashboard
                if (typeof loadDashboardConversations === 'function') {
                    loadDashboardConversations();
                }
                
                // Check if this was the active conversation in sidebar
                if (currentConversationId === convIdStr || currentConversationId === convId) {
                    currentConversationId = null;
                    clearSelectedConversation(); // Clear the saved selection
                    
                    // Reset sticky message state
                    stickyMessageIndex = -1;
                    messageElements = [];
                    userMessageElements = [];
                    
                    // Hide sticky last message overlay
                    const stickyLastMessage = document.getElementById('stickyLastMessage');
                    if (stickyLastMessage) {
                        stickyLastMessage.style.display = 'none';
                        stickyLastMessage.classList.remove('visible');
                    }
                    
                    // Clear messages and hide conversation header
                    const chatMessagesContainer = document.getElementById('chatMessages');
                    if (chatMessagesContainer) {
                        // Remove all messages except the headers
                        const messages = chatMessagesContainer.querySelectorAll('.ai-message-bubble:not(.welcome-message .ai-message-bubble), .user-message-bubble');
                        messages.forEach(msg => msg.remove());
                    }
                    
                    const chatHeader = document.getElementById('chatHeader');
                    if (chatHeader) {
                        chatHeader.textContent = 'AI Assistant';
                    }
                    
                    // Show welcome message and hide conversation header
                    const welcomeMessage = document.getElementById('welcomeMessage');
                    if (welcomeMessage) {
                        welcomeMessage.style.display = 'block';
                    }
                    
                    const conversationHeader = document.getElementById('notalConversationHeader');
                    if (conversationHeader) {
                        conversationHeader.style.display = 'none';
                    }
                    
                    // Return to landing mode
                    const chatContent = document.getElementById('chatContent');
                    if (chatContent) {
                        chatContent.classList.add('landing-mode');
                    }
                }
                
                // Check if this was the active conversation in dashboard
                const dashboardCurrentConversationId = window.dashboardCurrentConversationId;
                if (dashboardCurrentConversationId === convIdStr || dashboardCurrentConversationId === convId) {
                    // Reset dashboard chat card
                    if (typeof resetDashboardChatCard === 'function') {
                        resetDashboardChatCard(true); // Keep conversations column visible
                    }
                }
                
                // Remove from localStorage if it was saved
                const savedConversationId = localStorage.getItem('dashboardSelectedConversation');
                if (savedConversationId === convIdStr || savedConversationId === String(convId)) {
                    localStorage.removeItem('dashboardSelectedConversation');
                }
            } else {
                alert('Failed to delete note: ' + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Error deleting conversation:', error);
            alert('Failed to delete note');
        });
    
    // Remove context menu
    const contextMenu = document.getElementById('conversationContextMenu');
    if (contextMenu) {
        contextMenu.remove();
    }
}

// Update rename conversation to work with tabs and dashboard
function renameConversation(conversationId, currentTitle) {
    const newTitle = prompt('Enter new note name:', currentTitle);
    if (newTitle && newTitle.trim() !== '' && newTitle !== currentTitle) {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        fetch(`/Client/${orgId}/Chat/RenameConversation`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                conversationId: parseInt(conversationId),
                newTitle: newTitle.trim()
            })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Update the conversation title in the UI - check both sidebar and dashboard
                // Try sidebar first (.tab-title)
                let conversationElement = document.querySelector(`[data-conversation-id="${conversationId}"] .tab-title`);
                if (conversationElement) {
                    conversationElement.textContent = newTitle.trim();
                } else {
                    // Try dashboard (.conversation-title)
                    conversationElement = document.querySelector(`[data-conversation-id="${conversationId}"] .conversation-title`);
                if (conversationElement) {
                    conversationElement.textContent = newTitle.trim();
                    }
                }
            } else {
                alert('Failed to rename note: ' + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Error renaming conversation:', error);
            alert('Failed to rename note');
        });
    }
    
    // Remove context menu
    const contextMenu = document.getElementById('conversationContextMenu');
    if (contextMenu) {
        contextMenu.remove();
    }
}

// Initialize tab scrolling functionality
function initializeTabScrolling() {
    const tabList = document.querySelector('.tab-list');
    const conversationTabs = document.querySelector('.conversation-tabs');
    
    console.log('Tab list found:', tabList);
    console.log('Conversation tabs found:', conversationTabs);
    
    if (!tabList) {
        console.error('Tab list not found!');
        return;
    }
    
    // Check if scrolling is needed
    console.log('Tab list scrollWidth:', tabList.scrollWidth);
    console.log('Tab list clientWidth:', tabList.clientWidth);
    console.log('Can scroll:', tabList.scrollWidth > tabList.clientWidth);
    
    // Enable wheel scrolling on the entire conversation tabs container
    if (conversationTabs) {
        conversationTabs.addEventListener('wheel', function(e) {
            e.preventDefault();
            e.stopPropagation();
            const scrollAmount = e.deltaY * 0.8;
            tabList.scrollLeft += scrollAmount;
            console.log('Wheel scroll on container:', scrollAmount, 'New scrollLeft:', tabList.scrollLeft);
        }, { passive: false });
    }
    
    // Also try on the tab list itself
    tabList.addEventListener('wheel', function(e) {
        e.preventDefault();
        e.stopPropagation();
        const scrollAmount = e.deltaY * 0.8;
        tabList.scrollLeft += scrollAmount;
        console.log('Wheel scroll on tab list:', scrollAmount, 'New scrollLeft:', tabList.scrollLeft);
    }, { passive: false });
    
    // Enable drag scrolling on the entire container
    let isDragging = false;
    let startX = 0;
    let scrollLeft = 0;
    
    const dragContainer = conversationTabs || tabList;
    
    dragContainer.addEventListener('mousedown', function(e) {
        // Don't start dragging if clicking on a tab or close button
        if (e.target.closest('.conversation-tab') || e.target.closest('.tab-close')) {
            return;
        }
        
        isDragging = true;
        dragContainer.style.cursor = 'grabbing';
        startX = e.pageX;
        scrollLeft = tabList.scrollLeft;
        console.log('Started dragging at:', startX, 'scrollLeft:', scrollLeft);
    });
    
    dragContainer.addEventListener('mouseleave', function() {
        if (isDragging) {
            isDragging = false;
            dragContainer.style.cursor = 'grab';
        }
    });
    
    dragContainer.addEventListener('mouseup', function() {
        if (isDragging) {
            isDragging = false;
            dragContainer.style.cursor = 'grab';
            console.log('Stopped dragging');
        }
    });
    
    dragContainer.addEventListener('mousemove', function(e) {
        if (!isDragging) return;
        
        e.preventDefault();
        e.stopPropagation();
        
        const x = e.pageX;
        const walk = (x - startX) * 2;
        const newScrollLeft = scrollLeft - walk;
        
        tabList.scrollLeft = newScrollLeft;
        console.log('Dragging - walk:', walk, 'newScrollLeft:', newScrollLeft);
    });
}

// Initialize tab scrolling when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    // Add a small delay to ensure the tab list is rendered
    setTimeout(initializeTabScrolling, 100);
});

// Test function to manually scroll tabs
function testTabScroll() {
    const tabList = document.querySelector('.tab-list');
    if (tabList) {
        console.log('Current scrollLeft:', tabList.scrollLeft);
        tabList.scrollLeft += 100;
        console.log('New scrollLeft:', tabList.scrollLeft);
    } else {
        console.error('Tab list not found for testing');
    }
}

// Chat Selection Persistence Functions
function saveSelectedConversation(conversationId) {
    try {
        localStorage.setItem('selectedConversationId', conversationId);
        console.log('Saved selected conversation:', conversationId);
    } catch (error) {
        console.error('Error saving selected conversation:', error);
    }
}

function getSelectedConversation() {
    try {
        return localStorage.getItem('selectedConversationId');
    } catch (error) {
        console.error('Error getting selected conversation:', error);
        return null;
    }
}

function clearSelectedConversation() {
    try {
        localStorage.removeItem('selectedConversationId');
        console.log('Cleared selected conversation');
    } catch (error) {
        console.error('Error clearing selected conversation:', error);
    }
}

function restoreSelectedConversation() {
    const savedConversationId = getSelectedConversation();
    console.log('Attempting to restore conversation:', savedConversationId);
    
    if (savedConversationId) {
        // Check if the conversation tab exists - only look for sidebar tabs, not dashboard items
        const conversationTab = document.querySelector(`.conversation-tab[data-conversation-id="${savedConversationId}"]`);
        if (conversationTab) {
            console.log('Restoring conversation:', savedConversationId);
            loadConversation(savedConversationId);
        } else {
            console.log('Saved conversation not found in current tabs, clearing selection');
            clearSelectedConversation();
        }
    } else {
        console.log('No saved conversation to restore');
    }
}

// Make functions available globally for debugging
window.testNewlineFix = testNewlineFix;
window.debugMessageFormatting = debugMessageFormatting;
window.testTabScroll = testTabScroll;
