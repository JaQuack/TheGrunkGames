using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames.Entities;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public class StorageService : IStorageService
    {
        private Tournament? _cachedTournament;
        private readonly IMongoCollection<TournamentDocument>? _collection;

        static StorageService()
        {
            ConventionRegistry.Register("IgnoreExtraElements",
                new ConventionPack { new IgnoreExtraElementsConvention(true) },
                _ => true);
        }

        public StorageService()
        {
            _collection = null;
        }

        public StorageService(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("thegrunkgames");
            _collection = database.GetCollection<TournamentDocument>("tournaments");
        }

        public async Task SaveTournament(Tournament tournament)
        {
            ArgumentNullException.ThrowIfNull(tournament);
            _cachedTournament = tournament;

            if (_collection == null)
                return;

            var roundVersion = tournament.Rounds.Count > 0
                ? tournament.Rounds.Max(x => x.RoundId)
                : 0;
            var year = DateTime.Now.Year.ToString();

            var doc = new TournamentDocument
            {
                Id = $"{year}_{roundVersion}",
                Year = year,
                RoundVersion = roundVersion,
                TournamentData = tournament,
                SavedAt = DateTime.UtcNow
            };

            var filter = Builders<TournamentDocument>.Filter.Eq(x => x.Id, doc.Id);
            await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<Tournament> GetTournament(string? version = null, string? year = null)
        {
            if (_collection == null)
                return _cachedTournament ?? new Tournament();

            if (string.IsNullOrEmpty(version) && string.IsNullOrWhiteSpace(year) && _cachedTournament != null)
                return _cachedTournament;

            if (string.IsNullOrWhiteSpace(year))
                year = DateTime.Now.Year.ToString();

            TournamentDocument doc;
            if (string.IsNullOrEmpty(version))
            {
                doc = await _collection
                    .Find(Builders<TournamentDocument>.Filter.Eq(x => x.Year, year))
                    .SortByDescending(x => x.RoundVersion)
                    .FirstOrDefaultAsync();
            }
            else
            {
                doc = await _collection
                    .Find(Builders<TournamentDocument>.Filter.Eq(x => x.Id, version))
                    .FirstOrDefaultAsync();
            }

            var tournament = doc?.TournamentData ?? new Tournament();
            _cachedTournament = tournament;
            return tournament;
        }

        public async Task<List<TournamentHistorySummary>> ListTournamentHistory()
        {
            if (_collection == null)
                return [];

            var docs = await _collection
                .Find(Builders<TournamentDocument>.Filter.Empty)
                .SortByDescending(x => x.SavedAt)
                .ToListAsync();

            return docs.Select(d => new TournamentHistorySummary
            {
                Id = d.Id,
                Year = d.Year,
                RoundVersion = d.RoundVersion,
                SavedAt = d.SavedAt
            }).ToList();
        }
    }
}
