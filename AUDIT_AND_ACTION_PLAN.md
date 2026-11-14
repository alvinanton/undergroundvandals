# Audit and Action Plan — Underground Vandals

Date: November 12, 2026.

Status: static review findings awaiting remediation and runtime validation.

This document records the repository audit and a prioritized plan for creating GitHub issues. Each block between separators can be copied into an independent issue: use its heading as the title and the remaining text as the body. Labels are suggestions and can be created if they do not exist yet.

No application code was changed during the audit. No services, purchases, penetration tests, or queries against external infrastructure were executed. The review did not establish whether these weaknesses are exposed in current deployments or have been exploited. Priorities reflect risks observed in the code, not a confirmed incident.

## How the Project Works

The website uses Astro with server-side rendering and Tailwind to present multimedia posts, editorial articles, and a shop. The administration panel manages content, users, and products. The shopping cart is stored in the browser.

The content API uses ASP.NET Core on .NET 10, Entity Framework Core, and PostgreSQL. It stores users, posts, file references, articles, and visits. Passwords are hashed with BCrypt, and requests are authenticated using JWTs. Azure Blob Storage is the API's active storage implementation; some remaining workflows still expect Cloudinary.

The shop calls a separate API whose backend is not included in this repository. Its inventory rules, final prices, payments, orders, and actual administrative key permissions could therefore not be audited.

The Telegram bot accesses the shared database directly to manage posts and generate traffic reports. Docker Compose coordinates PostgreSQL, the API, the bot, and the frontend. GitHub Actions provides build and deployment workflows.

| Location | Responsibility |
| --- | --- |
| `frontend/src/pages` | Public pages, administration, and API proxy routes |
| `frontend/src/components` | Visual components and administration managers |
| `frontend/src/services` | API clients |
| `frontend/src/lib` | Cart, image optimization, and uploads |
| `backend/UndergroundVandals/UndergroundVandals.Api` | API, entities, contracts, data access, and migrations |
| `backend/UndergroundVandals/UndergroundVandals.Bot` | Telegram commands, post management, and reports |
| `.github/workflows` | Continuous integration and deployment |

Positive aspects include separate services, explicit data contracts, versioned migrations, hashed passwords, temporary Azure upload permissions, bot access restricted to one user and chat, and a database backup before migrations in the development deployment workflow.

## Execution Order

- **P0 — Immediate containment:** issues 01–04. Resolve together before exposing administration or shop management.
- **P1 — Security and functionality:** issues 05–11. Close content exposure and complete core workflows.
- **P2 — Reliability and operations:** issues 12–16. Consolidate configuration, testing, deployment, and growth controls.
- **P3 — Maintainability:** issue 17. Improve documentation and reduce complexity after behavior is stable.

The numbers below identify entries in this document, not actual GitHub issues. Replace dependency references with issue links after creating them. The order addresses risk first; regression tests belong in each fix and must not wait for the general testing issue.

| Issue | Priority | Expected Outcome |
| --- | --- | --- |
| 01 | P0 | Prevent public administrator creation |
| 02 | P0 | Authenticate Astro sessions correctly |
| 03 | P0 | Keep the shop administrative key exclusively on the server |
| 04 | P0 | Protect every administrative proxy route |
| 05 | P1 | Prevent malicious HTML execution in articles |
| 06 | P1 | Restore HTTPS certificate validation |
| 07 | P1 | Protect drafts and define archived content access |
| 08 | P1 | Unify multimedia uploads and deletion using Azure |
| 09 | P1 | Align user management between the panel and API |
| 10 | P1 | Revoke access for deactivated accounts |
| 11 | P1 | Unify roles and permissions |
| 12 | P2 | Centralize and validate configuration |
| 13 | P2 | Introduce automated tests and effective CI checks |
| 14 | P2 | Make deployment reproducible and verifiable |
| 15 | P2 | Paginate content queries |
| 16 | P2 | Control traffic statistics quality and growth |
| 17 | P3 | Document operations and split administration managers |

---

## Issue 01 — [P0] Disable Public Administrator Creation

