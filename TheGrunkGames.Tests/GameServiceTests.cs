using Xunit;
using TheGrunkGames.Models.TournamentModels;
using TheGrunkGames.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TheGrunkGames.Hubs;

namespace TheGrunkGames.Tests
{
    public class GameServiceTests
    {
        private static async Task<GameService> CreateGameServiceAsync()
        {
            var storageService = new StorageService();
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, matchmakingService, archiveService);
            await gameService.InitializeAsync();
            return gameService;
        }

        private static List<Team> CreateTeams(params string[] names) =>
            names.Select(n => new Team { TeamName = n }).ToList();

        private static List<Game> CreateStandardGames() =>
        [
            new() { Name = "GameA", Device = Device.LAP_Steam },
            new() { Name = "GameB", Device = Device.TV },
            new() { Name = "TimeTrial", Device = Device.TIMETRIAL }
        ];

        private static async Task<(GameService service, Tournament tournament)> CreateServiceWithTournamentAsync(
            List<Team> teams, List<Game> games, bool isTimeTrial = false, int nrTeamsToTimeTrial = 0)
        {
            var gameService = await CreateGameServiceAsync();
            var tournament = new Tournament
            {
                Games = games,
                Rounds = [],
                IsTimeTrial = isTimeTrial,
                NrTeamsToTimeTrial = nrTeamsToTimeTrial
            };
            tournament.SetTeams(teams);
            await gameService.SetTournament(tournament);
            return (gameService, tournament);
        }

        #region Team Management

        [Fact]
        public async Task AddTeam_AddsTeamToTournament()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            await service.AddTeam(new Team { TeamName = "Charlie" });

