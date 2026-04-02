# TheGrunkGames – Copilot Instructions

> **Keep these instructions up-to-date.** Whenever you make structural changes to the solution (new projects, renamed services, changed storage mechanisms, updated deployment targets, or modified conventions), update this file as part of the same change.

## Overview

TheGrunkGames is a LAN-party tournament management system. It handles team registration, game scheduling, round/match management, scoring, and live standings. The solution is built on **.NET 10 / C# 14** and is orchestrated with **.NET Aspire 13**.

The repository lives at: `https://github.com/JaQuack/TheGrunkGames`
Active development branch: `feature/adding_Aspire`

---

## Solution Structure

```
TheGrunkGames.sln
├── TheGrunkGames.Aspire/          # Aspire AppHost – orchestrates the distributed app
├── TheGrunkGames/                 # Backend API (ASP.NET Core Web API)
├── TheGrunkGames.BlazorApp/       # Frontend (Blazor Server with interactive SSR)
├── TheGrunkGames.Models/          # Shared domain models (class library)
├── TheGrunkGames.ServiceDefaults/ # Shared Aspire service defaults (OpenTelemetry, health checks, service discovery)
└── TheGrunkGames.Tests/           # Unit tests (xUnit)
```

### TheGrunkGames.Aspire (AppHost)

- **Role:** .NET Aspire orchestrator. Defines the distributed application topology.
- **Key file:** `AppHost.cs`
- Registers resources:
  - `mongodb` → MongoDB container (persistent lifetime, named volume `thegrunkgames-mongo-data`)
  - `thegrunkgames` → MongoDB database (hosted in the `mongodb` container)
  - `gameservice` → `TheGrunkGames` (backend API, depends on MongoDB)
  - `blazorapp` → `TheGrunkGames.BlazorApp` (frontend)
- Both services expose external HTTP endpoints.
- The Blazor app references the API via Aspire service discovery (`https+http://gameservice`).
- The API references MongoDB via Aspire service discovery — connection strings are injected automatically.
- Deployment target: Azure Container Apps (configured via `azure.yaml` and Bicep param files in `infra/`).
- Custom domain support is parameterised (`customDomain`, `certificateName`).

### TheGrunkGames (Backend API)

- **Role:** ASP.NET Core Web API that manages all tournament logic.
- **Entry point:** `Program.cs` — registers controllers, Swagger, CORS (wide-open), SignalR, and the three singleton services.
- **Controllers:**
  - `GameController` — all tournament endpoints (`/Game/...`): rounds, matches, teams, games, standings, tournament CRUD, reset. Thin delegation to `IGameService` — no business logic in the controller.
  - `HomeController` — placeholder MVC controller (returns a view at `/`).
- **Hubs:**
  - `TournamentHub` (`/hubs/tournament`) — SignalR hub for real-time push notifications. Broadcasts a `"TournamentUpdated"` message to all connected clients whenever tournament data is saved.
- **Services (registered as singletons via interfaces):**
  - `IGameService` / `GameService` — core tournament business logic (CRUD for teams/games/rounds/matches, staging workflow, standings, stats, tournament reset). Depends on `IStorageService` and `MatchmakingService`. Owns the `IHubContext<TournamentHub>` for broadcasting notifications after every save via a private `MutateTournament()` helper. Also provides `ResetTournament()` to archive the current state and start fresh with defaults.
  - `MatchmakingService` — the round-generation algorithm (matchup pairing, weighting, time-trial assignment). Pure logic, no storage dependency. Called by `GameService.GetNextRound()`.
  - `IStorageService` / `StorageService` — persistence layer only. Uses **MongoDB** (via `Aspire.MongoDB.Driver` / `IMongoClient`) with an **in-memory cache** for performance. Falls back to **in-memory-only** mode when no `IMongoClient` is available (tests). Has no SignalR dependency — notification is handled by `GameService`.
- **Entities:**
  - `TournamentDocument` — MongoDB document model storing tournament snapshots keyed by year and round version.
- **Known issues / tech-debt:**
  - `GameService` and `StorageService` are singletons holding state; this was fragile in previous runs.
  - `HomeController` exists but there are no Views in the project — likely leftover.

### TheGrunkGames.BlazorApp (Frontend)

