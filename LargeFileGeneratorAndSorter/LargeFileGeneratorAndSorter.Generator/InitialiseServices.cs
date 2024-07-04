using Microsoft.Extensions.DependencyInjection;

namespace LargeFileGeneratorAndSorter.Generator;

public static class InitialiseServices
{
    public static ServiceProvider ServiceProvider { get; set; }

    public static void Initialise()
    {
        var services = new ServiceCollection();
        
        Application.InitialiseServices.Initialise(services);

        ServiceProvider = services.BuildServiceProvider();
    }
}