            var tournament = await service.GetTournament();
            Assert.Equal(3, tournament.Teams.Count);
            Assert.Contains(tournament.Teams, t => t.TeamName == "Charlie");
        }

        [Fact]
        public async Task SetTeams_ReplacesAllTeams()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            var newTeams = CreateTeams("X", "Y", "Z");
            await service.SetTeams(newTeams);

            var tournament = await service.GetTournament();
            Assert.Equal(3, tournament.Teams.Count);
            Assert.DoesNotContain(tournament.Teams, t => t.TeamName == "Alpha");
            Assert.Contains(tournament.Teams, t => t.TeamName == "X");
        }

        [Fact]
        public async Task TeamExists_ReturnsTrueForExistingTeam()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            Assert.True(await service.TeamExists("Alpha"));
        }

        [Fact]
        public async Task TeamExists_ReturnsFalseForNonExistentTeam()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            Assert.False(await service.TeamExists("Ghost"));
        }

        [Fact]
        public async Task TeamExists_IsCaseInsensitive()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            Assert.True(await service.TeamExists("alpha"));
            Assert.True(await service.TeamExists("ALPHA"));
        }

        #endregion

        #region Game Management

        [Fact]
        public async Task AddGame_AddsGameToTournament()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            await service.AddGame(new Game { Name = "NewGame", Device = Device.PC });

            var tournament = await service.GetTournament();
            Assert.Equal(4, tournament.Games.Count);
            Assert.Contains(tournament.Games, g => g.Name == "NewGame");
        }

        [Fact]
        public async Task SetGames_ReplacesAllGames()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var newGames = new List<Game> { new() { Name = "Only", Device = Device.IRL } };
            await service.SetGames(newGames);

            var tournament = await service.GetTournament();
            Assert.Single(tournament.Games);
            Assert.Equal("Only", tournament.Games[0].Name);
        }

        #endregion

        #region Round Management

        [Fact]
        public async Task GetCurrentRound_ReturnsLatestNonCompletedNonStagingRound()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                Matches = [new() { MatchId = 2, Game = games[1], HasCompleted = false, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var current = await service.GetCurrentRound();

            Assert.NotNull(current);
            Assert.Equal(2, current.RoundId);
        }

        [Fact]
        public async Task GetCurrentRound_SkipsStagingRounds()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = false, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                isStaging = true,
                Matches = [new() { MatchId = 2, Game = games[1], HasCompleted = false, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var current = await service.GetCurrentRound();

            Assert.NotNull(current);
            Assert.Equal(1, current.RoundId);
        }

        [Fact]
        public async Task GetCurrentRound_ReturnsNullWhenAllCompleted()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var current = await service.GetCurrentRound();

            Assert.Null(current);
        }

        [Fact]
        public async Task GetRound_ReturnsCorrectRound()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 5,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var round = await service.GetRound(5);

            Assert.NotNull(round);
            Assert.Equal(5, round.RoundId);
        }

        [Fact]
        public async Task GetRound_ReturnsNullForNonExistentId()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var round = await service.GetRound(999);

            Assert.Null(round);
        }

        [Fact]
        public async Task SetRound_ReplacesExistingRound()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var replacement = new Round
            {
                RoundId = 1,
                Matches = [
                    new() { MatchId = 10, Game = games[1], Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                    new() { MatchId = 11, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }
                ]
            };
            await service.SetRound(replacement);

            var round = await service.GetRound(1);
            Assert.NotNull(round);
            Assert.Equal(2, round.Matches.Count);
            Assert.Contains(round.Matches, m => m.MatchId == 10);
        }

        [Fact]
        public async Task DeleteRound_RemovesRoundFromTournament()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                Matches = [new() { MatchId = 2, Game = games[1], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            await service.DeleteRound(1);

            var t = await service.GetTournament();
            Assert.Single(t.Rounds);
            Assert.Equal(2, t.Rounds[0].RoundId);
        }

        [Fact]
        public async Task RemoveInactiveRounds_RemovesOnlyStagingRounds()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                isStaging = true,
                Matches = [new() { MatchId = 2, Game = games[1], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 3,
                isStaging = true,
                Matches = [new() { MatchId = 3, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            await service.RemoveInactiveRounds();

            var t = await service.GetTournament();
            Assert.Single(t.Rounds);
            Assert.Equal(1, t.Rounds[0].RoundId);
        }

        #endregion

        #region Match Management

        [Fact]
        public async Task GetMatch_ReturnsMatchFromActiveRound()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 42, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var match = await service.GetMatch(42);

            Assert.NotNull(match);
            Assert.Equal(42, match.MatchId);
        }

        [Fact]
        public async Task GetMatch_ReturnsNullForNonExistentId()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var match = await service.GetMatch(999);

            Assert.Null(match);
        }

        [Fact]
        public async Task GetMatch_IgnoresStagingRounds()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                isStaging = true,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var match = await service.GetMatch(1);

            Assert.Null(match);
        }

        [Fact]
        public async Task CompleteMatch_SetsScoresAndCompletionFlag()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            await service.CompleteMatch(new MatchResult { MatchId = 1, Team1Score = 3, Team2Score = 1 });

            var match = await service.GetMatch(1);
            Assert.NotNull(match);
            Assert.True(match.HasCompleted);
            Assert.Equal(3, match.ScoreTeam1);
            Assert.Equal(1, match.ScoreTeam2);
        }

        [Fact]
        public async Task ChangeGameForMatch_UpdatesGameSuccessfully()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var result = await service.ChangeGameForMatch(1, "GameB");

            Assert.True(result);
            var match = await service.GetMatch(1);
            Assert.NotNull(match);
            Assert.Equal("GameB", match.Game.Name);
        }

        [Fact]
        public async Task ChangeGameForMatch_ReturnsFalseForInvalidMatchId()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var result = await service.ChangeGameForMatch(999, "GameA");

            Assert.False(result);
        }

        [Fact]
        public async Task ChangeGameForMatch_ReturnsFalseForInvalidGameName()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var result = await service.ChangeGameForMatch(1, "NonExistentGame");

            Assert.False(result);
        }

        [Fact]
        public async Task ChangeGameForMatch_IsCaseInsensitiveOnGameName()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var result = await service.ChangeGameForMatch(1, "gameb");

            Assert.True(result);
            var match = await service.GetMatch(1);
            Assert.NotNull(match);
            Assert.Equal("GameB", match.Game.Name);
        }

        [Fact]
        public async Task ChangeTeamsForMatch_SwapsTeams()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            await service.ChangeTeamsForMatch(1, "Charlie", "Alpha");

            var match = await service.GetMatch(1);
            Assert.NotNull(match);
            Assert.Equal("Charlie", match.Team_1_Name);
            Assert.Equal("Alpha", match.Team_2_Name);
        }

        [Fact]
        public async Task ChangeTeamsForMatch_ThrowsForInvalidTeamName()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ChangeTeamsForMatch(1, "Alpha", "Ghost"));
        }

        #endregion

        #region Stats & Standings

        [Fact]
        public async Task GetTeamStandings_ReturnsTeamsOrderedByScoreDescending()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo", ScoreTeam1 = 5, ScoreTeam2 = 2 },
                    new() { MatchId = 2, Game = games[1], HasCompleted = true, Team_1_Name = "Charlie", Team_2_Name = "Alpha", ScoreTeam1 = 10, ScoreTeam2 = 0 }
                ]
            });
            await service.SetTournament(tournament);

            var standings = await service.GetTeamStandings();

            Assert.Equal(3, standings.Count);
            Assert.Equal("Charlie", standings[0].TeamName);
            Assert.Equal(10, standings[0].TeamScore);
            Assert.Equal("Alpha", standings[1].TeamName);
            Assert.Equal(5, standings[1].TeamScore); // 5 as Team1 in match 1 + 0 as Team2 in match 2
            Assert.Equal("Bravo", standings[2].TeamName);
            Assert.Equal(2, standings[2].TeamScore);
        }

        [Fact]
        public async Task AddExtraPoints_IncreasesTeamScore()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo", ScoreTeam1 = 3, ScoreTeam2 = 1 }]
            });
            await service.SetTournament(tournament);

            await service.AddExtraPoints("Bravo", 10);

            var standings = await service.GetTeamStandings();
            var bravo = standings.First(s => s.TeamName == "Bravo");
            Assert.Equal(11, bravo.TeamScore); // 1 match score + 10 extra
        }

        [Fact]
        public async Task AddExtraPoints_DoesNothingForNonExistentTeam()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            await service.AddExtraPoints("Ghost", 10);

            var standings = await service.GetTeamStandings();
            Assert.Single(standings);
            Assert.Equal(0, standings[0].TeamScore);
        }

        [Fact]
        public async Task AddExtraPoints_IsCaseInsensitive()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            await service.AddExtraPoints("alpha", 7);

            var standings = await service.GetTeamStandings();
            Assert.Equal(7, standings[0].TeamScore);
        }

        [Fact]
        public async Task GetTeamStats_ReturnsOpponentsAndGamesPlayed()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                    new() { MatchId = 2, Game = games[1], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Charlie" }
                ]
            });
            await service.SetTournament(tournament);

            var stats = await service.GetTeamStats();

            var alphaStats = stats.First(s => s.TeamName == "Alpha");
            Assert.Equal(2, alphaStats.PlayedAgainstTeam.Count);
            Assert.Equal(2, alphaStats.PlayedGames.Count);
        }

        [Fact]
        public async Task GetTeamStats_ReturnsEmptyLists_WhenNoMatchesPlayed()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            var stats = await service.GetTeamStats();

            Assert.Equal(2, stats.Count);
            Assert.All(stats, s =>
            {
                Assert.Empty(s.PlayedAgainstTeam);
                Assert.Empty(s.PlayedGames);
            });
        }

        [Fact]
        public async Task GetTeamStats_CountsMultipleMatchesAgainstSameOpponent()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                Matches = [new() { MatchId = 2, Game = games[1], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var stats = await service.GetTeamStats();

            var alphaStats = stats.First(s => s.TeamName == "Alpha");
            var bravoEntry = alphaStats.PlayedAgainstTeam.First(x => x.Name == "Bravo");
            Assert.Equal(2, bravoEntry.TimesPlayed);
        }

        [Fact]
        public async Task GetTeamStats_OrdersByMostPlayedDescending()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                ]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 2,
                Matches =
                [
                    new() { MatchId = 2, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                ]
            });
            tournament.Rounds.Add(new Round
            {
                RoundId = 3,
                Matches =
                [
                    new() { MatchId = 3, Game = games[1], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Charlie" },
                ]
            });
            await service.SetTournament(tournament);

            var stats = await service.GetTeamStats();

            var alphaStats = stats.First(s => s.TeamName == "Alpha");
            Assert.Equal("Bravo", alphaStats.PlayedAgainstTeam[0].Name);
            Assert.Equal(2, alphaStats.PlayedAgainstTeam[0].TimesPlayed);
            Assert.Equal("Charlie", alphaStats.PlayedAgainstTeam[1].Name);
            Assert.Equal(1, alphaStats.PlayedAgainstTeam[1].TimesPlayed);

            Assert.Equal("GameA", alphaStats.PlayedGames[0].Name);
            Assert.Equal(2, alphaStats.PlayedGames[0].TimesPlayed);
            Assert.Equal("GameB", alphaStats.PlayedGames[1].Name);
            Assert.Equal(1, alphaStats.PlayedGames[1].TimesPlayed);
        }

        #endregion

        #region Round Generation

        [Fact]
        public async Task GetNextRound_GeneratesExpectedMatchups()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie");
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                teams, games, isTimeTrial: true, nrTeamsToTimeTrial: 1);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                isStaging = false,
                Matches =
                [
                    new() { Game = games[2], HasCompleted = true, IsTimeTrial = true, MatchId = 1, Team_1_Name = "Alpha" },
                    new() { Game = games[0], HasCompleted = true, IsTimeTrial = false, MatchId = 2, Team_1_Name = "Bravo", Team_2_Name = "Charlie", ScoreTeam1 = 3 }
                ]
            });

            var round = await service.GetNextRound();

            Assert.NotNull(round);
            Assert.Equal(2, round.Matches.Count);
            Assert.Contains(round.Matches, m => m.IsTimeTrial);
            Assert.Contains(round.Matches, m => !m.IsTimeTrial);
        }

        [Fact]
        public async Task GetNextRound_GeneratesExpectedMatchup_UnevenTeamsNoTimeTrial()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie");
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(teams, games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                isStaging = false,
                Matches =
                [
                    new() { Game = games[2], HasCompleted = true, IsTimeTrial = true, MatchId = 1, Team_1_Name = "Alpha" },
                    new() { Game = games[0], HasCompleted = true, IsTimeTrial = false, MatchId = 2, Team_1_Name = "Bravo", Team_2_Name = "Charlie", ScoreTeam1 = 3 }
                ]
            });

            var round = service.GetNextRound();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await round);
        }

        [Fact]
        public async Task GetNextRound_FirstRoundHasRoundIdOne()
        {
            var teams = CreateTeams("Alpha", "Bravo");
            var games = new List<Game> { new() { Name = "GameA", Device = Device.LAP_Steam } };
            var (service, _) = await CreateServiceWithTournamentAsync(teams, games);

            var round = await service.GetNextRound();

            Assert.Equal(1, round.RoundId);
        }

        [Fact]
        public async Task GetNextRound_IncrementsRoundId()
        {
            var teams = CreateTeams("Alpha", "Bravo");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV }
            };
            var (service, tournament) = await CreateServiceWithTournamentAsync(teams, games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });

            var round = await service.GetNextRound();

            Assert.Equal(2, round.RoundId);
        }

        [Fact]
        public async Task GetNextRound_AssignsUniqueDevicesPerMatch()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie", "Delta");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV },
            };
            var (service, _) = await CreateServiceWithTournamentAsync(teams, games);

            var round = await service.GetNextRound();

            var devices = round.Matches.Where(m => !m.IsTimeTrial).Select(m => m.Game.Device).ToList();
            Assert.Equal(devices.Distinct().Count(), devices.Count);
        }

        [Fact]
        public async Task GetNextRound_AllTeamsAreAssigned()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie", "Delta");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV },
            };
            var (service, _) = await CreateServiceWithTournamentAsync(teams, games);

            var round = await service.GetNextRound();

            var playingTeams = round.Matches
                .SelectMany(m => new[] { m.Team_1_Name, m.Team_2_Name })
                .Where(n => n != null)
                .Distinct()
                .ToList();
            Assert.Equal(4, playingTeams.Count);
        }

        [Fact]
        public async Task GetNextRound_MatchIdsAreUnique()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie", "Delta");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV },
            };
            var (service, _) = await CreateServiceWithTournamentAsync(teams, games);

            var round = await service.GetNextRound();

            var matchIds = round.Matches.Select(m => m.MatchId).ToList();
            Assert.Equal(matchIds.Distinct().Count(), matchIds.Count);
        }

        [Fact]
        public async Task GetNextRound_MatchIdsIncrementAcrossRounds()
        {
            var teams = CreateTeams("Alpha", "Bravo");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV }
            };
            var (service, tournament) = await CreateServiceWithTournamentAsync(teams, games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 5, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });

            var round = await service.GetNextRound();

            Assert.All(round.Matches, m => Assert.True(m.MatchId > 5));
        }

        [Fact]
        public async Task GetNextRound_WeightsPreferNewGamesForTeams()
        {
            var teams = CreateTeams("Alpha", "Bravo", "Charlie", "Delta");
            var games = new List<Game>
            {
                new() { Name = "GameA", Device = Device.LAP_Steam },
                new() { Name = "GameB", Device = Device.TV },
            };
            var (service, tournament) = await CreateServiceWithTournamentAsync(teams, games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, Game = games[0], HasCompleted = true, Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                    new() { MatchId = 2, Game = games[1], HasCompleted = true, Team_1_Name = "Charlie", Team_2_Name = "Delta" }
                ]
            });

            var round = await service.GetNextRound();

            var normalMatches = round.Matches.Where(m => !m.IsTimeTrial).ToList();
            Assert.Equal(2, normalMatches.Count);

            // Alpha played GameA in round 1 — weighting should prefer GameB for Alpha in round 2
            var alphaMatch = normalMatches.First(m => m.IsTeamPlaying("Alpha"));
            Assert.Equal("GameB", alphaMatch.Game.Name);
        }

        [Fact]
        public async Task GetNextRound_RoundIsAddedToTournament()
        {
            var teams = CreateTeams("Alpha", "Bravo");
            var games = new List<Game> { new() { Name = "GameA", Device = Device.LAP_Steam } };
            var (service, _) = await CreateServiceWithTournamentAsync(teams, games);

            var round = await service.GetNextRound();

            var tournament = await service.GetTournament();
            Assert.Contains(tournament.Rounds, r => r.RoundId == round.RoundId);
        }

        #endregion

        #region Concurrency

        [Fact]
        public async Task CompleteMatch_Concurrent_BothMatchesCompleted()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie", "Delta"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" },
                    new() { MatchId = 2, Game = games[1], Team_1_Name = "Charlie", Team_2_Name = "Delta" }
                ]
            });
            await service.SetTournament(tournament);

            await Task.WhenAll(
                service.CompleteMatch(new MatchResult { MatchId = 1, Team1Score = 5, Team2Score = 3 }),
                service.CompleteMatch(new MatchResult { MatchId = 2, Team1Score = 2, Team2Score = 7 })
            );

            var result = await service.GetTournament();
            var match1 = result.Rounds[0].Matches.First(m => m.MatchId == 1);
            var match2 = result.Rounds[0].Matches.First(m => m.MatchId == 2);
            Assert.True(match1.HasCompleted);
            Assert.Equal(5, match1.ScoreTeam1);
            Assert.True(match2.HasCompleted);
            Assert.Equal(7, match2.ScoreTeam2);
        }

        [Fact]
        public async Task AddTeam_Concurrent_BothTeamsAdded()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            await Task.WhenAll(
                service.AddTeam(new Team { TeamName = "Bravo" }),
                service.AddTeam(new Team { TeamName = "Charlie" })
            );

            var tournament = await service.GetTournament();
            Assert.Equal(3, tournament.Teams.Count);
            Assert.Contains(tournament.Teams, t => t.TeamName == "Bravo");
            Assert.Contains(tournament.Teams, t => t.TeamName == "Charlie");
        }

        [Fact]
        public async Task AddExtraPoints_Concurrent_BothApplied()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            await Task.WhenAll(
                service.AddExtraPoints("Alpha", 10),
                service.AddExtraPoints("Bravo", 20)
            );

            var standings = await service.GetTeamStandings();
            Assert.Equal(20, standings.First(s => s.TeamName == "Bravo").TeamScore);
            Assert.Equal(10, standings.First(s => s.TeamName == "Alpha").TeamScore);
        }

        #endregion

        #region Remove Team

        [Fact]
        public async Task RemoveTeam_RemovesTeamFromTournament()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo", "Charlie"), CreateStandardGames());

            var removed = await service.RemoveTeam("Bravo");

            Assert.True(removed);
            var tournament = await service.GetTournament();
            Assert.Equal(2, tournament.Teams.Count);
            Assert.DoesNotContain(tournament.Teams, t => t.TeamName == "Bravo");
        }

        [Fact]
        public async Task RemoveTeam_ReturnsFalse_WhenTeamNotFound()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var removed = await service.RemoveTeam("NonExistent");

            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveTeam_IsCaseInsensitive()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            var removed = await service.RemoveTeam("BRAVO");

            Assert.True(removed);
            var tournament = await service.GetTournament();
            Assert.Single(tournament.Teams);
        }

        [Fact]
        public async Task RemoveTeam_ReturnsFalse_WhenTeamInActiveMatch()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var removed = await service.RemoveTeam("Alpha");

            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveTeam_AllowsRemoval_WhenTeamOnlyInCompletedMatches()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo", HasCompleted = true, ScoreTeam1 = 3, ScoreTeam2 = 1 }]
            });
            await service.SetTournament(tournament);

            var removed = await service.RemoveTeam("Alpha");

            Assert.True(removed);
        }

        #endregion

        #region Remove Game

        [Fact]
        public async Task RemoveGame_RemovesGameFromTournament()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            var removed = await service.RemoveGame("GameA");

            Assert.True(removed);
            var tournament = await service.GetTournament();
            Assert.DoesNotContain(tournament.Games, g => g.Name == "GameA");
        }

        [Fact]
        public async Task RemoveGame_ReturnsFalse_WhenGameNotFound()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha"), CreateStandardGames());

            var removed = await service.RemoveGame("NonExistent");

            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveGame_IsCaseInsensitive()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            var removed = await service.RemoveGame("gamea");

            Assert.True(removed);
        }

        [Fact]
        public async Task RemoveGame_ReturnsFalse_WhenGameInActiveMatch()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo" }]
            });
            await service.SetTournament(tournament);

            var removed = await service.RemoveGame("GameA");

            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveGame_AllowsRemoval_WhenGameOnlyInCompletedMatches()
        {
            var games = CreateStandardGames();
            var (service, tournament) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            tournament.Rounds.Add(new Round
            {
                RoundId = 1,
                Matches = [new() { MatchId = 1, Game = games[0], Team_1_Name = "Alpha", Team_2_Name = "Bravo", HasCompleted = true, ScoreTeam1 = 3, ScoreTeam2 = 1 }]
            });
            await service.SetTournament(tournament);

            var removed = await service.RemoveGame("GameA");

            Assert.True(removed);
        }

        #endregion

        #region Reset Tournament

        [Fact]
        public async Task ResetTournament_ClearsAllRounds()
        {
            var games = CreateStandardGames();
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), games);

            await service.GetNextRound();
            await service.GetNextRound();

            await service.ResetTournament();

            var tournament = await service.GetTournament();
            Assert.Empty(tournament.Rounds);
        }

        [Fact]
        public async Task ResetTournament_ResetsToDefaultTeams()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("CustomTeamA", "CustomTeamB"), CreateStandardGames());

            await service.ResetTournament();

            var tournament = await service.GetTournament();
            Assert.Equal(7, tournament.Teams.Count);
            Assert.Contains(tournament.Teams, t => t.TeamName == "Scourge of the Goat Sea");
        }

        [Fact]
        public async Task ResetTournament_ResetsToDefaultGames()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), [new() { Name = "CustomGame", Device = Device.PC }]);

            await service.ResetTournament();

            var tournament = await service.GetTournament();
            Assert.Equal(14, tournament.Games.Count);
            Assert.Contains(tournament.Games, g => g.Name == "Bopl Battle");
        }

        [Fact]
        public async Task ResetTournament_ResetsExtraPoints()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames());

            await service.AddExtraPoints("Alpha", 50);

            await service.ResetTournament();

            var standings = await service.GetTeamStandings();
            Assert.All(standings, s => Assert.Equal(0, s.TeamScore));
        }

        [Fact]
        public async Task ResetTournament_ResetsSettingsToDefaults()
        {
            var (service, _) = await CreateServiceWithTournamentAsync(
                CreateTeams("Alpha", "Bravo"), CreateStandardGames(),
                isTimeTrial: true, nrTeamsToTimeTrial: 3);

            await service.ResetTournament();

            var tournament = await service.GetTournament();
            Assert.False(tournament.IsTimeTrial);
            Assert.Equal(0, tournament.NrTeamsToTimeTrial);
        }

        #endregion

        #region History

        [Fact]
        public async Task ListTournamentHistory_ReturnsEmpty_InMemoryMode()
        {
            var service = await CreateGameServiceAsync();

            var history = await service.ListTournamentHistory();

            Assert.NotNull(history);
            Assert.Empty(history);
        }

        #endregion

        #region Interface Construction

        [Fact]
        public async Task GameService_CanBeConstructed_WithIStorageService()
        {
            IStorageService storageService = new StorageService();
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, matchmakingService, archiveService);

            await gameService.InitializeAsync();

            var tournament = await gameService.GetTournament();
            Assert.NotNull(tournament);
        }

        #endregion

        #region SignalR Broadcasting

        [Fact]
        public async Task MutateTournament_BroadcastsTournamentUpdated_WhenHubContextProvided()
        {
            var storageService = new StorageService();
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var hubContext = Substitute.For<IHubContext<TournamentHub>>();
            var clients = Substitute.For<IHubClients>();
            var clientProxy = Substitute.For<IClientProxy>();
            hubContext.Clients.Returns(clients);
            clients.All.Returns(clientProxy);

            var gameService = new GameService(storageService, matchmakingService, archiveService, hubContext);
            await gameService.InitializeAsync();

            await gameService.AddTeam(new Team { TeamName = "TestTeam" });

            await clientProxy.Received().SendCoreAsync(
                "TournamentUpdated",
                Arg.Any<object[]>(),
                Arg.Any<System.Threading.CancellationToken>());
        }

        [Fact]
        public async Task MutateTournament_DoesNotThrow_WhenHubContextIsNull()
        {
            var storageService = new StorageService();
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, matchmakingService, archiveService);
            await gameService.InitializeAsync();

            await gameService.AddTeam(new Team { TeamName = "TestTeam" });

            var tournament = await gameService.GetTournament();
            Assert.Contains(tournament.Teams, t => t.TeamName == "TestTeam");
        }

        #endregion

        #region Mocked Storage

        [Fact]
        public async Task AddTeam_CallsSaveTournament()
        {
            var storageService = Substitute.For<IStorageService>();
            storageService.GetTournament().Returns(new Tournament
            {
                Games = [new() { Name = "G", Device = Device.TV }],
                Rounds = []
            });
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, matchmakingService, archiveService);

            await gameService.AddTeam(new Team { TeamName = "Alpha" });

            await storageService.Received(1).SaveTournament(Arg.Is<Tournament>(t =>
                t.Teams.Any(team => team.TeamName == "Alpha")));
        }

        [Fact]
        public async Task CompleteMatch_CallsSaveTournament_WithCompletedMatch()
        {
            var tournament = new Tournament
            {
                Games = [new() { Name = "G", Device = Device.TV }],
                Rounds = [new() {
                    RoundId = 1,
                    Matches = [new() { MatchId = 1, Game = new() { Name = "G", Device = Device.TV }, Team_1_Name = "A", Team_2_Name = "B" }]
                }]
            };
            tournament.SetTeams([new() { TeamName = "A" }, new() { TeamName = "B" }]);

            var storageService = Substitute.For<IStorageService>();
            storageService.GetTournament().Returns(tournament);
            var matchmakingService = new MatchmakingService();
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, matchmakingService, archiveService);

            await gameService.CompleteMatch(new MatchResult { MatchId = 1, Team1Score = 3, Team2Score = 1 });

            await storageService.Received(1).SaveTournament(Arg.Is<Tournament>(t =>
                t.Rounds[0].Matches[0].HasCompleted &&
                t.Rounds[0].Matches[0].ScoreTeam1 == 3));
        }

        #endregion

        #region Archive

        [Fact]
        public async Task ArchiveTournament_SetsMetadataAndDelegatesToArchiveService()
        {
            var tournament = new Tournament
            {
                Games = [new() { Name = "G", Device = Device.TV }],
                Rounds = [new() {
                    RoundId = 1,
                    Matches = [new() { MatchId = 1, Game = new() { Name = "G", Device = Device.TV }, Team_1_Name = "A", Team_2_Name = "B", HasCompleted = true, ScoreTeam1 = 3, ScoreTeam2 = 1 }]
                }]
            };
            tournament.SetTeams([new() { TeamName = "A" }, new() { TeamName = "B" }]);

            var storageService = Substitute.For<IStorageService>();
            storageService.GetTournament().Returns(tournament);
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, new MatchmakingService(), archiveService);

            await gameService.ArchiveTournamentAsync("Test Tournament", "test-2025");

            await archiveService.Received(1).ArchiveTournamentAsync(Arg.Is<Tournament>(t =>
                t.TournamentId == "test-2025" &&
                t.TournamentName == "Test Tournament" &&
                t.CompletedAt != null));
        }

        [Fact]
        public async Task ArchiveTournament_GeneratesDefaultId_WhenNoneProvided()
        {
            var tournament = new Tournament();
            tournament.SetTeams([new() { TeamName = "A" }]);

            var storageService = Substitute.For<IStorageService>();
            storageService.GetTournament().Returns(tournament);
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, new MatchmakingService(), archiveService);

            await gameService.ArchiveTournamentAsync(null, null);

            await archiveService.Received(1).ArchiveTournamentAsync(Arg.Is<Tournament>(t =>
                !string.IsNullOrEmpty(t.TournamentId) &&
                !string.IsNullOrEmpty(t.TournamentName) &&
                t.CompletedAt != null));
        }

        [Fact]
        public async Task ArchiveTournament_UsesProvidedIdAndName()
        {
            var tournament = new Tournament();
            var storageService = Substitute.For<IStorageService>();
            storageService.GetTournament().Returns(tournament);
            var archiveService = Substitute.For<ITournamentArchiveService>();
            var gameService = new GameService(storageService, new MatchmakingService(), archiveService);

            await gameService.ArchiveTournamentAsync("Summer LAN 2025", "2025-summer");

            await archiveService.Received(1).ArchiveTournamentAsync(Arg.Is<Tournament>(t =>
                t.TournamentId == "2025-summer" &&
                t.TournamentName == "Summer LAN 2025"));
        }

        #endregion
    }
}
