using System.ComponentModel.DataAnnotations;

namespace TheGrunkGames.Models.TournamentModels
{
    public class Player
    {
        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}