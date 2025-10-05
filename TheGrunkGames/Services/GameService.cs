using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TheGrunkGames.Hubs;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public class GameService : IGameService
    {
        private readonly IStorageService _storageService;
        private readonly MatchmakingService _matchmakingService;
        private readonly IHubContext<TournamentHub>? _hubContext;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public GameService(IStorageService storageService, MatchmakingService matchmakingService, IHubContext<TournamentHub>? hubContext = null)
        {
            _storageService = storageService;
            _matchmakingService = matchmakingService;
            _hubContext = hubContext;
        }

        public async Task InitializeAsync()
        {
            await MutateTournament(async tournament =>
            {
                if (tournament.Games == null)
                    tournament.Games = GetDefaultGames();
                if (tournament.Teams == null)
                    tournament.Teams = GetDefaultTeams();
            });
        }

        private async Task MutateTournament(Func<Tournament, Task> mutation)
        {
            await _lock.WaitAsync();
            try
            {
                var tournament = await _storageService.GetTournament();
                await mutation(tournament);
                await _storageService.SaveTournament(tournament);

                if (_hubContext != null)
                    await _hubContext.Clients.All.SendAsync("TournamentUpdated");
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<T> MutateTournament<T>(Func<Tournament, Task<T>> mutation)
        {
            await _lock.WaitAsync();
            try
            {
                var tournament = await _storageService.GetTournament();
                var result = await mutation(tournament);
                await _storageService.SaveTournament(tournament);

                if (_hubContext != null)
                    await _hubContext.Clients.All.SendAsync("TournamentUpdated");

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        #region Tournament Management

        public async Task<Tournament> GetTournament() => await _storageService.GetTournament();

        public async Task SetTournament(Tournament newTournament)
        {
            await _lock.WaitAsync();
            try
            {
                await _storageService.SaveTournament(newTournament);

                if (_hubContext != null)
                    await _hubContext.Clients.All.SendAsync("TournamentUpdated");
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region Team Management

        public async Task AddTeam(Team team)
        {
            await MutateTournament(async tournament =>
            {
                var teams = tournament.GetTeams();
                teams.Add(team);
                tournament.SetTeams(teams);
            });
        }

        public async Task SetTeams(List<Team> teams)
        {
            await MutateTournament(async tournament =>
            {
                tournament.SetTeams(teams);
            });
        }

        public async Task<bool> TeamExists(string teamName) =>
            (await GetTournament()).GetTeams().Any(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));

        public async Task<bool> RemoveTeam(string teamName)
        {
            return await MutateTournament(async tournament =>
            {
                var team = tournament.GetTeams().FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
                if (team == null)
                    return false;

                if (tournament.GetActiveMatches().Any(m => m.IsTeamPlaying(teamName)))
                    return false;

                tournament.Teams.Remove(team);
                return true;
            });
        }

        #endregion

        #region Game Management

        public async Task AddGame(Game game)
        {
            await MutateTournament(async tournament =>
            {
                tournament.Games.Add(game);
            });
        }

        public async Task SetGames(List<Game> games)
        {
            await MutateTournament(async tournament =>
            {
                tournament.Games = games;
            });
        }

        public async Task<bool> RemoveGame(string gameName)
        {
            return await MutateTournament(async tournament =>
            {
                var game = tournament.Games.FirstOrDefault(x => x.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
                if (game == null)
                    return false;

                if (tournament.GetActiveMatches().Any(m => m.Game.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase)))
                    return false;

                tournament.Games.Remove(game);
                return true;
            });
        }

        #endregion

        #region Round Management

        public async Task<Round?> GetCurrentRound() =>
            (await GetTournament()).Rounds.OrderByDescending(y => y.RoundId).FirstOrDefault(x => !x.IsCompleted() && !x.isStaging);

        public async Task<Round?> GetRound(int roundId) =>
            (await GetTournament()).Rounds.FirstOrDefault(x => x.RoundId == roundId);

        public async Task SetRound(Round round)
        {
            await MutateTournament(async tournament =>
            {
                var existingRound = tournament.Rounds.FirstOrDefault(x => x.RoundId == round.RoundId);
                if (existingRound != null)
                    tournament.Rounds.Remove(existingRound);
                tournament.Rounds.Add(round);
            });
        }

        public async Task DeleteRound(int roundId)
        {
            await MutateTournament(async tournament =>
            {
                var round = tournament.Rounds.FirstOrDefault(x => x.RoundId == roundId);
                if (round != null)
                    tournament.Rounds.Remove(round);
            });
        }

        public async Task RemoveInactiveRounds()
        {
            await MutateTournament(async tournament =>
            {
                tournament.Rounds.RemoveAll(x => x.isStaging);
            });
        }

        public async Task<Round> GetNextRound()
        {
            return await MutateTournament(async tournament =>
            {
                var activeRounds = tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging).ToList();
                var round = new Round
                {
                    RoundId = activeRounds.Count == 0 ? 1 : activeRounds.Max(x => x.RoundId) + 1
                };
                round.Matches = _matchmakingService.GenerateMatchups(tournament);
                tournament.Rounds.Add(round);
                return round;
            });
        }

        public async Task<Round> GetNextRoundStaging()
        {
            return await MutateTournament(async tournament =>
            {
                tournament.Rounds.RemoveAll(x => x.isStaging);

                var activeRounds = tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging).ToList();
                var round = new Round
                {
                    RoundId = activeRounds.Count == 0 ? 1 : activeRounds.Max(x => x.RoundId) + 1,
                    isStaging = true
                };
                round.Matches = _matchmakingService.GenerateMatchups(tournament);
                tournament.Rounds.Add(round);
                return round;
            });
        }

        public async Task<Round?> ActivateOrDiscardStaging(bool activate, int roundId)
        {
            if (roundId == 0)
                throw new ArgumentException("Invalid RoundId");

            return await MutateTournament<Round?>(async tournament =>
            {
                var round = tournament.Rounds.FirstOrDefault(x => x.RoundId == roundId);
                if (round != null && activate)
                {
                    round.isStaging = false;
                }
                tournament.Rounds.RemoveAll(x => x.isStaging);
                return round;
            });
        }

        #endregion

        #region Match Management

        public async Task<Match?> GetMatch(int matchId) =>
            (await GetTournament()).Rounds.Where(x => x.RoundId != 0 && !x.isStaging)
                .SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);

        public async Task CompleteMatch(MatchResult result)
        {
            await MutateTournament(async tournament =>
            {
                var match = tournament.Rounds.SelectMany(x => x.Matches).FirstOrDefault(x => x.MatchId == result.MatchId);
                if (match == null)
                    return;
                match.ScoreTeam1 = result.Team1Score;
                match.ScoreTeam2 = result.Team2Score;
                match.HasCompleted = true;
            });
        }

        public async Task<bool> ChangeGameForMatch(int matchId, string gameName)
        {
            return await MutateTournament(async tournament =>
            {
                var match = tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging)
                    .SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);
                var game = tournament.Games.FirstOrDefault(x => x.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
                if (match == null || game == null)
                    return false;
                match.Game = game;
                return true;
            });
        }

        public async Task ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            await MutateTournament(async tournament =>
            {
                var match = tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging)
                    .SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);
                if (match == null)
                    throw new ArgumentException("Match not found.");
                var teams = tournament.GetTeams();
                var team1 = teams.FirstOrDefault(x => x.TeamName.Equals(team1Name, StringComparison.InvariantCultureIgnoreCase));
                var team2 = teams.FirstOrDefault(x => x.TeamName.Equals(team2Name, StringComparison.InvariantCultureIgnoreCase));
                if (team1 == null || team2 == null)
                    throw new ArgumentException("Invalid team names provided.");
                match.Team_1_Name = team1.TeamName;
                match.Team_2_Name = team2.TeamName;
            });
        }

        #endregion

        #region Stats & Standings

        public async Task<List<TeamStanding>> GetTeamStandings() =>
            (await GetTournament()).GetTeams()
                .Select(x => new TeamStanding { TeamName = x.TeamName, TeamScore = x.CurrentScore })
                .OrderByDescending(x => x.TeamScore)
                .ToList();

        public async Task<List<TeamStats>> GetTeamStats()
        {
            var teamsStats = new List<TeamStats>();
            foreach (var team in (await GetTournament()).GetTeams())
            {
                var teamsPlayedAgainst = team.MatchesPlayed.Select(x => x.GetOpponentsName(team.TeamName)).Where(x => x != null).Distinct();
                var gamesPlayed = team.MatchesPlayed.Select(x => x.Game.Name).Distinct();
                var teamStats = new TeamStats
                {
                    TeamName = team.TeamName,
                    PlayedAgainstTeam = teamsPlayedAgainst
                        .Select(x => new KeyValuePair<string, int>(x!, team.MatchesPlayed.Count(y => y.IsTeamPlaying(x!))))
                        .OrderByDescending(x => x.Value)
                        .ToList(),
                    PlayedGames = gamesPlayed
                        .Select(x => new KeyValuePair<string, int>(x, team.MatchesPlayed.Count(y => y.Game.Name.Equals(x))))
                        .OrderByDescending(x => x.Value)
                        .ToList(),
                };
                teamsStats.Add(teamStats);
            }
            return teamsStats;
        }

        public async Task AddExtraPoints(string teamName, int points)
        {
            await MutateTournament(async tournament =>
            {
                var team = tournament.GetTeams().FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
                if (team != null)
                    team.ExtraPoints += points;
            });
        }

        #endregion

        #region Reset

        public async Task ResetTournament()
        {
            await _lock.WaitAsync();
            try
            {
                var current = await _storageService.GetTournament();
                await _storageService.SaveTournament(current);

                var fresh = new Tournament
                {
                    Games = GetDefaultGames(),
                    Rounds = [],
                    IsTimeTrial = false,
                    NrTeamsToTimeTrial = 0
                };
                fresh.SetTeams(GetDefaultTeams());
                await _storageService.SaveTournament(fresh);

                if (_hubContext != null)
                    await _hubContext.Clients.All.SendAsync("TournamentUpdated");
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        #region History

        public async Task GetAndSetHistory(string version, string year)
        {
            var tournament = await _storageService.GetTournament(version, year);
            if (tournament != null)
                await SetTournament(tournament);
        }

        public async Task<List<TournamentHistorySummary>> ListTournamentHistory()
        {
            return await _storageService.ListTournamentHistory();
        }

        #endregion

        #region Helpers

        private static List<Game> GetDefaultGames() => new()
        {
            new() { Name = "Bopl Battle", Device = Device.LAP_Steam },
            new() { Name = "Make way", Device = Device.LAP_Steam },
            new() { Name = "Stick Fight", Device = Device.LAP_Steam },
            new() { Name = "Screencheat", Device = Device.TV_Steam },
            new() { Name = "Hidden in plainsight", Device = Device.TV_Steam},
            new() { Name = "Bunny Hill", Device = Device.TV_Steam },
            new() { Name = "NBA JAM", Device = Device.TV },
            new() { Name = "Tekken 3", Device = Device.TV },
            new() { Name = "Mario Kart 64", Device = Device.TV },
            new() { Name = "Kubb", Device = Device.IRL },
            new() { Name = "Caps", Device = Device.IRL },
            new() { Name = "WC3 - WmW Reborn 13.4", Device = Device.PC },
            new() { Name = "Sacrifice", Device = Device.PC },
            new() { Name = "TIMETRIAL", Device = Device.TIMETRIAL }
        };

        private static List<Team> GetDefaultTeams() => new()
        {
            new() { TeamName = "Scourge of the Goat Sea" },
            new() { TeamName = "Skinkryttarna" },
            new() { TeamName = "De Försenade" },
            new() { TeamName = "Snöslask" },
            new() { TeamName = "Lag 9" },
            new() { TeamName = "Pink Fluffy Unicorns" },
            new() { TeamName = "Filipinska Fruföreningen" }
        };

        #endregion
    }
}
