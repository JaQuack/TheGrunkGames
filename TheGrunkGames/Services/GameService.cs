using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames2.Objects;

namespace TheGrunkGames2.Services
{
    public class GameService
    {
        private readonly ILogger<GameService> _logger;

        private Tournament Tournament;
        private static Random _random = new Random();

        public GameService(ILogger<GameService> logger)
        {
            _logger = logger;

            var games = new List<Game>
            {
                new Game { Name = "Age of Empires 2", Device = Device.PC_Steam },
                new Game { Name = "NFS: Most Wanted", Device = Device.PC_Steam},
                new Game { Name = "Dark Messiah of Might and Magic", Device = Device.PC_Steam_2 },
                new Game { Name = "Half life 1 vs", Device = Device.PC_Steam_2 },
                new Game { Name = "Audiosurf", Device = Device.PC_Couch },
                new Game { Name = "Doom 2", Device = Device.PC_Couch },
                new Game { Name = "Fifa 98", Device = Device.TV },
                new Game { Name = "Mario Kart 64", Device = Device.TV },
                new Game { Name = "Tetris Party Deluxe", Device = Device.TV_Wii },
                new Game { Name = "Nintendo Land", Device = Device.TV_Wii },
                new Game { Name = "Beerpong", Device = Device.IRL },
                new Game { Name = "TIMETRIAL", Device = Device.TIMETRIAL}
            };

            var teams = new List<Team>()
            {
                new Team {
                    TeamName = "Untitled Goose Team",
                    Players = new List<Player> {
                        new Player { Name = "Micke" },
                        new Player { Name = "Norrland",}
                    },
                },
                new Team {
                    TeamName = "One Dying Crew",
                    Players = new List<Player> {
                        new Player { Name = "Player3", },
                        new Player { Name = "Player4"  }
                    },
                },
                new Team {
                    TeamName = "Asspat",
                    Players = new List<Player> {
                        new Player { Name = "Player5" },
                        new Player { Name = "Player6" }
                    },
                },
                new Team {
                    TeamName = "Gubbstön",
                    Players = new List<Player> {
                        new Player { Name = "Player7" },
                        new Player { Name = "Player8" }
                    },
                },
                 new Team {
                    TeamName = "The Nippon Clamps",
                    Players = new List<Player> {
                        new Player { Name = "Player9" },
                        new Player { Name = "Player10" }
                    },
                },
                new Team {
                    TeamName = "Skinkryttarna",
                    Players = new List<Player> {
                        new Player { Name = "Player11" },
                        new Player { Name = "Player12" }
                    },
                }
            };

            Tournament = new Tournament { Teams = teams, Games = games, Rounds = new List<Round>() };
        }

        internal Match GetMatch(int matchId)
        {
            return GetActiveRounds().SelectMany(x => x.Matches).FirstOrDefault(y => y.MatchId == matchId);
        }

        private List<Round> GetActiveRounds()
        { 
            return Tournament.Rounds.Where(x => x.RoundId != 0 && !x.isStaging).ToList();
        }


        internal void CompleteMatch(MatchResult result)
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
        }

        private Team GetTeamByName(string name)
        {
            return Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        internal void SetRound(Round round)
        {
            var existingRound = GetRound(round.RoundId);
            Tournament.Rounds.Remove(existingRound);
            Tournament.Rounds.Add(round);
        }

        internal bool ChangeGameForMatch(int matchId, string gameName)
        {
            var match = GetMatch(matchId);
            var game = Tournament.Games.FirstOrDefault(x => x.Name.Equals(gameName, StringComparison.InvariantCultureIgnoreCase));
            if (match == null || game == null)
                return false;
            match.Game = game;
            return false;
        }

        internal void ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            var match = GetMatch(matchId);
            var team1 = GetTeamByName(team1Name);
            var team2 = GetTeamByName(team2Name);
            if (team1 == null || team2 == null)
                throw new Exception("You fuckedup the teamnames STUPID!");
            match.Team_1_Name = team1.TeamName;
            match.Team_2_Name = team2.TeamName;
        }

        internal Round GetRound(int roundId)
        {
            return Tournament.Rounds.FirstOrDefault(x => x.RoundId == roundId);
        }

        internal void AddGame(Game game)
        {
            Tournament.Games.Add(game);
        }

