# TheGrunkGames — Improvements Plan

> Created from a full codebase review. Each item is self-contained — pick up any item without needing prior context.

---

## 1. SignalR Real-Time Updates (replace polling)

**Problem:** `GetCurrentRound.razor` runs a `while(true)` loop calling the API every 1 second. Every open spectator browser hammers the backend continuously, and the UI only updates on the next poll even though data changed instantly.

**What to do:**

### Backend (API project — `TheGrunkGames`)
1. Add the `Microsoft.AspNetCore.SignalR` package (included in the shared framework, no NuGet needed).
2. Create `Hubs/TournamentHub.cs` — an empty hub class:
   ```csharp
   public class TournamentHub : Hub { }
   ```
3. In `Program.cs`:
   - Add `builder.Services.AddSignalR();`
   - Map the hub: `app.MapHub<TournamentHub>("/hubs/tournament");`
   - Update the CORS policy to allow SignalR's negotiation headers/credentials if needed.
4. Inject `IHubContext<TournamentHub>` into `StorageService` (or `GameService`).
5. At the end of `SaveTournament()`, broadcast a notification:
   ```csharp
   await _hubContext.Clients.All.SendAsync("TournamentUpdated");
   ```
   This is a lightweight "something changed" signal — clients re-fetch data themselves.

### Blazor App (`TheGrunkGames.BlazorApp`)
1. Add NuGet package `Microsoft.AspNetCore.SignalR.Client`.
2. Create a shared service (e.g., `TournamentHubConnection`) that:
   - Builds a `HubConnection` pointing to `https+http://gameservice/hubs/tournament` (Aspire service discovery works with SignalR).
   - Exposes an `event Action OnTournamentUpdated`.
   - Starts the connection and registers the `"TournamentUpdated"` handler.
   - Register it as a scoped service in `Program.cs`.
3. In `GetCurrentRound.razor`:
   - Inject `TournamentHubConnection`.
   - Subscribe to `OnTournamentUpdated` → call `LoadData()` + `StateHasChanged()`.
   - **Remove the `PeriodicRefresh()` `while(true)` loop entirely.**
   - Implement `IAsyncDisposable` to unsubscribe and dispose the connection.
4. Optionally do the same for admin pages so they auto-refresh when another admin makes changes.

### Files touched
- `TheGrunkGames/Hubs/TournamentHub.cs` (new)
- `TheGrunkGames/Program.cs`
- `TheGrunkGames/Services/StorageService.cs`
- `TheGrunkGames.BlazorApp/TournamentHubConnection.cs` (new)
- `TheGrunkGames.BlazorApp/Program.cs`
- `TheGrunkGames.BlazorApp/Components/Pages/GetCurrentRound.razor`

### Tests to add (`GameServiceTests.cs`)
> ✅ **Status: Item 1 is implemented.** Existing tests already pass because `StorageService()` parameterless constructor sets `_hubContext = null`, so `SaveTournament` skips broadcasting in tests.

- [x] All 48 existing tests pass (verified — no regressions from SignalR changes).
- [x] `StorageService_SaveTournament_DoesNotThrow_WhenHubContextIsNull` — explicitly verifies the in-memory-only path works after the constructor change. ✅ Added.
- [x] `StorageService_GetTournament_ReturnsNewTournament_WhenNothingSaved` — verifies fresh `StorageService` returns an empty tournament. ✅ Added.
- [x] `StorageService_GetTournament_ReturnsCachedData_AfterSave` — verifies save→load round-trip works in in-memory mode. ✅ Added.
- [x] `MutateTournament_BroadcastsTournamentUpdated_WhenHubContextProvided` — ✅ Added (deferred from item 1, enabled by item 8). Uses NSubstitute to mock `IHubContext<TournamentHub>`, calls `AddTeam`, verifies `SendAsync("TournamentUpdated")` was called.
- [x] `MutateTournament_DoesNotThrow_WhenHubContextIsNull` — ✅ Added. Verifies mutations work without SignalR.

