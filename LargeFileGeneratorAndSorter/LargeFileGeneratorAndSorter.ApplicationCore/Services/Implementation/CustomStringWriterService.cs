using System.Text;
using LargeFileGeneratorAndSorter.Application.Services.Interfaces;

namespace LargeFileGeneratorAndSorter.Application.Services.Implementation;

public class CustomStringWriterService : ICustomStringWriterService
{
    public async Task<long> WriteString(string fullPath, string stringToWrite, Encoding encoding)
    {
        await using var stream = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.Write);
        var charBuffer = encoding.GetBytes(stringToWrite);
        
        await stream.WriteAsync(charBuffer, 0, charBuffer.Length);
        await using var streamWriter = new StreamWriter(stream);
        
        await streamWriter.WriteLineAsync();

        await streamWriter.FlushAsync();

        return charBuffer.Length;
    }

    public async Task WriteBytes(string fullPath, List<byte> bytes, Encoding encoding)
    {
        await using var file = File.Create(fullPath);
        
        await file.WriteAsync(bytes.ToArray());
        
        file.Close();
    }

    public async Task<long> WriteString(Stream stream, StreamWriter streamWriter, string stringToWrite, Encoding encoding)
    {
        var charBuffer = encoding.GetBytes(stringToWrite);
        
        await stream.WriteAsync(charBuffer, 0, charBuffer.Length);
        
        await streamWriter.WriteLineAsync();

        return charBuffer.Length;
    }
}