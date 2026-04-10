using System.Collections.Generic;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public interface ITournamentArchiveService
    {
        Task<bool> IsAvailableAsync();
        Task ArchiveTournamentAsync(Tournament tournament);
        Task<List<TournamentArchiveSummary>> ListArchivedTournamentsAsync();
        Task<Tournament?> GetArchivedTournamentAsync(string year, string tournamentId);
    }
}
