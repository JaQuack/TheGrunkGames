using System;
using System.Collections.Generic;
using System.Linq;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public class MatchmakingService
    {
        private static readonly Random _random = new();

        public List<Match> GenerateMatchups(Tournament tournament)
        {
            var teams = tournament.GetTeams();
            var games = tournament.Games;
            var matchId = tournament.Rounds.SelectMany(x => x.Matches).DefaultIfEmpty().Max(x => x?.MatchId ?? 0) + 1;

            var matchups = GenerateTimeTrialMatchups(tournament, teams, games, ref matchId);

            var availableTeams = teams.Where(t => !matchups.Any(m => m.IsTeamPlaying(t.TeamName))).ToList();
            var nonTimeTrialGames = games.Where(g => g.Device != Device.TIMETRIAL).ToList();

            if (availableTeams.Count >= 2 && nonTimeTrialGames.Count > 0)
            {
                var gamesPerDevice = nonTimeTrialGames.GroupBy(g => g.Device).ToDictionary(g => g.Key, g => g.Count());
                var rankedCandidates = GetAllPossiblePairings(availableTeams)
                    .SelectMany(pairing => nonTimeTrialGames
                        .Select(game => new { pairing, game, weight = CalculateWeight(pairing, game, gamesPerDevice) }))
                    .OrderBy(x => x.weight);

                matchups.AddRange(SelectBestMatchups(rankedCandidates.Select(c => (c.pairing, c.game)), availableTeams.Count, ref matchId));
            }

            if (matchups.Any(x => x.Game == null && !x.IsTimeTrial) ||
                teams.Any(x => !matchups.Any(y => y.IsTeamPlaying(x.TeamName))))
            {
                throw new InvalidOperationException("Failed to assign games to all teams.");
            }

            return matchups;
        }

        private static List<Match> GenerateTimeTrialMatchups(Tournament tournament, List<Team> teams, List<Game> games, ref int matchId)
        {
            var matchups = new List<Match>();
            if (!tournament.IsTimeTrial)
                return matchups;

            var timeTrialGame = games.FirstOrDefault(x => x.Device == Device.TIMETRIAL);
            if (timeTrialGame == null)
                return matchups;

            var assignedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tournament.NrTeamsToTimeTrial; i++)
            {
                var eligible = teams.Where(t => !assignedTeams.Contains(t.TeamName)).ToList();
                if (eligible.Count == 0)
                    break;

                var minTimeTrial = eligible.Min(t => t.TimeTrialsPlayed());
                var candidates = eligible.Where(t => t.TimeTrialsPlayed() == minTimeTrial).ToList();
                var selected = candidates[_random.Next(candidates.Count)];

                assignedTeams.Add(selected.TeamName);
                matchups.Add(new Match
                {
                    Team_1_Name = selected.TeamName,
                    IsTimeTrial = true,
                    Game = timeTrialGame,
                    MatchId = matchId++
                });
            }

            return matchups;
        }

        private static List<TeamPairing> GetAllPossiblePairings(List<Team> teams)
        {
            var pairings = new List<TeamPairing>();
            for (int i = 0; i < teams.Count; i++)
            {
                for (int j = i + 1; j < teams.Count; j++)
                {
                    pairings.Add(new TeamPairing(teams[i], teams[j]));
                }
            }
            return pairings;
        }

        private static int CalculateWeight(TeamPairing pairing, Game game, Dictionary<Device, int> gamesPerDevice)
        {
            var weight = pairing.TeamA.NrTimesHavePlayedGame(game.Name) * 3;
            weight += pairing.TeamB.NrTimesHavePlayedGame(game.Name) * 3;
            weight += pairing.TeamA.NrTimesPlayedAgainstTeam(pairing.TeamB.TeamName);
            weight += pairing.TeamB.NrTimesPlayedAgainstTeam(pairing.TeamA.TeamName);
            weight += pairing.TeamB.HasCompetedWithTeamInGame(pairing.TeamA.TeamName, game.Name) ? 50 : 0;
            weight += gamesPerDevice.GetValueOrDefault(game.Device, 1) - 1;
            return weight;
        }

        private static List<Match> SelectBestMatchups(IEnumerable<(TeamPairing pairing, Game game)> rankedCandidates, int teamCount, ref int matchId)
        {
            var selected = new List<Match>();
            var assignedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedDevices = new HashSet<Device>();

            foreach (var (pairing, game) in rankedCandidates)
            {
                if (assignedTeams.Contains(pairing.TeamA.TeamName) ||
                    assignedTeams.Contains(pairing.TeamB.TeamName) ||
                    usedDevices.Contains(game.Device))
                {
                    continue;
                }

                assignedTeams.Add(pairing.TeamA.TeamName);
                assignedTeams.Add(pairing.TeamB.TeamName);
                usedDevices.Add(game.Device);

                selected.Add(new Match
                {
                    Game = game,
                    Team_1_Name = pairing.TeamA.TeamName,
                    Team_2_Name = pairing.TeamB.TeamName,
                    MatchId = matchId++
                });

                if (assignedTeams.Count >= teamCount)
                    break;
            }

            return selected;
        }

        private readonly record struct TeamPairing(Team TeamA, Team TeamB);
    }
}