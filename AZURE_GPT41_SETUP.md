# Azure GPT-4.1 (o1-preview) Setup for Premium Tier

## Quick Setup

Add these environment variables to your `.env` or Azure App Service configuration:

```env
AZURE_OPENAI_DEPLOYMENT_GPT4_1_MINI=gpt-4.1-mini
AZURE_OPENAI_DEPLOYMENT_GPT4_1=gpt-4.1
```

Replace with whatever you named your deployments in Azure AI Foundry.

## What Changed

✅ Added new Intermediate tier with GPT-4.1-mini (o1-mini)
✅ Premium tier now uses GPT-4.1 (o1-preview) instead of GPT-5
✅ Auto mode still uses 3-tier complexity (mini → 4o → 4.1)
✅ When GPT-5 becomes available, just add `AZURE_OPENAI_DEPLOYMENT_GPT5=gpt-5` and it will auto-upgrade
✅ Cost tracking updated with o1-mini ($3/$12) and o1-preview pricing ($15/$60 per 1M tokens)

## Testing

1. Set the environment variable
2. Restart your AI agents service
3. Go to Settings → AI Settings
4. Select **Premium** tier
5. Test with a chat message
6. Check AI Usage chart - you should see `gpt-4.1` in the model breakdown

## Current Model Tiers

| Tier | Model | Usage |
|------|-------|-------|
| Auto | Dynamic | Complexity-based (mini/4o/4.1) |
| Basic | gpt-4o-mini | Always |
| Intermediate | gpt-4.1-mini (o1-mini) | Always |
| Advanced | gpt-4o | Always |
| Premium | gpt-4.1* (o1-preview) | Always (will auto-upgrade to GPT-5) |

*Currently using o1-preview. Will automatically switch to GPT-5 once available.

## Note

The UI still says "Premium uses gpt-5" to future-proof the messaging. Under the hood, it's using gpt-4.1 (o1-preview) until GPT-5 is approved and deployed.

