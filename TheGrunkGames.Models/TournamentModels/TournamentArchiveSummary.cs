using System;

namespace TheGrunkGames.Models.TournamentModels
{
    public class TournamentArchiveSummary
    {
        public string TournamentId { get; set; } = string.Empty;
        public string TournamentName { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
        public string WinningTeam { get; set; } = string.Empty;
        public int TotalRounds { get; set; }
        public int TotalTeams { get; set; }
        public int TotalMatches { get; set; }
    }
}
