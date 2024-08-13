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
        //public string PartitionKey { get; set; }
        //public string RowKey { get; set; }
        //public DateTimeOffset? Timestamp { get; set; }
        //public ETag ETag { get; set; }
    }
}
