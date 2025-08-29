using TheGrunkGames.Models.TournamentModels;

namespace TheGrunkGames.BlazorApp
{
    public class GameServiceClient
    {
        private readonly HttpClient _httpClient;
        public GameServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Round> GetCurrentRound()
        {
            var response = await _httpClient.GetAsync("/Game/GetCurrentRound");
            if (response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                var round = await response.Content.ReadFromJsonAsync<Round>();
                if (round is null) throw new Exception("No round data");
                return round;
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }
            else
            {
                throw new Exception($"Error fetching current round: {response.ReasonPhrase}");
            }
        }

        public async Task<Round> GetNextRound()
        {
            return await _httpClient.GetFromJsonAsync<Round>("/Game/GetNextRound");
        }

        public async Task<Round> GetNextRoundStaging()
        {
            return await _httpClient.GetFromJsonAsync<Round>("/Game/GetNextRound_Staging");
        }

        public async Task<Round> SetStaging(bool activate, int roundId)
        {
            var url = $"/Game/SetStaging?activate={activate}&roundId={roundId}";
            return await _httpClient.GetFromJsonAsync<Round>(url);
        }

        public async Task<Round?> GetRound(int roundId)
        {
            var url = $"/Game/GetRound?roundId={roundId}";
            return await _httpClient.GetFromJsonAsync<Round>(url);
        }

        public async Task<bool> SetRound(Round round)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/SetRound_FullOverride", round);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CompleteMatch(MatchResult result)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/CompleteMatch", result);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> SetTournament(Tournament tournament)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/Tournament", tournament);
            return response.IsSuccessStatusCode;
        }

        public async Task<Tournament?> GetTournament()
        {
            return await _httpClient.GetFromJsonAsync<Tournament>("/Game/Tournament");
        }

        public async Task<List<Team>?> GetTeams()
        {
            return await _httpClient.GetFromJsonAsync<List<Team>>("/Game/Teams");
        }

        public async Task<bool> SetTeams(List<Team> teams)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/Teams", teams);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddTeam(Team team)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/Team", team);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<Game>?> GetGames()
        {
            return await _httpClient.GetFromJsonAsync<List<Game>>("/Game/Games");
        }

        public async Task<bool> SetGames(List<Game> games)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/Games", games);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddGame(Game game)
        {
            var response = await _httpClient.PostAsJsonAsync("/Game/Game", game);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<TeamStanding>?> GetTeamStandings()
        {
            return await _httpClient.GetFromJsonAsync<List<TeamStanding>>("/Game/GetTeamStandings");
        }

        public async Task<bool> AddExtraPoints(string teamName, int points)
        {
            var content = new Dictionary<string, object> { { "teamName", teamName }, { "points", points } };
            var response = await _httpClient.PostAsJsonAsync("/Game/AddExtraPoints", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ChangeGameForMatch(int matchId, string gameName)
        {
            var content = new Dictionary<string, object> { { "matchId", matchId }, { "gameName", gameName } };
            var response = await _httpClient.PostAsJsonAsync("/Game/ChangeGameForMatch", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ChangeTeamsForMatch(int matchId, string team1Name, string team2Name)
        {
            var content = new Dictionary<string, object> { { "matchId", matchId }, { "team1Name", team1Name }, { "team2Name", team2Name } };
            var response = await _httpClient.PostAsJsonAsync("/Game/ChangeTeamsForMatch", content);
            return response.IsSuccessStatusCode;
        }
    }
}
