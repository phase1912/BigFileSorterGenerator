// See https://aka.ms/new-console-template for more information

using LargeFileGeneratorAndSorter.Application.Services.Interfaces;
using LargeFileGeneratorAndSorter.Generator;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    [Option("-p|--path", Description = "The path to txt file")]
    public string ResultFilePath { get; }
    
    private string DestinationDir => Path.GetDirectoryName(ResultFilePath);
    
    private string ResultsDir => Path.Combine(DestinationDir, "temp\\largeFile.txt");

    private long MaxFileSize => 2147483648; //2GB  //131072; //  1MB

    private long ChunkStringLength => 1000000;

    private readonly IFileGenerateService _fileGenerateService;

    public Program()
    {
        ResultFilePath = $"{Environment.CurrentDirectory}..\\..\\..\\..\\..\\";
        InitialiseServices.Initialise();
        _fileGenerateService = InitialiseServices.ServiceProvider.GetRequiredService<IFileGenerateService>();
    }
    
    public static void Main(string[] args)
        => CommandLineApplication.Execute<Program>(args);

    private async Task OnExecute()
    {
        if (string.IsNullOrEmpty(ResultFilePath))
        {
            throw new ArgumentException("The path to CSV file is not specified.");
        }
        
        await _fileGenerateService.Generate(ResultsDir, MaxFileSize, ChunkStringLength);
    }
}