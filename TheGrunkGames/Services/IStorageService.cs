using System.Collections.Generic;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public interface IStorageService
    {
        Task SaveTournament(Tournament tournament);
        Task<Tournament> GetTournament(string? version = null, string? year = null);
        Task<List<TournamentHistorySummary>> ListTournamentHistory();
    }
}
