using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TheGrunkGames.Models.TournamentModels;
using Xunit;

namespace TheGrunkGames.Tests
{
    public class ModelValidationTests
    {
        private static bool TryValidate(object model, out List<ValidationResult> results)
        {
            results = [];
            var context = new ValidationContext(model);
            return Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        }

        #region Team

        [Fact]
        public void Team_Validation_FailsWithEmptyName()
        {
            var team = new Team { TeamName = "" };
            var valid = TryValidate(team, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "TeamName"));
        }

        [Fact]
        public void Team_Validation_FailsWithNameTooLong()
        {
            var team = new Team { TeamName = new string('x', 51) };
            var valid = TryValidate(team, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "TeamName"));
        }

        [Fact]
        public void Team_Validation_PassesWithValidName()
        {
            var team = new Team { TeamName = "Alpha" };
            var valid = TryValidate(team, out _);
            Assert.True(valid);
        }

        #endregion

        #region Game

        [Fact]
        public void Game_Validation_FailsWithEmptyName()
        {
            var game = new Game { Name = "" };
            var valid = TryValidate(game, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "Name"));
        }

        [Fact]
        public void Game_Validation_PassesWithValidName()
        {
            var game = new Game { Name = "Bopl Battle" };
            var valid = TryValidate(game, out _);
            Assert.True(valid);
        }

        #endregion

        #region MatchResult

        [Fact]
        public void MatchResult_Validation_FailsWithZeroMatchId()
        {
            var result = new MatchResult { MatchId = 0, Team1Score = 1, Team2Score = 1 };
            var valid = TryValidate(result, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "MatchId"));
        }

        [Fact]
        public void MatchResult_Validation_FailsWithNegativeScore()
        {
            var result = new MatchResult { MatchId = 1, Team1Score = -1, Team2Score = 0 };
            var valid = TryValidate(result, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "Team1Score"));
        }

        [Fact]
        public void MatchResult_Validation_PassesWithValidData()
        {
            var result = new MatchResult { MatchId = 1, Team1Score = 3, Team2Score = 1 };
            var valid = TryValidate(result, out _);
            Assert.True(valid);
        }

        #endregion

        #region Player

        [Fact]
        public void Player_Validation_FailsWithEmptyName()
        {
            var player = new Player { Name = "" };
            var valid = TryValidate(player, out var results);
            Assert.False(valid);
            Assert.Contains(results, r => r.MemberNames.Any(m => m == "Name"));
        }

        #endregion
    }
}
