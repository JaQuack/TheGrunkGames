using Azure;
using Azure.Data.Tables;
using System;
using System.Linq;
using System.Text.Json;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Entities
{
    public class TournamentArchiveEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string TournamentName { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
        public string WinningTeam { get; set; } = string.Empty;
        public int TotalRounds { get; set; }
        public int TotalTeams { get; set; }
        public int TotalMatches { get; set; }
        public string TournamentDataJson { get; set; } = string.Empty;

        public static TournamentArchiveEntity FromTournament(Tournament tournament, string year)
        {
            tournament.PopulateAllMatchesPlayed();

            var standings = tournament.Teams
                .Select(t => new { t.TeamName, t.CurrentScore })
                .OrderByDescending(t => t.CurrentScore)
                .ToList();

            var activeRounds = tournament.Rounds.Where(r => !r.isStaging).ToList();

            return new TournamentArchiveEntity
            {
                PartitionKey = year,
                RowKey = tournament.TournamentId,
                TournamentName = tournament.TournamentName,
                CompletedAt = tournament.CompletedAt ?? DateTime.UtcNow,
                WinningTeam = standings.FirstOrDefault()?.TeamName ?? string.Empty,
                TotalRounds = activeRounds.Count,
                TotalTeams = tournament.Teams.Count,
                TotalMatches = activeRounds.Sum(r => r.Matches.Count),
                TournamentDataJson = JsonSerializer.Serialize(tournament)
            };
        }

        public TournamentArchiveSummary ToSummary()
        {
            return new TournamentArchiveSummary
            {
                TournamentId = RowKey,
                TournamentName = TournamentName,
                Year = PartitionKey,
                CompletedAt = CompletedAt,
                WinningTeam = WinningTeam,
                TotalRounds = TotalRounds,
                TotalTeams = TotalTeams,
                TotalMatches = TotalMatches
            };
        }

        public Tournament? GetTournament()
        {
            if (string.IsNullOrEmpty(TournamentDataJson))
                return null;

            return JsonSerializer.Deserialize<Tournament>(TournamentDataJson);
        }
    }
}
