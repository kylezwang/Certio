FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install dependencies
COPY requirements-minimal.txt .
RUN pip install --no-cache-dir -r requirements-minimal.txt

# Copy all application modules
COPY main.py .
COPY simplified_cost_optimization.py .
COPY context_manager.py .
COPY background_agents.py .
COPY intelligent_routing.py .
COPY cost_analytics.py .
COPY user_data_rag_system.py .
COPY certio_training_pipeline.py .
COPY certio_rag_system_enhanced.py .
COPY certio_rag_system_fallback.py .
COPY certio_rag_system.py .
COPY certio_knowledge_base.py .
COPY certio_onboarding_knowledge.py .

# Copy training data if exists
COPY training_data/ ./training_data/

# Expose port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Run the application
CMD ["python", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
