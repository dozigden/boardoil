# TODO

## Next Up
- [ ] Fix typing TTL race in `TypingPresenceService.SweepExpiredAsync` so refreshed entries are not removed incorrectly.
- [ ] Reduce duplicate board refreshes in frontend after local mutations (avoid REST-triggered `loadBoard()` + SignalR-triggered `loadBoard()` double fetch).
- [ ] Refactor realtime tests to improve readability.

## Soon
- [ ] Add one lightweight frontend unit test for API base resolution (same-origin default + `VITE_API_BASE` override normalization).
- [ ] Add reconnect/typing churn test coverage for realtime edge cases.

## Nice To Have
- [ ] Document event-delivery semantics explicitly in README ("realtime updates are best-effort").
- [ ] Add a tiny helper script or make target for running backend + frontend together in dev.
