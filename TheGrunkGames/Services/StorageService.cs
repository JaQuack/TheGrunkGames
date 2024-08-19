using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames.Objects;

namespace TheGrunkGames.Services
{
    public class StorageService
    {
        private readonly TableClient _tableClient;
        public StorageService()
        {
            var connectionString = ""; //Add a connection string to use storage feature
            if (!string.IsNullOrEmpty(connectionString))
            {
                _tableClient = new TableClient(connectionString, "tournamentHistory");
                _tableClient.CreateIfNotExists();
            }
        }

        public async Task SaveTournament(Tournament tournament)
        {
            if (_tableClient == null)
                return;
            try
            {
                if (tournament == null)
                {
                    throw new ArgumentNullException("Unable to Save tournament, no tournament provided");
                }

                var history = new History
                {
                    RowKey = DateTime.Now.Year.ToString() + "_" + (tournament.Rounds.Any() ? tournament.Rounds.Max(x => x.RoundId).ToString() : "0"),
                    PartitionKey = DateTime.Now.Year.ToString()
                };
                history.SetTournament(tournament);

                if (_tableClient.GetEntityIfExists<History>(history.PartitionKey, history.RowKey).HasValue)
                {
                    await _tableClient.UpdateEntityAsync(history, ETag.All);
                }
                else
                {
                    await _tableClient.AddEntityAsync(history);
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<Tournament> GetTournament(string version, string year = null)
        {
            if (_tableClient == null)
                return null;
            try
            {
                if (string.IsNullOrWhiteSpace(year))
                    year = DateTime.Now.Year.ToString();

                Tournament tournament = null;
                var historyOrNull = await _tableClient.GetEntityIfExistsAsync<History>(year, version);

                if (historyOrNull == null || !historyOrNull.HasValue)
                {
                    tournament = new Tournament();
                }
                else
                    tournament = historyOrNull.Value.Tournament();

                return tournament;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public string GetNewestTournamentVersion(string year = null)
        {
            try
            {
                if (string.IsNullOrEmpty(year))
                    year = DateTime.Now.Year.ToString();

                List<History> tournaments = _tableClient.Query<History>($"PartitionKey eq {year}", 50).ToList();

                return tournaments.OrderByDescending(x => int.Parse(x.RowKey.Split("_").LastOrDefault())).FirstOrDefault().RowKey;
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }
}
