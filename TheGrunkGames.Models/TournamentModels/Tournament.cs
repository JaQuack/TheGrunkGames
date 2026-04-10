namespace TheGrunkGames.Models.TournamentModels
{
    public class Tournament
    {
        public Tournament()
        {
            Teams = [];
            Games = [];
            Rounds = [];
        }

        public List<Team> Teams { get; set; }
        public List<Game> Games { get; set; }
        public List<Round> Rounds { get; set; }

        public List<Team> GetTeams()
        {
            PopulateMatchesPlayed(Teams);
            return Teams;
        }

        public void SetTeams(List<Team> teams)
        {
            PopulateMatchesPlayed(teams);
            Teams = teams;
        }

        public IEnumerable<Match> GetActiveMatches() =>
            Rounds.Where(r => !r.isStaging && !r.IsCompleted())
                .SelectMany(r => r.Matches)
                .Where(m => !m.HasCompleted);

        public string TournamentId { get; set; } = string.Empty;
        public string TournamentName { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }

        public bool IsTimeTrial { get; set; }
        public int NrTeamsToTimeTrial { get; set; }

        public void PopulateAllMatchesPlayed()
        {
            PopulateMatchesPlayed(Teams);
        }

        private void PopulateMatchesPlayed(IEnumerable<Team> teams)
        {
            foreach (var team in teams)
            {
                team.MatchesPlayed = [.. Rounds.SelectMany(x => x.Matches).Where(x => x.IsTeamPlaying(team.TeamName))];
            }
        }
    }
}
