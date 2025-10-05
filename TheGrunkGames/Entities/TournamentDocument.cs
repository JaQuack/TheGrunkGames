using System;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Entities
{
    public class TournamentDocument
    {
        public required string Id { get; set; }
        public required string Year { get; set; }
        public int RoundVersion { get; set; }
        public required Tournament TournamentData { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