**Suggested labels:** `security`, `backend`, `priority:P0`

### Problem and Evidence

`POST /api/auth/seed-admin` creates an `Admin` user without authentication. It only checks whether the supplied email already exists and permits additional administrators even after the first one has been created.

Reference: `backend/UndergroundVandals/UndergroundVandals.Api/Controllers/AuthController.cs`, `SeedAdmin` method.

### Impact

If this route is accessible in a deployment, a visitor can obtain legitimate administrative privileges.

### Proposed Work

- Remove the public endpoint from the deployed service.
- Provide an explicit administrator initialization procedure outside HTTP, restricted to the operator.
- Review administrative accounts in affected environments for unexpected registrations. Do not delete accounts without verifying their origin.
- Assess session revocation if unauthorized use is found; do not assume an incident has been confirmed.

### Acceptance Criteria and Validation

- [ ] An anonymous request to the former route cannot create users.
- [ ] The test also passes when an administrator already exists.
- [ ] Authorized initialization is documented and does not log passwords.
- [ ] A regression test verifies that no equivalent public creation path is exposed.

**Dependencies:** none.

---

## Issue 02 — [P0] Cryptographically Validate Frontend Sessions

**Suggested labels:** `security`, `frontend`, `authentication`, `priority:P0`

### Problem and Evidence

`frontend/src/middleware.ts` decodes the JWT in `uv_auth_token` and assigns its role to `locals.user` without verifying the signature, issuer, audience, or expiration. Some administrative pages only check for the presence of the cookie.

### Impact

A client-created cookie can impersonate an administrative session in Astro. This does not bypass JWT verification in the .NET API, but it compromises access decisions made by the frontend, especially when the frontend uses its own server credentials.

### Proposed Work

- Replace decoding as a source of trust with server-side session validation, through the authentication API or cryptographic verification using exclusively private configuration.
- Validate the signature, permitted algorithm, expiration, issuer, and audience before populating `locals.user`.
- Require a validated identity for administrative pages and routes; never authorize based solely on cookie presence.
- Move session creation and removal to the server and use `HttpOnly`, `Secure` over HTTPS, and appropriate `SameSite` cookie settings.
- Adapt administrative calls that currently read the token from JavaScript to a server proxy flow; protect cookie-authenticated writes against requests from unauthorized origins.

### Acceptance Criteria and Validation

- [ ] Empty, malformed, forged, or expired cookies do not grant access.
- [ ] Tokens with an unexpected issuer, audience, or algorithm are rejected.
- [ ] An editor session cannot authorize administrator operations.
- [ ] JavaScript cannot read the session token.
- [ ] Login, session renewal or expiration, and logout work with the chosen flow.
- [ ] Write operations reject requests from unauthorized origins.

**Dependencies:** coordinate with issues 03 and 04; do not release a partial fix that leaves their access paths open.

---

## Issue 03 — [P0] Remove the Shop Administrative Key from the Browser

**Suggested labels:** `security`, `frontend`, `ecommerce`, `priority:P0`

### Problem and Evidence

`frontend/src/components/admin/ProductsManager.astro` renders `ECOMMERCE_ADMIN_API_KEY` in `data-api-key`. Browser JavaScript retrieves it from the DOM. The upload workflow also references `PUBLIC_ECOMMERCE_ADMIN_API_KEY`.

### Impact

The key is no longer private once included in HTML or client code. The products page only checks for cookie presence, worsening the exposure. The key's actual permissions must be verified in the external backend.

### Proposed Work

- Remove administrative keys from HTML attributes, public environment variables, scripts, and browser responses.
- Proxy all operations requiring this key through Astro, including upload authorization requests.
- Preserve direct storage uploads using limited temporary permissions; do not give the browser an administrative key to obtain them.
- Remove key-prefix logging from the products proxy.
- After closing the exposure, rotate the key used by affected deployments and update it exclusively in server secrets.

### Acceptance Criteria and Validation

- [ ] The key does not appear in HTML, compiled assets, network responses, or application logs.
- [ ] The browser does not call the shop API using an administrative `X-Api-Key`.
- [ ] Product creation, editing, and uploads work through protected routes.
- [ ] The previous key is invalid after rotation.

