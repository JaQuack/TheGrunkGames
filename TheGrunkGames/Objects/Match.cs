using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TheGrunkGames2.Objects
{
    public class Match
    {
        public int MatchId { get; set; }
        public string Team_1_Name { get; set; }
        public string Team_2_Name { get; set; }
        public Game Game { get; set; }

        public bool IsTimeTrial { get; set; }

        public int ScoreTeam1 { get; set; }
        public int ScoreTeam2 { get; set; }

        public bool Compleated { get; set; }

        public bool IsTeamPlaying(string teamName)
        {
            return Team_1_Name.Equals(teamName) || (Team_2_Name?.Equals(teamName) ?? false);
        }

        public string GetOpponentsName(string teamName)
        {
            if (Team_1_Name.Equals(teamName)) return Team_2_Name;
            return Team_1_Name;
        }

        [JsonIgnore]
        public Dictionary<string, string> Stat { get; set; }

    }
}