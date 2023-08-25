using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TheGrunkGames2.Objects
{
    public class Round
    {
        public int RoundId { get; set; }
        public List<Match> Matches { get; set; }

        public bool isStaging { get; set; }

        public bool IsCompleted()
        {
            return Matches.Count > 0 && Matches.All(m => m.Compleated);
        }
    }
}