**Dependencies:** issue 02; coordinate with issue 04.

---

## Issue 04 — [P0] Apply Consistent Authorization to Administrative Proxies

**Suggested labels:** `security`, `frontend`, `authorization`, `priority:P0`

### Problem and Evidence

`frontend/src/pages/api/admin/products/index.ts` queries the administrative catalog using a private key without checking identity or role. The `[id].ts` routes do check roles, but currently depend on the insecure middleware.

### Proposed Work

- Create a shared session and permission check for all Astro administrative routes.
- Apply it before contacting external services or reading sensitive operation bodies.
- Return `401` without a valid session and `403` for insufficient permissions.
- Avoid forwarding internal errors or sensitive provider details to clients.

### Acceptance Criteria and Validation

- [ ] Anonymous requests, forged JWTs, and editor sessions cannot retrieve administrative data.
- [ ] Rejected requests do not call the external provider.
- [ ] A valid administrator retains authorized operations.
- [ ] Tests cover product listing, details, and writes.

**Dependencies:** issue 02. Complete together with issue 03.

---

## Issue 05 — [P1] Sanitize Editorial Content Before Rendering HTML

**Suggested labels:** `security`, `frontend`, `content`, `priority:P1`

### Problem and Evidence

`frontend/src/pages/gallery/[slug].astro` converts Markdown using string replacements and inserts the result with `set:html`. No sanitization of arbitrary HTML in the input was observed.

### Impact

Malicious editorial content can execute JavaScript in a visitor's browser. With the current cookies, it can also compromise sessions accessible through JavaScript.

### Proposed Work

- Use Markdown processing with explicit sanitization and an allowlist of elements, attributes, and protocols.
- Reject scripts, event handlers, executable URLs, and unnecessary active constructs.
- Review `innerHTML` interpolation in administrative managers to ensure escaping or safe DOM construction.
- Apply the same protection to existing articles when rendering them.

### Acceptance Criteria and Validation

- [ ] Legitimate headings, paragraphs, bold text, and quotes preserve their presentation.
- [ ] Content containing script tags, HTML event handlers, or executable URLs does not execute.
- [ ] Titles and URLs interpolated into tables and previews are tested with special characters.
- [ ] Tests cover content persisted before the fix.

**Dependencies:** none; complement with issue 02.

---

## Issue 06 — [P1] Remove the Global TLS Validation Override

**Suggested labels:** `security`, `frontend`, `configuration`, `priority:P1`

### Problem and Evidence

`frontend/src/services/api.ts` sets `process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'`. When this module loads in Node, it disables normal certificate validation for connections made by the process.

### Proposed Work

- Remove the assignment and any equivalent configuration.
- Support development HTTPS through trusted certificates or explicit local configuration that does not globally weaken TLS.
- Document the development certificate procedure.

### Acceptance Criteria and Validation

- [ ] No application code or deployment configuration globally disables TLS validation.
- [ ] A server with an invalid certificate is rejected.
- [ ] Valid HTTPS connections continue to work.

**Dependencies:** none.

---

## Issue 07 — [P1] Protect Drafts and Define Archived Content Access

**Suggested labels:** `security`, `backend`, `content`, `priority:P1`

### Problem and Evidence

`GalleryController` allows unauthenticated requests using `includeUnpublished=true`. Its ID and slug queries also do not filter by `IsPublished`. `MediaController` publicly allows archived content to be included in listings and retrieved by ID.

### Proposed Work

- Restrict drafts to users with editorial permissions in listings, detail endpoints, and proxies.
- Limit public responses to published articles.
- Initially define archiving as hiding content from navigation without guaranteeing confidentiality. Explain this in the panel and documentation.
- If archived posts require true access restrictions, address this as an explicit access and storage change: filtering the API alone does not protect previously issued public file URLs.

### Acceptance Criteria and Validation

- [ ] Visitors cannot retrieve drafts through listings, IDs, slugs, or proxies.
- [ ] Authorized editors can preview and manage drafts.
- [ ] Published articles remain publicly accessible.
- [ ] Archiving behavior is documented and consistent between the website and bot.

