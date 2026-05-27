using System.ComponentModel.DataAnnotations;

namespace FootballPlayerCards.Models
{
    public class Player
    {
        public int PlayerID { get; set; }

        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required] public int Age { get; set; }
        [Required] public string Position { get; set; } = string.Empty;

        public int Matches { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        [Required] public int ClubID { get; set; }
        public string ClubName { get; set; } = string.Empty;

        // Automatic Rating System Module (Out of 10)
        public double Rating
        {
            get
            {
                if (Matches == 0) return 5.0; // Baseline rating

                // Formula: Goals are weighted slightly higher than assists
                double goalRatio = (double)Goals / Matches;
                double assistRatio = ((double)Assists * 0.75) / Matches;

                double calculatedRating = 5.0 + ((goalRatio + assistRatio) * 5.0);
                return Math.Clamp(Math.Round(calculatedRating, 1), 1.0, 10.0);
            }
        }
    }
}