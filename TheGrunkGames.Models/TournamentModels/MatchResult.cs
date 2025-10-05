using System.ComponentModel.DataAnnotations;

namespace TheGrunkGames.Models.TournamentModels
{
    public class MatchResult
    {
        [Range(1, int.MaxValue)]
        public int MatchId { get; set; }

        [Range(0, int.MaxValue)]
        public int Team1Score { get; set; }

        [Range(0, int.MaxValue)]
        public int Team2Score { get; set; }

    }
}