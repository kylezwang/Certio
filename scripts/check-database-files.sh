#!/bin/bash
# CI/CD script to check for database files in git repository
# This script should be run in CI/CD pipelines to prevent database files from being committed

echo "Checking for database files in repository..."

# Check for database files in git
if git ls-files | grep -E '\.(db|db-shm|db-wal)$'; then
    echo "❌ ERROR: Database files found in git repository!"
    echo "The following database files are tracked in git:"
    git ls-files | grep -E '\.(db|db-shm|db-wal)$'
    echo ""
    echo "Please remove these files from git using:"
    echo "  git rm --cached <file>"
    echo "  git commit -m 'Remove database files from version control'"
    exit 1
fi

echo "✅ No database files found in repository"
exit 0

