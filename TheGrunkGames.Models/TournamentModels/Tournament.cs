namespace TheGrunkGames.Models.TournamentModels
{
    public class Tournament
    {
        public Tournament()
        {
            _teams = [];
            Games = [];
            Rounds = [];
        }
       
        private List<Team> _teams { get; set; }
        public List<Game> Games { get; set; }
        public List<Round> Rounds { get; set; }

        public List<Team> GetTeams()
        {
            foreach (var team in _teams)
            {
                team.MatchesPlayed = [.. Rounds.SelectMany(x => x.Matches).Where(x => x.IsTeamPlaying(team.TeamName))];
            }
            return _teams;
        }

        public void SetTeams(List<Team> teams)
        {
            foreach (var team in teams)
            {
                team.MatchesPlayed = [.. Rounds.SelectMany(x => x.Matches).Where(x => x.IsTeamPlaying(team.TeamName))];
            }
            _teams = teams;
        }

        public bool IsTimeTrial { get; set; }
        public int NrTeamsToTimeTrial { get; set; }
    }
}
