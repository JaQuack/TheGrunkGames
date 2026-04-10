# Tournament Archive Plan

> **Goal:** Archive completed tournaments to Azure Table Storage so that past contestants
> can view their matches, opponents, and results. The archive is written once when a
> tournament is explicitly completed — there is no background sync and no automatic
> dual-write.

---

## Current State (as of writing)

### Storage

| What | Where | Notes |
|------|-------|-------|
| Active tournament | MongoDB (local Docker, Aspire-managed) | Saved on every mutation via `StorageService.SaveTournament()` |
| Round snapshots | MongoDB `tournaments` collection | One doc per round version, keyed `{year}_{roundVersion}` |
| History listing | `IStorageService.ListTournamentHistory()` | Returns all MongoDB docs sorted by `SavedAt` desc |
| Old archive | Azure Table Storage (CSV export available) | `PartitionKey` = year, `RowKey` = `{year}_{roundVersion}`, `TournamentSerialized` = JSON string |

### Models

- `Tournament` — root aggregate (`Teams`, `Games`, `Rounds`, `IsTimeTrial`, `NrTeamsToTimeTrial`). No metadata about tournament identity or completion.
- `TournamentHistorySummary` — `Id`, `Year`, `RoundVersion`, `SavedAt`. Thin, no display-friendly info.
- `TournamentDocument` (MongoDB entity) — `Id`, `Year`, `RoundVersion`, `TournamentData`, `SavedAt`.

### API

- `GET  /Game/Tournament/History` → returns `List<TournamentHistorySummary>` from MongoDB.
- `POST /Game/Tournament/Restore` → loads a MongoDB doc by version/year into the active tournament.
- `POST /Game/Tournament/Reset` → saves current state, then resets to defaults.

### Packages

- `Azure.Data.Tables 12.11.0` is already installed in `TheGrunkGames.csproj`.
- No Azure Table service code exists yet (previous attempt was undone).

### Blazor Pages

- Public: `/` (Home), `/currentRound`, `/standings`, `/stats`, `/history`
- Admin: `/admin/tournament`, `/admin/teams`, `/admin/games`, `/admin/rounds`, `/admin/standings`, `/admin/stats`, `/admin/history`
- The public `/history` page exists but only shows MongoDB history (round snapshots, not archived tournaments).

---

## Design Decisions

