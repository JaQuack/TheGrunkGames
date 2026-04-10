using System.Collections.Generic;
using System.Threading.Tasks;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.Services
{
    public interface IGameService
    {
        Task InitializeAsync();

        // Tournament Management
        Task<Tournament> GetTournament();
        Task SetTournament(Tournament newTournament);

        // Team Management
        Task AddTeam(Team team);
        Task SetTeams(List<Team> teams);
        Task<bool> TeamExists(string teamName);
        Task<bool> RemoveTeam(string teamName);

        // Game Management
        Task AddGame(Game game);
        Task SetGames(List<Game> games);
        Task<bool> RemoveGame(string gameName);

        // Round Management
        Task<Round?> GetCurrentRound();
        Task<Round?> GetRound(int roundId);
        Task SetRound(Round round);
        Task DeleteRound(int roundId);
        Task RemoveInactiveRounds();
        Task<Round> GetNextRound();
        Task<Round> GetNextRoundStaging();
        Task<Round?> ActivateOrDiscardStaging(bool activate, int roundId);

        // Match Management
        Task<Match?> GetMatch(int matchId);
        Task CompleteMatch(MatchResult result);
        Task<bool> ChangeGameForMatch(int matchId, string gameName);
        Task ChangeTeamsForMatch(int matchId, string team1Name, string team2Name);

        // Stats & Standings
        Task<List<TeamStanding>> GetTeamStandings();
        Task<List<TeamStats>> GetTeamStats();
        Task AddExtraPoints(string teamName, int points);

        // Reset
        Task ResetTournament();

        // History
        Task GetAndSetHistory(string version, string year);
        Task<List<TournamentHistorySummary>> ListTournamentHistory();

        // Archive
        Task ArchiveTournamentAsync(string? tournamentName, string? tournamentId);
        Task<List<TournamentArchiveSummary>> ListArchivedTournamentsAsync();
        Task<Tournament?> GetArchivedTournamentAsync(string year, string tournamentId);
    }
}