        internal void SetGames(List<Game> games)
        {
            Tournament.Games = games;
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

        internal List<RoundStats> GetRoundStats()
        {
            var rounds = GetActiveRounds();
            var roundStats = new List<RoundStats>();
            foreach (var round in rounds)
            {
                var roundstat = new RoundStats
                {
                    Matches = new List<MatchStat>(),
                    RoundId = round.RoundId
                };

                foreach (var match in round.Matches)
                {
                    roundstat.Matches.Add(new MatchStat { Id = match.MatchId, Stats = match.Stat });
                }
                roundStats.Add(roundstat);
            }
            return roundStats;
        }

        internal bool TeamExists(string teamName)
        {
            return Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase)) != null;
        }

        internal void AddTeam(Team team)
        {
            Tournament.Teams.Add(team);
        }

        public void SetTeams(List<Team> teams)
        {
            Tournament.Teams = teams;
        }

        internal void SetTournament(Tournament newTornament)
        {
            Tournament = newTornament;
        }

        public Round GetCurrentRound()
        {
            if (GetActiveRounds().FirstOrDefault(x => !x.IsCompleted() && !x.isStaging) == null)
            {
                GetNextRound();
            }
            return Tournament.Rounds.OrderBy(y => y.RoundId).FirstOrDefault(x => !x.IsCompleted() && !x.isStaging);
        }

        public Round GetNextRound()
        {
            var teams = Tournament.Teams;

            teams = teams.OrderByDescending(x => x.CurrentScore).ToList();
            var round = new Round
            {
                RoundId = GetActiveRounds().Count == 0 ? 1 : GetActiveRounds().Max(x => x.RoundId) + 1
            };
            var matchups = new List<Match>();
            var matchId = GetNextMatchId();
            if (teams.Count % 2 != 0)
            {
                var minTimeTrial = teams.Min(x => x.TimeTrialsPlayed());
                var teamsToChooseFrom = teams.Where(x => x.TimeTrialsPlayed() == minTimeTrial);
                var teamToPlayTimeTrial = teamsToChooseFrom.ElementAt(_random.Next(teamsToChooseFrom.Count()));

                matchups.Add(new Match
                {
                    Team_1_Name = teamToPlayTimeTrial.TeamName,
                    IsTimeTrial = true,
                    MatchId = matchId++
                });
            }

            foreach (var team in teams.OrderBy(x => Guid.NewGuid()))
            {
                if (matchups.Any(y => y.IsTeamPlaying(team.TeamName)))
                    continue;
                var teamsNotYetMatched = teams.Where(x => !matchups.Any(y => y.IsTeamPlaying(x.TeamName)) && !x.TeamName.Equals(team.TeamName)).ToList();

                var minGamesPlayedAgainstSame = team.GetMinPlaysAgainstTeams(teamsNotYetMatched);
                var teamsToChooseFrom = teamsNotYetMatched.Where(x => x.GetMinPlaysAgainstTeam(team.TeamName) == minGamesPlayedAgainstSame).ToList();
                var opponent = (teamsToChooseFrom.FirstOrDefault() ?? teamsNotYetMatched.FirstOrDefault()) ?? throw new Exception("Unable to find opponent!");
                matchups.Add(new Match
                {
                    Team_1_Name = team.TeamName,
                    Team_2_Name = opponent.TeamName,
                    MatchId = matchId++
                });
            }

            AssignGames(matchups);
            round.Matches = matchups;
            Tournament.Rounds.Add(round);
            return round;
        }

        private void AssignGames(List<Match> matches)
        {
            var games = Tournament.Games.Where(x => x.Device != Device.TIMETRIAL);
            foreach (var matchup in matches)
            {
                matchup.Stat = new Dictionary<string, string>();
                if (matchup.IsTimeTrial)
                {
                    matchup.Game = Tournament.Games.FirstOrDefault(x => x.Device == Device.TIMETRIAL);
                    continue;
                }
                else
                {
                    var team1 = GetTeamByName(matchup.Team_1_Name);
                    var team2 = GetTeamByName(matchup.Team_2_Name);
                    var gamesAvalible = games.Where(x => DeviceAndGameAvalible(matches, x)).OrderBy(x => Guid.NewGuid()).ToList();
                    matchup.Stat.Add(nameof(gamesAvalible) + "_1", JsonConvert.SerializeObject(gamesAvalible));
                    gamesAvalible = gamesAvalible.Where(x => !team1.HasCompetedWithTeamInGame(team2.TeamName, x.Name)).ToList();
                    matchup.Stat.Add(nameof(gamesAvalible) + "_2", JsonConvert.SerializeObject(gamesAvalible));
                    gamesAvalible = gamesAvalible.OrderBy(x => team1.NrTimesHavePlayedGame(x.Name) + team2.NrTimesHavePlayedGame(x.Name)).ToList();
                    matchup.Stat.Add(nameof(gamesAvalible) + "_3", JsonConvert.SerializeObject(gamesAvalible));

                    matchup.Game = gamesAvalible.FirstOrDefault();
                }
            }
            if (matches.Any(x => x.Game == null && !x.IsTimeTrial))
            {
                throw new Exception("ASSIGN GAMES LOGIC ERROR!!!");
            }
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
                if (game.Name == "Tetris Party Deluxe" || game.Name == "Nintendo Land")
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

        public void AddExtraPoints(string teamName, int points)
        {
            var team = Tournament.Teams.FirstOrDefault(x => x.TeamName.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
            team.ExtraPoints += points;
        }

        internal void DeleteRound(int v)
        {
            var round = GetRound(v);
            Tournament.Rounds.Remove(round);
        }

        internal void RemoveInactiveRounds()
        {
            Tournament.Rounds.RemoveAll(x => x.isStaging);
        }
    }
}
