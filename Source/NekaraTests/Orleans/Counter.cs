using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;
using Orleans;
using Orleans.Hosting;
using System.Collections.Generic;

namespace Nekara.Tests.Orleans
{
    class Counter
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;
        public static ISiloHost silo;
        public static IClusterClient client;

        [TestSetupMethod]
        public static void Setup()
        {
            (silo, client) = TestPlatform.Setup(typeof(CounterGrain), typeof(EmitterGrain));
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
            var emitter0 = client.GetGrain<IEmitterGrain>(0);
            var emitter1 = client.GetGrain<IEmitterGrain>(1);

            var t0 = emitter0.Emit();
            var t1 = emitter1.Emit();

            var r1 = t0.Result;
            var r2 = t1.Result;

            Console.WriteLine("r1 = {0}, r2 = {1}", r1, r2);

            nekara.Assert(r1 < r2, "Emitter2 Went First");

            return Task.CompletedTask;
        }
    }

    public interface ICounterGrain : IGrainWithIntegerKey
    {
        Task<int> Increment();
    }

    public interface IEmitterGrain : IGrainWithIntegerKey
    {
        Task<int> Emit();
    }

    public class CounterGrain : Grain, ICounterGrain
    {
        public int count = 0;

        public Task<int> Increment()
        {
            Console.WriteLine("CounterGrain.Increment()");
            return Task.FromResult(++count);
        }
    }

    public class EmitterGrain : Grain, IEmitterGrain
    {
        public Task<int> Emit()
        {
            Console.WriteLine("EmitterGrain.Emit()");

            var counter = Counter.client.GetGrain<ICounterGrain>(2);

            return counter.Increment();
        }
    }
}