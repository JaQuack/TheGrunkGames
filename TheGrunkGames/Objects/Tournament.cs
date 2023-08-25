using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TheGrunkGames2.Objects
{
    public class Tournament
    {
        public List<Team> Teams { get; set; }
        public List<Game> Games { get; set; }
        public List<Round> Rounds { get; set; }
    }
}
