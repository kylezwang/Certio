# Offline Development Setup for Certio

## What Works Offline ✅

### .NET Web Application
- Fully functional offline
- SQLite database works locally
- All web features work without internet

### Python AI Agents (Limited)
- Local file processing
- Data analysis with pandas/numpy
- Basic text processing with NLTK/spaCy
- Local machine learning with scikit-learn

## What Requires Internet ❌

### AI API Calls
- OpenAI API calls
- Anthropic API calls
- Google Gemini API calls
- Hugging Face model downloads

### Package Management
- Installing new Python packages
- Updating dependencies

## Preparing for Offline Work

### 1. Download Local AI Models
```bash
cd ai_agents
source venv/bin/activate

# Download spaCy models
python -m spacy download en_core_web_sm

# Download NLTK data
python -c "import nltk; nltk.download('punkt'); nltk.download('stopwords')"
```

### 2. Test Offline Mode
```bash
# Test .NET app offline
dotnet run --project Certio.Web

# Test Python agents (will work but AI APIs will fail)
cd ai_agents
source venv/bin/activate
python main.py
```

### 3. Offline Development Workflow
1. **Code Development**: Full functionality
2. **Database Work**: Full functionality
3. **Testing**: Full functionality
4. **AI Features**: Limited to local processing

## Recommended Offline Tasks

### High Priority
- Code refactoring and improvements
- Database schema changes
- UI/UX improvements
- Bug fixes
- Unit testing
- Documentation

### Medium Priority
- Local data analysis
- File processing features
- Basic text processing
- Performance optimization

### Low Priority
- AI API integration (requires internet)
- Real-time AI features
- Cloud-based features

## Quick Start Commands (Offline)

```bash
# Start web application
dotnet run --project Certio.Web

# Start AI agents (limited functionality)
cd ai_agents && source venv/bin/activate && python main.py

# Start Redis (if needed)
redis-server
```

## Notes
- The project is fully functional offline for development
- AI features will gracefully fail when APIs are unavailable
- All core functionality works without internet
- Perfect for coding, testing, and local development
