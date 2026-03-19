# TODO

## Auth Follow-ups
- [x] Require authentication on `/hubs/board` and ensure realtime board/card events are not delivered to unauthenticated clients.
- [x] Set auth cookies (`boardoil_access`, `boardoil_refresh`) to `Secure=true` in production (configurable for local HTTP dev).
- [x] Skip CSRF enforcement for auth session/bootstrap endpoints (`/api/auth/register-initial-admin`, `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`) to prevent stale-cookie setup/login failures.
- [x] Stop accepting client-supplied typing labels; derive typing identity from authenticated user claims server-side.
- [x] Improve frontend auth failure handling: preserve HTTP status in API errors, clear session on `401`, and route to an unauthorized page with a clear login path.

## Deferred Backlog
- [ ] Extract remaining endpoint route mapping from `Program.cs` into dedicated endpoint modules (board/columns/cards) to match `AuthEndpoints` structure.
- [ ] Revisit `IAuthService` placement after auth result/contracts are moved or shared; target is `BoardOil.Abstractions/Auth` once dependencies allow.
- [ ] Fix missing tags feature in card workflow (`dev card #8`: "Where's the tags").
- [ ] Add lightweight frontend tests for shared modal lifecycle (open/close via route state, backdrop close, ESC close).
