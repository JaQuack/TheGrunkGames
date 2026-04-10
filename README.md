# 🏆 TheGrunkGames

A LAN-party tournament management system built for **Gustavs Speltävling** — an annual gaming tournament among friends. Handles team registration, game scheduling, round/match management, scoring, and live standings with real-time updates.

## Features

### Public Pages
- **Home** — Live view of the current round with match cards and standings, auto-refreshed via SignalR
- **Standings** — Full ranked leaderboard with scores and extra points
- **Team Stats** — Per-team breakdowns: opponents faced (with W/D/L records), games played (with per-match results)
- **History** — Browse archived tournaments from previous years with full detail views

### Admin Pages *(SSO-protected)*
- **Tournament Settings** — Configure time trial mode and team counts
- **Teams** — Add, edit, remove teams; award extra points
- **Games** — Manage the game pool and device assignments
- **Rounds & Matches** — Generate rounds (with staging/preview), complete matches, override games/teams
- **Archive** — Save completed tournaments to Azure Table Storage for historical records
- **History** — Restore tournament snapshots from MongoDB

### Real-Time Updates
All public pages update instantly via **SignalR** — no manual refresh needed. When a match is completed or a round is generated, every connected browser updates automatically.

## Tech Stack

| Layer | Technology |
|---|---|
| **Orchestration** | .NET Aspire 13 |
| **Backend API** | ASP.NET Core Web API (.NET 10 / C# 14) |
| **Frontend** | Blazor Server (Interactive SSR) |
| **Active Storage** | MongoDB (Docker container, persistent volume) |
| **Archive Storage** | Azure Table Storage (optional, graceful degradation) |
| **Real-Time** | SignalR |
| **Auth** | OpenID Connect / OAuth (optional) |
| **Tests** | xUnit + NSubstitute |

## Architecture

```
Browser  →  Blazor Server  →  GameServiceClient (HttpClient)
                                    ↓ (Aspire service discovery)
                               ASP.NET Core API
                                    ↓
                               GameService
                                 ↓         ↓
                    StorageService    TournamentArchiveService
                         ↓                      ↓
                    MongoDB              Azure Table Storage
                    (active data)        (completed tournaments)
```

- The Blazor app never accesses storage directly — all data flows through the API
- `GameService` is the single source of truth for tournament logic
- Azure Table Storage is optional — archive features degrade gracefully when unavailable

## Solution Structure

```
TheGrunkGames.sln
├── TheGrunkGames.Aspire/          # Aspire AppHost — orchestrates the distributed app
├── TheGrunkGames/                 # Backend API (ASP.NET Core Web API)
├── TheGrunkGames.BlazorApp/       # Frontend (Blazor Server)
├── TheGrunkGames.Models/          # Shared domain models
├── TheGrunkGames.ServiceDefaults/ # Shared Aspire service defaults (OpenTelemetry, health checks)
└── TheGrunkGames.Tests/           # Unit tests (xUnit)
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Visual Studio 2026 17.x+ with Aspire tooling (recommended)

### Run Locally

1. Clone the repo:
   ```bash
   git clone https://github.com/JaQuack/TheGrunkGames.git
   cd TheGrunkGames
   ```

2. Start the app via Aspire:
   ```powershell
   dotnet run --project TheGrunkGames.Aspire
   ```

3. The Aspire dashboard opens automatically. Both the Blazor frontend and API are accessible from there.

Aspire automatically spins up a MongoDB Docker container with a persistent volume — no manual database setup needed. Service discovery between the frontend and API is handled automatically.

### Run Tests

```powershell
dotnet test TheGrunkGames.Tests
```

Tests use an in-memory storage fallback — no external services required.

### Azure Table Storage (Optional)

To enable tournament archiving, provide a connection string as an environment variable:

```
ConnectionStrings__archiveTables=DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net
```

When absent, the archive service starts in no-op mode — everything else works normally.

## Deployment

The app is designed to be self-hosted in Docker on a local server. Aspire handles container orchestration. Bicep parameter files for Azure deployment are available in `TheGrunkGames.Aspire/infra/`.

## License

This is a personal project for a private LAN-party tournament. Feel free to use it as inspiration for your own tournament system.
