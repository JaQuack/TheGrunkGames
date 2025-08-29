using Xunit;
using TheGrunkGames.Models.TournamentModels;
using TheGrunkGames.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TheGrunkGames.Tests
{
    public class GameServiceTests
    {
        [Fact]
        public async Task GetNextRound_GeneratesExpectedMatchups()
        {
            // Arrange
            var storageService = new StorageService();
            var gameService = new GameService(storageService);

            // Setup tournament with 3 teams and 2 games
            var teams = new List<Team>
            {
                new Team { TeamName = "Alpha" },
                new Team { TeamName = "Bravo" },
                new Team { TeamName = "Charlie" }
            };
            var games = new List<Game>
            {
                new Game { Name = "GameA", Device = Device.LAP_Steam },
                new Game { Name = "GameB", Device = Device.TV },
                new Game { Name = "TimeTrial", Device = Device.TIMETRIAL }
            };
            var tournament = new Tournament
            {
                Games = games,
                Rounds = new List<Round>(),
                IsTimeTrial = true,
                NrTeamsToTimeTrial = 1
            };
            tournament.SetTeams(teams);
            await gameService.SetTournament(tournament);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                isStaging = false,
                Matches = [
                    new() { Game = games[2], HasCompleted = true, IsTimeTrial = true, MatchId = 1, Team_1_Name = teams[0].TeamName },
                    new() { Game = games[0], HasCompleted = true, IsTimeTrial = false, MatchId = 2, Team_1_Name = teams[1].TeamName, Team_2_Name = teams[2].TeamName, ScoreTeam1 = 3 }
                    ]
            });
            // Act
            var round = await gameService.GetNextRound();

            // Assert
            Assert.NotNull(round);
            Assert.Equal(2, round.Matches.Count); // 1 time trial + 1 normal match
            Assert.Contains(round.Matches, m => m.IsTimeTrial);
            Assert.Contains(round.Matches, m => !m.IsTimeTrial);
            // You can add more asserts to check correct teams/games assigned
        }

        [Fact]
        public async Task GetNextRound_GeneratesExpectedMatchup_UnevenTeamsNoTimeTrial()
        {
            // Arrange
            var storageService = new StorageService();
            var gameService = new GameService(storageService);

            // Setup tournament with 3 teams and 2 games
            var teams = new List<Team>
            {
                new Team { TeamName = "Alpha" },
                new Team { TeamName = "Bravo" },
                new Team { TeamName = "Charlie" }
            };
            var games = new List<Game>
            {
                new Game { Name = "GameA", Device = Device.LAP_Steam },
                new Game { Name = "GameB", Device = Device.TV },
                new Game { Name = "TimeTrial", Device = Device.TIMETRIAL }
            };
            var tournament = new Tournament
            {
                Games = games,
                Rounds = new List<Round>(),
            };
            tournament.SetTeams(teams);
            await gameService.SetTournament(tournament);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                isStaging = false,
                Matches = [
                    new() { Game = games[2], HasCompleted = true, IsTimeTrial = true, MatchId = 1, Team_1_Name = teams[0].TeamName },
                    new() { Game = games[0], HasCompleted = true, IsTimeTrial = false, MatchId = 2, Team_1_Name = teams[1].TeamName, Team_2_Name = teams[2].TeamName, ScoreTeam1 = 3 }
                    ]
            });
            // Act
            var round = gameService.GetNextRound();

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await round);
            // You can add more asserts to check correct teams/games assigned
        }
    }
}
