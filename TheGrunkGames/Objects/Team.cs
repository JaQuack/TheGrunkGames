using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TheGrunkGames2.Objects
{
    public class Team
    {
        public string TeamName { get; set; }
        public List<Player> Players { get; set; }
        public List<Match> MatchesPlayed { get; set; }

        public int CurrentScore 
        {
            get 
            {
                var score = MatchesPlayed?.Sum(x => x.Team_1_Name.Equals(TeamName) ? x.ScoreTeam1 : x.ScoreTeam2) ?? 0;
                return score + ExtraPoints;
            } 
        }

        public int ExtraPoints { get; set; }

        public bool HasPlayedGame(string gameName)
        {
            return MatchesPlayed?.Any(x => x.Game.Name.Equals(gameName)) ?? false;
        }

        public int NrTimesHavePlayedGame(string gameName)
        {
            return MatchesPlayed?.Sum(x => x.Game.Name.Equals(gameName) ? 1 : 0 ) ?? 0;
        }

        public bool HasCompetedWithTeamInGame(string teamName, string gameName)
        {
            if (MatchesPlayed == null)
                return false;

            return MatchesPlayed.Where(x => x.Game.Name.Equals(gameName)).Any(x => x.Team_1_Name.Equals(teamName) || x.Team_2_Name.Equals(teamName));
        }

        internal void AddMatch(Match match)
        {
            if (MatchesPlayed == null)
                MatchesPlayed = new List<Match>();

            if (!MatchesPlayed.Any(x => x.MatchId == match.MatchId))
                MatchesPlayed.Add(match);
        }

        public int TimeTrialsPlayed()
        {
            if (MatchesPlayed == null)
                return 0;

            return MatchesPlayed.Sum(x => x.IsTimeTrial ? 1 : 0);
        }

        internal int GetMinPlaysAgainstTeams(List<Team> teams)
        {
            return teams.Min(x => GetMinPlaysAgainstTeam(x.TeamName));
        }

        internal int GetMinPlaysAgainstTeam(string teamName)
        {
            if (MatchesPlayed == null)
                return 0;

            return MatchesPlayed.Count(x => x.Team_1_Name.Equals(teamName) || x.Team_2_Name.Equals(teamName));
        }

    }
    public class TeamStanding 
    {
        public string TeamName { get; set; }
        public int TeamScore { get; set; }
    }

    public class TeamStats
    {    
        public string TeamName { get; set; }
        public List<KeyValuePair<string, int>> PlayedAgainstTeam { get; set; }
        public List<KeyValuePair<string, int>> PlayedGames { get; set; }
    }
}
