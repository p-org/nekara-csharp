using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Orleans;
using Orleans.Hosting;
using System.Collections.Generic;

namespace Nekara.Tests.Orleans
{
    class NetworkLogger
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(LoggerGrain), typeof(ReporterGrain));
            Console.WriteLine("\n... Orleans Silo and Client setup completed");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("\n... Orleans Silo and Client teardown completed");
        }

        public static int N = 3;

        [TestMethod(3000000, 1000)]
        public static Task Run()
        {
            var logger = client.GetGrain<ILoggerGrain>(2);
            logger.Reset().Wait();

            var reporter0 = client.GetGrain<IReporterGrain>(0);
            var reporter1 = client.GetGrain<IReporterGrain>(1);

            var t0 = reporter0.Report(N, "0");
            var t1 = reporter1.Report(N, "1");

            return Nekara.Models.Task.Run(()=> Task.WhenAll(t0, t1)).InnerTask;
        }
    }

    public interface ILoggerGrain : IGrainWithIntegerKey
    {
        Task Reset();
        
        Task Append(string entry);
    }

    public interface IReporterGrain : IGrainWithIntegerKey
    {
        Task Report(int count, string entry);
    }

    public class LoggerGrain : Grain, ILoggerGrain
    {
        public string log = "";

        public Task Reset()
        {
            log = "";
            return Task.CompletedTask;
        }

        public Task Append(string entry)
        {
            Console.WriteLine("LoggerGrain.Append({0})", entry);
            
            log = log + entry;
            Console.WriteLine(log);

            return Task.CompletedTask;
        }
    }

    public class ReporterGrain : Grain, IReporterGrain
    {
        public Task Report(int count, string entry)
        {
            Console.WriteLine("ReporterGrain.Report({0}, {1})", count, entry);
            var logger = NetworkLogger.client.GetGrain<ILoggerGrain>(2);

            var requests = new List<Task>();

            for (int i = 0; i < count; i++)
            {
                requests.Add(logger.Append(entry));
            }

            return Task.WhenAll(requests);
        }
    }
}