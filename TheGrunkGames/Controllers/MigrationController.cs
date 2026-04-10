using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;
using TheGrunkGames.Services;

namespace TheGrunkGames.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly ILogger<MigrationController> _logger;
        private readonly ITournamentArchiveService _archiveService;

        public MigrationController(ILogger<MigrationController> logger, ITournamentArchiveService archiveService)
        {
            _logger = logger;
            _archiveService = archiveService;
        }

        [HttpPost("ImportCsv")]
        public async Task<IActionResult> ImportCsv(IFormFile file)
        {
            if (!await _archiveService.IsAvailableAsync())
                return BadRequest("Tournament archive service is not available.");

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync();
            if (headerLine == null)
                return BadRequest("Empty file.");

            var headers = ParseCsvLine(headerLine);
            var partitionKeyIndex = headers.IndexOf("PartitionKey");
            var rowKeyIndex = headers.IndexOf("RowKey");
            var dataIndex = headers.IndexOf("TournamentSerialized");

            if (partitionKeyIndex < 0 || rowKeyIndex < 0 || dataIndex < 0)
                return BadRequest("CSV must contain PartitionKey, RowKey, and TournamentSerialized columns.");

            var rows = new List<(string Year, int RoundVersion, string RowKey, string Json)>();
            var parseWarnings = new List<string>();
            var lineNumber = 1;
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            while (!reader.EndOfStream)
            {
                lineNumber++;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var fields = ParseCsvLine(line);
                    var csvPartitionKey = fields[partitionKeyIndex];
                    var rowKey = fields[rowKeyIndex];
                    var json = fields[dataIndex];

                    var underscorePos = rowKey.LastIndexOf('_');
                    if (underscorePos < 0)
                    {
                        parseWarnings.Add($"Line {lineNumber}: RowKey '{rowKey}' has no underscore separator, skipped.");
                        continue;
                    }

                    var yearFromRowKey = rowKey[..underscorePos];
                    if (!int.TryParse(rowKey[(underscorePos + 1)..], out var roundVersion))
                    {
                        parseWarnings.Add($"Line {lineNumber}: RowKey '{rowKey}' has non-numeric round version, skipped.");
                        continue;
                    }

                    if (csvPartitionKey != yearFromRowKey)
                        parseWarnings.Add($"Line {lineNumber}: PartitionKey '{csvPartitionKey}' does not match year in RowKey '{yearFromRowKey}'. Using year from RowKey.");

                    rows.Add((yearFromRowKey, roundVersion, rowKey, json));
                }
                catch (Exception ex)
                {
                    parseWarnings.Add($"Line {lineNumber}: Failed to parse CSV row: {ex.Message}");
                }
            }

            var grouped = rows
                .GroupBy(r => r.Year)
                .Select(g => g.OrderByDescending(r => r.RoundVersion).First())
                .ToList();

            var importedCount = 0;
            var importWarnings = new List<string>();

            foreach (var row in grouped)
            {
                try
                {
                    var normalizedJson = row.Json
                        .Replace("\"Compleated\"", "\"HasCompleted\"");
                    var tournament = JsonSerializer.Deserialize<Tournament>(normalizedJson, jsonOptions);
                    if (tournament == null)
                    {
                        importWarnings.Add($"Year {row.Year}: Failed to deserialize tournament from RowKey '{row.RowKey}'.");
                        continue;
                    }

                    if (tournament.Rounds.Count == 0)
                    {
                        importWarnings.Add($"Year {row.Year}: Tournament has no rounds (RowKey '{row.RowKey}'), skipped.");
                        continue;
                    }

                    var incompleteRounds = tournament.Rounds
                        .Where(r => !r.isStaging && r.Matches.Any(m => !m.HasCompleted))
                        .ToList();

                    if (incompleteRounds.Count > 0)
                    {
                        importWarnings.Add($"Year {row.Year}: Removed {incompleteRounds.Count} incomplete round(s): {string.Join(", ", incompleteRounds.Select(r => $"Round {r.RoundId}"))}.");
                        tournament.Rounds = tournament.Rounds
                            .Where(r => !r.isStaging && r.Matches.All(m => m.HasCompleted))
                            .ToList();
                    }
                    else
                    {
                        tournament.Rounds = tournament.Rounds
                            .Where(r => !r.isStaging)
                            .ToList();
                    }

                    foreach (var team in tournament.Teams)
                        team.MatchesPlayed = null;

                    var hasGenericNames = tournament.Teams.Any(t =>
                        t.TeamName.StartsWith("Team_", StringComparison.OrdinalIgnoreCase));
                    if (hasGenericNames)
                        importWarnings.Add($"Year {row.Year}: Tournament contains generic team names (e.g. 'Team_0').");

                    tournament.TournamentId = $"grunk-{row.Year}";
                    tournament.TournamentName = $"TheGrunkGames {row.Year}";
                    tournament.CompletedAt = new DateTime(int.Parse(row.Year), 12, 31, 23, 59, 59, DateTimeKind.Utc);

                    await _archiveService.ArchiveTournamentAsync(tournament);
                    importedCount++;

                    var winner = tournament.Teams.OrderByDescending(t => t.CurrentScore).FirstOrDefault();
                    _logger.LogInformation(
                        "Imported: {Year} (from {RowKey}) — {Rounds} rounds, {Teams} teams, {Matches} matches, winner: {Winner} ({Score} pts)",
                        row.Year, row.RowKey, tournament.Rounds.Count, tournament.Teams.Count,
                        tournament.Rounds.Sum(r => r.Matches.Count),
                        winner?.TeamName ?? "N/A", winner?.CurrentScore ?? 0);
                }
                catch (Exception ex)
                {
                    importWarnings.Add($"Year {row.Year}: Import failed: {ex.Message}");
                    _logger.LogError(ex, "Failed to import tournament for year {Year}.", row.Year);
                }
            }

            var allWarnings = parseWarnings.Concat(importWarnings).ToList();

            return Ok(new
            {
                ImportedCount = importedCount,
                TotalRowsInCsv = rows.Count,
                YearsFound = grouped.Select(g => $"{g.Year} (used snapshot {g.RowKey}, version {g.RoundVersion})").ToList(),
                Warnings = allWarnings
            });
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }
    }
}
