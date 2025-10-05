using Xunit;
using TheGrunkGames.Models.TournamentModels;
using TheGrunkGames.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace TheGrunkGames.Tests
{
    public class StorageServiceTests
    {
        private static List<Team> CreateTeams(params string[] names) =>
            names.Select(n => new Team { TeamName = n }).ToList();

        [Fact]
        public async Task SaveTournament_PersistsDataInMemory()
        {
            var storageService = new StorageService();
            var tournament = new Tournament
            {
                Games = [new() { Name = "G", Device = Device.TV }],
                Rounds = [new() { RoundId = 1, Matches = [new() { MatchId = 1, Game = new() { Name = "G", Device = Device.TV }, Team_1_Name = "A", Team_2_Name = "B" }] }]
            };
            tournament.SetTeams(CreateTeams("A", "B"));

            await storageService.SaveTournament(tournament);

            var loaded = await storageService.GetTournament();
            Assert.Single(loaded.Rounds);
        }

        [Fact]
        public async Task GetTournament_ReturnsNewTournament_WhenNothingSaved()
        {
            var storageService = new StorageService();

            var tournament = await storageService.GetTournament();

            Assert.NotNull(tournament);
            Assert.Empty(tournament.Teams);
            Assert.Empty(tournament.Games);
            Assert.Empty(tournament.Rounds);
        }

        [Fact]
        public async Task GetTournament_ReturnsCachedData_AfterSave()
        {
            var storageService = new StorageService();
            var original = new Tournament
            {
                Games = [new() { Name = "G", Device = Device.TV }],
                Rounds = []
            };
            original.SetTeams(CreateTeams("Alpha", "Bravo"));

            await storageService.SaveTournament(original);
            var loaded = await storageService.GetTournament();

            Assert.Equal(2, loaded.Teams.Count);
            Assert.Contains(loaded.Teams, t => t.TeamName == "Alpha");
        }
    }
}