**Dependencies:** none for API protection; issue 02 for private Astro views.

---

## Issue 08 — [P1] Complete the Multimedia Migration from Cloudinary to Azure

**Suggested labels:** `bug`, `storage`, `frontend`, `backend`, `bot`, `priority:P1`

### Problem and Evidence

The API registers `AzureBlobStorageService`, but `AdminGalleryManager.astro` and `mediaService.ts` still request signatures using `folder` and expect Cloudinary parameters. The API requires `target` and returns Azure authorization parameters. The bot registers `CloudinaryStorageService` to delete files belonging to shared posts.

References: API and bot entry points, `frontend/src/components/admin/AdminGalleryManager.astro`, `frontend/src/services/mediaService.ts`, and `frontend/src/lib/azureStorage.ts`.

### Proposed Work

- Adopt Azure for new uploads and reuse the existing Azure client in active workflows.
- Inventory historical Cloudinary references before removing support; retain provider-aware deletion if legacy files exist.
- Validate upload destinations against an allowlist of authorized containers and prefixes.
- Align API and bot deletion, including thumbnails and orphaned file handling.
- Define recovery for successful uploads followed by failed registration, and for failed remote deletion; retain references needed for cleanup or retries.

### Acceptance Criteria and Validation

- [ ] Archive and gallery workflows upload images and videos using the current contract.
- [ ] Small files and block uploads above 8 MB work.
- [ ] The bot and API delete files from the correct provider, including thumbnails.
- [ ] Unauthorized destinations are rejected and expired authorizations cannot upload files.
- [ ] Partial failures preserve recoverable cleanup or retry information.
- [ ] Historical references keep working or have a validated migration path.

**Dependencies:** issues 02–04 for session changes and shop operations where applicable.

---

## Issue 09 — [P1] Align User Editing and Deactivation Between the Panel and API

**Suggested labels:** `bug`, `frontend`, `backend`, `users`, `priority:P1`

### Problem and Evidence

`UsersManager.astro` sends `PUT /api/users/{id}` and `DELETE /api/users/{id}`. The implemented controller only provides listing, creation, and `PATCH /api/users/{id}/status`. The form also handles a `name` field absent from the current entity.

### Proposed Work

- Implement administrative editing of existing fields with an explicit contract; preserve passwords when no replacement is provided.
- Replace deletion with deactivate/reactivate actions using the existing endpoint, avoiding physical deletion by default.
- Remove the `name` field until there is a product requirement to persist it.
- Validate email, password, and role; normalize email and enforce database uniqueness after reviewing existing duplicates.
- Prevent deactivation or demotion of the last active administrator.

### Acceptance Criteria and Validation

- [ ] Creation, editing, deactivation, and reactivation work end to end.
- [ ] Editors cannot manage users.
- [ ] Duplicate emails and invalid data produce understandable errors.
- [ ] Editing without changing the password preserves the existing password.
- [ ] At least one active administrator is always retained.

**Dependencies:** issues 01 and 02; coordinate roles with issue 11.

---

## Issue 10 — [P1] Revoke Access for Deactivated Accounts and Invalidated Sessions

**Suggested labels:** `security`, `backend`, `authentication`, `priority:P1`

### Problem and Evidence

Login checks `IsActive`, but issued JWTs last eight hours. Subsequent validation in `Program.cs` does not check user status or a session version.

### Proposed Work

- Check active status and session validity on protected API and frontend requests.
- Introduce a verifiable revocation strategy, such as a per-user session version, and update it on deactivation or credential and permission changes.
- Require revocation to take effect on the next protected request.

### Acceptance Criteria and Validation

- [ ] A token issued before account deactivation fails on the next protected request.
- [ ] Permission or credential changes invalidate previous sessions according to the documented policy.
- [ ] Reactivating an account does not restore previously revoked tokens.
- [ ] Active accounts retain their authorized access.

**Dependencies:** issues 02 and 09.

---

## Issue 11 — [P1] Unify Roles and Permissions Across Frontend and Backend

