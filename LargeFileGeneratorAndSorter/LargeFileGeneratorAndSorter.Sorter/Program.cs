using LargeFileGeneratorAndSorter.Application.Services.Interfaces;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    [Option("-p|--path", Description = "The path to txt file")]
    public string ResultFilePath { get; }
    
    private string DestinationDir => Path.GetDirectoryName(ResultFilePath);
    
    private string ResultsDir => Path.Combine(DestinationDir, "temp\\largeFile.txt");

    private string SortedFileDir => Path.Combine(DestinationDir, "temp\\largeSortedFile.txt");

    private string ChunksDir => Path.Combine(DestinationDir, "temp\\chunks");

    private long ChunkSize => 50485760; //10mb //1024 1kb

    private readonly ISorterService _sorterService;

    public Program()
    {
        ResultFilePath = $"{Environment.CurrentDirectory}..\\..\\..\\..\\..\\";;
        LargeFileGeneratorAndSorter.Sorter.InitialiseServices.Initialise();
        _sorterService = LargeFileGeneratorAndSorter.Sorter.InitialiseServices.ServiceProvider.GetRequiredService<ISorterService>();
    }
    
    public static void Main(string[] args)
        => CommandLineApplication.Execute<Program>(args);

    private async Task OnExecute()
    {
        if (string.IsNullOrEmpty(ResultFilePath))
        {
            throw new ArgumentException("The path to CSV file is not specified.");
        }
        
        await _sorterService.SortLargeFileData(ResultsDir, SortedFileDir, ChunksDir, ChunkSize);
    }
}