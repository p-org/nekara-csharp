using System;
using System.Linq;

namespace Nekara.Core
{
    public enum DecisionType { ContextSwitch, CreateNondetBool, CreateNondetInteger }

    public class DecisionTrace
    {
        public DecisionType decisionType;
        public int decisionValue;           // chosen task ID or generated random value
        public int currentTask;
        public (int, int[])[] tasks;

        public DecisionTrace(DecisionType decisionType, int decisionValue, int currentTask, (int, int[])[] tasks)
        {
            this.decisionType = decisionType;
            this.decisionValue = decisionValue;
            this.currentTask = currentTask;
            this.tasks = tasks;
        }

        public DecisionTrace(DecisionType decisionType, bool decisionValue, int currentTask, (int, int[])[] tasks) : this(decisionType, decisionValue ? 1 : 0, currentTask, tasks) { }

        public string Type { get
            {
                switch (this.decisionType)
                {
                    case DecisionType.ContextSwitch: return "ContextSwitch";
                    case DecisionType.CreateNondetBool: return "CreateNondetBool";
                    case DecisionType.CreateNondetInteger: return "CreateNondetInteger";
                    default: throw new Exception("Unknown Decision Type");
                }
            } }

        public string Value { get
            {
                return this.decisionType == DecisionType.CreateNondetBool ? (this.decisionValue == 0 ? "False" : "True") : this.decisionValue.ToString();
            } }

        public override string ToString()
        {
            return decisionType + "," + decisionValue.ToString() + "," +  currentTask.ToString() + "," + String.Join(";", tasks.Select(tup => tup.Item1.ToString() + ":" + string.Join(".", tup.Item2)));
        }

        public string ToReadableString()
        {
            return "[" + String.Join(", ", tasks.Select(tup => (tup.Item1 == currentTask ? "*" : "") + tup.Item1.ToString() + (tup.Item2.Length > 0 ? " |" + string.Join(",", tup.Item2) +"|" : ""))) + "]\t" + this.Type + " -> " + this.Value;
        }

        public static DecisionTrace FromString(string line)
        {
            var cols = line.Split(',');
            DecisionType decisionType = (DecisionType)Enum.Parse(typeof(DecisionType), cols[0]);
            int decisionValue = Int32.Parse(cols[1]);
            int currentTask = Int32.Parse(cols[2]);
            (int, int[])[] tasks = cols[3].Split(';')
                .Select(t => t.Split(':'))
                .Select(t => (Int32.Parse(t[0]), t[1].Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Select(r => Int32.Parse(r)).ToArray()))
                .ToArray();
            return new DecisionTrace(decisionType, decisionValue, currentTask, tasks);
        }

        public override bool Equals(object obj)
        {
            if (obj is DecisionTrace)
            {
                var other = (DecisionTrace)obj;
                var myTasks = tasks.Select(tup => (tup.Item1, tup.Item2.OrderBy(val => val).ToArray())).OrderBy(tup => tup.Item1).ToArray();
                var otherTasks = other.tasks.Select(tup => (tup.Item1, tup.Item2.OrderBy(val => val).ToArray())).OrderBy(tup => tup.Item1).ToArray();

                bool match = (myTasks.Count() == otherTasks.Count())
                    && decisionType == other.decisionType
                    && decisionValue == other.decisionValue
                    && currentTask == other.currentTask
                    && myTasks.Select((tup, i) => otherTasks[i].Item1 == tup.Item1 
                        && tup.Item2.Length == otherTasks[i].Item2.Length
                        && tup.Item2.Select((val, j) => otherTasks[i].Item2[j] == val).Aggregate(true, (acc, b) => acc && b)
                        ).Aggregate(true, (acc, b) => acc && b);

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