- **Role:** Blazor Server app with interactive server-side rendering.
- **Entry point:** `Program.cs` — adds service defaults, service discovery, a typed `HttpClient` for the API, a singleton `TournamentHubConnection` for real-time updates, and optional OIDC authentication.
- **Authentication:** Optional OAuth/OIDC via `Microsoft.AspNetCore.Authentication.OpenIdConnect`. Configured in `appsettings.json` under `Authentication`. When `Authority` is empty, auth is disabled and admin pages are open to all. When configured, admin pages require the `"Admin"` authorization policy. Login/logout via `/auth/login` and `/auth/logout` endpoints. Admin access can be restricted to specific email addresses via `Authentication:AdminEmails`.
- **GameServiceClient** — typed `HttpClient` wrapper calling the backend API via Aspire service discovery (`https+http://gameservice`). Exposes methods matching `GameController` endpoints.
- **TournamentHubConnection** — singleton service wrapping a SignalR `HubConnection` to the API's `TournamentHub`. Exposes an `OnTournamentUpdated` event that Blazor components subscribe to for real-time push updates. Uses `WithAutomaticReconnect()` and fires the event on reconnection to avoid stale data.
- **Pages:**
  - `/currentRound` (`GetCurrentRound.razor`) — live view of the current round and team standings, auto-refreshes via SignalR push notifications (no polling). **Public — no auth required.**
  - `/admin/tournament` (`TournamentSettings.razor`) — edit `IsTimeTrial` and `NrTeamsToTimeTrial`. **Requires `[Authorize(Policy = "Admin")]`.**
  - `/admin/teams` (`ManageTeams.razor`) — list, add, edit, remove teams; add extra points. **Requires admin auth.**
  - `/admin/games` (`ManageGames.razor`) — list, add, edit, remove games. **Requires admin auth.**
  - `/admin/rounds` (`RoundManagement.razor`) — view rounds, generate next round (normal/staging), activate/discard staged rounds, full override, complete matches, change game/teams for a match. **Requires admin auth.**
  - `/admin/standings` (`Standings.razor`) — read-only ordered standings table. **Requires admin auth.**
  - `/admin/stats` (`TeamStatsPage.razor`) — per-team opponent and game breakdown cards. **Requires admin auth.**
  - `/admin/history` (`TournamentHistory.razor`) — view and restore historical tournament snapshots from MongoDB. **Requires admin auth.**
- **Shared admin components** (`Components/Admin/Shared/`):
  - `AccessDenied.razor` — shown when an unauthenticated user tries to access an admin page; has a "Sign in with SSO" link.
  - `StatusMessage.razor` — success/error banner with auto-dismiss.
  - `TeamEditor.razor` — editable form for a single team (name + players).
  - `GameEditor.razor` — editable form for a single game (name + device).
  - `MatchCard.razor` — displays a match with inline actions (complete, change game, change teams).
  - `RoundView.razor` — renders a round as a list of `MatchCard`s with round-level actions.
- **Navigation:** `NavMenu.razor` has a Home link (public) and an **Admin** section (wrapped in `<AuthorizeView Policy="Admin">`) linking to all seven admin pages, plus sign-in/sign-out links.
- Uses `AddInteractiveServerComponents()` render mode.
- **Implementation plan:** `docs/admin-pages-plan.md` — all 11 steps are complete.

### TheGrunkGames.Models (Shared Models)

- **Role:** Shared class library referenced by both the API and Blazor app.
- **Namespace:** `TheGrunkGames.Models.TournamentModels`
- **Key types:**
  - `Tournament` — root aggregate. Contains `Teams`, `Games`, `Rounds`. Has `IsTimeTrial` and `NrTeamsToTimeTrial` config.
  - `Team` — name, players, computed `CurrentScore`, extra points, match history helpers.
  - `Game` — name + `Device` enum.
  - `Device` — enum: `TV`, `TV_Steam`, `LAP_Steam`, `PC`, `IRL`, `TIMETRIAL`.
  - `Round` — list of `Match`es, `isStaging` flag, `IsCompleted()` check.
  - `Match` — two teams, a game, scores, completion flag, `IsTimeTrial`.
  - `MatchResult` — DTO for completing a match (match ID + scores).
  - `Player` — just a name.
  - `TeamStanding` — used for standings display.

### TheGrunkGames.ServiceDefaults

