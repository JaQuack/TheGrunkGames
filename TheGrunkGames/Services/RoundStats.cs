using System.Collections.Generic;

namespace TheGrunkGames2.Services
{
    internal class RoundStats
    {
        public List<MatchStat> Matches { get; set; }
        public int RoundId { get; internal set; }
    }
}