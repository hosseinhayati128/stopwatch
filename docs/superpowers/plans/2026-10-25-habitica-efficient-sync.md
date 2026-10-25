# Efficient Habitica delivery

Goal: accelerate imports and useful work per request while respecting Habitica's published 30-second background-call interval.
Evidence: HabitRPG/habitica API-Usage-Guidelines requires 30 seconds. Official tasks.js accepts task arrays; official rateLimiter.js emits JavaScript date reset headers and fractional Retry-After.

- [x] Add failing fake-service checks for 20+11 bulk creates, uncertainty/restart, authoritative snapshots, exact reset headers and fractional retry.
- [x] Implement bounded bulk creation with stable aliases, durable attempted markers and per-task response validation.
- [x] Keep authoritative full successful responses to remove redundant GETs. Preserve score/reward and checklist safety.
- [x] Tune transient retries from 30 seconds with bounded exponential backoff; honor larger server deadlines, with conservative fallback when hints are absent.
- [x] Run HTTP, recovery and hidden UI compatibility checks. Publish/install/restart without live test mutations. Do not push unless asked.