### Verification
- Run the Aspire AppHost. Open `/currentRound` in two browser tabs.
- Complete a match via the admin page. Both spectator tabs should update within ~100ms without any polling.
- Confirm no `while(true)` loop remains.

---

## 2. Admin Authentication Gate

> ✅ **Status: Item 2 is implemented.** OAuth/OIDC authentication with configurable provider.

**Problem:** All `/admin/*` Blazor pages and all `GameController` API endpoints are publicly accessible. Any LAN attendee who discovers the URL can generate rounds, delete teams, or complete matches accidentally.

**Implemented approach:** OAuth/OIDC via ASP.NET Core Authentication
- **When `Authentication:Authority` is configured:** Full OIDC login flow. Admin pages require the `"Admin"` policy. Users see a "Sign in with SSO" link in the nav. Admin access can optionally be restricted to specific email addresses via `Authentication:AdminEmails`.
- **When `Authentication:Authority` is empty (default):** Auth is disabled. The `"Admin"` policy passes for everyone. Admin pages are open to all (same as before).

### Provider setup examples

**Microsoft Entra ID:**
```json
"Authentication": {
  "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
  "ClientId": "your-app-client-id",
  "ClientSecret": "your-client-secret",
  "AdminEmails": ["jacob@example.com"]
}
```

**Google:**
```json
"Authentication": {
  "Authority": "https://accounts.google.com",
  "ClientId": "your-google-client-id.apps.googleusercontent.com",
  "ClientSecret": "your-google-secret",
  "AdminEmails": []
}
```

