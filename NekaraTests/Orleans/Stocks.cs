using System;
using System.Net.Http;
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
    class Stocks
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

    public interface IStockGrain : IGrainWithStringKey
    {
        NativeTasks.Task<string> GetPrice();
    }

    public class StockGrain : Grain, IStockGrain
    {
        private static ITestingService nekara = RuntimeEnvironment.Client.Api;

        // request api key from here https://www.alphavantage.co/support/#api-key
        private const string ApiKey = "demo";
        string price;
        string graphData;

        public override async NativeTasks.Task OnActivateAsync()
        {
            this.GetPrimaryKey(out var stock);
            await UpdatePrice(stock);

            RegisterTimer(
                UpdatePrice,
                stock,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));

            await base.OnActivateAsync();
        }

        async NativeTasks.Task UpdatePrice(object stock)
        {
            // collect the task variables without awaiting
            var priceTask = GetPriceQuote(stock as string);
            var graphDataTask = GetDailySeries(stock as string);

            // await both tasks
            await NativeTasks.Task.WhenAll(priceTask, graphDataTask);

            // read the results
            price = priceTask.Result;
            graphData = graphDataTask.Result;
            Console.WriteLine(price);
        }

        async NativeTasks.Task<string> GetPriceQuote(string stock)
        {
            var uri = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={stock}&apikey={ApiKey}&datatype=csv";
            using (var http = new HttpClient())
            using (var resp = await http.GetAsync(uri))
            {
                return await resp.Content.ReadAsStringAsync();
            }
        }

        async NativeTasks.Task<string> GetDailySeries(string stock)
        {
            var uri = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={stock}&apikey={ApiKey}&datatype=csv";
            using (var http = new HttpClient())
            using (var resp = await http.GetAsync(uri))
            {
                return await resp.Content.ReadAsStringAsync();
            }
        }

        public NativeTasks.Task<string> GetPrice()
        {
            //return NativeTasks.Task.FromResult(price);
            var fakePrice = nekara.CreateNondetInteger(1000);
            return NativeTasks.Task.FromResult(fakePrice.ToString());
        }
    }
}
