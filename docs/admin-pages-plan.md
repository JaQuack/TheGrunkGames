# Admin Pages Plan — TheGrunkGames Blazor App

> This document is the implementation plan for adding admin/management Blazor pages that cover all mutable tournament endpoints exposed by `GameController`.

---

## 1. Scope — API Endpoints Covered

| # | Endpoint | HTTP | Admin Action |
|---|----------|------|--------------|
| 1 | `POST Tournament` | Set | Edit tournament settings (IsTimeTrial, NrTeamsToTimeTrial) |
| 2 | `GET Tournament` | Get | (used internally by pages to load state) |
| 3 | `POST Teams` | Set | Replace the full team list |
| 4 | `POST Team` | Add | Add a single team (with players) |
| 5 | `GET Teams` | Get | (load current teams for display/edit) |
| 6 | `POST Games` | Set | Replace the full game list |
| 7 | `POST Game` | Add | Add a single game |
| 8 | `GET Games` | Get | (load current games for display/edit) |
| 9 | `POST GetNextRound` | Action | Generate the next round |
| 10 | `POST GetNextRound_Staging` | Action | Generate the next round in staging mode |
| 11 | `POST SetStaging` | Action | Activate or discard a staged round |
| 12 | `POST SetRound_FullOverride` | Set | Full override of a round |
| 13 | `POST CompleteMatch` | Action | Submit match scores |
| 14 | `POST AddExtraPoints` | Action | Give bonus points to a team |
| 15 | `POST ChangeGameForMatch` | Action | Swap the game for an existing match |
| 16 | `POST ChangeTeamsForMatch` | Action | Swap the teams for an existing match |

---

## 2. Design Principles

1. **Load-then-edit** — Every "set" page first calls the matching GET endpoint to load the latest state. The admin edits that state and submits the delta.
2. **Reusable components** — Shared editor/display components for `Team`, `Game`, `Match`, and `Round` are built once and composed into pages.
3. **Feedback component** — A single `StatusMessage` component handles success/error banners across all admin pages.
4. **Nav grouping** — All admin pages are nested under an **Admin** group in `NavMenu`.
5. **Interactive SSR** — All admin pages use `@rendermode InteractiveServer` (matches the existing Blazor Server setup).

---

## 3. Shared Components (build first)

These live in `Components/Admin/Shared/`.

| # | Component | Purpose | Props (Parameters) |
|---|-----------|---------|-------------------|
| C1 | `StatusMessage.razor` | Shows a success or error banner, auto-fades after a few seconds. | `string? Message`, `bool IsError` |
| C2 | `TeamEditor.razor` | Editable form for a single `Team` (name + player list). Used for both "add" and "edit" scenarios. | `Team Team`, `EventCallback OnRemove` |
| C3 | `GameEditor.razor` | Editable form for a single `Game` (name + Device enum). Used for both "add" and "edit" scenarios. | `Game Game`, `EventCallback OnRemove` |
| C4 | `MatchCard.razor` | Displays a single `Match` with inline actions: complete (score entry), change game, change teams. | `Match Match`, `List<Team> Teams`, `List<Game> Games`, `EventCallback OnMatchChanged` |
| C5 | `RoundView.razor` | Renders a `Round` as a list of `MatchCard`s. Includes staging badge and round-level actions. | `Round Round`, `List<Team> Teams`, `List<Game> Games`, `EventCallback OnRoundChanged` |

---

## 4. Admin Pages

These live in `Components/Pages/Admin/`.

### P1 — Tournament Settings (`/admin/tournament`)

- **On load:** `GetTournament()`
- **Editable fields:** `IsTimeTrial` (checkbox), `NrTeamsToTimeTrial` (number input).
- **Save:** `SetTournament(tournament)`.
- **Components used:** `StatusMessage`.

### P2 — Manage Teams (`/admin/teams`)

- **On load:** `GetTeams()` + `GetGames()` (games needed for context, not editing).
- **Display:** List of `TeamEditor` components for every existing team.
- **Actions:**
  - Edit any team inline → **Save All** calls `SetTeams(teams)`.
  - **Add Team** button appends a blank `TeamEditor`; on save calls `AddTeam(team)` for new entries.
  - Remove a team from the list.
- **Extra points:** Inline per-team control (team name is known): number input + button → `AddExtraPoints(teamName, points)`.
- **Components used:** `TeamEditor`, `StatusMessage`.

### P3 — Manage Games (`/admin/games`)

