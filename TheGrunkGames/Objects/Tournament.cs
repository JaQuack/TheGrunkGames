using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TheGrunkGames.Objects
{
    public class Tournament
    {
        public Tournament() 
        {
        }

        public List<Team> Teams { get; set; }
        public List<Game> Games { get; set; }
        public List<Round> Rounds { get; set; }

        public bool IsTimeTrial()
        {
            return Teams.Count % 2 != 0;
        }
    }
}
