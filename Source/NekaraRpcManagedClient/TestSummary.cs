using System;
using System.Collections.Generic;
using System.Text;

namespace Nekara.Client
{
    public class TestSummary
    {
        public double elapsedClient;
        public double elapsedServer;
        public int iterations;
        public int maxDecisionsReached;
        public int minDecisions;
        public double avgDecisions;
        public int maxDecisions;
        private object writeLock;

        public TestSummary()
        {
            writeLock = new object();
            elapsedClient = 0;
            elapsedServer = 0;
            iterations = 0;
            maxDecisionsReached = 0;
            minDecisions = int.MaxValue;
            avgDecisions = 0;
            maxDecisions = int.MinValue;
        }

        public void Update(int decisions, double elapsedServerSession, bool maxDecisionsReached)
        {
            lock (writeLock)
            {
                iterations++;
                if (decisions < minDecisions) minDecisions = decisions;
                if (decisions > maxDecisions) maxDecisions = decisions;
                avgDecisions = ((double)decisions + (iterations - 1) * avgDecisions) / iterations;

                if (maxDecisionsReached) this.maxDecisionsReached++;

                elapsedServer += elapsedServerSession;
            }
        }

        public override string ToString()
        {
            return $"{iterations} Iterations, {minDecisions} (min), {avgDecisions} (avg), {maxDecisions} (max) Decisions, {elapsedClient} elapsed (client), {elapsedServer} elapsed (server)";
        }
    }
}
