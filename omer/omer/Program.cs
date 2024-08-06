using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

class Program
{
    private static readonly string filePath = "/home/kuzu/Desktop/output.txt";
    private static readonly int numberOfWrites = 100000;
    private static readonly int numberOfThreads = 10;

    private static readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
    private static readonly ManualResetEventSlim doneEvent = new ManualResetEventSlim(false);

    static void Main()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var stopwatch = Stopwatch.StartNew();

        // Start worker tasks
        var workers = new Task[numberOfThreads];
        for (int i = 0; i < numberOfThreads; i++)
        {
            int threadIndex = i; // Capture the loop variable correctly
            workers[i] = Task.Run(() => Worker(threadIndex));
        }

        // Start file writer task
        Task fileWriter = Task.Run(() => FileWriter());

        // Wait for all worker tasks to complete
        Task.WaitAll(workers);

        // Signal that no more items will be added to the queue
        doneEvent.Set();

        // Wait for file writer task to complete
        fileWriter.Wait();

        stopwatch.Stop();
        Console.WriteLine($"Total execution time: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine("All threads have finished executing. Timestamp written to file.");
    }

    static void Worker(int threadIndex)
    {
        for (int i = 0; i < numberOfWrites; i++)
        {
            long threadId = AppDomain.GetCurrentThreadId();
            logQueue.Enqueue($"Thread {threadIndex} :: {threadId} - Write {i + 1} - : {DateTime.Now}\n");
        }

        Console.WriteLine($"Thread {threadIndex} completed its work.");
    }

    static void FileWriter()
    {
        using (StreamWriter writer = new StreamWriter(filePath, append: true))
        {
            while (true)
            {
                // Process items from the queue
                if (logQueue.TryDequeue(out var logMessage))
                {
                    try
                    {
                        writer.Write(logMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception caught: {ex.Message}");
                    }
                }
                else if (doneEvent.IsSet)
                {
                    // Exit if no more items are expected and queue is empty
                    break;
                }
                else
                {
                    // Wait briefly before retrying to avoid busy-waiting
                    Thread.Sleep(10);
                }
            }
        }
    }
}
