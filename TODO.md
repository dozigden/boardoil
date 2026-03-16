# TODO

## Auth Follow-ups
- [ ] Require authentication on `/hubs/board` and ensure realtime board/card events are not delivered to unauthenticated clients.
- [ ] Set auth cookies (`boardoil_access`, `boardoil_refresh`) to `Secure=true` in production (configurable for local HTTP dev).
- [ ] Stop accepting client-supplied typing labels; derive typing identity from authenticated user claims server-side.
- [ ] Improve frontend auth failure handling: preserve HTTP status in API errors, clear session on `401`, and route to `/login` predictably.

## Deferred Backlog
- [ ] Fix missing tags feature in card workflow (`dev card #8`: "Where's the tags").
- [ ] Add lightweight frontend tests for shared modal lifecycle (open/close via route state, backdrop close, ESC close).
