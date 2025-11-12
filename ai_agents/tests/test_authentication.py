import os
import sys
from pathlib import Path

# Add parent directory to path so we can import main
sys.path.insert(0, str(Path(__file__).parent.parent))

os.environ.setdefault("AI_API_KEY", "test-secret")
os.environ.setdefault("AI_API_RATE_LIMIT_PER_MINUTE", "5")

from httpx import AsyncClient, ASGITransport  # noqa: E402
import pytest  # noqa: E402
import pytest_asyncio  # noqa: E402
from main import app, api_rate_limiter  # noqa: E402


@pytest_asyncio.fixture
async def client():
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac


@pytest.fixture(autouse=True)
def reset_rate_limiter():
    """Reset rate limiter before each test"""
    api_rate_limiter.reset()
    yield
    api_rate_limiter.reset()


def _sample_payload():
    return {
        "conversation_id": "1",
        "messages": [],
        "user_type": "Client",
    }


@pytest.mark.asyncio
async def test_request_without_api_key_is_rejected(client):
    response = await client.post("/agents/summarize", json=_sample_payload())
    assert response.status_code == 401


@pytest.mark.asyncio
async def test_request_with_valid_api_key_succeeds(client):
    headers = {"X-API-Key": os.environ["AI_API_KEY"]}
    response = await client.post("/agents/summarize", json=_sample_payload(), headers=headers)
    assert response.status_code == 200

