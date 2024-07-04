using LargeFileGeneratorAndSorter.Application.Services.Implementation;
using LargeFileGeneratorAndSorter.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LargeFileGeneratorAndSorter.Application;

public static class InitialiseServices
{
    public static void Initialise(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IFileGenerateService, FileGenerateService>();
        serviceCollection.AddScoped<ISorterService, SorterService>();
        serviceCollection.AddScoped<ICustomStringWriterService, CustomStringWriterService>();
    }
}