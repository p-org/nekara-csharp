using System;
using System.Collections.Generic;
using System.Text;
using NativeTasks = System.Threading.Tasks;
using Nekara.Core;
using Nekara.Client;
using Nekara.Models;
using Orleans;
using Orleans.Hosting;

namespace Nekara.Tests.Orleans
{
    class Account
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(StockGrain));
            Console.WriteLine("Setup");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("Teardown");
        }

        [TestMethod]
        public static async void Run()
        {
            var stockGrain = client.GetGrain<IStockGrain>("MSFT");

            await Task.Run(() => stockGrain.GetPrice());
            //var price = await stockGrain.GetPrice();
        }
    }

    public interface IAccountGrain : IGrainWithStringKey
    {
        NativeTasks.Task<string> GetPrice();
    }
}
