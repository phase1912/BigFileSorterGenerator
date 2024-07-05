using System.Text;
using LargeFileGeneratorAndSorter.Application.Services.Interfaces;

namespace LargeFileGeneratorAndSorter.Application.Services.Implementation;

public class FileGenerateService(ICustomStringWriterService customStringWriterService) : IFileGenerateService
{
    private readonly ICustomStringWriterService _customStringWriterService = customStringWriterService;
    
    public async Task Generate(string destinationPath, long maxFileSize, long stringLength)
    {
        try
        {
            long totalSize = 0;

            var destinationDir = Path.GetDirectoryName(destinationPath);
        
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
        
            await using var stream = new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);
            await using var streamWriter = new StreamWriter(stream);
        
            while (totalSize <= maxFileSize)
            {
                var list = GenerateBatchRandomStrings(stringLength);
                var str = string.Join("\n", list);
            
                totalSize += await _customStringWriterService.WriteString(stream, streamWriter, str, Encoding.UTF8);
            }
        
            await streamWriter.FlushAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private List<string> GenerateBatchRandomStrings(long length = 1000)
    {
        var result = new List<string>();

        for (int i = 0; i < length; i++)
        {
            var str = $"{GenerateRandomInt()}.{GenerateRandomString()}";
            
            result.Add(str);
        }

        return result;
    }

    private int GenerateRandomInt()
    {
        var rnd = new Random();
        
        return rnd.Next(1, 1000000);
    }

    private string GenerateRandomString(int length = 10)
    {
        var rnd = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }
}