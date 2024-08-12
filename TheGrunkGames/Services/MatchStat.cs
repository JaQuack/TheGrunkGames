using System.Collections.Generic;

namespace TheGrunkGames.Services
{
    internal class MatchStat
    {
        public int Id { get; set; }
        public Dictionary<string, string> Stats { get; set; }
    }
}