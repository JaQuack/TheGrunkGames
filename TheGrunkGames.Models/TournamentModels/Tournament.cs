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

        public bool IsTimeTrial()
        {
            return Teams.Count % 2 != 0;
        }
    }
}
