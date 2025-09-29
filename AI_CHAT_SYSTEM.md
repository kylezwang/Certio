# Certio AI-Powered Chat System

## 🚀 Overview

This is a comprehensive AI-powered chat system designed for legal services, featuring four specialized AI agents that provide real-time analysis, suggestions, and insights during conversations between clients, team members, lawyers, and businesses.

## 🤖 AI Agents

### 1. ChatSummarizer
- **Purpose**: Analyzes conversations in real-time
- **Features**:
  - Generates conversation summaries
  - Identifies key discussion points
  - Analyzes sentiment (Positive, Negative, Neutral)
  - Assesses urgency levels (Low, Medium, High, Urgent)
  - Suggests next actions

### 2. ClientGoalExtractor
- **Purpose**: Extracts business and legal goals from client conversations
- **Features**:
  - Identifies primary and secondary goals
  - Determines business type and legal area
  - Extracts timeline and budget information
  - Lists required documents
  - Categorizes legal practice areas

### 3. ReplySuggester
- **Purpose**: Suggests professional replies for team and lawyers
- **Features**:
  - Generates contextually appropriate responses
  - Suggests appropriate tone (Professional, Friendly, Formal, Casual)
  - Identifies key points to address
  - Flags when legal review is required
  - Adapts to different user types

### 4. ClarityAgent
- **Purpose**: Explains complex legal language in simple terms
- **Features**:
  - Simplifies legal jargon for clients
  - Identifies key legal terms
  - Explains implications
  - Assesses risk levels
  - Provides recommended actions

## 🏗️ Architecture

### Backend (ASP.NET Core)
- **ChatController**: Handles HTTP requests and responses
- **ChatService**: Manages conversation logic and AI agent integration
- **AIAgentService**: Communicates with Python AI service
- **SignalR**: Real-time communication for live chat

### AI Service (Python FastAPI)
- **FastAPI**: RESTful API for AI agent endpoints
- **OpenAI Integration**: GPT-4 powered AI agents
- **Async Processing**: Parallel agent processing for efficiency

### Frontend (Razor Pages + JavaScript)
- **Real-time Chat Interface**: Live messaging with SignalR
- **AI Insights Panel**: Displays real-time AI analysis
- **Interactive Modals**: Clarity requests and reply suggestions
- **Responsive Design**: Works on desktop and mobile

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Python 3.8+
- OpenAI API Key

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Certio
   ```

2. **Set up Python AI Service**
   ```bash
   cd ai_agents
   pip install -r requirements.txt
   ```

3. **Configure Environment Variables**
   Create a `.env` file in the `ai_agents` folder:
   ```
   OPENAI_API_KEY=your_openai_api_key_here
   ```

4. **Set up Database**
   ```bash
   cd Certio.Web
   dotnet ef database update
   ```

5. **Start Services**
   - **Option 1**: Use the batch file
     ```bash
     start_services.bat
     ```
   - **Option 2**: Start manually
     ```bash
     # Terminal 1 - AI Service
     cd ai_agents
     python main.py
     
     # Terminal 2 - Web App
     cd Certio.Web
     dotnet run
     ```

### Access Points
- **Web Application**: http://localhost:5092
- **AI Service API**: http://localhost:8000
- **API Documentation**: http://localhost:8000/docs

## 💡 Features

### Real-time AI Analysis
- Conversations are automatically analyzed as messages are sent
- AI insights appear in a collapsible panel
- Real-time sentiment and urgency tracking

### Interactive AI Tools
- **Clarity Button**: Request explanation of legal language
- **Suggestions Button**: Get AI-generated reply suggestions
- **AI Insights Panel**: View comprehensive conversation analysis

### Multi-User Support
- Support for Clients, team, Lawyers, and Businesses
- User-specific AI responses and suggestions
- Role-based conversation management

### Professional UI
- Clean, modern interface
- Responsive design for all devices
- Intuitive navigation and controls
- Real-time status indicators

## 🔧 API Endpoints

### AI Service Endpoints
- `POST /agents/summarize` - Generate conversation summary
- `POST /agents/extract-goals` - Extract client goals
- `POST /agents/suggest-reply` - Get reply suggestions
- `POST /agents/explain-clarity` - Explain legal language
- `POST /agents/process-all` - Run all agents in parallel

### Web Application Endpoints
- `GET /Chat` - Main chat interface
- `GET /Chat/GetMessages/{id}` - Get conversation messages
- `POST /Chat/SendMessage` - Send a message
- `POST /Chat/RequestClarity` - Request legal clarity
- `POST /Chat/GetSuggestions` - Get reply suggestions

## 🎯 Use Cases

### For Clients
- Get instant clarity on legal language
- Receive professional, AI-assisted responses
- Track conversation progress and insights


### For Lawyers
- Context-aware reply suggestions
- Legal language simplification tools
- Client requirement analysis
- Risk assessment assistance
- Automatic conversation summarization
- Client goal extraction and analysis
- Professional response templates

### For Businesses
- Streamlined communication with legal teams
- AI-powered conversation insights
- Professional response assistance

## 🔮 Future Enhancements

- **Voice Integration**: Speech-to-text and text-to-speech
- **Document Analysis**: AI-powered document review
- **Multi-language Support**: International client support
- **Advanced Analytics**: Conversation metrics and insights
- **Integration APIs**: Connect with external legal tools
- **Mobile App**: Native mobile application

## 🛠️ Development

### Adding New AI Agents
1. Create agent class in `ai_agents/main.py`
2. Add endpoint in FastAPI app
3. Update `AIAgentService.cs` in .NET app
4. Add UI components in Razor views
5. Update JavaScript for new functionality

### Customizing AI Responses
- Modify prompts in Python agent classes
- Adjust temperature and token limits
- Add custom business logic
- Implement user-specific training

## 📊 Monitoring

- **Health Checks**: Built-in health endpoints
- **Error Handling**: Comprehensive error logging
- **Performance**: Async processing for efficiency
- **Scalability**: Stateless design for horizontal scaling

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For support and questions:
- Create an issue in the repository
- Contact the development team
- Check the API documentation at `/docs`

---

**Built with ❤️ for the legal industry**
