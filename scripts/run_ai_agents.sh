#!/bin/bash
cd "$(dirname "$0")/.." || exit 1
# Run the AI service from the repo root.

cd ai_agents
source venv/bin/activate
python main.py
