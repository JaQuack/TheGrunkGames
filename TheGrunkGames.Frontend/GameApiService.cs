using System.Net.Http.Json;
using TheGrunkGames.Models.TournamentModels;


namespace TheGrunkGames.Frontend
{  

    public class GameApiService
    {
        private readonly HttpClient _http;

        public GameApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<Round> GetCurrentRoundAsync()
            => await _http.GetFromJsonAsync<Round>("Game/GetCurrentRound");

        public async Task<Round> GetNextRoundAsync()
            => await _http.GetFromJsonAsync<Round>("Game/GetNextRound");

        public async Task<List<Team>> GetTeamsAsync()
            => await _http.GetFromJsonAsync<List<Team>>("Game/Teams");

        // Add other methods for your endpoints as needed
    }
}
