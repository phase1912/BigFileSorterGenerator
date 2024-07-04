using System.Text;

namespace LargeFileGeneratorAndSorter.Application.Services.Interfaces;

public interface ICustomStringWriterService
{
    Task<long> WriteString(string fullPath, string stringToWrite, Encoding encoding);
    
    Task WriteBytes(string fullPath, List<byte> bytes, Encoding encoding);
    
    Task<long> WriteString(Stream stream, StreamWriter streamWriter, string stringToWrite, Encoding encoding);
}