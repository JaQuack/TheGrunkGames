using Xunit;
using TheGrunkGames.Models.TournamentModels;
using System.Collections.Generic;
using System.Linq;

namespace TheGrunkGames.Tests
{
    public class ModelTests
    {
        private static List<Team> CreateTeams(params string[] names) =>
            names.Select(n => new Team { TeamName = n }).ToList();

        #region Match

        [Fact]
        public void Match_IsTeamPlaying_IsCaseInsensitive()
        {
            var match = new Match { Team_1_Name = "Alpha", Team_2_Name = "Bravo", Game = new Game { Name = "G", Device = Device.TV } };

            Assert.True(match.IsTeamPlaying("alpha"));
            Assert.True(match.IsTeamPlaying("BRAVO"));
            Assert.False(match.IsTeamPlaying("Charlie"));
        }

        [Fact]
        public void Match_IsTeamPlaying_HandlesNullTeam2()
        {
            var match = new Match { Team_1_Name = "Alpha", Team_2_Name = null, IsTimeTrial = true, Game = new Game { Name = "TT", Device = Device.TIMETRIAL } };

            Assert.True(match.IsTeamPlaying("Alpha"));
            Assert.False(match.IsTeamPlaying("Bravo"));
        }

        [Fact]
        public void Match_GetOpponentsName_ReturnsCorrectOpponent()
        {
            var match = new Match { Team_1_Name = "Alpha", Team_2_Name = "Bravo", Game = new Game { Name = "G", Device = Device.TV } };

            Assert.Equal("Bravo", match.GetOpponentsName("Alpha"));
            Assert.Equal("Alpha", match.GetOpponentsName("Bravo"));
        }

        [Fact]
        public void Match_GetOpponentsName_IsCaseInsensitive()
        {
            var match = new Match { Team_1_Name = "Alpha", Team_2_Name = "Bravo", Game = new Game { Name = "G", Device = Device.TV } };

            Assert.Equal("Bravo", match.GetOpponentsName("alpha"));
        }

        #endregion

        #region Round

        [Fact]
        public void Round_IsCompleted_ReturnsTrueWhenAllMatchesCompleted()
        {
            var round = new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, HasCompleted = true, Game = new Game { Name = "G", Device = Device.TV }, Team_1_Name = "A", Team_2_Name = "B" },
                    new() { MatchId = 2, HasCompleted = true, Game = new Game { Name = "G2", Device = Device.PC }, Team_1_Name = "C", Team_2_Name = "D" }
                ]
            };

            Assert.True(round.IsCompleted());
        }

        [Fact]
        public void Round_IsCompleted_ReturnsFalseWhenAnyMatchIncomplete()
        {
            var round = new Round
            {
                RoundId = 1,
                Matches =
                [
                    new() { MatchId = 1, HasCompleted = true, Game = new Game { Name = "G", Device = Device.TV }, Team_1_Name = "A", Team_2_Name = "B" },
                    new() { MatchId = 2, HasCompleted = false, Game = new Game { Name = "G2", Device = Device.PC }, Team_1_Name = "C", Team_2_Name = "D" }
                ]
            };

            Assert.False(round.IsCompleted());
        }

        #endregion

        #region Tournament

        [Fact]
        public void Tournament_GetTeams_PopulatesMatchesPlayed()
        {
            var game = new Game { Name = "G", Device = Device.TV };
            var tournament = new Tournament
            {
                Games = [game],
                Rounds =
                [
                    new Round
                    {
                        RoundId = 1,
                        Matches = [new() { MatchId = 1, Game = game, Team_1_Name = "Alpha", Team_2_Name = "Bravo", HasCompleted = true }]
                    }
                ]
            };
            tournament.Teams = CreateTeams("Alpha", "Bravo", "Charlie");

            var teams = tournament.GetTeams();

            Assert.Single(teams.First(t => t.TeamName == "Alpha").MatchesPlayed);
            Assert.Single(teams.First(t => t.TeamName == "Bravo").MatchesPlayed);
            Assert.Empty(teams.First(t => t.TeamName == "Charlie").MatchesPlayed);
        }

        #endregion

        #region Team

        [Fact]
        public void Team_CurrentScore_SumsMatchScoresAndExtraPoints()
        {
            var game = new Game { Name = "G", Device = Device.TV };
            var team = new Team
            {
                TeamName = "Alpha",
                ExtraPoints = 5,
                MatchesPlayed =
                [
                    new() { MatchId = 1, Game = game, Team_1_Name = "Alpha", Team_2_Name = "Bravo", ScoreTeam1 = 3, ScoreTeam2 = 1 },
                    new() { MatchId = 2, Game = game, Team_1_Name = "Bravo", Team_2_Name = "Alpha", ScoreTeam1 = 2, ScoreTeam2 = 4 }
                ]
            };

            Assert.Equal(12, team.CurrentScore); // 3 + 4 + 5 extra
        }

        #endregion
    }
}