### Files touched
- `TheGrunkGames.BlazorApp/Program.cs` — OIDC + Cookie auth, authorization policies, login/logout endpoints
- `TheGrunkGames.BlazorApp/appsettings.json` — `Authentication` config section
- `TheGrunkGames.BlazorApp/Components/_Imports.razor` — added auth `@using` directives
- `TheGrunkGames.BlazorApp/Components/Routes.razor` — `AuthorizeRouteView` with `AccessDenied` fallback
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/AccessDenied.razor` (new) — sign-in prompt shown for unauthenticated users
- `TheGrunkGames.BlazorApp/Components/Layout/NavMenu.razor` — admin section wrapped in `<AuthorizeView>`, sign in/out links
- All 5 admin pages — added `@attribute [Authorize(Policy = "Admin")]`

### Tests to add
> Auth tests require `WebApplicationFactory<Program>` or similar integration test setup.

- [ ] `AdminPolicy_AllowsAll_WhenAuthNotConfigured` — when `Authority` is empty, unauthenticated users can access admin pages.
- [ ] `AdminPolicy_RequiresLogin_WhenAuthConfigured` — when `Authority` is set, unauthenticated requests to admin pages → `AccessDenied` component shown.
- [ ] `AdminPolicy_RestrictsToAdminEmails_WhenConfigured` — when `AdminEmails` is set, only users with matching email claims pass the policy.
- [ ] `LoginEndpoint_RedirectsToOidcProvider` — GET `/auth/login` → 302 redirect to the configured OIDC authority.
- [ ] `LogoutEndpoint_ClearsCookie` — GET `/auth/logout` → clears the authentication cookie.

### Verification
- With `Authority` empty: admin pages work exactly as before (no login required).
- With `Authority` configured: navigate to `/admin/teams` → see "Admin Access Required" prompt → click "Sign in with SSO" → redirected to OIDC provider → after login, redirected back to admin area.
- `NavMenu` shows admin links only when authorized; shows "🔑 Admin Sign In" link when not.

---

## 3. Concurrency Protection on StorageService

> ✅ **Status: Item 3 is implemented.** `SemaphoreSlim` lock in `GameService` with `MutateTournament` helper.

**Problem:** `StorageService` is a singleton. Every mutation follows Get→Mutate→Save. If two requests hit simultaneously (e.g., two admins completing different matches), one save overwrites the other because they both read the same snapshot.

**Implemented approach:** `SemaphoreSlim` lock in `GameService`
- Added `private readonly SemaphoreSlim _lock = new(1, 1)` to `GameService`.
- Created `MutateTournament(Func<Tournament, Task>)` and `MutateTournament<T>(Func<Tournament, Task<T>>)` helper methods that acquire the lock, get the tournament, run the mutation, save, notify via SignalR, and release the lock in a `try/finally`.
- Refactored **all mutating methods** (`AddTeam`, `SetTeams`, `AddGame`, `SetGames`, `SetRound`, `DeleteRound`, `RemoveInactiveRounds`, `GetNextRound`, `GetNextRoundStaging`, `ActivateOrDiscardStaging`, `CompleteMatch`, `ChangeGameForMatch`, `ChangeTeamsForMatch`, `AddExtraPoints`, `InitializeAsync`) to use `MutateTournament`.
- `SetTournament` acquires the lock directly (it replaces the whole tournament, no read-modify-write needed).
- Read-only methods (`GetTournament`, `GetCurrentRound`, `GetRound`, `GetMatch`, `GetTeamStandings`, `GetTeamStats`, `TeamExists`) remain lock-free — they read the cached snapshot which is always in a consistent state.
- This also eliminated the previous `GetNextRoundStaging`/`ActivateOrDiscardStaging` multi-step re-entrancy issue — they now do their full operation in a single locked block.

### Files touched
- `TheGrunkGames/Services/GameService.cs` — all mutating methods refactored

### Tests to add (`GameServiceTests.cs`)
- [x] `CompleteMatch_Concurrent_BothMatchesCompleted` — ✅ Added. Fires `CompleteMatch` for two different matches via `Task.WhenAll`. Asserts both have correct scores.
- [x] `AddTeam_Concurrent_BothTeamsAdded` — ✅ Added. Fires `AddTeam` for two teams concurrently. Asserts both exist.
- [x] `AddExtraPoints_Concurrent_BothApplied` — ✅ Added. Fires `AddExtraPoints` for two teams concurrently. Asserts both score changes are applied.

### Verification
- The concurrency tests above are the primary verification — all pass.
- Additionally: run the Aspire AppHost, open two admin browser tabs, complete different matches at the same time → both should be saved.

---

## 4. Consistent Error Handling in GameServiceClient

> ✅ **Status: Item 4 is implemented.** `ApiResult` / `ApiResult<T>` records with all pages updated.

**Problem:** `GameServiceClient` methods are inconsistent — some return `bool`, some return `null`, some throw raw `Exception`s. The Blazor pages show raw exception messages to the admin.

**Implemented approach:**
- Created `ApiResult` and `ApiResult<T>` records in `TheGrunkGames.BlazorApp/ApiResult.cs` with `Ok()` and `Fail(error)` factory methods.
- Refactored every `GameServiceClient` method to return `ApiResult` or `ApiResult<T>`. Each method wraps its HTTP call in try/catch and returns a clean result on HTTP error or exception.
- Updated all 5 admin pages + `MatchCard.razor` + `GetCurrentRound.razor` to check `result.Success` and display `result.Error` via the `StatusMessage` component.
- Also fixed `AddExtraPoints`, `ChangeGameForMatch`, and `ChangeTeamsForMatch` to use query string parameters instead of JSON body (matching the controller's parameter binding).

### Files touched
- `TheGrunkGames.BlazorApp/ApiResult.cs` (new)
- `TheGrunkGames.BlazorApp/GameServiceClient.cs` (all methods refactored)
- `TheGrunkGames.BlazorApp/Components/Pages/GetCurrentRound.razor`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/TournamentSettings.razor`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/ManageTeams.razor`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/ManageGames.razor`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/RoundManagement.razor`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/Standings.razor`
- `TheGrunkGames.BlazorApp/Components/Admin/Shared/MatchCard.razor`

### Verification
- Stop the API service, try an admin action in the Blazor app → shows a clean error message, not a stack trace.

