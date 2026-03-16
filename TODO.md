# TODO

## Prioritized Execution Order

1. Backend auth data model  
- [x] Add `User` + refresh-token persistence model and EF migration.  
- [x] Add password hashing utilities and role enum/policies.

2. JWT plumbing  
- [x] Configure JWT issuing/validation and cookie transport.  
- [x] Add auth middleware + authorization policy registration.
- [x] Add CSRF protection strategy for cookie-authenticated state-changing requests.

3. Bootstrap path  
- [x] Implement `register-initial-admin` with guard: only when user count is `0`.  
- [x] Lock first registered account to `admin`.

4. Core auth APIs  
- [x] Implement `login`, `refresh`, `logout`, `me`.  
- [x] Define consistent error contract for `401`/`403`.

5. Authorization on existing APIs  
- [x] Require authentication for `GET /api/board`.
- [x] Apply admin-only policy to column config routes.  
- [x] Apply admin/standard policy to card routes.  

6. Admin user-management APIs  
- [x] Add list/create/change-role/activate-deactivate endpoints.  
- [x] Enforce self-protection rules (e.g., prevent removing last admin).

7. Realtime typing contract update  
- [ ] Change hub payload to card-level typing events (remove `field`).  
- [ ] Keep TTL/reconnect behavior.

8. Frontend auth integration  
- [ ] Add login/logout/session restore flow.  
- [ ] Attach auth state to API calls and handle `401`/`403`.  
- [ ] Add role-based UI gating for admin-only areas.

9. Frontend typing UI alignment  
- [ ] Keep typing pill only in board card title and card dialog title.  
- [ ] Remove other typing indicator render paths.

10. Tests and hardening  
- [ ] Backend: auth flows, bootstrap guard, authorization matrix.  
- [ ] Realtime: card-level typing lifecycle + reconnect.  
- [ ] Frontend: auth gating + forbidden handling + typing placement.

11. Docs and wrap-up  
- [ ] Update README for JWT env config + bootstrap flow.  
- [ ] Document CSRF behavior and client requirements in README.
- [ ] Update TODO status and keep `BoardOil.Web` check gate (`npm run check`) as pre-commit validation.

## Deferred Backlog
- [ ] Fix missing tags feature in card workflow (`dev card #8`: "Where's the tags").
- [ ] Add lightweight frontend tests for shared modal lifecycle (open/close via route state, backdrop close, ESC close).
- [ ] Add one lightweight frontend unit test for API base resolution (same-origin default + `VITE_API_BASE` override normalization).
- [ ] Add reconnect/typing churn test coverage for realtime edge cases.
- [ ] Document JWT configuration and bootstrap-first-admin flow in README.
- [ ] Document event-delivery semantics explicitly in README (`realtime updates are best-effort`).
