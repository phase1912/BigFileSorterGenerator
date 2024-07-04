using System.Text;
using LargeFileGeneratorAndSorter.Application.Models;
using LargeFileGeneratorAndSorter.Application.Services.Interfaces;

namespace LargeFileGeneratorAndSorter.Application.Services.Implementation;

public class SorterService(ICustomStringWriterService customStringWriterService) : ISorterService
{
    private readonly ICustomStringWriterService _customStringWriterService = customStringWriterService;

    public async Task SortLargeFileData(string fileDestinationPath, string sortedFilePath, string chunkDir, long chunkSize)
    {
        try
        {
            var chunkFilesNames =
                await DivideNotSortedFileBySortedChunks(fileDestinationPath, chunkDir, chunkSize);

            await MergeFiles(sortedFilePath, chunkFilesNames, chunkDir, chunkSize);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task<List<string>> DivideNotSortedFileBySortedChunks(string fileDestinationPath, string chunkDir, long chunkSize)
    {
        await using var stream = new FileStream(fileDestinationPath, FileMode.OpenOrCreate, FileAccess.Read);
        using var streamReader = new StreamReader(stream);
        
        if (!Directory.Exists(chunkDir))
        {
            Directory.CreateDirectory(chunkDir);
        }
        
        string? line;
        long countOfLines = 0;
        var lines = new List<string>();
        long prevPosition = 0;
        var files = new List<string>();
        
        while ((line = await streamReader.ReadLineAsync()) is not null)
        {
            countOfLines++;
            lines.Add(line);
            prevPosition += Encoding.UTF8.GetBytes(line).Length; //line.Length * sizeof(char);
        
            if (prevPosition > chunkSize)
            {
                SortList(lines);
        
                await WriteChunkFile(chunkDir, $"{countOfLines}.txt", string.Join("\n", lines), files);
                
                prevPosition = 0;
                lines.Clear();
            }
        }

        return files;
    }

    private async Task MergeFiles(string sortedFilePath, List<string> chunkFilesNames, string chunkDir, long chunkSize)
    {
        var chunkFiles = chunkFilesNames.Select(x => new ChunkFileInfoModel { FullPath = x }).ToList();
        
        while (chunkFiles.Count(x => !x.IsProcessed) > 2)
        {
            var tasks = new List<Task>();

            var tasksCount = chunkFiles.Count(x => x is { IsProcessed: false, IsTaken: false });

            if (tasksCount > 40)
            {
                tasksCount = 40;
            }
            
            for (var i = 0; i < tasksCount / 2; i++)
            {
                var twoChunks = chunkFiles.Where(x => x is { IsProcessed: false, IsTaken: false }).Take(2).ToList();
                twoChunks[0].IsTaken = true;
                twoChunks[1].IsTaken = true;

                var task = MergeTwoFiles(chunkFiles, twoChunks, chunkDir);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
        }
        
        var twoChunksFinal = chunkFiles.Where(x => !x.IsProcessed).Take(2).ToList();
        
        var finalChunks = chunkFiles.Where(x => !x.IsProcessed).Take(2).ToList();
        finalChunks[0].IsTaken = true;
        finalChunks[1].IsTaken = true;
        await MergeTwoFiles(twoChunksFinal, finalChunks, chunkDir, sortedFilePath);
    }

    private async Task MergeTwoFiles(List<ChunkFileInfoModel> chunkFiles, List<ChunkFileInfoModel> twoChunks, string chunkDir, string? sortedFilePath = null)
    {
        var isFirstFileProcessed = false;
        var isSecondFileProcessed = false;
        long countOfLinesFirsFile = 0;
        long countOfLinesSecondFile = 0;
        string? firstLine = null;
        string? secondLine = null;

        await using var firstChunkStream = new FileStream(twoChunks[0].FullPath, FileMode.Open, FileAccess.Read);
        using var firstChunkStreamReader = new StreamReader(firstChunkStream);
        await using var secondChunkStream = new FileStream(twoChunks[1].FullPath, FileMode.Open, FileAccess.Read);
        using var secondChunkStreamReader = new StreamReader(secondChunkStream);
        var newFileName = Path.Combine(chunkDir, $"{Guid.NewGuid()}.txt");
        await using var thirdStream = new FileStream(sortedFilePath ?? newFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        await using var thirdStreamWriter = new StreamWriter(thirdStream);
            
        while (!isFirstFileProcessed || !isSecondFileProcessed)
        {
            var compare = 0;
            if (countOfLinesFirsFile == 0 && countOfLinesSecondFile == 0)
            {
                firstLine = await firstChunkStreamReader.ReadLineAsync();
                secondLine = await secondChunkStreamReader.ReadLineAsync();
                compare = CompareTwoStrings(firstLine, secondLine);
                countOfLinesFirsFile++;
                countOfLinesSecondFile++;
            }
            else if(firstLine == null)
            {
                compare = 1;
            }
            else if(secondLine == null)
            {
                compare = -1;
            }
            else
            {
                compare = CompareTwoStrings(firstLine, secondLine);
            }

            var moveType = MoveByMerge.None;
            if (compare < 0)
            {
                await thirdStreamWriter.WriteLineAsync(firstLine);
                moveType = MoveByMerge.MoveFirst;
            }
            else if (compare > 0)
            {
                await thirdStreamWriter.WriteLineAsync(secondLine);
                moveType = MoveByMerge.MoveSecond;
            }
            else
            {
                await thirdStreamWriter.WriteLineAsync(firstLine);
                await thirdStreamWriter.WriteLineAsync(secondLine);
                moveType = MoveByMerge.MoveBoth;
            }

            switch (moveType)
            {
                case MoveByMerge.MoveFirst:
                    firstLine = await firstChunkStreamReader.ReadLineAsync();
                    countOfLinesFirsFile++;
                    break;
                case MoveByMerge.MoveSecond:
                    secondLine = await secondChunkStreamReader.ReadLineAsync();
                    countOfLinesSecondFile++;
                    break;
                case MoveByMerge.MoveBoth:
                    firstLine = await firstChunkStreamReader.ReadLineAsync();
                    secondLine = await secondChunkStreamReader.ReadLineAsync();
                    countOfLinesFirsFile++;
                    countOfLinesSecondFile++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (firstLine == null)
            {
                isFirstFileProcessed = true;
            }
                
            if (secondLine == null)
            {
                isSecondFileProcessed = true;
            }
        }

        twoChunks[0].IsProcessed = true;
        twoChunks[1].IsProcessed = true;
            
        await thirdStreamWriter.FlushAsync();
        await firstChunkStream.DisposeAsync();
        await secondChunkStream.DisposeAsync();
        
        File.Delete(twoChunks[0].FullPath);
        File.Delete(twoChunks[1].FullPath);

        if (sortedFilePath == null)
        {
            chunkFiles.Add(new ChunkFileInfoModel { FullPath = newFileName });
        }
    }

    private void SortList(List<string> notSortedList)
    {
        notSortedList.Sort(CompareTwoStrings);
    }

    private async Task WriteChunkFile(string chunkFileDir, string chunkName, string linesStr, List<string> files)
    {
        var fullPath = Path.Combine(chunkFileDir, chunkName);
        
        await _customStringWriterService.WriteString(fullPath, linesStr, Encoding.UTF8);
        
        files.Add(fullPath);
    }

    private int CompareTwoStrings(string a, string b)
    {
        var (number1, line1) = SplitLine(a);
        var (number2, line2) = SplitLine(b);
        
        int comparison = String.Compare(line1, line2, comparisonType: StringComparison.OrdinalIgnoreCase);

        if (comparison < 0)
            return -1;
        else if (comparison > 0)
            return 1;
        else
        {
            if (number1 < number2)
            {
                return -1;
            }
            else if(number1 > number2)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }

    private (long, string) SplitLine(string str)
    {
        if (str.Contains('.'))
        {
            var split = str.Split('.');
            long.TryParse(split[0], out var number);

            return (number, split[1]);
        }
        else
        {
            return (0, str);
        }
    }
    
    // old code
    
    // private async Task ReadChunk(long chunkSize, Stream stream, List<byte> extraBuffer)
    // {
    //     long prevPosition = 0;
    //     var maxSize = (int)chunkSize / 10;
    //     var buffer = new byte[maxSize];
    //     while (prevPosition <= chunkSize)
    //     {
    //         //var offset = stream.Position;
    //         prevPosition += await stream.ReadAsync(buffer);
    //         if (prevPosition == 0)
    //         {
    //             break;
    //         }
    //         
    //         extraBuffer.AddRange(buffer);
    //     }
    //
    //     var extraByte = new byte();
    //     if (stream.Length - stream.Position < chunkSize && stream.Position < stream.Length)
    //     {
    //         while (true)
    //         {
    //             var flag = stream.ReadByte();
    //             if (flag == -1)
    //             {
    //                 break;
    //             }
    //             extraByte = (byte)flag;
    //             extraBuffer.Add(extraByte);
    //         }
    //     }
    //
    //     extraByte = buffer[maxSize - 1];
    //     while (extraByte != '\n')
    //     {
    //         var flag = stream.ReadByte();
    //         if (flag == -1)
    //         {
    //             break;
    //         }
    //         extraByte = (byte)flag;
    //         extraBuffer.Add(extraByte);
    //     }
    // }
    
    // private async Task WriteChunkFileBytes(string chunkFileDir, string chunkName, List<byte> bytes, List<string> files)
    // {
    //     var fullPath = Path.Combine(chunkFileDir, chunkName);
    //     
    //     await _customStringWriterService.WriteBytes(fullPath, bytes, Encoding.UTF8);
    //     
    //     files.Add(fullPath);
    // }
    
    //     private async Task<List<string>> DivideNotSortedFileBySortedChunks(string fileDestinationPath, string chunkDir, long chunkSize)
    // {
    //     await using var stream = new FileStream(fileDestinationPath, FileMode.OpenOrCreate, FileAccess.Read);
    //     using var streamReader = new StreamReader(stream);
    //     
    //     if (!Directory.Exists(chunkDir))
    //     {
    //         Directory.CreateDirectory(chunkDir);
    //     }
    //     
    //     string? line;
    //     long countOfLines = 0;
    //     var lines = new List<string>();
    //     long prevPosition = 0;
    //     var files = new List<string>();
    //    
    //     var tasks = new List<Task>();
    //     var extraBuffers = new List<List<byte>>();
    //
    //     while (stream.Position < stream.Length)
    //     {
    //         var extraBuffer = new List<byte>();
    //         await ReadChunk(chunkSize, stream, extraBuffer);
    //         extraBuffers.Add(extraBuffer);
    //
    //         if (extraBuffers.Count >= 1)
    //         {
    //             //foreach (var i in extraBuffers)
    //             //{
    //               //  prevPosition++;
    //                 var task = SortAndWrite(extraBuffer, files, stream.Position, chunkDir);
    //                 tasks.Add(task);
    //            // }
    //             
    //             await Task.WhenAll(tasks);
    //             tasks.Clear();
    //             extraBuffers.Clear();
    //             //prevPosition = 0;
    //         }
    //
    //         //extraBuffer.Clear();
    //     }
    //     
    //     // while ((line = await streamReader.ReadLineAsync()) is not null)
    //     // {
    //     //     countOfLines++;
    //     //     lines.Add(line);
    //     //     prevPosition += Encoding.UTF8.GetBytes(line).Length; //line.Length * sizeof(char);
    //     //
    //     //     if (prevPosition > chunkSize)
    //     //     {
    //     //         SortList(lines);
    //     //
    //     //         await WriteChunkFile(chunkDir, $"{countOfLines}.txt", string.Join("\n", lines), files);
    //     //         
    //     //         prevPosition = 0;
    //     //         lines.Clear();
    //     //     }
    //     // }
    //
    //     return files;
    // }
    
    // private async Task SortAndWrite(List<byte> extraBuffer, List<string> files, long position, string chunkDir)
    // {
    //     var lines = Encoding.UTF8.GetString(extraBuffer.ToArray()).Split('\n').ToList();
    //         
    //     SortList(lines);
    //
    //     extraBuffer = Encoding.UTF8.GetBytes(String.Join(Environment.NewLine, lines)).ToList();
    //         
    //     await WriteChunkFileBytes(chunkDir, $"{position}.txt", extraBuffer, files);
    //         
    //     extraBuffer.Clear();
    //     lines.Clear();
    // }
    
    //  private async Task MergeTwoFilesReadByChunk(List<ChunkFileInfoModel> chunkFiles, List<ChunkFileInfoModel> twoChunks, string chunkDir, long chunkSize, string? sortedFilePath = null)
    // {
    //     var isFirstFileProcessed = false;
    //     var isSecondFileProcessed = false;
    //     long countOfLinesFirsFile = 0;
    //     long countOfLinesSecondFile = 0;
    //     string? firstLine = null;
    //     string? secondLine = null;
    //     List<string> lines1 = new List<string>();
    //     List<string> lines2 = new List<string>();
    //     int index1 = 0;
    //     int index2 = 0;
    //     var compare = 0;
    //
    //     await using var firstChunkStream = new FileStream(twoChunks[0].FullPath, FileMode.Open, FileAccess.Read);
    //     using var firstChunkStreamReader = new StreamReader(firstChunkStream);
    //     await using var secondChunkStream = new FileStream(twoChunks[1].FullPath, FileMode.Open, FileAccess.Read);
    //     using var secondChunkStreamReader = new StreamReader(secondChunkStream);
    //     var newFileName = Path.Combine(chunkDir, $"{Guid.NewGuid()}.txt");
    //     await using var thirdStream = new FileStream(sortedFilePath ?? newFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    //     await using var thirdStreamWriter = new StreamWriter(thirdStream);
    //         
    //     while (!isFirstFileProcessed || !isSecondFileProcessed)
    //     {
    //         var extraBuffer = new List<byte>();
    //
    //         if (index1 >= lines1.Count)
    //         {
    //             await ReadChunk(chunkSize, firstChunkStream, extraBuffer);
    //             lines1 = Encoding.UTF8.GetString(extraBuffer.ToArray()).Split('\n').ToList();
    //             SortList(lines1);
    //             index1 = 0;
    //             extraBuffer.Clear();
    //         }
    //         
    //         if (index2 >= lines2.Count)
    //         {
    //             await ReadChunk(chunkSize, secondChunkStream, extraBuffer);
    //             lines2 = Encoding.UTF8.GetString(extraBuffer.ToArray()).Split('\n').ToList();
    //             SortList(lines2);
    //             index2 = 0;
    //             extraBuffer.Clear();
    //         }
    //         
    //         if (countOfLinesFirsFile == 0 && countOfLinesSecondFile == 0)
    //         {
    //             await ReadChunk(chunkSize, firstChunkStream, extraBuffer);
    //             lines1 = Encoding.UTF8.GetString(extraBuffer.ToArray()).Split('\n').ToList();
    //             SortList(lines1);
    //             extraBuffer.Clear();
    //             
    //             await ReadChunk(chunkSize, secondChunkStream, extraBuffer);
    //             lines2 = Encoding.UTF8.GetString(extraBuffer.ToArray()).Split('\n').ToList();
    //             SortList(lines2);
    //             extraBuffer.Clear();
    //             
    //             firstLine = lines1[index1];
    //             index1++;
    //             secondLine = lines2[index2];
    //             index2++;
    //             compare = CompareTwoStrings(firstLine, secondLine);
    //             countOfLinesFirsFile++;
    //             countOfLinesSecondFile++;
    //         }
    //         else if(firstLine == null)
    //         {
    //             compare = 1;
    //         }
    //         else if(secondLine == null)
    //         {
    //             compare = -1;
    //         }
    //         else
    //         {
    //             compare = CompareTwoStrings(firstLine, secondLine);
    //         }
    //
    //         var moveType = MoveByMerge.None;
    //         if (compare < 0)
    //         {
    //             await thirdStreamWriter.WriteLineAsync(firstLine);
    //             moveType = MoveByMerge.MoveFirst;
    //         }
    //         else if (compare > 0)
    //         {
    //             await thirdStreamWriter.WriteLineAsync(secondLine);
    //             moveType = MoveByMerge.MoveSecond;
    //         }
    //         else
    //         {
    //             await thirdStreamWriter.WriteLineAsync(firstLine);
    //             await thirdStreamWriter.WriteLineAsync(secondLine);
    //             moveType = MoveByMerge.MoveBoth;
    //         }
    //
    //         switch (moveType)
    //         {
    //             case MoveByMerge.MoveFirst:
    //                 firstLine = lines1[index1];
    //                 index1++;
    //                 //firstLine = await firstChunkStreamReader.ReadLineAsync();
    //                 countOfLinesFirsFile++;
    //                 break;
    //             case MoveByMerge.MoveSecond:
    //                 //secondLine = await secondChunkStreamReader.ReadLineAsync();
    //                 secondLine = lines2[index2];
    //                 index2++;
    //                 countOfLinesSecondFile++;
    //                 break;
    //             //case MoveByMerge.None:
    //             case MoveByMerge.MoveBoth:
    //                 firstLine = lines1[index1];
    //                 index1++;
    //                 secondLine = lines2[index2];
    //                 index2++;
    //                 //firstLine = await firstChunkStreamReader.ReadLineAsync();
    //                // secondLine = await secondChunkStreamReader.ReadLineAsync();
    //                 countOfLinesFirsFile++;
    //                 countOfLinesSecondFile++;
    //                 break;
    //             default:
    //                 throw new ArgumentOutOfRangeException();
    //         }
    //
    //         if (firstLine == null)
    //         {
    //             isFirstFileProcessed = true;
    //         }
    //             
    //         if (secondLine == null)
    //         {
    //             isSecondFileProcessed = true;
    //         }
    //     }
    //
    //     twoChunks[0].IsProcessed = true;
    //     twoChunks[1].IsProcessed = true;
    //         
    //     await thirdStreamWriter.FlushAsync();
    //     await firstChunkStream.DisposeAsync();
    //     await secondChunkStream.DisposeAsync();
    //     
    //     File.Delete(twoChunks[0].FullPath);
    //     File.Delete(twoChunks[1].FullPath);
    //
    //     if (sortedFilePath == null)
    //     {
    //         chunkFiles.Add(new ChunkFileInfoModel { FullPath = newFileName });
    //     }
    // }
}