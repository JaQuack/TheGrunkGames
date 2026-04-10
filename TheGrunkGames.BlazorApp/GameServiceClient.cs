using System.Text.Json;
using System.Text.Json.Serialization;
using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.BlazorApp
{
    public class GameServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GameServiceClient> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public GameServiceClient(HttpClient httpClient, ILogger<GameServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        private async Task<ApiResult> ExecuteAsync(Func<Task<HttpResponseMessage>> request)
        {
            try
            {
                var response = await request();
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API request failed with {StatusCode}: {Body}", response.StatusCode, body);
                    return ApiResult.Fail(body);
                }
                return ApiResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API request threw an exception");
                return ApiResult.Fail(ex.Message);
            }
        }

        private async Task<ApiResult<T>> ExecuteAsync<T>(Func<Task<HttpResponseMessage>> request)
        {
            try
            {
                var response = await request();
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API request failed with {StatusCode}: {Body}", response.StatusCode, body);
                    return ApiResult<T>.Fail(body);
                }
                var data = await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
                return ApiResult<T>.Ok(data!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API request threw an exception");
                return ApiResult<T>.Fail(ex.Message);
            }
        }

        private async Task<ApiResult<T>> GetJsonAsync<T>(string url)
        {
            try
            {
                var data = await _httpClient.GetFromJsonAsync<T>(url, _jsonOptions);
                return ApiResult<T>.Ok(data!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET {Url} threw an exception", url);
                return ApiResult<T>.Fail(ex.Message);
            }
        }

        public async Task<ApiResult<Round>> GetCurrentRound()
        {
            try
            {
                var response = await _httpClient.GetAsync("/Game/GetCurrentRound");
                if (response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    var round = await response.Content.ReadFromJsonAsync<Round>(_jsonOptions);
                    return round is not null
                        ? ApiResult<Round>.Ok(round)
                        : ApiResult<Round>.Fail("No round data returned");
                }
                return ApiResult<Round>.Ok(null!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /Game/GetCurrentRound threw an exception");
                return ApiResult<Round>.Fail(ex.Message);
            }
        }

        public async Task<ApiResult<Round>> GetNextRound() =>
            await ExecuteAsync<Round>(() => _httpClient.PostAsync("/Game/GetNextRound", null));

        public async Task<ApiResult<Round>> GetNextRoundStaging() =>
            await ExecuteAsync<Round>(() => _httpClient.PostAsync("/Game/GetNextRound_Staging", null));

        public async Task<ApiResult<Round>> SetStaging(bool activate, int roundId) =>
            await ExecuteAsync<Round>(() => _httpClient.PostAsync($"/Game/SetStaging?activate={activate}&roundId={roundId}", null));

        public async Task<ApiResult<Round>> GetRound(int roundId) =>
            await GetJsonAsync<Round>($"/Game/GetRound?roundId={roundId}");

        public async Task<ApiResult> SetRound(Round round) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/SetRound_FullOverride", round));

        public async Task<ApiResult> CompleteMatch(MatchResult result) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/CompleteMatch", result));

        public async Task<ApiResult> SetTournament(Tournament tournament) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/Tournament", tournament));

        public async Task<ApiResult<Tournament>> GetTournament() =>
            await GetJsonAsync<Tournament>("/Game/Tournament");

        public async Task<ApiResult> ResetTournament() =>
            await ExecuteAsync(() => _httpClient.PostAsync("/Game/Tournament/Reset", null));

        public async Task<ApiResult<List<Team>>> GetTeams() =>
            await GetJsonAsync<List<Team>>("/Game/Teams");

        public async Task<ApiResult> SetTeams(List<Team> teams) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/Teams", teams));

        public async Task<ApiResult> AddTeam(Team team) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/Team", team));

        public async Task<ApiResult<List<Game>>> GetGames() =>
            await GetJsonAsync<List<Game>>("/Game/Games");

        public async Task<ApiResult> SetGames(List<Game> games) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/Games", games));

        public async Task<ApiResult> AddGame(Game game) =>
            await ExecuteAsync(() => _httpClient.PostAsJsonAsync("/Game/Game", game));

        public async Task<ApiResult<List<TeamStanding>>> GetTeamStandings() =>
            await GetJsonAsync<List<TeamStanding>>("/Game/GetTeamStandings");

        public async Task<ApiResult<List<TeamStats>>> GetTeamStats() =>
            await GetJsonAsync<List<TeamStats>>("/Game/TeamStats");

        public async Task<ApiResult> AddExtraPoints(string teamName, int points) =>
            await ExecuteAsync(() => _httpClient.PostAsync($"/Game/AddExtraPoints?teamName={Uri.EscapeDataString(teamName)}&points={points}", null));

        public async Task<ApiResult> ChangeGameForMatch(int matchId, string gameName) =>
            await ExecuteAsync(() => _httpClient.PostAsync($"/Game/ChangeGameForMatch?matchId={matchId}&gameName={Uri.EscapeDataString(gameName)}", null));

        public async Task<ApiResult> ChangeTeamsForMatch(int matchId, string team1Name, string team2Name) =>
            await ExecuteAsync(() => _httpClient.PostAsync($"/Game/ChangeTeamsForMatch?matchId={matchId}&team1Name={Uri.EscapeDataString(team1Name)}&team2Name={Uri.EscapeDataString(team2Name)}", null));

        public async Task<ApiResult> RemoveTeam(string teamName) =>
            await ExecuteAsync(() => _httpClient.DeleteAsync($"/Game/Team?teamName={Uri.EscapeDataString(teamName)}"));

        public async Task<ApiResult> RemoveGame(string gameName) =>
            await ExecuteAsync(() => _httpClient.DeleteAsync($"/Game/Game?gameName={Uri.EscapeDataString(gameName)}"));

        public async Task<ApiResult<List<TournamentHistorySummary>>> GetTournamentHistory() =>
            await GetJsonAsync<List<TournamentHistorySummary>>("/Game/Tournament/History");

        public async Task<ApiResult> RestoreTournament(string version, string year) =>
            await ExecuteAsync(() => _httpClient.PostAsync($"/Game/Tournament/Restore?version={Uri.EscapeDataString(version)}&year={Uri.EscapeDataString(year ?? "")}", null));

        // Archive
        public async Task<ApiResult> ArchiveTournament(string? name, string? tournamentId)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(name))
                query.Add($"name={Uri.EscapeDataString(name)}");
            if (!string.IsNullOrWhiteSpace(tournamentId))
                query.Add($"tournamentId={Uri.EscapeDataString(tournamentId)}");
            var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
            return await ExecuteAsync(() => _httpClient.PostAsync($"/Game/Tournament/Archive{qs}", null));
        }

        public async Task<ApiResult<List<TournamentArchiveSummary>>> GetArchivedTournaments() =>
            await GetJsonAsync<List<TournamentArchiveSummary>>("/Game/Tournament/Archives");

        public async Task<ApiResult<Tournament>> GetArchivedTournament(string year, string tournamentId) =>
            await GetJsonAsync<Tournament>($"/Game/Tournament/Archives/{Uri.EscapeDataString(year)}/{Uri.EscapeDataString(tournamentId)}");
    }
}
