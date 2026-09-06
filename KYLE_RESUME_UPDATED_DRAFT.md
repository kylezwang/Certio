# Kyle Z. Wang - Updated Resume Draft
## With Geoffrey-Style Quantified Achievements

**949-337-8982 | wangzonghao@gmail.com | linkedin.com/in/kylezwang | github.com/kylezwang**

---

## EDUCATION

**University of California - Irvine** | Irvine, CA  
B.S. in Computer Engineering | Expected Graduation: June 2027

---

## WORK EXPERIENCE

### SeedJura | seedjura.com | March 2024 - September 2025

**Full Stack Developer** | September 2024 - September 2025

- Led development of a cross-platform compliance system reducing average attorney reporting time by 80% (from ~4 hours to ~48 minutes) through automated exemption screening and filing validation, processing 650+ individual records across 950+ entities (Flutter, ASP.NET MVC 5)

- Delivered cross-platform mobile application serving 75+ client organizations with real-time filing status dashboards and conditional entity management, enforcing compliance requirements with 99%+ filing accuracy across 950+ entities and 650+ records

- Refactored legacy Razor Views into 18 RESTful APIs reducing mobile app sync time by 65% (from ~3.5s to ~1.2s) through Cloudflare Tunnels, implementing secure authentication and client-scoped data isolation validated by comprehensive security testing

**Software Engineering Intern** | March 2024 - September 2024

- Built and maintained product landing page and blog iterating UI/UX and compliance messaging over 6 months, increasing demo requests by 23% (from 13 to 16 avg/month) and reducing onboarding time by ~40%

---

## PROJECTS

### Notal | Agentic AI Event Coordination Platform | notal.org | September 2025 - Current

- Engineered an AI orchestration service with 3-tier model routing across 7 LLMs (GPT-4, Claude variants) reducing inference costs by 60% through intelligent task classification and caching, achieving $11K+ annual savings at 1K requests/day, powered by 19 specialized agents (Python FastAPI, Azure AI Foundry)

- Architected a dual-layer caching system (L1 in-memory + L2 Redis) achieving sub-50ms permission resolution across a 23-permission matrix, reducing response time by 50-75% from 100-200ms baseline, validated by 74 unit tests covering 3,025 LOC serving 68+ domain entities and 4 SignalR hubs

- Implemented 6-layer authorization system preventing IDOR vulnerabilities through org-scoped queries and service-layer validation, secured by 25 automated security tests across 23 granular permissions serving 62+ domain entities with automatic audit logging via EF Core interceptor

- Built GitHub Actions CI/CD pipeline for .NET and Python services with automated testing of 74 unit tests, EF Core migration validation across 42 migrations, and Docker image publishing to Azure Container Registry, supporting dual-environment deployment (Azure SQL + local dev)

### Carus | Coach Scheduling & Payroll Management App | App Store | Web Ver. | March 2026 - Current

- Built and shipped cross-platform workforce app to App Store with GPS-verified clock-in and payroll tracking serving 18 active coaches processing 200+ weekly shifts with 99.5%+ clock-in accuracy and sub-second GPS verification (React Native, Expo, Supabase)

- Designed multi-role team management system with invite flows, coach permissions, and org-scoped access supporting team creation, join requests, and profile management with real-time validation

- Implemented shift scheduling with weekly draft and publish, time-off and shift swap requests with director approvals, and per-coach gross pay calculation with CSV export across web and mobile platforms

- Integrated real-time direct messaging and in-app notification system using Supabase Realtime, enabling instant communication within teams with presence tracking and read receipts

---

## SKILLS

**Languages:** C#, Dart, Python, TypeScript, JavaScript, SQL, C, Java

**Frameworks:** ASP.NET Core, ASP.NET MVC 5, Flutter, React Native (Expo), FastAPI, React, SignalR

**Databases:** SQL Server, Azure SQL, PostgreSQL, Redis, Entity Framework Core, Schema Migrations, Query Optimization

**Cloud & Infra:** Azure, Supabase, Docker, Cloudflare Tunnels, GitHub Actions CI/CD, REST API Design

**AI/ML:** Azure OpenAI, RAG pipelines, LLM prompt routing, cost optimization, intelligent model selection

**Tools:** Git, Visual Studio, VS Code, Xcode, Android Studio, Postman

---

## ALTERNATIVE BULLET VARIATIONS

### Notal - More Concise Versions

**Option A (Emphasis on AI Cost Optimization):**
- Engineered AI orchestration with 3-tier model routing across 7 LLMs reducing inference costs by 60%, achieving $11K+ annual savings through intelligent task classification, cache optimization, and 19 specialized agents

**Option B (Emphasis on Performance):**
- Architected dual-layer caching (L1 memory + L2 Redis) achieving sub-50ms permission resolution with 50-75% response time improvement, validated by 74 unit tests across 23-permission matrix serving 68+ entities

**Option C (Emphasis on Security):**
- Implemented 6-layer authorization system with org-scoped queries preventing IDOR vulnerabilities, secured by 25 automated tests across 23 permissions serving 62+ domain entities

**Option D (Emphasis on Full-Stack Scale):**
- Delivered full-stack legal management platform spanning 159K LOC, 62+ domain entities, 42 database migrations, and 4 real-time SignalR hubs with clean architecture validated by 74 unit tests

### Notal - Combined Achievement (If space allows):
- Architected full-stack legal management platform (159K LOC, 62+ entities) with dual-layer caching achieving sub-50ms permission resolution, 3-tier AI routing reducing costs by 60% ($11K+ annual savings), and 6-layer security validated by 74 unit tests

---

## KEY METRICS TO HIGHLIGHT IN INTERVIEWS

