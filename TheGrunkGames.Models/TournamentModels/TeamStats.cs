namespace TheGrunkGames.Models.TournamentModels
{
    public class TeamStats
    {
        public string TeamName { get; set; } = string.Empty;
        public List<OpponentStats> PlayedAgainstTeam { get; set; } = [];
        public List<GameStats> PlayedGames { get; set; } = [];
    }

    public class OpponentStats
    {
        public string Name { get; set; } = string.Empty;
        public int TimesPlayed { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
    }

    public class GameStats
    {
        public string Name { get; set; } = string.Empty;
        public int TimesPlayed { get; set; }
        public List<char> Results { get; set; } = [];
    }
}
