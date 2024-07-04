namespace LargeFileGeneratorAndSorter.Application.Services.Interfaces;

public interface ISorterService
{
    public Task SortLargeFileData(string fileDestinationPath, string sortedFilePath, string chunkDir, long chunkSize);
}