**Suggested labels:** `authorization`, `frontend`, `backend`, `priority:P1`

### Problem and Evidence

The backend defines `Admin` and `Editor`; parts of the frontend use `SuperAdmin`, `ArchiveAdmin`, and `GalleryAdmin`. These additional roles do not represent independent permissions enforced by the current API.

### Proposed Work

- Retain the existing `Admin` and `Editor` roles as the single contract for this correction.
- Allow only `Admin` to manage users and delete posts or articles; allow both roles to edit content according to current API policies.
- Restrict shop administration to `Admin` until the external contract establishes editorial permissions.
- Remove visual mappings suggesting archive or gallery isolation without server enforcement.
- Centralize policies and document an operation-by-operation permission matrix.

### Acceptance Criteria and Validation

- [ ] The panel, proxies, and API enforce the same matrix.
- [ ] Unknown roles receive no default permissions.
- [ ] Direct requests obey restrictions even when the interface is manipulated.
- [ ] Existing accounts retain their intended permissions.

**Dependencies:** issues 02 and 04.

---

## Issue 12 — [P2] Centralize URLs and Validate Startup Configuration

**Suggested labels:** `configuration`, `frontend`, `backend`, `priority:P2`

### Problem and Evidence

Clients use fallback ports `5098` and `5114`, while Compose publishes the content API on `5050`. Public and private URLs also have different requirements inside Docker.

### Proposed Work

- Centralize client configuration and distinguish the content API, public shop API, and administrative shop API.
- Separate browser-facing and server-facing addresses where Docker requires it.
- Validate required settings at startup and fail with messages that do not disclose secrets.
- Update environment examples and document which values are embedded during builds and which are read at runtime.
- Verify that private shop settings reach the Astro server in the deployed image; do not assume `import.meta.env` reads them dynamically.

### Acceptance Criteria and Validation

- [ ] The project starts using documented local and Docker configurations.
- [ ] Conflicting fallback ports are no longer scattered among clients.
- [ ] Missing configuration produces an explicit, safe error.
- [ ] Server secret changes do not require exposing secrets as public variables.

**Dependencies:** issue 03.

---

## Issue 13 — [P2] Add Automated Tests and Effective CI Checks

**Suggested labels:** `testing`, `ci`, `priority:P2`

### Problem and Evidence

The solution contains the API and bot, with no test projects identified. The frontend defines no test scripts. Workflows execute `dotnet test`, but this does not establish behavioral coverage.

### Proposed Work

- Add integration tests for authentication, permissions, users, drafts, and content contracts.
- Test proxies against a simulated shop provider without real keys or orders.
- Cover uploads and partial failures using simulated storage or an isolated environment.
- Add frontend type checking and browser tests for critical workflows.
- Configure CI to execute and report these tests, preventing a run with no tests from being treated as sufficient validation.

### Acceptance Criteria and Validation

- [ ] CI detects regressions associated with issues 01–11.
- [ ] Tests do not require production credentials.
- [ ] Authentication or authorization failures block integration.
- [ ] Commands and expected results are documented.

**Dependencies:** specific regression tests accompany each fix; this issue consolidates shared test infrastructure and coverage.

---

## Issue 14 — [P2] Make Deployments Reproducible and Verifiable

**Suggested labels:** `devops`, `docker`, `ci`, `priority:P2`

### Problem and Evidence

The frontend Dockerfile uses `npm install`, while CI uses `npm ci`. Development deployment on `main` does not explicitly depend on a CI result. Compose alone does not apply migrations; the API dependency uses `service_started` without verifying readiness. The development workflow backs up the database before migrations, but a complete documented recovery procedure is missing.

### Proposed Work

- Use reproducible installation based on the lockfile.
- Run mandatory checks before deployment, including manual deployments.
- Add health checks and service readiness waits.
- Document and automate initial startup with migrations without concurrent migration execution against the same database.
- Preserve pre-migration backups and test restoration in an isolated environment.
- Document recovery from failed migrations and application/schema compatibility.
- Verify that public environments use production configuration and deliberate port exposure.

### Acceptance Criteria and Validation