- **On load:** `GetGames()`.
- **Display:** List of `GameEditor` components.
- **Actions:**
  - Edit any game inline → **Save All** calls `SetGames(games)`.
  - **Add Game** button appends a blank `GameEditor`; on save calls `AddGame(game)` for new entries.
  - Remove a game from the list.
- **Components used:** `GameEditor`, `StatusMessage`.

### P4 — Round Management (`/admin/rounds`)

- **On load:** `GetTournament()` to get all rounds; also loads teams and games for drop-downs.
- **Display:** Each round rendered via `RoundView`.
- **Actions (round-level):**
  - **Generate Next Round** → `GetNextRound()`.
  - **Generate Next Round (Staging)** → `GetNextRoundStaging()`.
  - **Activate Staged Round** → `SetStaging(activate: true, roundId)`.
  - **Discard Staged Round** → `SetStaging(activate: false, roundId)`.
  - **Full Override** (advanced, collapsed by default) → `SetRound(round)`.
- **Actions (match-level, inside `MatchCard`):**
  - **Complete Match** — inline score inputs → `CompleteMatch(result)`.
  - **Change Game** — dropdown → `ChangeGameForMatch(matchId, gameName)`.
  - **Change Teams** — two dropdowns → `ChangeTeamsForMatch(matchId, team1, team2)`.
- **Components used:** `RoundView`, `MatchCard`, `StatusMessage`.

### P5 — Standings (read-only, `/admin/standings`)

- **On load:** `GetTeamStandings()`.
- **Display:** Ordered table of team names and scores.
- **Components used:** `StatusMessage` (for load errors only).

---

## 5. Navigation Update

Add an **Admin** section to `NavMenu.razor`:

```
Admin
├── Tournament Settings   → /admin/tournament
├── Teams                 → /admin/teams
├── Games                 → /admin/games
├── Rounds & Matches      → /admin/rounds
└── Standings             → /admin/standings
```

---

## 6. Implementation Order

The build order ensures each step is self-contained and testable.

| Step | Item | Type | Depends On |
|------|------|------|------------|
| 1 | `StatusMessage.razor` | Component (C1) | — |
| 2 | `GameEditor.razor` | Component (C3) | — |
| 3 | `TeamEditor.razor` | Component (C2) | — |
| 4 | **Manage Games** page | Page (P3) | C1, C3 |
| 5 | **Manage Teams** page | Page (P2) | C1, C2 |
| 6 | `MatchCard.razor` | Component (C4) | C1 |
| 7 | `RoundView.razor` | Component (C5) | C4 |
| 8 | **Round Management** page | Page (P4) | C1, C4, C5 |
| 9 | **Tournament Settings** page | Page (P1) | C1 |
| 10 | **Standings** page | Page (P5) | C1 |
| 11 | **NavMenu update** | Layout change | All pages |

---

## 7. File Manifest

```
TheGrunkGames.BlazorApp/
├── Components/
│   ├── Admin/
│   │   └── Shared/
│   │       ├── StatusMessage.razor
│   │       ├── TeamEditor.razor
│   │       ├── GameEditor.razor
│   │       ├── MatchCard.razor
│   │       └── RoundView.razor
│   ├── Pages/
│   │   └── Admin/
│   │       ├── TournamentSettings.razor    (P1)
│   │       ├── ManageTeams.razor           (P2)
│   │       ├── ManageGames.razor           (P3)
│   │       ├── RoundManagement.razor       (P4)
│   │       └── Standings.razor             (P5)
│   └── Layout/
│       └── NavMenu.razor                   (updated)
```

---

## 8. How to Resume From This Plan

Each step in Section 6 is independent once its dependencies are met. To continue implementation from a fresh prompt (no prior conversation context), use:

> **Prompt template:**
>
> ```
> Continue implementing the admin pages plan from `docs/admin-pages-plan.md`.
> Build **Step N** — `<Item Name>`.
> Read the plan first, then implement only that step.
> After the step is done, run a build to verify.
> ```
>
> **Example — starting from step 1:**
>
> ```
> Continue implementing the admin pages plan from `docs/admin-pages-plan.md`.
> Build Step 1 — `StatusMessage.razor`.
> Read the plan first, then implement only that step.
> After the step is done, run a build to verify.
> ```

Increment the step number for each subsequent prompt. If a step produces a shared component, the next page-step will automatically pick it up.

To build **all remaining steps** in one go:

> ```
> Continue implementing the admin pages plan from `docs/admin-pages-plan.md`.
> Build all remaining steps in order. Read the plan first.
> After each step, run a build to verify before moving on.
> ```

---

*Plan created: ready for step-by-step implementation.*
