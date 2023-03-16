using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;

internal class Program
{
    static object lockObject = new object();
    static EventWaitHandle eventWaitHandle = new(false, EventResetMode.ManualReset);
    static EventWaitHandle eventWaitHandleCountDown = new(false, EventResetMode.ManualReset);


    static int threadCounter = 0;
    static int requestCounterSuccess = 0;
    static int requestCounterFail = 0;

    static StringBuilder log = new StringBuilder();

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Enter Thread Count:");
        bool bResult = int.TryParse(Console.ReadLine(), out int threadCount);
        if (!bResult)
        {
            Console.WriteLine("Please Correct Enter Thread Count!");
            return;
        }

        new Thread(delegate ()
        {
            eventWaitHandleCountDown.WaitOne();
            Console.WriteLine($"Request Success Count:{requestCounterSuccess}");
            Console.WriteLine($"Request Faild Count:{requestCounterFail}");

            lock (lockObject)
            {
                File.WriteAllText($"response.txt", log.ToString());
            }

            Process process = Process.GetCurrentProcess();
            Console.WriteLine($"  Private memory         :    {process.PrivateMemorySize64}");
            Console.WriteLine($"  Physical memory usage     : {process.WorkingSet64}");
            Console.WriteLine($"  Base priority             : {process.BasePriority}");
            Console.WriteLine($"  Priority class            : {process.PriorityClass}");
            Console.WriteLine($"  User processor time       : {process.UserProcessorTime}");
            Console.WriteLine($"  Privileged processor time : {process.PrivilegedProcessorTime}");
            Console.WriteLine($"  Total processor time      : {process.TotalProcessorTime}");
            Console.WriteLine($"  Paged system memory size  : {process.PagedSystemMemorySize64}");
            Console.WriteLine($"  Paged memory size         : {process.PagedMemorySize64}");
        }).Start();



        for (int i = 1; i <= threadCount; i++)
        {
            new Thread(async delegate (object? obj)
            {
                int incValue = Interlocked.Increment(ref threadCounter);

                int iThreadCount = Interlocked.CompareExchange(ref threadCount, 0, 0);
                if (incValue == iThreadCount)
                {
                    Console.WriteLine("All Thread Waits...");
                    Console.WriteLine("Please Enter to start requests....");
                }


                eventWaitHandle.WaitOne();

                try
                {
                    var response = await DoRequest();
                    lock (lockObject)
                    {
                        log.AppendLine(response);
                        log.AppendLine("---------------------------------------------");
                    }

                    /*
                    try
                    {
                        if (Monitor.TryEnter(lockObject))
                            File.WriteAllText($"response-{incValue}.txt", response);
                    }
                    finally
                    {
                        if (Monitor.IsEntered(lockObject))
                            Monitor.Exit(lockObject);
                    }
                    */
                }
                catch (Exception ex)
                {
                    log.AppendLine(ex.Message);
                    log.AppendLine(ex.StackTrace);
                    log.AppendLine("---------------------------------------------");
                }


                int decrementedValue = Interlocked.Decrement(ref threadCounter);
                if (decrementedValue == 0)
                    eventWaitHandleCountDown.Set();


                Console.Title = $"Thread executed {obj?.ToString()}";
            }, 100000000).Start(i);


            Console.Title = $"Memory Usage:{(Process.GetCurrentProcess().PrivateMemorySize64 / 1024) / 1024}";
        }

        Console.ReadKey();

        eventWaitHandle.Set();

        Console.ReadKey();
    }


    static async Task<string> DoRequest()
    {
        var response = HttpGet("https://api.github.com/orgs/dotnet/repos");
        if (response.IsSuccessStatusCode)
            Interlocked.Increment(ref requestCounterSuccess);
        else
            Interlocked.Increment(ref requestCounterFail);

        return await response.Content.ReadAsStringAsync();
    }


    static HttpResponseMessage HttpPost(string requestUrl, object requestBody)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Add("User-Agent", ".NET Core App");

        var json = JsonSerializer.Serialize(requestBody);
        var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

        return httpClient.PostAsync(requestUrl, stringContent).Result;
    }

    static HttpResponseMessage HttpGet(string requestUrl)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Add("User-Agent", ".NET Core App");
        return httpClient.GetAsync(requestUrl).Result;
    }

}