---

## 5. Styled Spectator Scoreboard

> ✅ **Status: Item 5 is implemented.** Dark-themed scoreboard with CSS isolation.

**Problem:** `/currentRound` (the page shown on a LAN-party projector) is unstyled raw HTML. No visual hierarchy, no large fonts, no team differentiation.

**Implemented approach:**
- Redesigned `GetCurrentRound.razor` with a dark projector-friendly layout:
  - Large title banner with emoji
  - Card-based match grid with team names left/right, red "VS" badge, game name below
  - Completed matches shown with green border and score
  - Standings table with medal emojis (🥇🥈🥉) for top 3, gold/silver/bronze text colours
  - Large readable fonts throughout
- CSS isolation via `GetCurrentRound.razor.css` — dark navy background (`#1a1a2e`), responsive grid layout, hover effects.

### Files touched
- `TheGrunkGames.BlazorApp/Components/Pages/GetCurrentRound.razor` (rewritten)
- `TheGrunkGames.BlazorApp/Components/Pages/GetCurrentRound.razor.css` (new)

### Verification
- Open `/currentRound` on a large screen or projector. Team names and scores should be readable from 5+ meters away.

---

## 6. Delete Team / Delete Game Endpoints

> ✅ **Status: Item 6 is implemented.** Backend + controller + client + tests.

**Problem:** You can add teams and games but can only remove them via full-list replacement (`SetTeams`/`SetGames`). No targeted delete exists.

**Implemented approach:**
- `GameService.RemoveTeam(teamName)` and `GameService.RemoveGame(gameName)` — validate the team/game isn't in any active (non-completed) match before removing. Return `false` if not found or blocked.
- `[HttpDelete("Team")]` and `[HttpDelete("Game")]` endpoints in `GameController`.
- `GameServiceClient.RemoveTeam()` and `GameServiceClient.RemoveGame()` returning `ApiResult`.

### Files touched
- `TheGrunkGames/Services/GameService.cs`
- `TheGrunkGames/Controllers/GameController.cs`
- `TheGrunkGames.BlazorApp/GameServiceClient.cs`

### Tests added (`GameServiceTests.cs`)
- [x] `RemoveTeam_RemovesTeamFromTournament` — ✅
- [x] `RemoveTeam_ReturnsFalse_WhenTeamNotFound` — ✅
- [x] `RemoveTeam_IsCaseInsensitive` — ✅
- [x] `RemoveTeam_ReturnsFalse_WhenTeamInActiveMatch` — ✅
- [x] `RemoveTeam_AllowsRemoval_WhenTeamOnlyInCompletedMatches` — ✅
- [x] `RemoveGame_RemovesGameFromTournament` — ✅
- [x] `RemoveGame_ReturnsFalse_WhenGameNotFound` — ✅
- [x] `RemoveGame_IsCaseInsensitive` — ✅
- [x] `RemoveGame_ReturnsFalse_WhenGameInActiveMatch` — ✅
- [x] `RemoveGame_AllowsRemoval_WhenGameOnlyInCompletedMatches` — ✅

### Verification
- Add a team, then delete it via the API. Confirm it's gone.
- Try deleting a team that's in an active match → returns error.

---

## 7. Tournament Reset / Archive

> ✅ **Status: Item 7 is implemented.** Backend + controller + client + UI with confirmation + tests.

**Problem:** No way to start a fresh tournament. `StorageService` keys documents by `{year}_{roundVersion}` so history exists in MongoDB, but there's no endpoint to archive the current state and reset.

**Implemented approach:**
- `GameService.ResetTournament()` — acquires the lock, saves the current tournament (archive), creates a fresh `Tournament` with default games/teams and default settings (`IsTimeTrial = false`, `NrTeamsToTimeTrial = 0`), saves it, and broadcasts via SignalR.
- `[HttpPost("Tournament/Reset")]` endpoint in `GameController`.
- `GameServiceClient.ResetTournament()` returning `ApiResult`.
- "Reset Tournament" section on `TournamentSettings.razor` with a two-step confirmation (click "Reset Tournament" → shown warning + "Yes, Reset" / "Cancel"). After reset, reloads the tournament data to reflect defaults.

