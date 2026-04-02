using System.ComponentModel.DataAnnotations;

namespace TheGrunkGames.Models.TournamentModels
{
    public class Game
    {
        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;
        public Device Device { get; set; }
    }
}
