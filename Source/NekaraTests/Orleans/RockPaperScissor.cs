using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Orleans;
using Orleans.Hosting;


namespace Nekara.Tests.Orleans
{
    class RockPaperScissor
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(PlayerGrain));
            Console.WriteLine("Setup");
        }

        [TestTeardownMethod]
        public static void Teardown()
        {
            silo.StopAsync().Wait();
            Console.WriteLine("Teardown");
        }

        [TestMethod(30000, 1000)]
        public static Task Run()
        {
            var player0 = client.GetGrain<IPlayerGrain>(0);
            var player1 = client.GetGrain<IPlayerGrain>(1);
            var player2 = client.GetGrain<IPlayerGrain>(2);

            var t0 = player0.Play("Rock");
            var t1 = player1.Play("Paper");
            var t2 = player2.Play("Scissor");

            return Task.CompletedTask;
        }
    }

    public interface IPlayerGrain : IGrainWithIntegerKey
    {
        Task<string> Play(string token);
    }

    public class PlayerGrain : Grain, IPlayerGrain
    {
        private static string[] Choices = new[] { "Rock", "Paper", "Scissor" };
        private List<string> Seen = new List<string>();
        private Random random;

        public override Task OnActivateAsync()
        {
            int Id = (int)this.GetPrimaryKeyLong();
            Console.WriteLine("Player {0} activated", Id);
            random = new Random(Id);
            return base.OnActivateAsync();
        }

        public Task<string> Play(string token)
        {
            string answer;
            if (Seen.Count == 0)
            {
                answer = Choices[random.Next(0, 2)];
            }
            else
            {
                answer = Seen.Last();
            }
            Seen.Add(token);
            Console.WriteLine("Player {0} plays {1}\thistory: [{2}]", this.GetPrimaryKey(), answer, string.Join(", ", Seen));
            return Task.FromResult(answer);
        }
    }
}