### Files touched
- `TheGrunkGames/Services/GameService.cs`
- `TheGrunkGames/Controllers/GameController.cs`
- `TheGrunkGames.BlazorApp/GameServiceClient.cs`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/TournamentSettings.razor`

### Tests added (`GameServiceTests.cs`)
- [x] `ResetTournament_ClearsAllRounds` — ✅ play 2 rounds, reset, assert rounds empty.
- [x] `ResetTournament_ResetsToDefaultTeams` — ✅ custom teams → reset → 7 default teams present.
- [x] `ResetTournament_ResetsToDefaultGames` — ✅ custom games → reset → 14 default games present.
- [x] `ResetTournament_ResetsExtraPoints` — ✅ extra points added → reset → all scores 0.
- [x] `ResetTournament_ResetsSettingsToDefaults` — ✅ `IsTimeTrial=true` → reset → `IsTimeTrial=false`, `NrTeamsToTimeTrial=0`.

### Verification
- Run a few rounds, then hit reset on `/admin/tournament`. Tournament should have 0 rounds, default teams/games.
- Previous tournament data should still exist in MongoDB (verify via the history feature in item 10).

---

## 8. Extract Service Interfaces

> ✅ **Status: Item 8 is implemented.** Interfaces extracted, DI updated, all tests pass.

**Problem:** `GameService` and `StorageService` are concrete classes registered directly. Testing requires instantiating the real classes, and swapping implementations (e.g., a different storage backend) isn't possible without code changes.

**Implemented approach:**
- Created `IStorageService` interface with `SaveTournament` and `GetTournament` methods.
- Created `IGameService` interface with all public methods from `GameService` (tournament, team, game, round, match, stats, reset, history).
- `StorageService` implements `IStorageService`; `GameService` implements `IGameService`.
- `GameService` constructor now accepts `IStorageService` instead of concrete `StorageService`.
- `GameController` constructor now accepts `IGameService` instead of concrete `GameService`.
- DI registration in `Program.cs` updated to `AddSingleton<IStorageService, StorageService>()` and `AddSingleton<IGameService, GameService>()`.
- All existing tests pass unchanged — they construct concrete classes which now also satisfy the interfaces.

### Files touched
- `TheGrunkGames/Services/IStorageService.cs` (new)
- `TheGrunkGames/Services/IGameService.cs` (new)
- `TheGrunkGames/Services/StorageService.cs` (implements `IStorageService`)
- `TheGrunkGames/Services/GameService.cs` (implements `IGameService`, depends on `IStorageService`)
- `TheGrunkGames/Controllers/GameController.cs` (depends on `IGameService`)
- `TheGrunkGames/Program.cs`

### Tests
- [x] All 69 existing tests pass with zero changes. ✅
- [x] `GameService_CanBeConstructed_WithIStorageService` — ✅ Added. Verifies `GameService` accepts `IStorageService`.
- [x] `MutateTournament_BroadcastsTournamentUpdated_WhenHubContextProvided` — ✅ Added (deferred from item 1). Uses NSubstitute to mock `IHubContext<TournamentHub>`.
- [x] `AddTeam_CallsSaveTournament` — ✅ Added. Uses mocked `IStorageService` to verify `SaveTournament` is called with the correct tournament state.
- [x] `CompleteMatch_CallsSaveTournament_WithCompletedMatch` — ✅ Added. Verifies `SaveTournament` receives the tournament with the completed match.

### Verification
- Build succeeds. All 87 tests pass.

---

## 9. Team Stats Page

> ✅ **Status: Item 9 is implemented.** API endpoint + client + Blazor page + nav link + tests.

**Problem:** `GameService.GetTeamStats()` exists and returns detailed per-team opponent/game breakdowns, but there's no API endpoint exposing it and no Blazor page displaying it.

**Implemented approach:**
- Added `[HttpGet("TeamStats")]` endpoint in `GameController`.
- Added `GetTeamStats()` method in `GameServiceClient` returning `ApiResult<List<TeamStats>>`.
- Created `TeamStatsPage.razor` at `/admin/stats` (file named `TeamStatsPage` to avoid naming conflict with the `TeamStats` model class). Shows a card per team with "Played Against" and "Games Played" tables, ordered by count descending.
- Added nav link "Team Stats" in `NavMenu.razor` under the Admin section.

### Files touched
- `TheGrunkGames/Controllers/GameController.cs`
- `TheGrunkGames.BlazorApp/GameServiceClient.cs`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/TeamStatsPage.razor` (new)
- `TheGrunkGames.BlazorApp/Components/Layout/NavMenu.razor`

