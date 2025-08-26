using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;
using System;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Entities
{
    public class History : ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string TournamentSerialized { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        internal Tournament Tournament()
        {
            return JsonConvert.DeserializeObject<Tournament>(TournamentSerialized);
        }

        internal void SetTournament(Tournament tournament)
        {
            TournamentSerialized = JsonConvert.SerializeObject(tournament);
        }
    }
}