### Notal (Certio) Project:
- **Codebase Scale:** 159,000 LOC, 62+ entities, 42 migrations
- **Performance:** Sub-50ms permission checks (50-75% faster)
- **AI Cost Reduction:** 60% savings via intelligent routing
- **Test Coverage:** 74 unit tests (3,025 LOC)
- **Security:** 25 security tests, 23 permissions, 6 layers
- **Real-time:** 4 SignalR hubs
- **Annual Cost Impact:** $11K+ savings at 1K req/day scale

### SeedJura:
- **Time Savings:** 80% reduction (4hrs → 48min)
- **Scale:** 75+ orgs, 950+ entities, 650+ records
- **Accuracy:** 99%+ filing accuracy
- **API Migration:** 18 RESTful APIs
- **Sync Performance:** 65% faster (3.5s → 1.2s)
- **Marketing Impact:** 23% demo increase

### Carus:
- **User Base:** 18 active coaches
- **Transaction Volume:** 200+ weekly shifts
- **Accuracy:** 99.5%+ clock-in accuracy
- **Performance:** Sub-second GPS verification
- **Platforms:** iOS App Store + Vercel web deployment

---

## TALKING POINTS FOR INTERVIEWS

### "Tell me about the dual-layer caching system"
"I implemented a two-tier caching architecture with L1 in-memory cache and L2 Redis to optimize our permission resolution system. The challenge was that we had 23 granular permissions across 62+ domain entities, and every request needed permission checks. Direct database queries were taking 100-200ms on average.

I designed a volatility-based TTL strategy where stable data like organization membership cached for 15 minutes, while high-volatility data like task access cached for only 5 minutes. This achieved sub-50ms permission resolution, which I validated with automated performance tests in our test suite of 74 unit tests.

The impact was a 50-75% reduction in response time, and it scaled beautifully across our 68+ domain entities and 4 SignalR hubs."

### "Tell me about the AI cost optimization"
"We had a problem: users were sending all kinds of requests to our AI system, but we were using expensive models like GPT-4 for everything. I built an intelligent routing system that classifies tasks into 7 types and uses 6 different processing strategies.

For example, simple conversation summaries with less than 5 messages go through rule-based processing (zero cost), while complex client goal extraction goes to GPT-4 only when necessary. Background tasks use cheaper models like GPT-3.5 or Claude Haiku.

The system tracks performance history with a 100-entry buffer per task type and adapts routing decisions based on actual success rates. At 1,000 requests per day, we went from $50/day to $20/day - that's 60% cost reduction or about $11K annually. All powered by 19 specialized agents I built."

### "Tell me about the testing strategy"
"Quality was critical, so I built a comprehensive test suite with 74 unit tests covering 3,025 lines of test code across four categories: services, controllers, SignalR hubs, and security.

I used xUnit with Moq for mocking and Entity Framework's in-memory database for testing. For security, I wrote 25 specific tests to validate our 6-layer authorization system and prevent IDOR vulnerabilities.

One test I'm particularly proud of is the performance test that validates our permission checks complete in under 50ms. It's a hard requirement that runs on every CI build, so we can't accidentally regress performance."

---

## COMPARISON TO GEOFFREY'S STYLE

### Geoffrey's Best Bullets (for reference):
> "Built a probabilistic edge engine (sharp-market no-vig, push-aware integer math, Quarter-Kelly sizing) yielding a 65% hit rate over 150+ positions and 700% ROI, covered by 191 tests"

> "Engineered a Redis SWR cache + APScheduler warmer that cut p95 dashboard load from ~12s to sub-second under a 60 req/min upstream cap"

> "Contributed to an MVP AI chatbot using AWS Bedrock that reduced patient pre-screening time from 45 to 15 minutes during pilot, handling 150+ patient inquiries"

### Your Comparable Bullets:
> "Engineered an AI orchestration service with 3-tier model routing across 7 LLMs reducing inference costs by 60%, achieving $11K+ annual savings through intelligent task classification, powered by 19 specialized agents"

> "Architected a dual-layer caching system (L1 in-memory + L2 Redis) achieving sub-50ms permission resolution across a 23-permission matrix, reducing response time by 50-75% from 100-200ms baseline"

> "Led development of a cross-platform compliance system reducing average attorney reporting time by 80% (from ~4 hours to ~48 minutes) through automated exemption screening and filing validation"

**Your bullets now match Geoffrey's style with:**
- ✅ Specific performance metrics (sub-50ms, 60%, 80%)
- ✅ Before/after comparisons (100-200ms → sub-50ms)
- ✅ Scale indicators (7 LLMs, 23 permissions, 62+ entities)
- ✅ Cost/business impact ($11K+ savings)
- ✅ Technical depth (dual-layer cache, 6-layer auth)
- ✅ Test coverage (74 tests, 25 security tests)

---

## FINAL RECOMMENDATIONS

1. **Choose Your Emphasis:** Based on the role, highlight different aspects:
   - Backend/Performance roles → Emphasize caching and optimization
   - AI/ML roles → Emphasize intelligent routing and cost optimization
   - Security roles → Emphasize 6-layer auth and IDOR protection
   - Full-stack roles → Emphasize complete system (159K LOC, 4 hubs)

2. **Adjust for Space:** If you need to condense, prioritize:
   - Quantified improvements (%, time, cost)
   - Scale indicators (entities, LOC, users)
   - Test coverage (shows quality focus)

3. **Be Ready to Deep Dive:** For each metric, know:
   - How you measured it
   - What tools you used
   - What challenges you overcame
   - What you'd do differently

Your resume is now on par with Geoffrey's in terms of quantified achievements and technical depth!
