"""
Notal Platform Onboarding Knowledge Base
Comprehensive onboarding content for helping new users understand and use the platform
"""

from typing import Dict, List, Any
from dataclasses import dataclass

@dataclass
class OnboardingGuide:
    """Represents an onboarding guide or tutorial"""
    title: str
    user_types: List[str]
    content: str
    steps: List[str]
    related_features: List[str]
    common_questions: List[str]
    tips: List[str]

@dataclass
class FAQ:
    """Represents a frequently asked question"""
    question: str
    answer: str
    category: str
    user_types: List[str]
    related_features: List[str]

class NotalOnboardingKnowledgeBase:
    """Comprehensive onboarding knowledge for new users"""
    
    def __init__(self):
        self.getting_started_guides = self._initialize_getting_started_guides()
        self.faqs = self._initialize_faqs()
        self.task_tutorials = self._initialize_task_tutorials()
        self.navigation_guides = self._initialize_navigation_guides()
        self.common_scenarios = self._initialize_common_scenarios()
        self.troubleshooting = self._initialize_troubleshooting()
    
    def _initialize_getting_started_guides(self) -> Dict[str, OnboardingGuide]:
        """Initialize getting started guides for different user types"""
        return {
            "first_login_planner": OnboardingGuide(
                title="Getting Started as an Event Planner",
                user_types=["Director", "Planner", "Coordinator"],
                content="""Welcome to Notal! As an event planner, you'll use Notal to manage events, track tasks, collaborate with clients and team members, and leverage AI assistance for your event work. The platform streamlines your daily workflow with an intuitive interface and powerful automation. Start by connecting your own email account through Communications, then you can Notalize client emails to bring them into the platform.""",
                steps=[
                    "Log in and explore the Dashboard - Your central hub showing upcoming deadlines, calendar, recent activity, and quick access to all features",
                    "Review the left sidebar navigation - Access Dashboard, Events, Tasks, Calendar, Communications, Documents, Teams, and Settings",
                    "Go to Communications - Navigate to Communications in the sidebar to access email integration features",
                    "Sync your own email account first - Connect your Gmail or Outlook account through secure OAuth authentication. This allows you to view your inbox within Notal",
                    "View your inbox - Once your email is synced, you'll be able to see your email inbox in the Communications hub",
                    "Notalize client emails - After your inbox is visible, you can start notalizing client emails. Click 'Notalize' on emails from clients to process and transfer them to the Notal platform",
                    "Add the new client through the new client flow once confirmed - After notalizing client emails, use the 'New Client' flow to formally add them to your organization",
                    "Select your client in the clients sidebar on the left - Once added, you'll see them in the clients list for easy access",
                    "Begin by analyzing the client's intake through the Communications hub - Review their Notalized emails and communications to understand their needs and event vision",
                    "Create your first event - Click 'Events' in the sidebar, then '+ New Event' button, and link it to the client you just added",
                    "Create tasks and subtasks - Break down event work into actionable tasks with subtasks for detailed tracking",
                    "Set up calendar events - Schedule important deadlines, vendor walkthroughs, and meetings in the Calendar section",
                    "Try the Notal AI Assistant - Click the chat icon in the top-right to get instant help and guidance throughout your workflow"
                ],
                related_features=["dashboard", "events", "tasks", "notal_ai_assistant", "calendar", "communications", "teams", "email_integration", "Notalize"],
                common_questions=[
                    "How do I invite a new client?",
                    "How does the Notalize email sync work?",
                    "How do I create my first event?",
                    "Where can I see all my deadlines?",
                    "How do I communicate with my team?",
                    "Can I sync client emails automatically?"
                ],
                tips=[
                    "Start by syncing your own email first - you must connect your Gmail or Outlook account before you can Notalize client emails",
                    "Go to Communications first - this is where you'll sync your email and access your inbox",
                    "Once your inbox is visible, you can Notalize client emails - this processes and transfers emails to the Notal platform",
                    "Use Notalize to bring client communications into Notal - this automatically transfers their emails for seamless management",
                    "The new client flow guides you through adding clients properly after notalizing their emails",
                    "Use the clients sidebar to quickly switch between different client organizations",
                    "Analyze client intake in Communications before creating events - this ensures you have all the context from their Notalized emails",
                    "Use the Quick Access grid on the dashboard to jump to frequently used features",
                    "The Notal AI Assistant can answer questions about the platform and help with event planning tasks",
                    "Each event automatically gets its own communications channel for team collaboration",
                    "Profile icons show you who's assigned to each event and task at a glance"
                ]
            ),
            
            "first_login_client": OnboardingGuide(
                title="Getting Started as a Client",
                user_types=["Client"],
                content="""Welcome to Notal! As a client, you'll use Notal to track your events, communicate with your event planning team, view important documents, and get instant answers through our AI assistant. Everything you need to stay informed about your event is in one place.""",
                steps=[
                    "Log in and view your Dashboard - See an overview of your events, upcoming deadlines, and recent activity",
                    "Ask the Notal AI Assistant - Click the chat icon to ask questions about your event or get planning guidance",
                    "View your events - Click 'Events' to see all your active events and their progress",
                    "Check Communications - See messages from your event planning team in the Communications section",
                    "Review documents - Access event documents in the Documents section (coming soon with full features)",
                    "Stay updated on deadlines - Your calendar shows all important dates and meetings"
                ],
                related_features=["dashboard", "notal_ai_assistant", "events", "communications", "calendar"],
                common_questions=[
                    "How do I ask my event planner a question?",
                    "Where can I see the status of my event?",
                    "How do I know when I have new messages?",
                    "Can I access my documents anytime?"
                ],
                tips=[
                    "The Notal AI can answer common event planning questions 24/7 when your planner isn't available",
                    "You'll receive notifications for new messages and important updates",
                    "Click on any event to see detailed information, tasks, and conversation history",
                    "The dashboard Daily Briefing shows you what needs your attention today"
                ]
            ),
            
            "first_login_business": OnboardingGuide(
                title="Getting Started as a Business User",
                user_types=["Business"],
                content="""Welcome to Notal! As a business user, you'll manage your company's events, coordinate with your event planning team, track compliance requirements, and oversee multiple events. Notal provides the tools to keep your business's event operations organized and efficient.""",
                steps=[
                    "Access your Dashboard - View all active events, metrics, and firm summary",
                    "Review business events - Click 'Events' to see all events for your business",
                    "Invite team members - Add colleagues who need access to events in the Teams section",
                    "Set up compliance tracking - Create events for ongoing compliance requirements",
                    "Use Communications - Coordinate with your event planning team and business stakeholders",
                    "Monitor progress - Track task completion and event status across all your events"
                ],
                related_features=["dashboard", "events", "tasks", "teams", "communications", "compliance"],
                common_questions=[
                    "How do I track multiple events?",
                    "Can multiple team members access our events?",
                    "How do I monitor our compliance requirements?",
                    "Where can I see overall progress across all events?"
                ],
                tips=[
                    "The Firm Summary card shows key metrics across all your events",
                    "Create separate events for different event types (weddings, corporate events, galas, conferences, etc.)",
                    "Use the filter feature in Events to focus on specific status or priority items",
                    "The History page provides a complete audit trail of all activities"
                ]
            ),
            
            "platform_overview": OnboardingGuide(
                title="Notal Platform Overview - Complete Feature Guide",
                user_types=["All"],
                content="""Notal is a comprehensive event planning management platform with AI-powered assistance. This guide explains what each feature does and when to use it.""",
                steps=[
                    "Dashboard: Your daily starting point with AI chat, deadlines, calendar, and quick access to everything",
                    "Events: Create and manage events with team assignments, status tracking, and progress monitoring",
                    "Tasks: Assign work items, track progress, set priorities, and manage subtasks across all events",
                    "Calendar: Schedule meetings, deadlines, and manage attendees with full calendar views",
                    "Communications: Real-time team chat with event-specific channels and direct messaging",
                    "Documents: Store and organize event files (full features coming soon - currently shows UI preview)",
                    "Teams: Manage organization members, roles, and permissions",
                    "Settings: Configure firm details, user management, and preferences",
                    "History: Complete audit log of all platform activities",
                    "Notal AI: Intelligent assistant available throughout the platform for instant help"
                ],
                related_features=["dashboard", "events", "tasks", "calendar", "communications", "documents", "teams", "firm_settings", "history", "notal_ai_assistant"],
                common_questions=[
                    "What's the difference between Events and Tasks?",
                    "Where should I start each day?",
                    "How do I communicate with my team?",
                    "What can the Notal AI help me with?"
                ],
                tips=[
                    "Start each day on the Dashboard to see what needs your attention",
                    "Events are the big picture (weddings, galas, conferences, etc.), Tasks are the specific work items within them",
                    "Use Communications for quick team coordination without email overload",
                    "Ask Notal AI anything - it understands the platform and can guide you to the right feature"
                ]
            )
        }
    
    def _initialize_faqs(self) -> List[FAQ]:
        """Initialize frequently asked questions"""
        return [
            # Navigation FAQs
            FAQ(
                question="How do I navigate between different sections of Notal?",
                answer="Use the left sidebar navigation which contains icons and labels for Dashboard, Events, Tasks, Calendar, Communications, Documents, Teams, and Settings. Click any icon to go to that section. On mobile, tap the menu icon to open the navigation.",
                category="navigation",
                user_types=["All"],
                related_features=["navigation_system"]
            ),
            FAQ(
                question="Where can I see all my upcoming deadlines?",
                answer="Upcoming deadlines appear in three places: (1) The Upcoming Deadlines card on the left side of your Dashboard, (2) The Calendar page with full month/week/day views, and (3) On individual event pages under the Details tab.",
                category="navigation",
                user_types=["All"],
                related_features=["dashboard", "calendar", "events"]
            ),
            FAQ(
                question="How do I access the AI assistant?",
                answer="Click the Notal AI chat icon in the top-right corner of any page. A chat panel will slide in from the right where you can ask questions, get help, and receive event planning guidance. The AI assistant knows about your events and can provide contextual help.",
                category="ai_features",
                user_types=["All"],
                related_features=["notal_ai_assistant"]
            ),
            
            # Event Management FAQs
            FAQ(
                question="How do I create a new event?",
                answer="Go to the Events page using the sidebar navigation, then click the blue '+ New Event' button at the top of the page. Fill in the event details including client name, event type, description, and status. You can also assign team members during creation or add them later.",
                category="events",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["events"]
            ),
            FAQ(
                question="Can I see all tasks for a specific event?",
                answer="Yes! Navigate to an event by clicking it in the Events list, then click the 'Tasks' tab in the event details page. This shows all tasks associated with that event. You can also filter tasks by event on the main Tasks page.",
                category="events",
                user_types=["All"],
                related_features=["events", "tasks"]
            ),
            FAQ(
                question="What do the status badges on events mean?",
                answer="Status badges indicate the current state: 'Critical' (red) means urgent attention needed, 'Review' (yellow) means awaiting review, 'In Progress' (blue) means active work is happening, and 'Completed' (green) means the event is finished. You can customize statuses when creating or editing events.",
                category="events",
                user_types=["All"],
                related_features=["events"]
            ),
            FAQ(
                question="How do I assign team members to an event?",
                answer="Open the event details page, click the 'Details' or 'Overview' tab, and look for the Team Members section. Click 'Add Team Member' or the edit icon, then select from your organization's members. Each member's profile picture will appear on the event card.",
                category="events",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["events", "teams"]
            ),
            
            # Task Management FAQs
            FAQ(
                question="How do I create a task?",
                answer="Click 'Tasks' in the sidebar navigation, then click '+ New Task'. Fill in the task name, description, assign to a team member, set a due date and priority, and optionally link it to an event. You can also create tasks directly from an event's Tasks tab.",
                category="tasks",
                user_types=["All"],
                related_features=["tasks"]
            ),
            FAQ(
                question="Can I create subtasks?",
                answer="Yes! Open a task by clicking it, then look for the Subtasks section. Click 'Add Subtask' to create smaller work items within the main task. Subtasks can have their own assignees, due dates, and status tracking.",
                category="tasks",
                user_types=["All"],
                related_features=["tasks"]
            ),
            FAQ(
                question="How do I see only my assigned tasks?",
                answer="On the Tasks page, use the filter options at the top. Click the 'Filter' button and select 'Assigned to Me' or use the assignee dropdown to filter by specific team members. You can also filter by status (To Do, In Progress, Completed) and by event.",
                category="tasks",
                user_types=["All"],
                related_features=["tasks"]
            ),
            FAQ(
                question="What happens when I complete a task?",
                answer="When you mark a task as completed, it moves to the 'Completed' status, appears in the completed filter, and is logged in the event history (if linked to an event). Your team members may receive notifications, and the task progress updates on the event card.",
                category="tasks",
                user_types=["All"],
                related_features=["tasks", "history"]
            ),
            
            # Communications FAQs
            FAQ(
                question="How do I get started with client onboarding?",
                answer="First, go to Communications in the sidebar. You must sync your own email account (Gmail or Outlook) through OAuth authentication before you can Notalize client emails. Once your inbox is visible, you can click 'Notalize' on emails from clients to process and transfer them to the Notal platform. After notalizing client emails, add them through the new client flow.",
                category="communications",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["communications", "email_integration", "Notalize"]
            ),
            FAQ(
                question="What is Notalize and how does it work?",
                answer="Notalize is a feature that processes and transfers client emails into the Notal platform. After you've synced your own email account and can view your inbox in Communications, you can click 'Notalize' on emails from clients. This processes the emails and transfers them to Notal's Communications hub, allowing you to analyze client intake and manage communications in one place.",
                category="communications",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["Notalize", "email_integration", "communications"]
            ),
            FAQ(
                question="How do I sync my email account?",
                answer="Go to Communications in the sidebar. You'll need to connect your Gmail or Outlook account through secure OAuth authentication. This allows you to view your inbox within Notal. Once your inbox is visible, you can then Notalize client emails. The OAuth process is secure and uses industry-standard OAuth 2.0 with encrypted token storage.",
                category="communications",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["email_integration", "communications"]
            ),
            FAQ(
                question="Can I Notalize client emails before syncing my own email?",
                answer="No, you must first sync your own email account (Gmail or Outlook) through OAuth in the Communications hub. Once your inbox is visible, you can then start notalizing client emails. This ensures you have access to your email inbox first, which is required for the notalization process.",
                category="communications",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["email_integration", "Notalize", "communications"]
            ),
            FAQ(
                question="Is the OAuth email integration secure?",
                answer="Yes! The OAuth email integration uses industry-standard OAuth 2.0 protocol with encrypted token storage. Your client's email credentials are never stored - only secure access tokens are used. The connection is managed through Gmail and Outlook's secure authentication systems, and all data is encrypted in transit and at rest.",
                category="security",
                user_types=["Director", "Planner", "Coordinator", "Client"],
                related_features=["email_integration", "security_privacy"]
            ),
            FAQ(
                question="What email providers does Notal support?",
                answer="Notal currently supports Gmail and Outlook (Microsoft 365) email integration through OAuth. When you click Notalize, you can choose which provider the client uses. The OAuth flow will guide them through the secure connection process specific to their email provider.",
                category="communications",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["email_integration", "Notalize"]
            ),
            FAQ(
                question="How do I send a message to my team?",
                answer="Go to Communications in the sidebar. You'll see channels for each event and direct message options. Click a channel to send messages to everyone in that event, or click a team member's name to send a direct message. Messages are delivered in real-time.",
                category="communications",
                user_types=["All"],
                related_features=["communications"]
            ),
            FAQ(
                question="What's the difference between channels and direct messages?",
                answer="Channels are group conversations tied to specific events where all team members can see messages. Direct messages are private one-on-one conversations between you and another team member. Use channels for event-related discussions and DMs for private conversations.",
                category="communications",
                user_types=["All"],
                related_features=["communications"]
            ),
            FAQ(
                question="Can I message someone from a different organization?",
                answer="Yes! Notal supports cross-organization direct messaging. This is useful when a planner from an event planning company needs to communicate with a client from a business organization. Just search for the person and send them a direct message.",
                category="communications",
                user_types=["All"],
                related_features=["communications"]
            ),
            FAQ(
                question="How do I know if someone is online?",
                answer="In the Communications section, you'll see active member status indicators (green dots) next to names of people who are currently online. The sidebar shows member lists with online status for quick reference.",
                category="communications",
                user_types=["All"],
                related_features=["communications"]
            ),
            
            # Calendar FAQs
            FAQ(
                question="How do I schedule a meeting?",
                answer="Go to Calendar in the sidebar, click '+ New Event' or click on a date/time in the calendar view. Fill in the event title, description, location, start and end times. Add attendees by selecting team members from your organization. The event will appear on everyone's calendar.",
                category="calendar",
                user_types=["All"],
                related_features=["calendar"]
            ),
            FAQ(
                question="Can I see my calendar in different views?",
                answer="Yes! The Calendar page offers Month, Week, and Day views. Click the view buttons at the top of the calendar to switch between them. Month view shows the big picture, while Day view shows detailed hour-by-hour scheduling.",
                category="calendar",
                user_types=["All"],
                related_features=["calendar"]
            ),
            FAQ(
                question="How do I add attendees to a calendar event?",
                answer="When creating or editing an event, look for the Attendees section. Click 'Add Attendees' and select team members from your organization. Attendees will see the event on their calendars and may receive notifications.",
                category="calendar",
                user_types=["All"],
                related_features=["calendar"]
            ),
            
            # Teams & Settings FAQs
            FAQ(
                question="How do I add new team members to my organization?",
                answer="Go to Settings, then look for the 'Add Person' or team management section. Enter the person's name, email, and select their role (Director, Planner, Coordinator, Client). They'll receive an invitation to join your organization on Notal.",
                category="teams",
                user_types=["Director"],
                related_features=["teams", "firm_settings"]
            ),
            FAQ(
                question="What are the different user roles and what can they do?",
                answer="Director: Full access to all features, can manage organization, add members, view all events. Planner: Can create/manage events, tasks, and collaborate. Coordinator: Can assist with tasks, documents, and event support. Client: Can view their own events, communicate with the event planning team, and access their documents. Each role has appropriate permissions.",
                category="teams",
                user_types=["Director"],
                related_features=["teams", "firm_settings"]
            ),
            FAQ(
                question="How do I update my account settings?",
                answer="Click the user icon in the top-right corner, then select 'Account Settings' from the dropdown menu. You can update your profile (name, email, photo), security settings (password, 2FA), notification preferences, and personal preferences.",
                category="settings",
                user_types=["All"],
                related_features=["account_settings"]
            ),
            
            # AI Assistant FAQs
            FAQ(
                question="What can the Notal AI assistant help me with?",
                answer="Notal AI can: (1) Answer questions about using the platform, (2) Explain contract and vendor terms in simple language, (3) Analyze your conversations and extract key goals, (4) Suggest professional responses, (5) Provide event planning guidance, (6) Help you navigate to the right features, and (7) Summarize conversations and identify action items.",
                category="ai_features",
                user_types=["All"],
                related_features=["notal_ai_assistant"]
            ),
            FAQ(
                question="Is my conversation with the AI assistant private?",
                answer="Yes! Your AI conversations are private and tied to your account. They're stored securely and can be accessed from the conversation tabs in the AI chat panel. The AI uses context from your events to provide better assistance, but always maintains confidentiality.",
                category="ai_features",
                user_types=["All"],
                related_features=["notal_ai_assistant"]
            ),
            FAQ(
                question="Can the AI help me understand vendor contracts?",
                answer="Yes! The AI's ClarityAgent feature specializes in explaining contract and vendor language in simple, understandable terms. You can ask it to explain venue contracts, vendor agreements, or specific contract terms, and it will break them down into plain English.",
                category="ai_features",
                user_types=["Client", "Business"],
                related_features=["notal_ai_assistant"]
            ),
            
            # Security & Privacy FAQs
            FAQ(
                question="Is my data secure on Notal?",
                answer="Yes! Notal uses enterprise-grade security with Azure OpenAI and Azure SQL Database. All data is encrypted at rest and in transit. We implement multi-factor authentication, role-based access control, comprehensive audit logging, and follow industry best practices. Notal leverages Azure's SOC 2, ISO 27001, and GDPR compliance frameworks and is working toward its own SOC 2 Type II certification.",
                category="security",
                user_types=["All"],
                related_features=["security_privacy"]
            ),
            FAQ(
                question="Who can see my events and communications?",
                answer="Only team members assigned to a specific event can see its details, tasks, and communications. Directors and administrators have broader access to manage the organization. Clients can only see events they're involved in. Everything is controlled by role-based permissions and organization membership.",
                category="security",
                user_types=["All"],
                related_features=["security_privacy", "teams"]
            ),
            FAQ(
                question="Can I see a history of all actions taken on my events?",
                answer="Yes! Go to the History page from the sidebar or view event-specific history on each event's History tab. You'll see a complete audit trail of all activities including who created/updated events, completed tasks, sent messages, and made changes. Each entry includes timestamps and user attribution.",
                category="security",
                user_types=["All"],
                related_features=["history"]
            ),
            
            # General Platform FAQs
            FAQ(
                question="I'm stuck! How do I get help?",
                answer="Several ways to get help: (1) Ask the Notal AI assistant - click the chat icon and ask your question, (2) Look for the '?' help icon in various sections, (3) Check the Daily Briefing and Next Suggestions on your Dashboard for guidance, (4) Contact your organization's administrator or your event planning team for specific questions.",
                category="general",
                user_types=["All"],
                related_features=["notal_ai_assistant", "dashboard"]
            ),
            FAQ(
                question="Can I use Notal on my mobile device?",
                answer="Yes! Notal has a responsive mobile-first design. Access it through your mobile browser at the same URL. The interface automatically adapts to your screen size with a hamburger menu for navigation and optimized layouts for touch interaction.",
                category="general",
                user_types=["All"],
                related_features=["navigation_system"]
            ),
            FAQ(
                question="Where should I start if I'm completely new?",
                answer="For event planning company users, start by going to Communications. First, sync your own email account (Gmail or Outlook) through OAuth authentication. Once your inbox is visible, you can then Notalize client emails by clicking 'Notalize' on emails from clients - this processes and transfers them to the Notal platform. After notalizing client emails, add them through the new client flow. Once added, select them in the clients sidebar, analyze their intake through Communications, then create events, tasks, subtasks, and calendar entries. You can also start at the Dashboard - it's designed as your daily starting point. From there: (1) Try the Notal AI chat to ask questions (try 'How do I get started?' or 'Notal, let's get started'), (2) Click through the Quick Access grid to explore features, (3) Check the Daily Briefing for today's important items, (4) Review the Next Suggestions for guided next steps. The AI assistant can walk you through any feature.",
                category="general",
                user_types=["Director", "Planner", "Coordinator"],
                related_features=["dashboard", "notal_ai_assistant", "communications", "email_integration", "Notalize"]
            ),
            FAQ(
                question="Where should I start if I'm completely new?",
                answer="Start at the Dashboard! It's designed as your daily starting point. From there: (1) Try the Notal AI chat to ask questions, (2) Click through the Quick Access grid to explore features, (3) Check the Daily Briefing for today's important items, (4) Review the Next Suggestions for guided next steps. The AI assistant can walk you through any feature.",
                category="general",
                user_types=["Client", "Business"],
                related_features=["dashboard", "notal_ai_assistant"]
            )
        ]
    
    def _initialize_task_tutorials(self) -> Dict[str, OnboardingGuide]:
        """Initialize step-by-step task tutorials"""
        return {
            "create_first_event": OnboardingGuide(
                title="Tutorial: Creating Your First Event",
                user_types=["Director", "Planner", "Coordinator"],
                content="""An event (also called an engagement) is the central organizational unit in Notal. Everything - tasks, communications, documents - connects to an event. Here's how to create your first one.""",
                steps=[
                    "Click 'Events' in the left sidebar navigation",
                    "Look for the blue '+ New Event' button at the top of the Events page and click it",
                    "Enter the Client Name (e.g., 'Acme Corporation' or 'John Smith')",
                    "Choose the Event Type from the dropdown (Wedding, Corporate Event, Conference, Gala, etc.)",
                    "Write a brief Description explaining what this event is about",
                    "Set the Status (Active, In Review, etc.) - you can change this anytime",
                    "Optionally set a Due Date if there's a major deadline",
                    "Click 'Add Team Member' to assign planners, coordinators, or the client",
                    "Click 'Create Event' or 'Save' to finish",
                    "Your new event appears in the Events list with a card showing key info",
                    "Click the event card to open the details page with Overview, Details, Tasks, Conversations, and History tabs"
                ],
                related_features=["events"],
                common_questions=[
                    "Can I edit the event details later? Yes, click the event and use the edit button",
                    "Do I have to assign team members now? No, you can add them anytime",
                    "What if I don't know the due date yet? Leave it blank and add it later"
                ],
                tips=[
                    "Use descriptive event names so team members instantly know what it's about",
                    "Assign yourself and relevant team members right away for better collaboration",
                    "Each event automatically gets its own Communications channel",
                    "You can create tasks directly from the event page after creation"
                ]
            ),
            
            "assign_first_task": OnboardingGuide(
                title="Tutorial: Assigning Your First Task",
                user_types=["Director", "Planner", "Coordinator"],
                content="""Tasks are specific work items that need to be completed. They can be standalone or linked to an event. Here's how to create and assign your first task.""",
                steps=[
                    "Click 'Tasks' in the left sidebar navigation",
                    "Click the '+ New Task' button at the top of the Tasks page",
                    "Enter a clear Task Title (e.g., 'Confirm venue booking' or 'Review vendor contract')",
                    "Write a Description with details about what needs to be done",
                    "Click 'Assign To' and select a team member (or assign to yourself)",
                    "Set a Due Date using the date picker",
                    "Choose a Priority level: Low, Medium, or High",
                    "Optionally link to an Event using the event dropdown",
                    "Click 'Create Task' or 'Save'",
                    "The assignee will see the task in their task list",
                    "The task appears on the linked event's Tasks tab",
                    "Track progress by updating Status: To Do → In Progress → Completed"
                ],
                related_features=["tasks", "events"],
                common_questions=[
                    "Can I reassign a task later? Yes, edit the task and change the assignee",
                    "What happens when someone completes a task? It moves to Completed status and appears in event history",
                    "Can one task have multiple assignees? Currently one primary assignee, but you can use subtasks for team collaboration"
                ],
                tips=[
                    "Use clear, action-oriented task titles (start with verbs: Confirm, Review, Book, Schedule)",
                    "Add detailed descriptions so the assignee knows exactly what to do",
                    "Link tasks to events for better organization and tracking",
                    "Create subtasks for complex work that needs multiple steps",
                    "Use High priority for urgent items that need immediate attention"
                ]
            ),
            
            "start_team_conversation": OnboardingGuide(
                title="Tutorial: Starting a Team Conversation",
                user_types=["All"],
                content="""Communications in Notal provides real-time team chat. Each event has its own channel, and you can send direct messages to any team member. Here's how to start communicating.""",
                steps=[
                    "Click 'Communications' in the left sidebar navigation",
                    "You'll see two sections: Channels (for events) and Direct Messages",
                    "For event discussions: Click an event channel from the channels list",
                    "For private conversations: Click 'New Message' or a team member's name in Direct Messages",
                    "Type your message in the input box at the bottom",
                    "Press Enter or click Send to deliver the message",
                    "Your message appears instantly for all channel members or the DM recipient",
                    "You can see who's online by the green status dot next to their name",
                    "Upload files by clicking the attachment icon (if available)",
                    "Use @mentions to notify specific team members in channels"
                ],
                related_features=["communications", "events"],
                common_questions=[
                    "Can clients see all communications? No, only channels for events they're assigned to",
                    "Are messages private? Direct messages are private. Channel messages are visible to all event team members",
                    "Can I search old messages? Yes, most implementations include message search"
                ],
                tips=[
                    "Use event channels for event-related discussions so everyone stays informed",
                    "Use direct messages for quick one-on-one questions",
                    "Check the online status indicators to see who's available",
                    "Communications are organized by event, making it easy to find relevant conversations",
                    "Important decisions made in chat should be documented in event notes or history"
                ]
            ),
            
            "use_ai_assistant": OnboardingGuide(
                title="Tutorial: Using the Notal AI Assistant",
                user_types=["All"],
                content="""The Notal AI Assistant is your intelligent helper available 24/7. It understands the platform, can answer questions, explain contract and vendor terms, and guide you through features. Here's how to use it effectively.""",
                steps=[
                    "Click the Notal AI chat icon in the top-right corner of any page",
                    "A chat panel slides in from the right side",
                    "Type your question or request in the message box",
                    "Examples: 'How do I create an event?', 'Explain our cancellation policy', 'Show me my upcoming deadlines'",
                    "Press Enter or click Send",
                    "Watch the thinking indicator as the AI processes your request",
                    "The AI responds with helpful information, formatted clearly with bullet points and explanations",
                    "You can ask follow-up questions - the AI remembers your conversation context",
                    "Switch between different conversations using the tabs at the top",
                    "Click the X or back button to close the chat panel"
                ],
                related_features=["notal_ai_assistant"],
                common_questions=[
                    "What can I ask the AI? Anything about using Notal, event planning questions, event guidance, feature explanations",
                    "Does the AI know about my events? Yes, it has context about your events and can provide personalized help",
                    "Is it really available 24/7? Yes, the AI is always available, even when your team is offline"
                ],
                tips=[
                    "Ask clear, specific questions for the best results",
                    "The AI can explain contract and vendor terms in simple language - great for clients",
                    "Use it to navigate: 'How do I find my calendar?' or 'Where do I add team members?'",
                    "The AI can analyze your conversations and extract goals or action items",
                    "Try asking for step-by-step guidance: 'Walk me through creating a task'",
                    "The AI understands context, so you can have natural back-and-forth conversations"
                ]
            ),
            
            "schedule_first_meeting": OnboardingGuide(
                title="Tutorial: Scheduling Your First Meeting",
                user_types=["All"],
                content="""The Calendar feature helps you schedule meetings, track deadlines, and coordinate with your team. Here's how to create your first calendar event.""",
                steps=[
                    "Click 'Calendar' in the left sidebar (this may redirect to the global dashboard with calendar widget)",
                    "Navigate to the full Calendar view",
                    "Click '+ New Event' button or click directly on a date/time in the calendar",
                    "Enter the Event Title (e.g., 'Client consultation' or 'Event planning meeting')",
                    "Add a Description with meeting details or agenda",
                    "Enter the Location (venue address, Zoom link, or 'Virtual')",
                    "Set the Start Date and Time",
                    "Set the End Date and Time",
                    "Click 'Add Attendees' and select team members who should attend",
                    "Optionally link to an Event if the meeting is event-related",
                    "Click 'Create Event' or 'Save'",
                    "The event appears on your calendar and attendees' calendars",
                    "View the event by clicking it in any calendar view"
                ],
                related_features=["calendar", "events"],
                common_questions=[
                    "Do attendees get notified? Depending on settings, yes - check notification preferences",
                    "Can I create recurring events? Yes, look for the recurring event option when creating",
                    "What if someone can't attend? They can update their attendance status or message you"
                ],
                tips=[
                    "Include video meeting links in the Location field for remote meetings",
                    "Add an agenda in the Description so everyone comes prepared",
                    "Link meetings to events to keep everything organized",
                    "Use the Week or Day view for detailed scheduling of back-to-back meetings",
                    "Check the Upcoming Deadlines card on the Dashboard for a quick overview",
                    "Color-code or categorize events for easy visual scanning"
                ]
            ),
            
            "navigate_event_details": OnboardingGuide(
                title="Tutorial: Navigating Event Details",
                user_types=["All"],
                content="""The Event Details page is your command center for each event. It has multiple tabs with different information. Here's how to navigate and use it effectively.""",
                steps=[
                    "Go to Events in the sidebar and click any event card",
                    "You're now in the Event Details page - notice the tabs at the top",
                    "OVERVIEW tab: See event summary, key info, status, team members, and quick stats",
                    "DETAILS tab: View complete event information, client details, dates, description, and edit button",
                    "TASKS tab: See all tasks for this event, create new tasks, and track progress",
                    "CONVERSATIONS tab: Access the event's communications channel and message history",
                    "HISTORY tab: Review complete audit trail of all activities on this event",
                    "Use the event carousel (if visible) to navigate between events without going back to the list",
                    "Click the Edit button (on Details tab) to modify event information",
                    "Click breadcrumbs or back button to return to the Events list"
                ],
                related_features=["events", "tasks", "communications", "history"],
                common_questions=[
                    "Which tab should I check daily? Overview for quick status, Tasks for work items",
                    "Where do I see who's working on what? Overview and Tasks tabs show assignees",
                    "How do I know what's changed recently? Check the History tab for all changes"
                ],
                tips=[
                    "The Overview tab gives you the quickest snapshot of event status",
                    "Tasks tab is where daily work happens - check it frequently",
                    "Conversations tab keeps all event discussions in one place",
                    "History tab is essential for audit trails and compliance",
                    "Use the carousel to quickly jump between related events",
                    "Profile icons show team members at a glance - click to see their details"
                ]
            )
        }
    
    def _initialize_navigation_guides(self) -> Dict[str, str]:
        """Initialize navigation and UI guidance"""
        return {
            "sidebar_navigation": """
The left sidebar is your main navigation hub. From top to bottom:
- **Dashboard**: Your daily starting point with overview and quick access
- **Events** (briefcase icon): All your events
- **Tasks** (progress bars icon): Work items and assignments
- **Calendar** (calendar icon): Events, meetings, and deadlines
- **Communications** (chat icon): Team chat and direct messages
- **Documents** (folder icon): Event files and documents
- **Teams** (people icon): Organization members and roles
- **Settings** (gear icon): Firm and account configuration
- **Logout** (at bottom): Sign out of your account

On mobile, tap the hamburger menu (≡) to open the sidebar.
""",
            
            "dashboard_layout": """
The Dashboard has two main columns:

**Left Column (28% width, gray background):**
- **Upcoming Deadlines**: Next 5 deadlines with priority indicators
- **Calendar Widget**: Current month with today highlighted
- **Recent Activity**: Latest files and updates

**Right Column (72% width):**
- **Notal AI Chat**: Large "What can I help with?" search box
- **Quick Access Grid**: 4x2 grid of main features (Clients, Calendar, Billing, History, Events, Tasks, Communications, Documents)
- **Firm Summary**: Key metrics and trends (active events, completed tasks, billing backlog, team utilization)
- **Daily Briefing**: Today's important items
- **Next Suggestions**: AI-powered recommendations for next actions

All cards have rounded corners (12px radius) and subtle shadows.
""",
            
            "event_card_anatomy": """
Each event card shows:
- **Client/Event Name** (top, bold)
- **Status Badge** (colored): Critical (red), Review (yellow), In Progress (blue), Completed (green)
- **Progress Indicator**: "X of Y tasks completed"
- **Due Date**: With calendar icon if set
- **Team Member Profile Pictures**: Circular avatars of assigned team
- **Hover Effect**: Card lifts with shadow when you hover over it

Click anywhere on the card to open the full event details page.
""",
            
            "ai_chat_panel": """
The Notal AI chat panel:
- **Location**: Slides in from the right side
- **Access**: Click the Notal AI icon in top-right corner
- **Features**:
  - Conversation tabs at top (switch between multiple conversations)
  - Message history in the middle (scrollable)
  - Your messages: Right side with your profile icon
  - AI messages: Left side with "Notal AI" branding
  - Input box at bottom with Send button
  - Thinking indicators show when AI is processing
  - Sticky message overlay for reviewing past messages
- **Close**: Click X button or click outside the panel
""",
            
            "top_navigation": """
The top bar (header) contains:
- **Left**: Organization/firm logo and name
- **Center**: Page title or breadcrumbs
- **Right**: 
  - Notifications icon (bell) with unread count badge
  - Notal AI chat icon
  - User profile picture dropdown (Account Settings, Billing & Plan, Logout)

On the global dashboard, there's also a "Clients" button for event planning companies managing multiple client organizations.
""",
            
            "quick_access_grid": """
The Quick Access grid on the Dashboard provides one-click access to main features:

**Row 1:**
- Clients/Teams: Manage organization members
- Calendar: View schedule and events
- Billing: View billing and subscriptions (placeholder)
- History: Audit log and activity trail

**Row 2:**
- Events: All your events
- Tasks: Work items and assignments
- Communications: Team chat and DM
- Documents: Files and documents (preview)

Each card has an icon, label, and hover effect. Click to go directly to that feature.
"""
        }
    
    def _initialize_common_scenarios(self) -> Dict[str, OnboardingGuide]:
        """Initialize common usage scenarios"""
        return {
            "new_client_onboarding": OnboardingGuide(
                title="Scenario: Onboarding a New Client",
                user_types=["Director", "Planner", "Coordinator"],
                content="""A new client just signed a contract. Here's the complete workflow to onboard them into Notal using the modern client flow with Communications hub and email integration. You must first sync your own email account before you can Notalize client emails.""",
                steps=[
                    "Go to Communications - Navigate to Communications in the sidebar to access email integration features",
                    "Sync your own email account first - Connect your Gmail or Outlook account through secure OAuth authentication. This allows you to view your inbox within Notal",
                    "View your inbox - Once your email is synced, you'll be able to see your email inbox in the Communications hub",
                    "Notalize client emails - After your inbox is visible, you can start notalizing emails from clients. Click 'Notalize' on client emails to process and transfer them to the Notal platform",
                    "Wait for notalization to complete - Once Notalized, the client's emails will be processed and transferred into Notal's Communications hub",
                    "Add the new client through the new client flow - After notalizing client emails, navigate to the New Client page and use the join code system to formally add them to your organization",
                    "Select your client in the clients sidebar on the left - Once added, you'll see them in the clients list for easy access",
                    "Begin by analyzing the client's intake through the Communications hub - Review their Notalized emails and communications to understand their needs, event vision, and key goals",
                    "Create a new event for their occasion: Click Events → + New Event",
                    "Add event details including client name, event type, and description based on your intake analysis",
                    "Assign yourself and any team members (planners, coordinators) to the event",
                    "Add the client as a team member on the event so they can view progress",
                    "Create initial tasks: Go to the event's Tasks tab → + New Task",
                    "Create tasks for client intake, vendor research, initial venue scouting, etc., based on insights from the Communications hub",
                    "Create subtasks for complex work items - Break down larger tasks into manageable subtasks",
                    "Send a welcome message: Go to Communications → find the event channel → send introduction",
                    "Schedule a kickoff meeting: Go to Calendar → + New Event → add client as attendee",
                    "The client receives their invitation and can log in to see everything, including their Notalized emails"
                ],
                related_features=["events", "teams", "tasks", "communications", "calendar", "email_integration", "Notalize"],
                common_questions=[
                    "Do I need to sync my own email first? Yes, you must sync your own Gmail or Outlook account in Communications before you can Notalize client emails",
                    "What if I can't see my inbox? Make sure you've completed the OAuth authentication process for your email account in Communications",
                    "Can I Notalize client emails before syncing my own email? No, you must sync your own email and view your inbox first before notalizing client emails",
                    "How secure is the OAuth email integration? Very secure - uses industry-standard OAuth 2.0 with encrypted token storage",
                    "What if the client has never used event planning software? The Notal AI can guide them through, and Notalized emails make it seamless",
                    "Can I customize what the client sees? Yes, through role-based permissions"
                ],
                tips=[
                    "Always sync your own email first - you must connect your Gmail or Outlook account before you can Notalize client emails",
                    "Go to Communications first - this is where you'll sync your email and access your inbox",
                    "Once your inbox is visible, you can Notalize client emails - this processes and transfers emails to the Notal platform",
                    "Use Notalize to bring client communications into Notal - this automatically transfers their emails for seamless management",
                    "Analyze client intake in Communications before creating events - this ensures you have all the context and goals from their Notalized emails",
                    "The new client flow guides you through proper client addition after notalizing their emails",
                    "Use the clients sidebar to quickly switch between different client organizations",
                    "Create an event template for common event types to speed up onboarding after intake analysis",
                    "Send a welcome message immediately so the client knows you're ready",
                    "Add tasks with near-term deadlines to show immediate progress",
                    "Use subtasks to break down complex work into manageable pieces",
                    "Schedule a brief platform walkthrough meeting for new clients",
                    "The client can use Notal AI 24/7 to ask questions about their event"
                ]
            ),
            
            "daily_workflow_planner": OnboardingGuide(
                title="Scenario: Daily Workflow for Event Planners",
                user_types=["Director", "Planner", "Coordinator"],
                content="""How to use Notal efficiently in your daily event planning workflow.""",
                steps=[
                    "Start your day at the Dashboard - review Upcoming Deadlines and Daily Briefing",
                    "Check the Firm Summary card for overnight changes in metrics",
                    "Click Communications to review any messages from clients or team overnight",
                    "Go to Tasks and filter 'Assigned to Me' to see your work for today",
                    "Update task statuses as you complete work (To Do → In Progress → Completed)",
                    "Open high-priority events from the Events page to check progress",
                    "Use Notal AI to ask questions like 'summarize this vendor contract' or 'explain our cancellation policy'",
                    "When clients ask questions, use the ReplySuggester feature for professional responses",
                    "Create new tasks as work items emerge during the day",
                    "Schedule any new meetings or deadlines in the Calendar",
                    "End your day by reviewing the Daily Briefing to ensure nothing was missed",
                    "Check History if you need to recall what happened on any event"
                ],
                related_features=["dashboard", "events", "tasks", "communications", "notal_ai_assistant", "calendar"],
                common_questions=[
                    "Do I need to keep Notal open all day? No, but check it 2-3 times for updates",
                    "How do I avoid missing important deadlines? Check Dashboard deadlines daily",
                    "What if I'm overwhelmed with tasks? Use filtering and prioritization, delegate when possible"
                ],
                tips=[
                    "Dashboard first, always - it shows what needs attention",
                    "Mark tasks as 'In Progress' when you start so team knows you're on it",
                    "Use Communications instead of email for faster team coordination",
                    "Ask Notal AI for quick vendor research or contract explanations",
                    "End each day with a quick Dashboard check to plan tomorrow"
                ]
            ),
            
            "urgent_event_handling": OnboardingGuide(
                title="Scenario: Handling an Urgent Event",
                user_types=["Director", "Planner", "Coordinator"],
                content="""A client contacts you with an urgent event issue. Here's how to handle it quickly in Notal.""",
                steps=[
                    "Immediately create a new event: Events → + New Event",
                    "Set the status to 'Critical' and add URGENT to the event name",
                    "Add a clear description of the urgent issue and deadline",
                    "Set the due date to the deadline date",
                    "Assign all relevant team members who need to work on this",
                    "Create high-priority tasks with specific owners and near-term deadlines",
                    "Go to Communications and message the team in the event channel about urgency",
                    "Or send direct messages to each team member if immediate action needed",
                    "Add a calendar event for any urgent vendor deadlines or walkthroughs",
                    "Use Notal AI to quickly research vendors or analyze relevant contract terms",
                    "Update the event regularly as status changes",
                    "Keep the client informed through Communications or DM",
                    "Document everything in History for the audit trail"
                ],
                related_features=["events", "tasks", "communications", "calendar", "notal_ai_assistant"],
                common_questions=[
                    "How do I make sure everyone sees this is urgent? Use Critical status, high priority tasks, and message the team",
                    "Can I escalate to directors quickly? Yes, assign them to the event and DM them",
                    "What if it's after hours? Communications and tasks work 24/7, team sees them when they log in"
                ],
                tips=[
                    "Use 'Critical' status and visual indicators to signal urgency",
                    "Break down the urgent event into immediate tasks with hourly/daily deadlines",
                    "Over-communicate in the early hours - message the team multiple times",
                    "Use Calendar to block time for urgent work",
                    "Notal AI can help with quick vendor research while you coordinate the team",
                    "Update the client frequently through Communications to manage expectations"
                ]
            ),
            
            "cross_team_collaboration": OnboardingGuide(
                title="Scenario: Collaborating Across Teams",
                user_types=["Director", "Planner", "Coordinator"],
                content="""A complex event requires collaboration between multiple team members. Here's how to coordinate effectively.""",
                steps=[
                    "Ensure all team members are assigned to the event",
                    "Break down the work into specific tasks, each with a clear owner",
                    "Use the event's Communications channel for all event-related discussions",
                    "Create a shared Calendar event for a team strategy meeting",
                    "In Communications, use @mentions to notify specific team members",
                    "Share documents in the Documents section (when fully available)",
                    "Update task statuses regularly so everyone knows progress",
                    "Use subtasks if a task requires multiple people",
                    "Check the event's History tab to see what others have done",
                    "Hold regular check-ins through Calendar events",
                    "Use Direct Messages for quick one-on-one questions",
                    "Keep the client informed through periodic updates in Communications"
                ],
                related_features=["events", "tasks", "communications", "calendar", "teams"],
                common_questions=[
                    "How do I know what others are working on? Check the Tasks tab and filter by assignee",
                    "What if team members are in different time zones? Async communication via Communications works well",
                    "How do I avoid duplicate work? Clear task assignments and regular status updates"
                ],
                tips=[
                    "Assign clear task owners - no ambiguity about who does what",
                    "Use event channels for group discussions, DMs for quick questions",
                    "Check online status in Communications to see who's available now",
                    "Regular status updates prevent confusion and duplication",
                    "The History tab shows everything everyone has done on the event",
                    "Schedule recurring team check-in meetings for complex, long-running events"
                ]
            )
        }
    
    def _initialize_troubleshooting(self) -> Dict[str, Dict[str, str]]:
        """Initialize troubleshooting guides"""
        return {
            "cant_find_feature": {
                "problem": "I can't find a specific feature or page",
                "solutions": """
1. Check the left sidebar navigation - all main features are listed there
2. Use the Quick Access grid on the Dashboard to jump to features
3. Ask the Notal AI: "Where do I find [feature name]?"
4. Check if you're looking at the right organization (if you're in multiple)
5. Verify your user role has permission to access that feature
6. Try refreshing the page if the sidebar isn't loading properly
                """
            },
            
            "no_events_showing": {
                "problem": "My events list is empty or not showing expected events",
                "solutions": """
1. Check if you have filters applied at the top of the Events page - clear them
2. Verify you're in the correct organization (check org name at top)
3. If you're a client, you only see events you're assigned to
4. Check the status filter - events might be in a different status category
5. If you just created an event, try refreshing the page
6. Ask your administrator if your permissions are set correctly
                """
            },
            
            "cant_assign_tasks": {
                "problem": "I can't assign tasks to certain team members",
                "solutions": """
1. Verify the team member is part of your organization (check Teams page)
2. Confirm they're assigned to the relevant event if it's an event-specific task
3. Check if your user role has permission to create/assign tasks
4. Ensure the person hasn't left the organization
5. Try refreshing the team member list in the task assignment dropdown
6. Contact your organization administrator to verify permissions
                """
            },
            
            "messages_not_sending": {
                "problem": "My messages in Communications aren't sending",
                "solutions": """
1. Check your internet connection - real-time messaging requires stable connection
2. Verify you're a member of the channel you're trying to message
3. Ensure the recipient hasn't blocked you (for direct messages)
4. Try refreshing the page to re-establish the connection
5. Check if there's a character limit and your message is too long
6. Clear your browser cache and cookies
7. Try a different browser to rule out browser-specific issues
                """
            },
            
            "ai_not_responding": {
                "problem": "The Notal AI assistant isn't responding to my questions",
                "solutions": """
1. Check your internet connection - AI requires server communication
2. Try closing and reopening the AI chat panel
3. Verify the AI service is running (may show status indicator)
4. Try a simpler question first to test if it's working at all
5. Clear your browser cache and refresh the page
6. Check if you've hit a daily usage limit (unlikely but possible)
7. Try a different browser
8. Contact support if the issue persists
                """
            },
            
            "calendar_events_not_showing": {
                "problem": "Calendar events aren't appearing or showing incorrectly",
                "solutions": """
1. Check the calendar view (Month/Week/Day) - event might be in a different view
2. Verify the date range you're viewing includes the event date
3. Check if you're viewing the correct organization's calendar
4. Ensure you're an attendee or creator of the event
5. Try refreshing the page to reload events from the server
6. Check your time zone settings if event times seem wrong
7. Verify the event was actually saved (check History for confirmation)
                """
            },
            
            "forgot_password": {
                "problem": "I forgot my password or can't log in",
                "solutions": """
1. Click 'Forgot Password' on the login page
2. Enter your email address - you'll receive a password reset link
3. Check your spam/junk folder if you don't see the email
4. Verify you're using the correct email address (the one your organization used)
5. Wait a few minutes - email delivery can take time
6. If you still can't reset, contact your organization administrator
7. For new users, check if you need to complete registration first
                """
            },
            
            "profile_picture_not_showing": {
                "problem": "Profile pictures aren't displaying correctly",
                "solutions": """
1. Verify the image file is in a supported format (JPG, PNG, GIF)
2. Check the image file size isn't too large (usually < 5MB)
3. Try uploading a different image to rule out file corruption
4. Clear your browser cache to reload images
5. Refresh the page after uploading
6. Try a different browser to rule out browser caching issues
7. Check your account settings to ensure the upload was saved
                """
            },
            
            "slow_performance": {
                "problem": "Notal is running slowly or pages aren't loading quickly",
                "solutions": """
1. Check your internet connection speed and stability
2. Clear your browser cache and cookies
3. Close unnecessary browser tabs to free up memory
4. Try a different browser (Chrome, Firefox, Edge, Safari)
5. Disable browser extensions that might interfere
6. Check if your computer has sufficient resources (RAM, CPU)
7. Try accessing during off-peak hours if server load is high
8. Contact support if slowness persists across all activities
                """
            }
        }

# Global instance
notal_onboarding_kb = NotalOnboardingKnowledgeBase()
