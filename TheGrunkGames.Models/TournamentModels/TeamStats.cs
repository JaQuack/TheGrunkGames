namespace TheGrunkGames.Models.TournamentModels
{
    public class TeamStats
    {
        public string TeamName { get; set; } = string.Empty;
        public List<KeyValuePair<string, int>> PlayedAgainstTeam { get; set; } = [];
        public List<KeyValuePair<string, int>> PlayedGames { get; set; } = [];
    }
}
