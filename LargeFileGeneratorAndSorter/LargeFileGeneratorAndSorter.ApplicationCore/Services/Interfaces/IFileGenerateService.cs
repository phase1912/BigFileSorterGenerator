namespace LargeFileGeneratorAndSorter.Application.Services.Interfaces;

public interface IFileGenerateService
{
    public Task Generate(string destinationPath, long maxFileSize, long stringLength);
}