1. **One row per completed tournament.** Azure Table Storage gets a single row when the admin clicks "Archive Tournament". No per-round saves to Azure.
2. **TournamentId = admin-chosen or auto-generated.** Default: `{year}-{month}-{day}` (e.g., `2025-06-15`). Admin can override (e.g., `2025-Summer-LAN`).
3. **Tournament model gets identity and completion metadata.** New properties on `Tournament`: `TournamentId`, `TournamentName`, `CompletedAt`.
4. **Archive summary has display-friendly metadata.** New model `TournamentArchiveSummary` replaces `TournamentHistorySummary` for the archive use case (keeps winner, team count, round count, etc. so the listing page doesn't need to deserialize the full JSON).
5. **Public history page shows archived tournaments.** Clicking one navigates to a detail page showing final standings, all rounds/matches, and per-team stats.
6. **MongoDB history stays for admin use.** The admin `/admin/history` page still lists MongoDB round snapshots for restore. The public `/history` page shows archived tournaments from Azure Table.
7. **CSV import is a one-time admin endpoint.** Reads the old CSV format and inserts rows into the new Azure Table schema.

---

## Azure Table Schema

**Table name:** `TournamentArchive`

| Column | Type | Example | Source |
|--------|------|---------|--------|
| `PartitionKey` | string | `"2025"` | `DateTime.UtcNow.Year` |
| `RowKey` | string | `"2025-06-15"` or `"2025-Summer-LAN"` | Admin-provided or auto-generated |
| `TournamentName` | string | `"Gustavs Speltävling Sommar 2025"` | Admin input |
| `CompletedAt` | DateTime | `2025-06-15T22:30:00Z` | Set at archive time |
| `WinningTeam` | string | `"Skinkryttarna"` | Computed from standings at archive time |
| `TotalRounds` | int | `12` | `tournament.Rounds.Count(r => !r.isStaging)` |
| `TotalTeams` | int | `8` | `tournament.Teams.Count` |
| `TotalMatches` | int | `48` | Sum of all round match counts |
| `TournamentDataJson` | string | Full JSON | `System.Text.Json.JsonSerializer.Serialize(tournament)` |

---

## Implementation Steps

### Step 1 — Add Tournament Metadata to Model

**File:** `TheGrunkGames.Models/TournamentModels/Tournament.cs`

Add three new properties to the `Tournament` class:

```
TournamentId   : string  (default empty)
TournamentName : string  (default empty)
CompletedAt    : DateTime? (default null)
```

These are set when the admin archives the tournament. They are ignored during
normal active-tournament operations (MongoDB saves don't need them).

### Step 2 — Create Archive Summary Model

**New file:** `TheGrunkGames.Models/TournamentModels/TournamentArchiveSummary.cs`

Properties:
- `TournamentId` (string) — RowKey
- `TournamentName` (string)
- `Year` (string) — PartitionKey
- `CompletedAt` (DateTime)
- `WinningTeam` (string)
- `TotalRounds` (int)
- `TotalTeams` (int)
- `TotalMatches` (int)

This is the DTO returned by the history listing endpoint. It does NOT contain the
full tournament JSON — that's only fetched when drilling into a specific tournament.

### Step 3 — Create Azure Table Entity

**New file:** `TheGrunkGames/Entities/TournamentArchiveEntity.cs`

Implements `Azure.Data.Tables.ITableEntity`. Maps between the Azure Table schema
and the `Tournament` / `TournamentArchiveSummary` models.

Methods:
- `static FromTournament(Tournament t, string year)` — creates entity, auto-computes `WinningTeam`, `TotalRounds`, `TotalTeams`, `TotalMatches`, serializes tournament JSON.
- `ToSummary()` — returns `TournamentArchiveSummary` (without deserializing JSON).
- `GetTournament()` — deserializes and returns full `Tournament`.

### Step 4 — Create Archive Storage Service

**New file:** `TheGrunkGames/Services/ITournamentArchiveService.cs`  
**New file:** `TheGrunkGames/Services/TournamentArchiveService.cs`

Interface:
```
Task<bool> IsAvailableAsync();
Task ArchiveTournamentAsync(Tournament tournament);
Task<List<TournamentArchiveSummary>> ListArchivedTournamentsAsync();
Task<Tournament?> GetArchivedTournamentAsync(string year, string tournamentId);
```

Implementation:
- Constructor reads `ConnectionStrings:AzureTableStorage` from `IConfiguration`.
- If empty/missing, `IsAvailable` returns false and all methods gracefully no-op or return empty.
- Table name: `TournamentArchive`.
- `ArchiveTournamentAsync`: Creates `TournamentArchiveEntity.FromTournament()`, calls `TableClient.UpsertEntityAsync()`.
- `ListArchivedTournamentsAsync`: Queries all rows, maps to `TournamentArchiveSummary`, sorted by `CompletedAt` desc.
- `GetArchivedTournamentAsync`: Point lookup by `PartitionKey` + `RowKey`, calls `GetTournament()` to deserialize.

Register as singleton in `Program.cs`:
```
builder.Services.AddSingleton<ITournamentArchiveService, TournamentArchiveService>();
```

### Step 5 — Add Archive Method to GameService

**File:** `TheGrunkGames/Services/IGameService.cs`

Add to interface:
```
Task ArchiveTournamentAsync(string? tournamentName, string? tournamentId);
Task<List<TournamentArchiveSummary>> ListArchivedTournamentsAsync();
Task<Tournament?> GetArchivedTournamentAsync(string year, string tournamentId);
```

**File:** `TheGrunkGames/Services/GameService.cs`

- Add `ITournamentArchiveService` as constructor dependency.
- `ArchiveTournamentAsync`:
  1. Get current tournament from storage.
  2. Generate `TournamentId` if not provided: `$"{DateTime.UtcNow:yyyy-MM-dd}"`.
  3. Set `TournamentName` (use provided or default `$"Gustavs Speltävling {DateTime.UtcNow.Year}"`).
  4. Set `CompletedAt = DateTime.UtcNow`.
  5. Call `_archiveService.ArchiveTournamentAsync(tournament)`.
  6. Do NOT reset the active tournament — that remains a separate admin action.
- `ListArchivedTournamentsAsync`: delegates to `_archiveService`.
- `GetArchivedTournamentAsync`: delegates to `_archiveService`.

### Step 6 — Add API Endpoints

**File:** `TheGrunkGames/Controllers/GameController.cs`

Add endpoints:

```
POST /Game/Tournament/Archive?name={name}&tournamentId={id}
```
- Both params optional. Calls `_gameService.ArchiveTournamentAsync(name, id)`.
- Returns 200 on success, 400 if archive service unavailable.

```
GET /Game/Tournament/Archives
```
- Returns `List<TournamentArchiveSummary>` from `_gameService.ListArchivedTournamentsAsync()`.

```
GET /Game/Tournament/Archives/{year}/{tournamentId}
```
- Returns full `Tournament` from `_gameService.GetArchivedTournamentAsync(year, tournamentId)`.
- Returns 404 if not found.

### Step 7 — Add CSV Import Endpoint

**New file:** `TheGrunkGames/Controllers/MigrationController.cs`

Single endpoint:
```
POST /Migration/ImportCsv
```
- Accepts multipart file upload (the old CSV).
- For each CSV row:
  1. Parse `PartitionKey` → year.
  2. Parse `RowKey` → use as-is for `TournamentId` (e.g., `"2024_0"`).
  3. Deserialize `TournamentSerialized` → `Tournament`.
  4. Set `TournamentName = $"Tournament {year} (Imported)"`.
  5. Set `CompletedAt = DateTime.UtcNow` (original timestamp not in CSV).
  6. Call `_archiveService.ArchiveTournamentAsync(tournament)`.
- Returns count of imported records.
- Safe to run multiple times (upsert).

### Step 8 — Update Blazor GameServiceClient

**File:** `TheGrunkGames.BlazorApp/GameServiceClient.cs`

Add methods:
```csharp
Task<ApiResult> ArchiveTournament(string? name, string? tournamentId)
Task<ApiResult<List<TournamentArchiveSummary>>> GetArchivedTournaments()
Task<ApiResult<Tournament>> GetArchivedTournament(string year, string tournamentId)
```

### Step 9 — Update Public History Page

**File:** `TheGrunkGames.BlazorApp/Components/Pages/History.razor`

Replace the current MongoDB-based listing with Azure Table archive listing:
- Call `gameServiceClient.GetArchivedTournaments()` on init.
- Display cards for each archived tournament showing:
  - Tournament name
  - Year
  - Date completed
  - Winning team (trophy emoji)
  - Stats summary (X rounds, Y teams, Z matches)
  - "View Details →" link to `/history/{year}/{tournamentId}`

**File:** `TheGrunkGames.BlazorApp/Components/Pages/History.razor.css`

Update styling to match the dark scoreboard theme (same pattern as existing pages).

### Step 10 — Create Archived Tournament Detail Page

**New file:** `TheGrunkGames.BlazorApp/Components/Pages/ArchivedTournament.razor`  
**New file:** `TheGrunkGames.BlazorApp/Components/Pages/ArchivedTournament.razor.css`

Route: `@page "/history/{Year}/{TournamentId}"`

Loads full tournament via `gameServiceClient.GetArchivedTournament(Year, TournamentId)`.

Sections:
1. **Header** — Tournament name, date completed, winner banner.
2. **Final Standings** — Ranked table with medals (reuse standings styling).
3. **Team Stats** — For each team, show:
   - Opponents played and how many times
   - Games played and how many times
   - Win/loss record per opponent (computed from match scores)
4. **All Rounds** — Expandable/collapsible round list showing all matches with scores.
5. **Back link** — "← Back to History"

### Step 11 — Add Archive Action to Admin UI

**New file:** `TheGrunkGames.BlazorApp/Components/Pages/Admin/ArchiveTournament.razor`

Route: `@page "/admin/archive"`  
Requires: `[Authorize(Policy = "Admin")]`

UI:
- Input: Tournament Name (text, optional, placeholder: "Gustavs Speltävling {year}")
- Input: Tournament ID (text, optional, placeholder: auto-generated date)
- Shows current tournament summary: team count, round count, top 3 standings
- Warning text: "This will save a permanent snapshot of the current tournament to the cloud archive."
- Button: "Archive Tournament" → calls `ArchiveTournament()` → shows success/error
- Does NOT reset the tournament. The admin can reset separately via the existing Reset button.

### Step 12 — Update NavMenu

**File:** `TheGrunkGames.BlazorApp/Components/Layout/NavMenu.razor`

- Verify public section has: Home, Standings, Team Stats, History
- Add to admin section: "Archive Tournament" (between "History" and "Sign out")

### Step 13 — Add Configuration

**File:** `TheGrunkGames/appsettings.json`

Add:
```json
"ConnectionStrings": {
  "AzureTableStorage": ""
}
```

When empty, the `TournamentArchiveService` logs a warning and operates in no-op
mode. All archive-related UI shows "Archive not configured" instead of failing.

### Step 14 — Update Tests

**File:** `TheGrunkGames.Tests/GameServiceTests.cs`

Add tests:
- `ArchiveTournament_SetsMetadataAndDelegatesToArchiveService`
- `ArchiveTournament_GeneratesDefaultId_WhenNoneProvided`
- `ArchiveTournament_UsesProvidedIdAndName`

Mock `ITournamentArchiveService` with NSubstitute (same pattern as existing tests).

### Step 15 — Update Documentation

**File:** `.github/copilot-instructions.md`

Update the following sections:
- **TheGrunkGames (Backend API) → Services**: Add `ITournamentArchiveService` / `TournamentArchiveService`.
- **TheGrunkGames (Backend API) → Controllers**: Add `MigrationController` and new archive endpoints.
- **TheGrunkGames.Models**: Add `TournamentArchiveSummary` to key types, note new metadata on `Tournament`.
- **TheGrunkGames.BlazorApp → Pages**: Add `/admin/archive` and `/history/{Year}/{TournamentId}` page descriptions.
- **Architecture & Data Flow**: Add Azure Table Storage as archive-only destination.

**File:** `docs/tournament-archive-plan.md` (this file)

Mark steps as complete as they are implemented.

---

## Step Checklist

- [x] Step 1 — Add Tournament metadata to `Tournament` model
- [x] Step 2 — Create `TournamentArchiveSummary` model
- [x] Step 3 — Create `TournamentArchiveEntity` (Azure Table entity)
- [x] Step 4 — Create `ITournamentArchiveService` / `TournamentArchiveService`
- [x] Step 5 — Add archive methods to `IGameService` / `GameService`
- [x] Step 6 — Add archive API endpoints to `GameController`
- [x] Step 7 — Add CSV import endpoint (`MigrationController`)
- [x] Step 8 — Update `GameServiceClient` with archive methods
- [x] Step 9 — Update public `/history` page
- [x] Step 10 — Create `/history/{Year}/{TournamentId}` detail page
- [x] Step 11 — Create `/admin/archive` page
- [x] Step 12 — Update `NavMenu` with archive link
- [x] Step 13 — Add `AzureTableStorage` connection string to config
- [x] Step 14 — Add unit tests
- [x] Step 15 — Update documentation

---

## Files Changed (Summary)

### New Files
| File | Purpose |
|------|---------|
| `TheGrunkGames.Models/TournamentModels/TournamentArchiveSummary.cs` | Archive listing DTO |
| `TheGrunkGames/Entities/TournamentArchiveEntity.cs` | Azure Table entity mapping |
| `TheGrunkGames/Services/ITournamentArchiveService.cs` | Archive service interface |
| `TheGrunkGames/Services/TournamentArchiveService.cs` | Azure Table Storage implementation |
| `TheGrunkGames/Controllers/MigrationController.cs` | CSV import endpoint |
| `TheGrunkGames.BlazorApp/Components/Pages/ArchivedTournament.razor` | Tournament detail view |
| `TheGrunkGames.BlazorApp/Components/Pages/ArchivedTournament.razor.css` | Detail view styling |
| `TheGrunkGames.BlazorApp/Components/Pages/Admin/ArchiveTournament.razor` | Admin archive action page |

### Modified Files
| File | Change |
|------|--------|
| `TheGrunkGames.Models/TournamentModels/Tournament.cs` | Add `TournamentId`, `TournamentName`, `CompletedAt` |
| `TheGrunkGames/Services/IGameService.cs` | Add archive method signatures |
| `TheGrunkGames/Services/GameService.cs` | Implement archive methods, add `ITournamentArchiveService` dependency |
| `TheGrunkGames/Controllers/GameController.cs` | Add archive endpoints |
| `TheGrunkGames/Program.cs` | Register `ITournamentArchiveService` singleton |
| `TheGrunkGames/appsettings.json` | Add `AzureTableStorage` connection string |
| `TheGrunkGames.BlazorApp/GameServiceClient.cs` | Add archive client methods |
| `TheGrunkGames.BlazorApp/Components/Pages/History.razor` | Switch to Azure archive listing |
| `TheGrunkGames.BlazorApp/Components/Pages/History.razor.css` | Update styling for cards |
| `TheGrunkGames.BlazorApp/Components/Layout/NavMenu.razor` | Add archive link to admin section |
| `TheGrunkGames.Tests/GameServiceTests.cs` | Add archive tests |
| `.github/copilot-instructions.md` | Document new services, pages, and architecture |

### Unchanged Files
| File | Why |
|------|-----|
| `TheGrunkGames/Services/StorageService.cs` | No changes — MongoDB saves stay as-is for the active tournament |
| `TheGrunkGames/Services/IStorageService.cs` | No changes — archive is a separate service |
| `TheGrunkGames/Entities/TournamentDocument.cs` | No changes — MongoDB document stays the same |
| `TheGrunkGames.Models/TournamentModels/TournamentHistorySummary.cs` | Keep for admin MongoDB history (not archived tournaments) |

---

## Old CSV Data Migration Notes

The existing CSV has this format:
```
PartitionKey: "2024"
RowKey: "2024_0"
TournamentSerialized: "{\"Teams\":[...],\"Games\":[...],\"Rounds\":[]}"
```

Mapping to new schema:
- `PartitionKey` → `Year` (keep as-is, e.g., `"2024"`)
- `RowKey` → `TournamentId` (keep as-is, e.g., `"2024_0"`)
- `TournamentSerialized` → deserialize to `Tournament`, then re-serialize as `TournamentDataJson`
- `TournamentName` → set to `"Tournament 2024 (Round 0)"` or similar
- `CompletedAt` → not available in CSV; use import timestamp
- `WinningTeam`, `TotalRounds`, etc. → compute from deserialized tournament data

Multiple rows with the same PartitionKey but different RowKeys (e.g., `2024_0`, `2024_5`, `2024_12`) represent different round snapshots from the same year. During import, either:
- Import only the highest RowKey per year as the "final" state, OR
- Import all as separate archive entries (each gets a unique TournamentId)

Recommendation: Import all rows. The higher round versions represent more complete
tournaments. Label them `"Tournament 2024 (Round X)"` so the history page shows progression.
