let connection;
let currentConversationId = null;
let currentOrganizationId = null;
let currentMessages = [];
let aiThinkingInterval = null;
let lastMessageCount = 0;
let aiInsights = null;
let insightsVisible = false;

// Initialize SignalR connection
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, initializing chat...');
    
    // Initialize chat layout
    initializeChatLayout();
    
    // Load conversations for the global chat panel
    loadConversationsForPanel();
    
    // Debug: Check conversation items
    const conversationItems = document.querySelectorAll('.conversation-tab');
    console.log('Found conversation items:', conversationItems.length);
    conversationItems.forEach((item, index) => {
        console.log(`Conversation ${index}:`, {
            element: item,
            id: item.dataset.conversationId,
            title: item.querySelector('.tab-title')?.textContent
        });
    });
    
    // Initialize SignalR connection (optional)
    if (typeof signalR !== 'undefined') {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/chat")
            .build();

        // Start connection
        connection.start().then(function () {
            console.log("SignalR Connected");
        }).catch(function (err) {
            console.error("SignalR Connection Error: ", err.toString());
        });
    } else {
        console.log("SignalR not available, using fallback communication");
    }

    // Event listeners
    document.getElementById('sendButton')?.addEventListener('click', sendMessage);
    document.getElementById('messageInput')?.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            sendMessage();
        }
    });
    document.getElementById('clarityButton')?.addEventListener('click', requestClarity);
    document.getElementById('requestClarity')?.addEventListener('click', processClarityRequest);
    document.getElementById('toggleInsights')?.addEventListener('click', toggleAIInsights);
    document.getElementById('backButton')?.addEventListener('click', toggleSidebar);
    document.getElementById('loadAIInsights')?.addEventListener('click', loadAIInsights);
    document.getElementById('menuButton')?.addEventListener('click', toggleSidebar);
    
    // Handle conversation creation form
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
                modal.hide();
                
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
            chatMessages.innerHTML = '';
            
            messages.forEach(message => {
                addMessageToChat(message);
            });
            
            // Process AI insights
            processAIInsights(messages);
        } else {
            console.error('Chat messages container not found');
        }
        
    } catch (error) {
        console.error('Error loading messages:', error);
    }
}

