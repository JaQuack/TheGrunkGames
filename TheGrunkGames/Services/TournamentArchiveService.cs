using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGrunkGames.Entities;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public class TournamentArchiveService : ITournamentArchiveService
    {
        private readonly TableClient? _tableClient;
        private readonly ILogger<TournamentArchiveService> _logger;
        private readonly bool _isAvailable;

        public TournamentArchiveService(TableServiceClient tableServiceClient, ILogger<TournamentArchiveService> logger)
        {
            _logger = logger;

            try
            {
                _tableClient = tableServiceClient.GetTableClient("TournamentArchive");
                _tableClient.CreateIfNotExists();
                _isAvailable = true;
                _logger.LogInformation("Tournament archive service initialized (Azure Table Storage).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Table Storage. Tournament archiving is disabled.");
                _isAvailable = false;
            }
        }

        public TournamentArchiveService(ILogger<TournamentArchiveService> logger)
        {
            _logger = logger;
            _isAvailable = false;
            _logger.LogWarning("Azure Table Storage not configured. Tournament archiving is disabled.");
        }

        public Task<bool> IsAvailableAsync() => Task.FromResult(_isAvailable);

        public async Task ArchiveTournamentAsync(Tournament tournament)
        {
            if (!_isAvailable || _tableClient == null)
                throw new InvalidOperationException("Tournament archive service is not available.");

            var year = tournament.CompletedAt?.Year.ToString() ?? DateTime.UtcNow.Year.ToString();
            var entity = TournamentArchiveEntity.FromTournament(tournament, year);
            await _tableClient.UpsertEntityAsync(entity);

            _logger.LogInformation("Tournament archived: {Year}/{TournamentId} ({Name})",
                year, tournament.TournamentId, tournament.TournamentName);
        }

        public async Task<List<TournamentArchiveSummary>> ListArchivedTournamentsAsync()
        {
            if (!_isAvailable || _tableClient == null)
                return [];

            var entities = _tableClient.QueryAsync<TournamentArchiveEntity>();
            var summaries = new List<TournamentArchiveSummary>();

            await foreach (var entity in entities)
            {
                summaries.Add(entity.ToSummary());
            }

            return summaries.OrderByDescending(s => s.CompletedAt).ToList();
        }

        public async Task<Tournament?> GetArchivedTournamentAsync(string year, string tournamentId)
        {
            if (!_isAvailable || _tableClient == null)
                return null;

            try
            {
                var response = await _tableClient.GetEntityAsync<TournamentArchiveEntity>(year, tournamentId);
                return response.Value?.GetTournament();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }
    }
}
