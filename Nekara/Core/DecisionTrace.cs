using System;
using System.Linq;

namespace Nekara.Core
{
    public class DecisionTrace
    {
        public int currentTask;
        public int chosenTask;
        public (int, int)[] tasks;
        public DecisionTrace(int currentTask, int chosenTask, (int, int)[] tasks)
        {
            this.currentTask = currentTask;
            this.chosenTask = chosenTask;
            this.tasks = tasks;
        }

        public override string ToString()
        {
            return currentTask.ToString() + "," + chosenTask.ToString() + "," + String.Join(";", tasks.Select(tup => tup.Item1.ToString() + ":" + tup.Item2.ToString()));
        }

        public string ToReadableString()
        {
            return "Picked Task " + chosenTask.ToString() + " from [ " + String.Join(", ", tasks.Select(tup => tup.Item1.ToString() + (tup.Item2 > -1 ? " |" + tup.Item2.ToString() : ""))) + " ]";
        }

        public static DecisionTrace FromString(string line)
        {
            var cols = line.Split(',');
            int currentTask = Int32.Parse(cols[0]);
            int chosenTask = Int32.Parse(cols[1]);
            (int, int)[] tasks = cols[2].Split(';').Select(t => t.Split(':')).Select(t => (Int32.Parse(t[0]), Int32.Parse(t[1]))).ToArray();
            return new DecisionTrace(currentTask, chosenTask, tasks);
        }

        public override bool Equals(object obj)
        {
            if (obj is DecisionTrace)
            {
                var other = (DecisionTrace)obj;
                var myTasks = tasks.OrderBy(tup => tup.Item1).ToArray();
                var otherTasks = other.tasks.OrderBy(tup => tup.Item1).ToArray();

                bool match = (myTasks.Count() == otherTasks.Count())
                    && currentTask == other.currentTask
                    && chosenTask == other.chosenTask
                    && myTasks.Select((tup, i) => otherTasks[i].Item1 == tup.Item1 && otherTasks[i].Item2 == tup.Item2).Aggregate(true, (acc, b) => acc && b);

                return match;
            }
            return false;
        }

        public static bool operator ==(DecisionTrace t1, DecisionTrace t2)
        {
            return t1.Equals(t2);
        }

        public static bool operator !=(DecisionTrace t1, DecisionTrace t2)
        {
            return !t1.Equals(t2);
        }
    }
}
