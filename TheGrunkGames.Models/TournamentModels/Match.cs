namespace TheGrunkGames.Models.TournamentModels
{
    public class Match
    {
        public int MatchId { get; set; }
        public string Team_1_Name { get; set; } = string.Empty;
        public string? Team_2_Name { get; set; }
        public Game Game { get; set; } = default!;

        public bool IsTimeTrial { get; set; }

        public int ScoreTeam1 { get; set; }
        public int ScoreTeam2 { get; set; }

        public bool HasCompleted { get; set; }

        public bool IsTeamPlaying(string teamName)
        {
            return Team_1_Name.Equals(teamName, StringComparison.OrdinalIgnoreCase) || (Team_2_Name?.Equals(teamName, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public string? GetOpponentsName(string teamName)
        {
            if (Team_1_Name.Equals(teamName, StringComparison.OrdinalIgnoreCase)) return Team_2_Name;
            return Team_1_Name;
        }
    }
}