### Tests added (`GameServiceTests.cs`)
- [x] `GetTeamStats_ReturnsOpponentsAndGamesPlayed` — already existed. ✅
- [x] `GetTeamStats_ReturnsEmptyLists_WhenNoMatchesPlayed` — ✅ Added.
- [x] `GetTeamStats_CountsMultipleMatchesAgainstSameOpponent` — ✅ Added.
- [x] `GetTeamStats_OrdersByMostPlayedDescending` — ✅ Added.

### Verification
- Play a few rounds, then visit `/admin/stats`. Each team should show accurate opponent and game history.

---

## 10. Tournament History Page

> ✅ **Status: Item 10 is implemented.** API endpoints + client + Blazor page + nav link + test.

**Problem:** `StorageService.GetTournament(version, year)` and `GameService.GetAndSetHistory(version, year)` exist but aren't exposed via any controller endpoint or UI. The versioning feature is unused.

**Implemented approach:**
- Created `TournamentHistorySummary` DTO in `TheGrunkGames.Models` (id, year, roundVersion, savedAt).
- Added `ListTournamentHistory()` to `IStorageService` / `StorageService` — queries MongoDB for all documents, returns summaries sorted by `SavedAt` descending. Returns empty list in in-memory mode.
- Added `ListTournamentHistory()` to `IGameService` / `GameService` — delegates to storage.
- Added `[HttpGet("Tournament/History")]` and `[HttpPost("Tournament/Restore")]` endpoints in `GameController`.
- Added `GetTournamentHistory()` and `RestoreTournament()` client methods in `GameServiceClient`.
- Created `TournamentHistory.razor` at `/admin/history` — table of saved snapshots with "Restore" button per row (with confirmation step).
- Added "History" nav link in `NavMenu.razor`.

### Files touched
- `TheGrunkGames.Models/TournamentModels/TournamentHistorySummary.cs` (new)
- `TheGrunkGames/Services/IStorageService.cs`
- `TheGrunkGames/Services/StorageService.cs`
- `TheGrunkGames/Services/IGameService.cs`
- `TheGrunkGames/Services/GameService.cs`
- `TheGrunkGames/Controllers/GameController.cs`
- `TheGrunkGames.BlazorApp/GameServiceClient.cs`
- `TheGrunkGames.BlazorApp/Components/Pages/Admin/TournamentHistory.razor` (new)
- `TheGrunkGames.BlazorApp/Components/Layout/NavMenu.razor`

### Tests added
- [x] `ListTournamentHistory_ReturnsEmpty_InMemoryMode` — ✅ Verifies empty list returned gracefully without MongoDB.
- [ ] `ListTournamentHistory_ReturnsSavedSnapshots` — **integration test** (requires MongoDB).
- [ ] `GetAndSetHistory_LoadsHistoricalTournament` — requires MongoDB versioning to test properly.
- [ ] `GetAndSetHistory_DoesNothing_WhenVersionNotFound` — requires MongoDB versioning to test properly.

