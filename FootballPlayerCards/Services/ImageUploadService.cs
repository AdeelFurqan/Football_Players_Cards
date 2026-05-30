using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;

namespace FootballPlayerCards.Services
{
    public class ImageUploadService
    {
        private readonly IWebHostEnvironment _env;

        public ImageUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> UploadImageAsync(IBrowserFile file)
        {
            if (file == null) return string.Empty;

            // 1. Define the local save path (wwwroot/images)
            var folderPath = Path.Combine(_env.WebRootPath, "images");

            // Create the folder if it doesn't exist yet
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 2. Generate a unique filename using a Guid
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.Name);
            var filePath = Path.Combine(folderPath, fileName);

            // 3. Save the file to the local disk (Max size set to ~5MB)
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.OpenReadStream(maxAllowedSize: 5120000).CopyToAsync(stream);
            }

            // 4. Return the relative URL to be stored in the SQL Database
            return $"/images/{fileName}";
        }
    }
}