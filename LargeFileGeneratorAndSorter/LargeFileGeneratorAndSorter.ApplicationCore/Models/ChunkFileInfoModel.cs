namespace LargeFileGeneratorAndSorter.Application.Models;

public class ChunkFileInfoModel
{
    public string FullPath { get; set; }
    
    public bool IsProcessed { get; set; }
    
    public bool IsTaken { get; set; }
}