### Verification
- Play a few rounds (each save creates a new version document in MongoDB).
- Visit `/admin/history` → see the list of snapshots.
- Restore an earlier version → tournament reverts to that state.
- In in-memory mode (tests), the page shows "No history available."

---

## 11. Model Validation Attributes

> ✅ **Status: Item 11 is implemented.** Validation attributes + redundant controller checks removed + tests.

**Problem:** Models like `Team`, `Game`, `Match`, `MatchResult` have no validation. The controller does manual null checks, but empty names, negative scores, etc. slip through.

**Implemented approach:**
- Added `System.ComponentModel.DataAnnotations` attributes:
  - `Team.TeamName` → `[Required, StringLength(50)]`
  - `Game.Name` → `[Required, StringLength(50)]`
  - `Player.Name` → `[Required, StringLength(50)]`
  - `MatchResult.MatchId` → `[Range(1, int.MaxValue)]`
  - `MatchResult.Team1Score` / `Team2Score` → `[Range(0, int.MaxValue)]`
- Removed redundant `== null` checks in `GameController` for body-bound parameters (`SetRound`, `CompleteMatch`, `SetTournament`, `SetTeams`, `AddTeam`, `SetGames`, `AddGame`) — the `[ApiController]` attribute handles automatic 400 responses for null/invalid models.
- Kept business-logic checks (e.g., "does this round exist", "does this team exist in an active match").

### Files touched
- `TheGrunkGames.Models/TournamentModels/Team.cs`
- `TheGrunkGames.Models/TournamentModels/Game.cs`
- `TheGrunkGames.Models/TournamentModels/Player.cs`
- `TheGrunkGames.Models/TournamentModels/MatchResult.cs`
- `TheGrunkGames/Controllers/GameController.cs`

### Tests added (`TheGrunkGames.Tests/ModelValidationTests.cs` — new file)
- [x] `Team_Validation_FailsWithEmptyName` — ✅
- [x] `Team_Validation_FailsWithNameTooLong` — ✅
- [x] `Team_Validation_PassesWithValidName` — ✅
- [x] `Game_Validation_FailsWithEmptyName` — ✅
- [x] `Game_Validation_PassesWithValidName` — ✅
- [x] `MatchResult_Validation_FailsWithZeroMatchId` — ✅
- [x] `MatchResult_Validation_FailsWithNegativeScore` — ✅
- [x] `MatchResult_Validation_PassesWithValidData` — ✅
- [x] `Player_Validation_FailsWithEmptyName` — ✅

### Verification
- POST to `/Game/Team` with an empty body → should get `400` with validation errors.
- POST to `/Game/CompleteMatch` with negative scores → should get `400`.

---

## 12. Explicit `@rendermode InteractiveServer` on Spectator Page

> ✅ **Status: Item 12 was already implemented.** `GetCurrentRound.razor` already has `@rendermode InteractiveServer` (added during item 1/5). `Home.razor` has no interactive code and doesn't need it.

### Verification
- Open `/currentRound` — works with explicit render mode.
- SignalR circuit is established (visible in browser dev tools network tab).

---

## Priority Order

| Order | Item | Effort | Impact | Status |
|-------|------|--------|--------|--------|
| 1 | SignalR real-time updates | Medium | High | ✅ |
| 2 | Admin authentication gate | Small | High | ✅ |
| 3 | Concurrency protection | Small | High | ✅ |
| 4 | Consistent error handling | Small | Medium | ✅ |
| 5 | Styled spectator scoreboard | Medium | Medium | ✅ |
| 6 | Delete team/game endpoints | Small | Low | ✅ |
| 7 | Tournament reset/archive | Small | Medium | ✅ |
| 8 | Extract service interfaces | Small | Medium | ✅ |
| 9 | Team stats page | Small | Low | ✅ |
| 10 | Tournament history page | Small | Low | ✅ |
| 11 | Model validation attributes | Small | Low | ✅ |
| 12 | Explicit render mode | Trivial | Low | ✅ |
