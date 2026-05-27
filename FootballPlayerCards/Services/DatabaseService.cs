using FootballPlayerCards.Models;
using Microsoft.Data.SqlClient;

namespace FootballPlayerCards.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing.");
        }

        public async Task<List<Player>> GetAllPlayersAsync()
        {
            var list = new List<Player>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    p.PlayerID, p.FirstName, p.LastName, p.Age, p.Position, p.ClubID,
                    c.ClubName,
                    ISNULL(s.Matches, 0) AS Matches, 
                    ISNULL(s.Goals, 0) AS Goals, 
                    ISNULL(s.Assists, 0) AS Assists,
                    ISNULL(i.ImageUrl, '') AS ImageUrl
                FROM Players p
                LEFT JOIN Clubs c ON p.ClubID = c.ClubID
                LEFT JOIN [Statistics] s ON p.PlayerID = s.PlayerID
                LEFT JOIN Images i ON p.PlayerID = i.PlayerID
                ORDER BY p.PlayerID DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new Player
                {
                    PlayerID = (int)rdr["PlayerID"],
                    FirstName = rdr["FirstName"].ToString()!,
                    LastName = rdr["LastName"].ToString()!,
                    Age = (int)rdr["Age"],
                    Position = rdr["Position"].ToString()!,
                    ClubID = (int)rdr["ClubID"],
                    ClubName = rdr["ClubName"].ToString()!,
                    Matches = (int)rdr["Matches"],
                    Goals = (int)rdr["Goals"],
                    Assists = (int)rdr["Assists"],
                    ImageUrl = rdr["ImageUrl"].ToString()!
                });
            }
            return list;
        }

        public async Task<List<Club>> GetAllClubsAsync()
        {
            var list = new List<Club>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT ClubID, ClubName FROM Clubs ORDER BY ClubName", conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new Club { ClubID = (int)rdr["ClubID"], ClubName = rdr["ClubName"].ToString()! });
            return list;
        }

        public async Task<bool> SavePlayerAsync(Player p)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();

            try
            {
                int newOrExistingPlayerId = p.PlayerID;

                if (p.PlayerID == 0) // ADD NEW PLAYER
                {
                    // A. Insert Core Player Data
                    string sqlPlayer = "INSERT INTO Players (FirstName, LastName, Age, Position, ClubID) OUTPUT INSERTED.PlayerID VALUES (@F, @L, @A, @Pos, @C)";
                    using var cmdPlayer = new SqlCommand(sqlPlayer, conn, transaction);
                    cmdPlayer.Parameters.AddWithValue("@F", p.FirstName);
                    cmdPlayer.Parameters.AddWithValue("@L", p.LastName);
                    cmdPlayer.Parameters.AddWithValue("@A", p.Age);
                    cmdPlayer.Parameters.AddWithValue("@Pos", p.Position);
                    cmdPlayer.Parameters.AddWithValue("@C", p.ClubID);

                    newOrExistingPlayerId = (int)await cmdPlayer.ExecuteScalarAsync();

                    // B. Insert Statistics
                    string sqlStats = "INSERT INTO [Statistics] (PlayerID, Matches, Goals, Assists) VALUES (@ID, @M, @G, @Ast)";
                    using var cmdStats = new SqlCommand(sqlStats, conn, transaction);
                    cmdStats.Parameters.AddWithValue("@ID", newOrExistingPlayerId);
                    cmdStats.Parameters.AddWithValue("@M", p.Matches);
                    cmdStats.Parameters.AddWithValue("@G", p.Goals);
                    cmdStats.Parameters.AddWithValue("@Ast", p.Assists);
                    await cmdStats.ExecuteNonQueryAsync();

                    // C. Insert Image
                    string sqlImg = "INSERT INTO Images (PlayerID, ImageUrl) VALUES (@ID, @Img)";
                    using var cmdImg = new SqlCommand(sqlImg, conn, transaction);
                    cmdImg.Parameters.AddWithValue("@ID", newOrExistingPlayerId);
                    cmdImg.Parameters.AddWithValue("@Img", string.IsNullOrEmpty(p.ImageUrl) ? DBNull.Value : p.ImageUrl);
                    await cmdImg.ExecuteNonQueryAsync();

                    // D. Insert Calculated Rating
                    string sqlRating = "INSERT INTO Ratings (PlayerID, RatingValue) VALUES (@ID, @R)";
                    using var cmdRat = new SqlCommand(sqlRating, conn, transaction);
                    cmdRat.Parameters.AddWithValue("@ID", newOrExistingPlayerId);
                    cmdRat.Parameters.AddWithValue("@R", p.Rating);
                    await cmdRat.ExecuteNonQueryAsync();
                }
                else // UPDATE EXISTING PLAYER
                {
                    // A. Update Players
                    string sqlPlayer = "UPDATE Players SET FirstName=@F, LastName=@L, Age=@A, Position=@Pos, ClubID=@C WHERE PlayerID=@ID";
                    using var cmdPlayer = new SqlCommand(sqlPlayer, conn, transaction);
                    cmdPlayer.Parameters.AddWithValue("@ID", p.PlayerID);
                    cmdPlayer.Parameters.AddWithValue("@F", p.FirstName);
                    cmdPlayer.Parameters.AddWithValue("@L", p.LastName);
                    cmdPlayer.Parameters.AddWithValue("@A", p.Age);
                    cmdPlayer.Parameters.AddWithValue("@Pos", p.Position);
                    cmdPlayer.Parameters.AddWithValue("@C", p.ClubID);
                    await cmdPlayer.ExecuteNonQueryAsync();

                    // B. Update Statistics
                    string sqlStats = "UPDATE [Statistics] SET Matches=@M, Goals=@G, Assists=@Ast WHERE PlayerID=@ID";
                    using var cmdStats = new SqlCommand(sqlStats, conn, transaction);
                    cmdStats.Parameters.AddWithValue("@ID", p.PlayerID);
                    cmdStats.Parameters.AddWithValue("@M", p.Matches);
                    cmdStats.Parameters.AddWithValue("@G", p.Goals);
                    cmdStats.Parameters.AddWithValue("@Ast", p.Assists);
                    await cmdStats.ExecuteNonQueryAsync();

                    // C. Safely Update Image
                    string sqlImg = @"
                        IF EXISTS (SELECT 1 FROM Images WHERE PlayerID=@ID)
                            UPDATE Images SET ImageUrl=@Img WHERE PlayerID=@ID
                        ELSE
                            INSERT INTO Images (PlayerID, ImageUrl) VALUES (@ID, @Img)";
                    using var cmdImg = new SqlCommand(sqlImg, conn, transaction);
                    cmdImg.Parameters.AddWithValue("@ID", p.PlayerID);
                    cmdImg.Parameters.AddWithValue("@Img", string.IsNullOrEmpty(p.ImageUrl) ? DBNull.Value : p.ImageUrl);
                    await cmdImg.ExecuteNonQueryAsync();

                    // D. Safely Update Rating
                    string sqlRating = @"
                        IF EXISTS (SELECT 1 FROM Ratings WHERE PlayerID=@ID)
                            UPDATE Ratings SET RatingValue=@R WHERE PlayerID=@ID
                        ELSE
                            INSERT INTO Ratings (PlayerID, RatingValue) VALUES (@ID, @R)";
                    using var cmdRat = new SqlCommand(sqlRating, conn, transaction);
                    cmdRat.Parameters.AddWithValue("@ID", p.PlayerID);
                    cmdRat.Parameters.AddWithValue("@R", p.Rating);
                    await cmdRat.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                return false;
            }
        }

        public async Task<bool> DeletePlayerAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM Players WHERE PlayerID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}