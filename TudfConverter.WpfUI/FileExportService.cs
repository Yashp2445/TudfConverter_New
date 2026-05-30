using System.IO;
using System.Threading.Tasks;

namespace TudfConverter.WpfUI
{
    public class FileExportService
    {
        public async Task<string> ExportTudfAsync(string outputDirectory, string tudfContent, string fileName)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(filePath, tudfContent);

            return filePath;
        }
    }
}
