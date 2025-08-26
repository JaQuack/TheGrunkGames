using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public class GameService
    {
        private Tournament _tournament;
        private static readonly Random _random = new();
        private readonly StorageService _storageService;

        public GameService(StorageService storageService)
        {
            _tournament = new Tournament
            {
                Teams = GetDefaultTeams(),
                Games = GetDefaultGames(),
                Rounds = []
            };
            _storageService = storageService;
        }

        #region Tournament Management

        /// <summary>
        /// Gets the current tournament.
        /// </summary>
        public Tournament GetTournament() => _tournament;

        /// <summary>
        /// Sets the tournament and persists it.
        /// </summary>
        internal async Task SetTournament(Tournament newTournament)
        {
            _tournament = newTournament;
            await _storageService.SaveTournament(_tournament);
        }

        #endregion

        #region Team Management

        /// <summary>
        /// Adds a team to the tournament.
        /// </summary>
        internal async Task AddTeam(Team team)
        {
            _tournament.Teams.Add(team);
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Sets the list of teams for the tournament.
        /// </summary>
        public async Task SetTeams(List<Team> teams)
        {
            _tournament.Teams = teams;
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Checks if a team exists by name.
        /// </summary>
        internal bool TeamExists(string teamName) =>
            _tournament.Teams.Any(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));

        private Team GetTeamByName(string name) =>
            _tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        #endregion

        #region Game Management

        /// <summary>
        /// Adds a game to the tournament.
        /// </summary>
        internal async Task AddGame(Game game)
        {
            _tournament.Games.Add(game);
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Sets the list of games for the tournament.
        /// </summary>
        internal async Task SetGames(List<Game> games)
        {
            _tournament.Games = games;
            await _storageService.SaveTournament(_tournament);
        }

        #endregion

        #region Round Management

        /// <summary>
        /// Gets the current round.
        /// </summary>
        public Round GetCurrentRound() =>
            _tournament.Rounds.OrderByDescending(y => y.RoundId).FirstOrDefault(x => !x.IsCompleted() && !x.isStaging);

        /// <summary>
        /// Gets a round by its ID.
        /// </summary>
        internal Round GetRound(int roundId) =>
            _tournament.Rounds.FirstOrDefault(x => x.RoundId == roundId);

        /// <summary>
        /// Sets a round, replacing any existing round with the same ID.
        /// </summary>
        internal async Task SetRound(Round round)
        {
            var existingRound = GetRound(round.RoundId);
            _tournament.Rounds.Remove(existingRound);
            _tournament.Rounds.Add(round);
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Deletes a round by its ID.
        /// </summary>
        internal async Task DeleteRound(int roundId)
        {
            var round = GetRound(roundId);
            _tournament.Rounds.Remove(round);
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Removes all inactive (staging) rounds.
        /// </summary>
        internal async Task RemoveInactiveRounds()
        {
            _tournament.Rounds.RemoveAll(x => x.isStaging);
            await _storageService.SaveTournament(_tournament);
        }

        private List<Round> GetActiveRounds() =>
            _tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging).ToList();

        #endregion

        #region Match Management

        /// <summary>
        /// Gets a match by its ID.
        /// </summary>
        internal Match GetMatch(int matchId) =>
            GetActiveRounds().SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);

        /// <summary>
        /// Completes a match and updates team stats.
        /// </summary>
        internal async Task CompleteMatch(MatchResult result)
        {
            var match = GetMatch(result.MatchId);
            match.ScoreTeam1 = result.Team1Score;
            match.ScoreTeam2 = result.Team2Score;
            match.HasCompleted = true;

            var team1 = GetTeamByName(match.Team_1_Name);
            team1.AddMatch(match);

            if (!match.IsTimeTrial)
            {
                var team2 = GetTeamByName(match.Team_2_Name);
                team2.AddMatch(match);
            }
            await _storageService.SaveTournament(_tournament);
        }

        /// <summary>
        /// Changes the game for a match.
        /// </summary>
        internal async Task<bool> ChangeGameForMatch(int matchId, string gameName)
        {
            var match = GetMatch(matchId);
            var game = _tournament.Games.FirstOrDefault(x => x.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
            if (match == null || game == null)
                return false;
            match.Game = game;
            await _storageService.SaveTournament(_tournament);
            return true;
        }

        /// <summary>
        /// Changes the teams for a match.
        /// </summary>
        internal async Task ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            var match = GetMatch(matchId);
            var team1 = GetTeamByName(team1Name);
            var team2 = GetTeamByName(team2Name);
            if (team1 == null || team2 == null)
                throw new ArgumentException("Invalid team names provided.");
            match.Team_1_Name = team1.TeamName;
            match.Team_2_Name = team2.TeamName;
            await _storageService.SaveTournament(_tournament);
        }

        private int GetNextMatchId()
        {
            var matches = _tournament.Rounds.SelectMany(x => x.Matches);
            return !matches.Any() ? 1 : matches.Max(x => x.MatchId) + 1;
        }

        #endregion

        #region Stats & Standings

        /// <summary>
        /// Gets team standings.
        /// </summary>
        internal List<TeamStanding> GetTeamStandings() =>
            _tournament.Teams
                .Select(x => new TeamStanding { TeamName = x.TeamName, TeamScore = x.CurrentScore })
                .OrderByDescending(x => x.TeamScore)
                .ToList();

        /// <summary>
        /// Gets team statistics.
        /// </summary>
        internal List<TeamStats> GetTeamStats()
        {
            var teamsStats = new List<TeamStats>();
            foreach (var team in _tournament.Teams)
            {
                var teamsPlayedAgainst = team.MatchesPlayed.Select(x => x.GetOpponentsName(team.TeamName)).Distinct();
                var gamesPlayed = team.MatchesPlayed.Select(x => x.Game.Name).Distinct();
                var teamStats = new TeamStats
                {
                    TeamName = team.TeamName,
                    PlayedAgainstTeam = teamsPlayedAgainst
                        .Select(x => new KeyValuePair<string, int>(x, team.MatchesPlayed.Count(y => y.IsTeamPlaying(x))))
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

        /// <summary>
        /// Adds extra points to a team.
        /// </summary>
        public async Task AddExtraPoints(string teamName, int points)
        {
            var team = GetTeamByName(teamName);
            if (team != null)
            {
                team.ExtraPoints += points;
                await _storageService.SaveTournament(_tournament);
            }
        }

        #endregion

        #region Round Generation

        /// <summary>
        /// Generates and adds the next round.
        /// </summary>
        public async Task<Round> GetNextRound()
        {
            var round = new Round
            {
                RoundId = GetActiveRounds().Count == 0 ? 1 : GetActiveRounds().Max(x => x.RoundId) + 1
            };
            var matchups = GenerateMatchups(round.RoundId);
            round.Matches = matchups;
            _tournament.Rounds.Add(round);
            await _storageService.SaveTournament(_tournament);
            return round;
        }

        private List<Match> GenerateMatchups(int roundId)
        {
            var matchups = new List<Match>();
            var matchId = GetNextMatchId();

            if (_tournament.IsTimeTrial())
            {
                var minTimeTrial = _tournament.Teams.Min(x => x.TimeTrialsPlayed());
                var teamsToChooseFrom = _tournament.Teams.Where(x => x.TimeTrialsPlayed() == minTimeTrial).ToList();
                var teamToPlayTimeTrial = teamsToChooseFrom[_random.Next(teamsToChooseFrom.Count)];

                matchups.Add(new Match
                {
                    Team_1_Name = teamToPlayTimeTrial.TeamName,
                    IsTimeTrial = true,
                    Game = _tournament.Games.FirstOrDefault(x => x.Device == Device.TIMETRIAL),
                    MatchId = matchId++
                });
            }

            var allPossibleMatchups = GetAllPossibleMatchups(matchups);
            var allGamesForAllMatchups = allPossibleMatchups
                .SelectMany(x => _tournament.Games.Where(g => g.Device != Device.TIMETRIAL)
                    .Select(game => new { matchUp = x, game, weight = CalculateWeight(x, game) }))
                .ToList();

            foreach (var matchToAdd in allGamesForAllMatchups.OrderBy(x => x.weight))
            {
                if (matchups.Any(x =>
                    x.IsTeamPlaying(matchToAdd.matchUp.Value.TeamName) ||
                    x.IsTeamPlaying(matchToAdd.matchUp.Key.TeamName) ||
                    x.Game.Device == matchToAdd.game.Device))
                {
                    continue;
                }

                matchups.Add(new Match
                {
                    Game = matchToAdd.game,
                    Team_1_Name = matchToAdd.matchUp.Key.TeamName,
                    Team_2_Name = matchToAdd.matchUp.Value.TeamName,
                    MatchId = matchId++
                });

                if (_tournament.Teams.All(x => matchups.Any(y => y.IsTeamPlaying(x.TeamName))))
                    break;
            }

            if (matchups.Any(x => x.Game == null && !x.IsTimeTrial) ||
                _tournament.Teams.Any(x => !matchups.Any(y => y.IsTeamPlaying(x.TeamName))))
            {
                throw new InvalidOperationException("Failed to assign games to all teams.");
            }

            return matchups;
        }

        private List<KeyValuePair<Team, Team>> GetAllPossibleMatchups(List<Match> existingMatchups)
        {
            var allPossibleMatchups = new List<KeyValuePair<Team, Team>>();
            foreach (var team in _tournament.Teams.Where(x => !existingMatchups.Any(y => y.IsTeamPlaying(x.TeamName))))
            {
                var newMatchups = _tournament.Teams.Where(x => !x.Equals(team) &&
                    !(allPossibleMatchups.Contains(new KeyValuePair<Team, Team>(team, x)) ||
                      allPossibleMatchups.Contains(new KeyValuePair<Team, Team>(x, team))));
                foreach (var matchup in newMatchups)
                {
                    allPossibleMatchups.Add(new KeyValuePair<Team, Team>(team, matchup));
                }
            }
            return allPossibleMatchups;
        }

        private static int CalculateWeight(KeyValuePair<Team, Team> teams, Game game)
        {
            var weight = teams.Value.NrTimesHavePlayedGame(game.Name) * 3;
            weight += teams.Key.NrTimesHavePlayedGame(game.Name) * 3;
            weight += teams.Value.NrTimesPlayedAgainstTeam(teams.Key.TeamName);
            weight += teams.Value.HasCompetedWithTeamInGame(teams.Key.TeamName, game.Name) ? 50 : 0;
            return weight;
        }

        #endregion

        #region History

        /// <summary>
        /// Loads tournament history and sets it.
        /// </summary>
        internal async Task GetAndSetHistory(string partitionKey, string rowKey)
        {
            var tournament = await _storageService.GetTournament(partitionKey, rowKey);
            if (tournament != null)
                await SetTournament(tournament);
        }

        #endregion

        #region Helpers

        private static List<Game> GetDefaultGames() => new()
    {
        new() { Name = "Beerpong", Device = Device.IRL },
        new() { Name = "Mario Kart 64", Device = Device.TV },
        new() { Name = "Mario Strickers (Football)", Device = Device.TV_GameCube },
        new() { Name = "Cel Damage Overdrive", Device = Device.TV_GameCube },
        new() { Name = "Super Bomber man", Device = Device.TV },
        new() { Name = "Trackmania", Device = Device.PC_Steam },
        new() { Name = "WC3 - Castle Fight", Device = Device.PC_Steam },
        new() { Name = "Bopl Battle", Device = Device.PC_Couch },
        new() { Name = "Magequit", Device = Device.PC_Couch },
        new() { Name = "Pocket Mini Golf", Device = Device.PC_Couch },
        new() { Name = "Unreal Tournament", Device = Device.PC_Steam_2 },
        new() { Name = "Mortal Kombat 3", Device = Device.TV },
        new() { Name = "Liero", Device = Device.PC_Steam_2 },
        new() { Name = "TIMETRIAL", Device = Device.TIMETRIAL }
    };

        private static List<Team> GetDefaultTeams() => new()
    {
        new() { TeamName = "Scourge of the Goat Sea" },
        new() { TeamName = "Skinkryttarna" },
        new() { TeamName = "xX_framstjärtsFals1-;_Xx;noscope" },
        new() { TeamName = "Snöslask" },
        new() { TeamName = "Nicki Minaj's Golden Shower" },
        new() { TeamName = "Replacement Team" }
    };

        #endregion
    }
}