// Create a new conversation from a message
async function createNewConversationFromMessage(message) {
    const orgId = getCurrentOrganizationId();
    if (!orgId) {
        console.error('Organization ID not found');
        return;
    }
    
    try {
        // Create conversation with a title based on the first message
        const title = message.length > 30 ? message.substring(0, 30) + '...' : message;
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
                
                // Update the default tab with the real conversation ID
                const defaultTab = document.querySelector('.conversation-tab[data-conversation-id="default"]');
                if (defaultTab) {
                    defaultTab.dataset.conversationId = data.conversationId;
                    defaultTab.querySelector('.tab-title').textContent = title;
                }
                
                // Now send the message to the new conversation
                await sendMessageToConversation(message, data.conversationId);

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

// Send message to a specific conversation
async function sendMessageToConversation(message, conversationId) {
    return await sendMessageInternal(message, conversationId);
}

// Send message
async function sendMessage() {
    const messageInput = document.getElementById('messageInput');
    const message = messageInput.value.trim();
    
    if (!message) return;
    
    // Clear input immediately to avoid duplicate text lingering
    messageInput.value = '';
    
    // If we don't have a conversation ID, create a new one first
    if (!currentConversationId) {
        await createNewConversationFromMessage(message);
        return;
    }
    
    await sendMessageInternal(message, currentConversationId);
}

// Generate AI response
async function generateAIResponse(userMessage) {
    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GenerateAIResponse`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                conversationId: parseInt(currentConversationId),
                userMessage: userMessage
            })
        });
        
        const result = await response.json();
        
        if (result.success) {
            // Hide thinking indicator
            hideAIThinkingIndicator();
            
            // Add AI response to chat
            addMessageToChat(result.message);
            
            // Update current messages
            currentMessages.push(result.message);
            
            // Load AI insights after a short delay to allow background processing
            setTimeout(() => {
                if (insightsVisible) {
                    loadAIInsights();
                }
            }, 2000);
        } else {
            console.error('Error generating AI response:', result.error);
            hideAIThinkingIndicator();
        }
    } catch (error) {
        console.error('Error generating AI response:', error);
        hideAIThinkingIndicator();
    }
}

// Internal function to handle message sending logic
async function sendMessageInternal(message, conversationId) {
    try {
        // Add user message to chat immediately
        addUserMessageToChat(message);
        
        // Show intelligent AI thinking indicator
        showIntelligentAIThinkingIndicator(message);
        
        // Send to server
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            hideAIThinkingIndicator();
            return false;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/SendMessage`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `conversationId=${conversationId}&content=${encodeURIComponent(message)}&messageType=Text`
        });
        
        const data = await response.json();
        
        if (data.success) {
            console.log('Message sent successfully');
            // Generate intelligent AI response
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

// Add user message to chat
function addUserMessageToChat(message) {
    const chatMessages = document.getElementById('chatMessages');
    const welcomeMessage = document.getElementById('welcomeMessage');
    
    // Hide welcome message when first real message arrives
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
    }
    
    const messageDiv = document.createElement('div');
    messageDiv.className = 'user-message-bubble';
    messageDiv.innerHTML = `
        <div class="message-avatar">
            <div class="user-avatar">U</div>
        </div>
        <div class="message-content">
            <div class="message-header">
                <span class="sender-name">You</span>
                <span class="timestamp">${new Date().toLocaleTimeString()}</span>
            </div>
            <div class="message-text">${message}</div>
        </div>
    `;
    
    chatMessages.appendChild(messageDiv);
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
    // This should return the actual user ID from your authentication system
    const userId = document.querySelector('[data-user-id]')?.dataset.userId;
    return userId ? parseInt(userId) : null;
}

// Get current user type
function getCurrentUserType() {
    // This should return the actual user type from your authentication system
    return document.querySelector('[data-user-type]')?.dataset.userType || 'Client';
}

// Get current organization ID from URL or data attribute
function getCurrentOrganizationId() {
    if (currentOrganizationId) {
        return currentOrganizationId;
    }
    
    // Try to get from URL path (e.g., /Client/123/Chat/... or /Client/123/Matter)
    const pathMatch = window.location.pathname.match(/\/Client\/(\d+)\/(?:Dashboard|Chat|Matter|Services|Documents|Teams|Settings|Tasks|Calendar)/);
    if (pathMatch) {
        currentOrganizationId = parseInt(pathMatch[1]);
        return currentOrganizationId;
    }
    
    // Fallback to data attribute
    const orgId = document.querySelector('[data-organization-id]')?.dataset.organizationId;
    if (orgId) {
        currentOrganizationId = parseInt(orgId);
        return currentOrganizationId;
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
    
    const messageDiv = document.createElement('div');
    
    if (message.isFromAI) {
        messageDiv.className = 'ai-message-bubble';
        messageDiv.innerHTML = `
            <div class="message-avatar">
                <div class="avatar-icon">C</div>
            </div>
            <div class="message-content">
                <div class="message-header">
                    <span class="sender-name">Certio</span>
                    <span class="ai-badge">AI Agent</span>
                    <span class="timestamp">${new Date(message.createdAt).toLocaleTimeString()}</span>
                </div>
                <div class="message-text">
                    ${formatAIMessage(message)}
                </div>
                ${message.messageType === 'AI_Reply' ? generateSourcesSection() : ''}
            </div>
        `;
    } else {
        messageDiv.className = 'user-message-bubble';
        const userInitial = message.userType.charAt(0).toUpperCase();
        messageDiv.innerHTML = `
            <div class="message-avatar">
                <div class="user-avatar">${userInitial}</div>
            </div>
            <div class="message-content">
                <div class="message-header">
                    <span class="sender-name">${message.userType}</span>
                    <span class="timestamp">${new Date(message.createdAt).toLocaleTimeString()}</span>
                </div>
                <div class="message-text">${message.content}</div>
            </div>
        `;
    }
    
    chatMessages.appendChild(messageDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
}

// Format AI message based on message type
function formatAIMessage(message) {
    if (!message.messageType.startsWith('AI_')) {
        // Convert markdown to HTML for regular messages too
        return convertMarkdownToHtml(message.content);
    }
    
    // Handle AI_Response type directly (it's plain text, not JSON)
    if (message.messageType === 'AI_Response') {
        return formatIntelligentResponse(message.content);
    }
    
    try {
        const aiData = JSON.parse(message.content);
        
        switch (message.messageType) {
            case 'AI_Summary':
                return formatSummaryMessage(aiData);
            case 'AI_Goal':
                return formatGoalMessage(aiData);
            case 'AI_Reply':
                return formatReplyMessage(aiData);
            case 'AI_Clarity':
                return formatClarityMessage(aiData);
            default:
                return `<div class="ai-result"><pre>${cleanNewlines(message.content)}</pre></div>`;
        }
    } catch (e) {
        // If JSON parsing fails, try to format as intelligent response
        return formatIntelligentResponse(message.content);
    }
}

// Format conversation summary
function formatSummaryMessage(data) {
    if (data.Summary === "Error processing conversation") {
        return `
            <div class="ai-summary-error">
                <p><strong>⚠️ Summary Unavailable</strong></p>
                <p>I'm having trouble processing this conversation right now. Please try again in a moment.</p>
            </div>
        `;
    }
    
    return `
        <div class="ai-summary">
            <h6><i class="fas fa-chart-line"></i> Conversation Summary</h6>
            <p><strong>Summary:</strong> ${data.Summary || 'No summary available'}</p>
            <p><strong>Sentiment:</strong> <span class="badge bg-${getSentimentColor(data.Sentiment)}">${data.Sentiment || 'Neutral'}</span></p>
            <p><strong>Urgency:</strong> <span class="badge bg-${getUrgencyColor(data.Urgency)}">${data.Urgency || 'Medium'}</span></p>
            ${data.KeyPoints && data.KeyPoints.length > 0 ? `
                <div class="key-points">
                    <strong>Key Points:</strong>
                    <ul>
                        ${data.KeyPoints.map(point => `<li>${point}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
            ${data.SuggestedActions && data.SuggestedActions.length > 0 ? `
                <div class="suggested-actions">
                    <strong>Suggested Actions:</strong>
                    <ul>
                        ${data.SuggestedActions.map(action => `<li>${action}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
        </div>
    `;
}

// Format client goals
function formatGoalMessage(data) {
    if (!data.PrimaryGoal && !data.BusinessType) {
        return `
            <div class="ai-goal-error">
                <p><strong>🎯 Goals Analysis</strong></p>
                <p>I'm still analyzing the conversation to identify your goals. This may take a moment.</p>
            </div>
        `;
    }
    
    return `
        <div class="ai-goal">
            <h6><i class="fas fa-target"></i> Client Goals Analysis</h6>
            <p><strong>Primary Goal:</strong> ${data.PrimaryGoal || 'Not yet identified'}</p>
            <p><strong>Business Type:</strong> ${data.BusinessType || 'Not specified'}</p>
            <p><strong>Legal Area:</strong> ${data.LegalArea || 'General'}</p>
            <p><strong>Timeline:</strong> ${data.Timeline || 'Not specified'}</p>
            <p><strong>Budget:</strong> ${data.Budget || 'Not specified'}</p>
            ${data.SecondaryGoals && data.SecondaryGoals.length > 0 ? `
                <div class="secondary-goals">
                    <strong>Secondary Goals:</strong>
                    <ul>
                        ${data.SecondaryGoals.map(goal => `<li>${goal}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
            ${data.RequiredDocuments && data.RequiredDocuments.length > 0 ? `
                <div class="required-documents">
                    <strong>Required Documents:</strong>
                    <ul>
                        ${data.RequiredDocuments.map(doc => `<li>${doc}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
        </div>
    `;
}

// Format reply suggestions
function formatReplyMessage(data) {
    if (!data.SuggestedReply) {
        return `
            <div class="ai-reply-error">
                <p><strong>💡 Reply Suggestions</strong></p>
                <p>I'm working on generating reply suggestions for you. Please wait a moment.</p>
            </div>
        `;
    }
    
    return `
        <div class="ai-reply">
            <h6><i class="fas fa-lightbulb"></i> Suggested Reply</h6>
            <div class="reply-meta">
                <span class="badge bg-${getToneColor(data.Tone)}">${data.Tone || 'Professional'}</span>
                <span class="badge bg-info">${data.Purpose || 'Response'}</span>
                ${data.RequiresLegalReview ? '<span class="badge bg-warning">Requires Legal Review</span>' : ''}
            </div>
            <div class="suggested-reply">
                <p>${data.SuggestedReply}</p>
            </div>
            ${data.KeyPoints && data.KeyPoints.length > 0 ? `
                <div class="reply-keypoints">
                    <strong>Key Points:</strong>
                    <ul>
                        ${data.KeyPoints.map(point => `<li>${point}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
        </div>
    `;
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
    // Convert markdown to HTML if needed
    const htmlContent = convertMarkdownToHtml(content);
    
    // Check if this is a service unavailable message
    if (content.includes("AI Services Temporarily Unavailable")) {
        return `
            <div class="intelligent-ai service-unavailable">
                <div class="intelligent-response">
                    <div class="ai-thinking">
                        <i class="fas fa-exclamation-triangle"></i>
                        <span>Service Status</span>
                    </div>
                    <div class="response-content">
                        ${htmlContent}
                    </div>
                </div>
            </div>
        `;
    }
    
    return `
        <div class="intelligent-ai">
            <div class="intelligent-response">
                <div class="ai-thinking">
                    <i class="fas fa-brain"></i>
                    <span>Intelligent AI Response</span>
                </div>
                <div class="response-content">
                    ${htmlContent}
                </div>
            </div>
        </div>
    `;
}

// Convert markdown to HTML
function convertMarkdownToHtml(text) {
    if (!text) return text;
    
    return text
        // First, aggressively handle all forms of newlines
        .replace(/\\n/g, '\n')  // Convert \n to actual newlines
        .replace(/\\r\\n/g, '\n')  // Convert \r\n to newlines
        .replace(/\\r/g, '\n')  // Convert \r to newlines
        // Convert **bold** to <strong>bold</strong>
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        // Convert *italic* to <em>italic</em>
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        // Convert actual newlines to <br> tags
        .replace(/\n/g, '<br>')
        // Convert bullet points to HTML list (handle both • and -)
        .replace(/^[•\-] (.*)$/gm, '<li>$1</li>')
        // Convert numbered lists
        .replace(/^\d+\. (.*)$/gm, '<li>$1</li>')
        // Wrap consecutive list items in ul tags
        .replace(/(<li>.*<\/li>)/gs, '<ul>$1</ul>')
        // Clean up multiple consecutive ul tags
        .replace(/<\/ul><ul>/g, '')
        // Convert escaped quotes
        .replace(/\\"/g, '"')
        // Clean up multiple consecutive br tags
        .replace(/(<br\s*\/?>){3,}/g, '<br><br>')
        // Convert double line breaks to paragraphs for better structure
        .replace(/(<br\s*\/?>){2,}/g, '</p><p>')
        // Wrap in paragraph tags if not already wrapped
        .replace(/^(.+)$/, '<p>$1</p>')
        // Clean up empty paragraphs
        .replace(/<p><br\s*\/?><\/p>/g, '')
        .replace(/<p><\/p>/g, '');
}

// Generate sources section for AI responses
function generateSourcesSection() {
    const sources = [
        'Legal Documentation',
        'Help Center: Legal Services',
        'FAQs: Legal Questions'
    ];
    
    return `
        <div class="sources-section">
            <div class="sources-title">Sources</div>
            ${sources.map(source => `
                <a href="#" class="source-link">
                    ${source}
                    <i class="fas fa-arrow-right"></i>
                </a>
            `).join('')}
        </div>
    `;
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
        <div class="message-avatar">
            <div class="avatar-icon">C</div>
        </div>
        <div class="message-content">
            <div class="message-header">
                <span class="sender-name">Certio</span>
                <span class="ai-badge">Clarity Agent</span>
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
    
    if (!insights.summary && !insights.goals && !insights.suggestions) {
        insightsPanel.style.display = 'none';
        return;
    }
    
    insightsPanel.style.display = 'block';
    
    let html = '<div class="ai-insights-grid">';
    
    if (insights.summary) {
        html += `
            <div class="ai-insight-card">
                <h6><i class="fas fa-chart-line"></i> Conversation Summary</h6>
                <p><strong>Sentiment:</strong> <span class="badge bg-${getSentimentColor(insights.summary.sentiment)}">${insights.summary.sentiment}</span></p>
                <p><strong>Urgency:</strong> <span class="badge bg-${getUrgencyColor(insights.summary.urgency)}">${insights.summary.urgency}</span></p>
                <p>${insights.summary.summary}</p>
                <ul>
                    ${insights.summary.keyPoints.map(point => `<li>${point}</li>`).join('')}
                </ul>
            </div>
        `;
    }
    
    if (insights.goals) {
        html += `
            <div class="ai-insight-card">
                <h6><i class="fas fa-target"></i> Client Goals</h6>
                <p><strong>Primary Goal:</strong> ${insights.goals.primaryGoal}</p>
                <p><strong>Business Type:</strong> ${insights.goals.businessType}</p>
                <p><strong>Legal Area:</strong> ${insights.goals.legalArea}</p>
                <p><strong>Timeline:</strong> ${insights.goals.timeline}</p>
                <ul>
                    ${insights.goals.secondaryGoals.map(goal => `<li>${goal}</li>`).join('')}
                </ul>
            </div>
        `;
    }
    
    if (insights.suggestions) {
        html += `
            <div class="ai-insight-card">
                <h6><i class="fas fa-lightbulb"></i> Reply Suggestions</h6>
                <p><strong>Tone:</strong> ${insights.suggestions.tone}</p>
                <p><strong>Purpose:</strong> ${insights.suggestions.purpose}</p>
                <div class="suggestion-preview">${insights.suggestions.suggestedReply}</div>
                ${insights.suggestions.requiresLegalReview ? '<span class="badge bg-warning">Requires Legal Review</span>' : ''}
            </div>
        `;
    }
    
    html += '</div>';
    insightsContent.innerHTML = html;
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

    let html = '<div class="ai-insights-container">';
    
    // Summary
    if (insights.AI_Summary) {
        const summary = insights.AI_Summary;
        html += `
            <div class="insight-card mb-3">
                <h6 class="insight-title">
                    <i class="fas fa-chart-line text-primary"></i> Conversation Summary
                </h6>
                <div class="insight-content">
                    <p class="summary-text">${summary.summary || 'No summary available'}</p>
                    <div class="summary-meta">
                        <span class="badge badge-${getSentimentColor(summary.sentiment)}">${summary.sentiment || 'Neutral'}</span>
                        <span class="badge badge-${getUrgencyColor(summary.urgency)}">${summary.urgency || 'Medium'}</span>
                    </div>
                    ${summary.key_points && summary.key_points.length > 0 ? `
                        <div class="key-points mt-2">
                            <strong>Key Points:</strong>
                            <ul class="list-unstyled mt-1">
                                ${summary.key_points.map(point => `<li>• ${point}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                    ${summary.suggested_actions && summary.suggested_actions.length > 0 ? `
                        <div class="suggested-actions mt-2">
                            <strong>Suggested Actions:</strong>
                            <ul class="list-unstyled mt-1">
                                ${summary.suggested_actions.map(action => `<li>• ${action}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                </div>
            </div>
        `;
    }

    // Client Goals
    if (insights.AI_Goal) {
        const goals = insights.AI_Goal;
        html += `
            <div class="insight-card mb-3">
                <h6 class="insight-title">
                    <i class="fas fa-target text-success"></i> Client Goals
                </h6>
                <div class="insight-content">
                    <p><strong>Primary Goal:</strong> ${goals.primary_goal || 'Not identified'}</p>
                    ${goals.business_type ? `<p><strong>Business Type:</strong> ${goals.business_type}</p>` : ''}
                    ${goals.legal_area ? `<p><strong>Legal Area:</strong> ${goals.legal_area}</p>` : ''}
                    ${goals.timeline ? `<p><strong>Timeline:</strong> ${goals.timeline}</p>` : ''}
                    ${goals.budget ? `<p><strong>Budget:</strong> ${goals.budget}</p>` : ''}
                    ${goals.secondary_goals && goals.secondary_goals.length > 0 ? `
                        <div class="secondary-goals mt-2">
                            <strong>Secondary Goals:</strong>
                            <ul class="list-unstyled mt-1">
                                ${goals.secondary_goals.map(goal => `<li>• ${goal}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                    ${goals.required_documents && goals.required_documents.length > 0 ? `
                        <div class="required-docs mt-2">
                            <strong>Required Documents:</strong>
                            <ul class="list-unstyled mt-1">
                                ${goals.required_documents.map(doc => `<li>• ${doc}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                </div>
            </div>
        `;
    }

    // Reply Suggestions
    if (insights.AI_Reply) {
        const reply = insights.AI_Reply;
        html += `
            <div class="insight-card mb-3">
                <h6 class="insight-title">
                    <i class="fas fa-lightbulb text-warning"></i> Reply Suggestion
                </h6>
                <div class="insight-content">
                    <div class="reply-suggestion">
                        <p class="suggested-reply">${reply.suggested_reply || 'No suggestion available'}</p>
                        <div class="reply-meta">
                            <span class="badge badge-info">${reply.tone || 'Professional'}</span>
                            <span class="badge badge-${reply.requires_legal_review ? 'danger' : 'success'}">
                                ${reply.requires_legal_review ? 'Needs Review' : 'Ready to Send'}
                            </span>
                        </div>
                        ${reply.key_points && reply.key_points.length > 0 ? `
                            <div class="reply-key-points mt-2">
                                <strong>Key Points:</strong>
                                <ul class="list-unstyled mt-1">
                                    ${reply.key_points.map(point => `<li>• ${point}</li>`).join('')}
                                </ul>
                            </div>
                        ` : ''}
                    </div>
                </div>
            </div>
        `;
    }

    // Metadata
    if (insights.AI_Metadata) {
        const metadata = insights.AI_Metadata;
        html += `
            <div class="insight-card mb-3">
                <h6 class="insight-title">
                    <i class="fas fa-info-circle text-info"></i> Processing Info
                </h6>
                <div class="insight-content">
                    <small class="text-muted">
                        Processed: ${new Date(metadata.processing_timestamp).toLocaleString()}<br>
                        Agents: ${metadata.agents_executed ? metadata.agents_executed.join(', ') : 'Unknown'}
                    </small>
                </div>
            </div>
        `;
    }

    html += '</div>';
    insightsContainer.innerHTML = html;
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
    
    if (messageLower.includes("document review") || messageLower.includes("review")) {
        thinkingMessage = "Analyzing document requirements...";
    } else if (messageLower.includes("contract") || messageLower.includes("agreement")) {
        thinkingMessage = "Reviewing contract details...";
    } else if (messageLower.includes("hello") || messageLower.includes("hi")) {
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
            <span class="user-type">AI</span>
            <span class="ai-agent">IntelligentAI</span>
            <span class="timestamp">${new Date().toLocaleTimeString()}</span>
        </div>
        <div class="message-content">
            <div class="ai-thinking">
                <div class="thinking-spinner">
                    <div class="spinner-border spinner-border-sm" role="status">
                        <span class="sr-only">Loading...</span>
                    </div>
                </div>
                <span class="thinking-text">${thinkingMessage}</span>
            </div>
        </div>
    `;
    
    chatMessages.appendChild(thinkingDiv);
    chatMessages.scrollTo({ top: chatMessages.scrollHeight, behavior: 'instant' });
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
    if (message.isFromAI && message.aiAgentType === 'IntelligentAI') {
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


// Chat Layout Functions
function initializeChatLayout() {
    // Initialize resize functionality
    initializeResizeHandle();
    
    // Set initial chat panel width from localStorage or default
    const savedWidth = localStorage.getItem('chatPanelWidth');
    const defaultWidth = 320; // Default narrower width
    const chatPanelWidth = savedWidth ? parseInt(savedWidth) : defaultWidth;
    
    const chatPanel = document.getElementById('chatPanel');
    const mainContent = document.querySelector('.main-content');
    const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
    const resizeHandle = document.getElementById('resizeHandle');
    
    if (chatPanel) {
        chatPanel.style.width = chatPanelWidth + 'px';
    }
    
    if (resizeHandle) {
        resizeHandle.style.right = chatPanelWidth + 'px';
    }
    
    // Adjust main content wrapper to account for chat panel
    if (mainContentWrapper) {
        mainContentWrapper.style.right = chatPanelWidth + 'px';
    }
    
    // Also set the initial header width
    const topHeader = document.querySelector('.top-header');
    if (topHeader) {
        topHeader.style.marginRight = chatPanelWidth + 'px';
    }
}

// Load conversations for the global chat panel
async function loadConversationsForPanel() {
    console.log('Loading conversations for panel...');
    const tabList = document.querySelector('.tab-list');
    if (!tabList) {
        console.error('Tab list not found');
        return;
    }
    
    // Only skip if we already have conversations loaded
    if (tabList.children.length > 0) {
        console.log('Conversations already loaded');
        return;
    }
    
    console.log('No conversations found, creating default...');

    try {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            await createDefaultConversationTab();
            return;
        }
        
        const response = await fetch(`/Client/${orgId}/Chat/GetConversations`);
        if (!response.ok) {
            console.log('No conversations endpoint available, using fallback');
            // Create a default conversation tab for demo purposes
            await createDefaultConversationTab();
            return;
        }
        
        const conversations = await response.json();
        console.log('Loaded conversations:', conversations);
        
        if (tabList) {
            tabList.innerHTML = '';
            
            if (conversations && conversations.length > 0) {
                conversations.forEach(conversation => {
                    const tab = document.createElement('div');
                    tab.className = 'conversation-tab';
                    tab.dataset.conversationId = conversation.id.toString();
                    tab.title = conversation.title;
                    tab.innerHTML = `
                        <span class="tab-title">${conversation.title}</span>
                        <button class="tab-close" onclick="event.stopPropagation(); deleteConversation('${conversation.id}')">
                            <i class="fas fa-times"></i>
                        </button>
                    `;
                    tabList.appendChild(tab);
                });
                
                // Restore the last selected conversation after loading
                restoreSelectedConversation();
            } else {
                // Create a default conversation tab if no conversations exist
                await createDefaultConversationTab();
            }
        }
    } catch (error) {
        console.error('Error loading conversations:', error);
        // Create a default conversation tab as fallback
        await createDefaultConversationTab();
    }
}

// Create a default conversation tab
async function createDefaultConversationTab() {
    console.log('Creating default conversation tab...');
    const tabList = document.querySelector('.tab-list');
    if (!tabList) {
        console.error('Tab list not found in createDefaultConversationTab');
        return;
    }
    
    // Create a real conversation instead of a default one
    const orgId = getCurrentOrganizationId();
    if (!orgId) {
        console.error('Organization ID not found');
        return;
    }
    
    console.log('Creating conversation with orgId:', orgId);
    
    try {
        const response = await fetch(`/Client/${orgId}/Chat/CreateConversation`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `title=${encodeURIComponent('New Chat')}&description=${encodeURIComponent('New conversation started')}`
        });
        
        if (response.ok) {
            const data = await response.json();
            if (data.success && data.conversationId) {
                // Create tab with real conversation ID
                const tab = document.createElement('div');
                tab.className = 'conversation-tab active';
                tab.dataset.conversationId = data.conversationId;
                tab.title = data.title;
                tab.innerHTML = `
                    <span class="tab-title">${data.title}</span>
                    <button class="tab-close" onclick="event.stopPropagation(); deleteConversation('${data.conversationId}')">
                        <i class="fas fa-times"></i>
                    </button>
                `;
                tabList.appendChild(tab);
                
                // Set as current conversation
                currentConversationId = data.conversationId;
                
                // Restore the last selected conversation after creating tab
                restoreSelectedConversation();
            } else {
                console.error('Failed to create default conversation:', data.error);
            }
        } else {
            console.error('Failed to create default conversation:', response.statusText);
        }
    } catch (error) {
        console.error('Error creating default conversation:', error);
    }
}

// Force refresh conversations (useful after creating new conversations)
function refreshConversations() {
    const tabList = document.querySelector('.tab-list');
    if (tabList) {
        tabList.innerHTML = '';
    }
    loadConversationsForPanel();
}

function initializeResizeHandle() {
    const resizeHandle = document.getElementById('resizeHandle');
    const chatPanel = document.getElementById('chatPanel');
    const mainContent = document.querySelector('.main-content');
    const mainContentWrapper = document.querySelector('.client-main-content-wrapper');
    const topHeader = document.querySelector('.top-header');
    
    if (!resizeHandle || !chatPanel || !mainContent || !mainContentWrapper) return;
    
    let isResizing = false;
    let startX = 0;
    let startWidth = 0;
    
    resizeHandle.addEventListener('mousedown', function(e) {
        isResizing = true;
        startX = e.clientX;
        startWidth = parseInt(window.getComputedStyle(chatPanel).width, 10);
        
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
            chatPanel.style.width = newWidth + 'px';
            resizeHandle.style.right = newWidth + 'px';
            mainContentWrapper.style.right = newWidth + 'px';
            
            // Also adjust the top header width
            if (topHeader) {
                topHeader.style.marginRight = newWidth + 'px';
            }
            
            // Save to localStorage
            localStorage.setItem('chatPanelWidth', newWidth);
        }
    });
    
    document.addEventListener('mouseup', function() {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
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
    
    // Update conversation selection
    document.querySelectorAll('.conversation-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    const selectedTab = document.querySelector(`[data-conversation-id="${conversationId}"]`);
    if (selectedTab) {
        selectedTab.classList.add('active');
        console.log('Selected conversation tab:', selectedTab);
    } else {
        console.error('Could not find conversation tab with ID:', conversationId);
    }
    
    // Show chat interface
    const chatHeader = document.getElementById('chatHeader');
    const chatInput = document.querySelector('.chat-input');
    
    if (chatHeader) chatHeader.textContent = 'AI Assistant';
    if (chatInput) chatInput.style.display = 'block';
    
    // Hide welcome message
    const welcomeMessage = document.getElementById('welcomeMessage');
    if (welcomeMessage) {
        welcomeMessage.style.display = 'none';
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
            <i class="fas fa-edit"></i> Rename Chat
        </div>
        <div class="context-menu-item delete-item" onclick="deleteConversation('${conversationId}')">
            <i class="fas fa-trash"></i> Delete Chat
        </div>
    `;

    // Position the menu
    contextMenu.style.left = event.pageX + 'px';
    contextMenu.style.top = event.pageY + 'px';

    document.body.appendChild(contextMenu);
}

// Update delete conversation to work with tabs
function deleteConversation(conversationId) {
    if (confirm('Are you sure you want to delete this conversation? This action cannot be undone.')) {
        const orgId = getCurrentOrganizationId();
        if (!orgId) {
            console.error('Organization ID not found');
            return;
        }
        
        fetch(`/Client/${orgId}/Chat/DeleteConversation`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                conversationId: parseInt(conversationId)
            })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Remove the conversation from the UI
                const conversationElement = document.querySelector(`[data-conversation-id="${conversationId}"]`);
                if (conversationElement) {
                    conversationElement.remove();
                }
                
                // If this was the active conversation, clear the chat area
                if (currentConversationId === conversationId) {
                    currentConversationId = null;
                    clearSelectedConversation(); // Clear the saved selection
                    document.getElementById('chatMessages').innerHTML = '';
                    document.getElementById('chatHeader').textContent = 'AI Assistant';
                    document.querySelector('.chat-input').style.display = 'none';
                    document.getElementById('welcomeMessage').style.display = 'block';
                }
            } else {
                alert('Failed to delete conversation: ' + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Error deleting conversation:', error);
            alert('Failed to delete conversation');
        });
    }
    
    // Remove context menu
    const contextMenu = document.getElementById('conversationContextMenu');
    if (contextMenu) {
        contextMenu.remove();
    }
}

// Update rename conversation to work with tabs
function renameConversation(conversationId, currentTitle) {
    const newTitle = prompt('Enter new chat name:', currentTitle);
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
                // Update the conversation title in the UI
                const conversationElement = document.querySelector(`[data-conversation-id="${conversationId}"] .tab-title`);
                if (conversationElement) {
                    conversationElement.textContent = newTitle.trim();
                }
            } else {
                alert('Failed to rename conversation: ' + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Error renaming conversation:', error);
            alert('Failed to rename conversation');
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
        // Check if the conversation tab exists
        const conversationTab = document.querySelector(`[data-conversation-id="${savedConversationId}"]`);
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
