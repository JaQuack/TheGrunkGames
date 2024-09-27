using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames.Objects;

namespace TheGrunkGames.Services
{
    public class GameService
    {
        private Tournament Tournament;
        private static Random _random = new();

        private readonly StorageService _storageService;

        public GameService(StorageService storageService)
        {
            var games = new List<Game>
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

            var teams = new List<Team>()
            {
                new() { TeamName = "Scourge of the Goat Sea" },
                new() { TeamName = "Skinkryttarna" },
                new() { TeamName = "xX_framstjärtsFals1-;_Xx;noscope" },
                new() { TeamName = "Snöslask" },
                new() { TeamName = "Nicki Minaj's Golden Shower" },
                new() { TeamName = "Replacement Team" }
            };

            Tournament = new Tournament { Teams = teams, Games = games, Rounds = new List<Round>() };
            _storageService = storageService;
        }

        internal Match GetMatch(int matchId)
        {
            return GetActiveRounds().SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);
        }

        private List<Round> GetActiveRounds()
        {
            return Tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging).ToList();
        }


        internal async Task CompleteMatch(MatchResult result)
        {
            var match = GetMatch(result.MatchId);
            match.ScoreTeam1 = result.Team1Score;
            match.ScoreTeam2 = result.Team2Score;
            match.Compleated = true;
            var team1 = GetTeamByName(match.Team_1_Name);
            team1.AddMatch(match);

            if (!match.IsTimeTrial)
            {
                var team2 = GetTeamByName(match.Team_2_Name);
                team2.AddMatch(match);
            }
            await _storageService.SaveTournament(Tournament);
        }

        private Team GetTeamByName(string name)
        {
            return Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        internal async Task SetRound(Round round)
        {
            var existingRound = GetRound(round.RoundId);
            Tournament.Rounds.Remove(existingRound);
            Tournament.Rounds.Add(round);
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task<bool> ChangeGameForMatch(int matchId, string gameName)
        {
            var match = GetMatch(matchId);
            var game = Tournament.Games.FirstOrDefault(x => x.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
            if (match == null || game == null)
                return false;
            match.Game = game;
            await _storageService.SaveTournament(Tournament);
            return false;
        }

        internal async Task ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            var match = GetMatch(matchId);
            var team1 = GetTeamByName(team1Name);
            var team2 = GetTeamByName(team2Name);
            if (team1 == null || team2 == null)
                throw new Exception("You fuckedup the teamnames STUPID!");
            match.Team_1_Name = team1.TeamName;
            match.Team_2_Name = team2.TeamName;
            await _storageService.SaveTournament(Tournament);
        }

        internal Round GetRound(int roundId)
        {
            return Tournament.Rounds.FirstOrDefault(x => x.RoundId == roundId);
        }

        internal async Task AddGame(Game game)
        {
            Tournament.Games.Add(game);
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task SetGames(List<Game> games)
        {
            Tournament.Games = games;
            await _storageService.SaveTournament(Tournament);
        }

        internal List<TeamStanding> GetTeamStandings()
        {
            var teams = Tournament.Teams;
            return teams.Select(x => new TeamStanding { TeamName = x.TeamName, TeamScore = x.CurrentScore }).OrderByDescending(x => x.TeamScore).ToList();
        }

        internal List<TeamStats> GetTeamStats()
        {
            var teams = Tournament.Teams;
            var teamsStats = new List<TeamStats>();
            foreach (var team in teams)
            {
                var teamsPlayedAgainst = team.MatchesPlayed.Select(x => x.GetOpponentsName(team.TeamName)).Distinct();
                var gamesPlayed = team.MatchesPlayed.Select(x => x.Game.Name).Distinct();
                var teamStats = new TeamStats
                {
                    TeamName = team.TeamName,
                    PlayedAgainstTeam = teamsPlayedAgainst.Select(x => new KeyValuePair<string, int>(x, team.MatchesPlayed.Sum(y => y.IsTeamPlaying(x) ? 1 : 0))).OrderByDescending(x => x.Value).ToList(),
                    PlayedGames = gamesPlayed.Select(x => new KeyValuePair<string, int>(x, team.MatchesPlayed.Sum(y => y.Game.Name.Equals(x) ? 1 : 0))).OrderByDescending(x => x.Value).ToList(),
                };
                teamsStats.Add(teamStats);
            }
            return teamsStats;
        }

        internal bool TeamExists(string teamName)
        {
            return Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase)) != null;
        }

        internal async Task AddTeam(Team team)
        {
            Tournament.Teams.Add(team);
            await _storageService.SaveTournament(Tournament);
        }

        public async Task SetTeams(List<Team> teams)
        {
            Tournament.Teams = teams;
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task SetTournament(Tournament newTornament)
        {
            Tournament = newTornament;
            await _storageService.SaveTournament(Tournament);
        }

        public Round GetCurrentRound()
        {
            return Tournament.Rounds.OrderByDescending(y => y.RoundId).FirstOrDefault(x => !x.IsCompleted() && !x.isStaging);
        }

        public async Task<Round> GetNextRound()
        {
            var round = new Round { RoundId = GetActiveRounds().Count == 0 ? 1 : GetActiveRounds().Max(x => x.RoundId) + 1 };
            var nrGamesToSelect = (int)Math.Floor(Tournament.Teams.Count / 2m);
            var matchups = new List<Match>();
            var matchId = GetNextMatchId();

            if (Tournament.IsTimeTrial())
            {
                var minTimeTrial = Tournament.Teams.Min(x => x.TimeTrialsPlayed());
                var teamsToChooseFrom = Tournament.Teams.Where(x => x.TimeTrialsPlayed() == minTimeTrial);
                var teamToPlayTimeTrial = teamsToChooseFrom.ElementAt(_random.Next(teamsToChooseFrom.Count()));

                matchups.Add(new Match
                {
                    Team_1_Name = teamToPlayTimeTrial.TeamName,
                    IsTimeTrial = true,
                    Game = Tournament.Games.FirstOrDefault(x => x.Device == Device.TIMETRIAL),
                    MatchId = matchId++
                });
            }

            var allPossibleMatchups = new List<KeyValuePair<Team, Team>>();
            foreach (var team in Tournament.Teams.Where(x => !matchups.Any(y => y.IsTeamPlaying(x.TeamName))))
            {
                var newMatchups = Tournament.Teams.Where(x => !x.Equals(team) && !(allPossibleMatchups.Contains(new KeyValuePair<Team, Team>(team, x)) || allPossibleMatchups.Contains(new KeyValuePair<Team, Team>(x, team))));
                foreach (var matchup in newMatchups)
                {
                    allPossibleMatchups.Add(new KeyValuePair<Team, Team>(team, matchup));
                }
            }

            var allGamesForAllMatchups = allPossibleMatchups
                 .SelectMany(x => Tournament.Games.Where(x => x.Device != Device.TIMETRIAL)
                    .Select(y => new { matchUp = x, game = y, weight = CalculateWeight(x, y) }))
                    .ToList();

            foreach (var matchtoadd in allGamesForAllMatchups.OrderBy(x => x.weight))
            {
                //skip occupied game/device/team
                if (matchups.Any(x => x.IsTeamPlaying(matchtoadd.matchUp.Value.TeamName)
                    || x.IsTeamPlaying(matchtoadd.matchUp.Key.TeamName)
                    || x.Game.Device == matchtoadd.game.Device))
                {
                    continue;
                }

                matchups.Add(new Match
                {
                    Game = matchtoadd.game,
                    Team_1_Name = matchtoadd.matchUp.Key.TeamName,
                    Team_2_Name = matchtoadd.matchUp.Value.TeamName,
                    MatchId = matchId++
                });

                //if all teams have a matchup we no longer need to loop
                if (Tournament.Teams.All(x => matchups.Any(y => y.IsTeamPlaying(x.TeamName)))) 
                    break;
            }

            if (matchups.Any(x => x.Game == null && !x.IsTimeTrial) || Tournament.Teams.Any(x => !matchups.Any(y => y.IsTeamPlaying(x.TeamName))))
            {
                throw new Exception("ASSIGN GAMES LOGIC ERROR!!!");
            }

            round.Matches = matchups;
            Tournament.Rounds.Add(round);
            await _storageService.SaveTournament(Tournament);
            return round;
        }

        private static int CalculateWeight(KeyValuePair<Team, Team> x, Game y)
        {
            var weight = x.Value.NrTimesHavePlayedGame(y.Name) * 3; //nrTimes teams have played game
            weight += x.Key.NrTimesHavePlayedGame(y.Name) * 3; //nrTimes teams have played game
            weight += x.Value.NrTimesPlayedAgainstTeam(x.Key.TeamName); //nrTimes teams have met eachother
            weight += x.Value.HasCompetedWithTeamInGame(x.Key.TeamName, y.Name) ? 50 : 0; //nr times teams met eachother in game (high weight for it to basically never happend)
            return weight;
        }

        public Tournament GetTournament()
        {
            return Tournament;
        }

        private bool DeviceAndGameAvalible(List<Match> matches, Game game)
        {
            var deviceAndGameAvalible = true;
            if (game.Device == Device.TV)
            {
                if (game.Name == "Tetris Party Deluxe" || game.Name == "Mario Tennis")
                {
                    deviceAndGameAvalible = !matches.Any(x => (x.Game?.Name ?? string.Empty).Equals(game.Name)) && matches.Count(x => (x.Game?.Device ?? Device.Undefined) == game.Device) < 2;
                }
                else
                {
                    deviceAndGameAvalible = matches.Count(x => (x.Game?.Device ?? Device.Undefined) == game.Device) < 2;
                }
            }
            else
            {
                deviceAndGameAvalible = !matches.Any(x => (x.Game?.Device ?? Device.Undefined) == game.Device);
            }
            return deviceAndGameAvalible;
        }

        private int GetNextMatchId()
        {
            var matches = Tournament.Rounds.SelectMany(x => x.Matches);
            if (!matches.Any())
                return 1;
            return matches.Max(x => x.MatchId) + 1;
        }

        public async Task AddExtraPoints(string teamName, int points)
        {
            var team = Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
            team.ExtraPoints += points;
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task DeleteRound(int v)
        {
            var round = GetRound(v);
            Tournament.Rounds.Remove(round);
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task RemoveInactiveRounds()
        {
            Tournament.Rounds.RemoveAll(x => x.isStaging);
            await _storageService.SaveTournament(Tournament);
        }

        internal async Task GetAndSetHistory(string partitionKey, string rowKey)
        {
            var tournament = await _storageService.GetTournament(partitionKey, rowKey);
            if (tournament == null) return;
            await SetTournament(tournament);
        }
    }
}
