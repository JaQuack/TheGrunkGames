using System.Collections.Generic;

namespace TheGrunkGames.Services
{
    internal class RoundStats
    {
        public List<MatchStat> Matches { get; set; }
        public int RoundId { get; internal set; }
    }
}