- **Role:** Shared Aspire service defaults project, referenced by both the API and Blazor app.
- Configures OpenTelemetry (logging, metrics, tracing), health check endpoints (`/health`, `/alive`), service discovery, and HTTP client resilience.

### TheGrunkGames.Tests

- **Role:** xUnit test project.
- Tests `GameService` logic using the in-memory `StorageService` fallback (no connection string).
- Also tests model validation via `System.ComponentModel.DataAnnotations.Validator`.
- Uses **NSubstitute** for mocking `IStorageService` and `IHubContext<TournamentHub>` in isolation tests.
- Test framework: xUnit 2.9 + Microsoft.NET.Test.Sdk 18.3 + NSubstitute 5.3.

---

## How to Run

### Prerequisites

- .NET 10 SDK
- Visual Studio 2026 17.x+ (Aspire tooling installed)
- Docker Desktop (for Aspire orchestration / container support)

### Local Development (via Aspire)

1. Set `TheGrunkGames.Aspire` as the startup project.
2. Press **F5** or run:
   ```powershell
   dotnet run --project TheGrunkGames.Aspire
   ```
3. The Aspire dashboard will open automatically showing both services.
4. The Blazor frontend discovers the API via Aspire service discovery — no manual URL configuration needed.
5. **Storage:** Aspire automatically spins up a MongoDB Docker container with a persistent named volume (`thegrunkgames-mongo-data`). Tournament data survives restarts. When running tests without Aspire, `StorageService` falls back to in-memory storage.

### Running Tests

```powershell
dotnet test TheGrunkGames.Tests
```

Tests use the in-memory storage fallback, so no Azure resources are required.

### Deployment (Azure Container Apps via azd)

The Aspire host is configured for Azure Container Apps deployment:

```powershell
azd init
azd up
```

Bicep parameter files are in `TheGrunkGames.Aspire/infra/`.

---

## Architecture & Data Flow

```
Browser  →  Blazor Server (blazorapp)  →  GameServiceClient (HttpClient)
                                              ↓ (Aspire service discovery)
                                         ASP.NET Core API (gameservice)
                                              ↓
                                         GameService (singleton)
                                              ↓
                                         StorageService (singleton, in-memory cache)
                                              ↓
                                    MongoDB (Docker container)  OR  In-Memory
```

- The Blazor app never accesses storage directly; all data flows through the API.
- `GameService` is the only consumer of `StorageService`.
- Both services are singletons — state is shared across all requests within the process lifetime.

---

## Coding Conventions

- **Target:** .NET 10, C# 14, nullable reference types enabled, implicit usings enabled.
- **Serialisation:** MongoDB BSON serialiser is used for storage (via `MongoDB.Driver`). The Blazor client uses `System.Text.Json` (via `ReadFromJsonAsync` / `PostAsJsonAsync`).
- **Dependency injection:** Services registered as singletons via interfaces (`IGameService`, `IStorageService`) in `Program.cs`. Controllers and other consumers depend on interfaces, not concrete classes.
- **API style:** Controller-based (`[ApiController]`), route prefix `[Route("[controller]")]`.
- **Naming:** PascalCase for public members. Some model properties use `snake_case`-style names (`Team_1_Name`, `Team_2_Name`) — maintain consistency with existing names unless refactoring broadly.
- **Async:** Service methods are `async Task` / `async Task<T>`. Follow this pattern for new methods.
- **Test style:** xUnit `[Fact]` methods, Arrange-Act-Assert pattern, in-memory storage for isolation.
- **No comments unless necessary** — the codebase is light on comments; match that style.

---

## Planned Direction

- Address session/state management issues (singleton services with mutable state).
- Containerise for both Azure and self-hosted Aspire deployments.
- ~~Expand the Blazor frontend with admin/management pages.~~ ✅ Done — all admin pages implemented (see `docs/admin-pages-plan.md`).
- Consider a managed MongoDB service (e.g. Azure Cosmos DB for MongoDB) for production.

---

## Maintaining These Instructions

**When making changes to the solution, update this file to reflect:**

- New or removed projects
- Changes to service registration, DI, or lifetime
- New or modified API endpoints
- Storage or persistence changes
- Deployment configuration changes
- New conventions or dependency additions
- Updated prerequisites or run instructions

This file should always be an accurate reflection of the current state of the solution.