- [ ] A failing test prevents deployment.
- [ ] An empty environment can be initialized using the documented procedure.
- [ ] Deployment detects an API that has started but is unavailable.
- [ ] Backup restoration is demonstrated outside production.
- [ ] The image uses dependency versions from the lockfile.

**Dependencies:** issues 12 and 13.

---

## Issue 15 — [P2] Paginate Listings and Bound Content Queries

**Suggested labels:** `performance`, `backend`, `frontend`, `priority:P2`

### Problem and Evidence

The main listings in `MediaController` and `GalleryController` load every matching record with `ToListAsync`. Response size and memory usage will grow with the catalog.

### Proposed Work

- Add pagination with default and maximum page sizes, stable ordering, and navigation metadata.
- Update public pages, administrative managers, and proxies to the paginated contract in the same change.
- Use no-tracking queries for reads where appropriate and review indexes against actual filters.
- Preserve publication and permission restrictions on every result page.

### Acceptance Criteria and Validation

- [ ] No listing returns an unbounded number of records.
- [ ] Filtering and navigation neither skip nor duplicate items for a stable dataset.
- [ ] Invalid parameters receive a defined response.
- [ ] Response size and query duration are compared using a representative dataset.

**Dependencies:** issue 07. Coordinate API contract and consumer changes.

---

## Issue 16 — [P2] Protect Visit Tracking and Define Statistics Retention

**Suggested labels:** `analytics`, `backend`, `priority:P2`

### Problem and Evidence

`VisitsController` accepts anonymous visits and directly reads IP and geographic headers. No request limits or automated retention were observed. `Layout.astro` records page loads, and the bot builds reports from these records.

### Proposed Work

- Configure trusted proxies and do not accept network identity from arbitrary client headers.
- Limit request frequency and input lengths without disrupting normal navigation.
- Verify that each intended navigation produces a single visit.
- Initially adopt configurable 90-day retention for detailed records, with batch cleanup and documentation of the visitor identifier.
- Treat statistics as estimates; anonymous collection cannot guarantee each request represents a real person.

### Acceptance Criteria and Validation

- [ ] Headers supplied outside an authorized proxy cannot override the trusted IP address.
- [ ] Abusive bursts are limited while normal page loads are recorded.
- [ ] Bot reports continue to work with valid data.
- [ ] Cleanup removes expired records without affecting the retained period.

**Dependencies:** issue 12 for environment-specific proxy and limit configuration.

---

## Issue 17 — [P3] Document Operations and Reduce Administration Panel Complexity

**Suggested labels:** `documentation`, `refactor`, `frontend`, `priority:P3`

### Problem and Evidence

`frontend/README.md` still contains Astro starter documentation. Some large managers mix interface, requests, validation, and state; `ProductsManager.astro` exceeds 1,700 lines. The bot also shares entities and data access through the API project.

### Proposed Work

- Write a project README covering architecture, requirements, startup, environment variables without secret values, migrations, testing, and deployment.
- Document the external shop API dependency, roles, storage, backups, and recovery.
- First split the products manager into interface, state, client, and upload modules while preserving behavior.
- Remove obsolete code only after verifying it has no consumers.
- Document the bot/API dependency and extract a shared library only if a concrete justification shows reduced coupling.
- Preserve this document as the audit snapshot and link actual issues and their resolutions.

### Acceptance Criteria and Validation

- [ ] A new contributor can start the project using the documentation and authorized development credentials.
- [ ] The external service absent from this repository is clearly identified.
- [ ] Refactoring preserves workflows covered by tests.
- [ ] Each finding links to its issue and closure evidence.

**Dependencies:** stabilize issues 08 and 11–14 before documenting final contracts or refactoring their workflows.

## Audit Closure

An issue is resolved when its acceptance criteria are met, appropriate tests are included, and behavior is verified in the target environment where applicable. A successful build does not replace permission, upload, restoration, or external integration testing.

Checking dependencies against current security advisories, deployed infrastructure, provider configuration, and the external shop backend falls outside the evidence gathered. These areas must be verified before declaring the complete system ready for production.
