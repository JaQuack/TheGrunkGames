using System;

namespace TheGrunkGames.Models.TournamentModels
{
    public class TournamentHistorySummary
    {
        public string Id { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public int RoundVersion { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
