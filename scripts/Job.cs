using Godot;
using System;
using ComputeNodes;
using System.Linq;

namespace Job
{
    public abstract class Job<Tin, Tout>
    {
        public readonly int id;
        public readonly string title;
        public readonly string provider;
        public readonly string description;

        public readonly int difficulty;

        public JobStatus Status { get; protected set; }

        protected readonly Predicate<Tout> resultPredicate;
        protected readonly Func<Job<Tin, Tout>, Tin> inputProvider;

        public int ResultCount { get; protected set; }
        public int CorrectResultCount { get; protected set; }
        public int RunCount { get; protected set; }

        public Tout[] Results { get; protected set; }


        protected dynamic luaCode = new DynamicLua.DynamicLua();

        public readonly Payment<Tin, Tout>[] plan;

        public void Accept()
        {
            if (Status == JobStatus.Incoming)
            {
                Status = JobStatus.Pending;
            } else if (Status == JobStatus.IncomingContract)
            {
                Status = JobStatus.Contract;
            } else
            {
                throw new InvalidOperationException();
            }
        }

        public void SetScript(string code)
        {
            luaCode(code);
        }

        public void Update()
        {
            Tout result = (Tout) luaCode.update(inputProvider(this));
            RunCount++;

            if (result != null)
            {
                ResultCount++;
                _ = Results.Append(result);

                if (resultPredicate(result)) { CorrectResultCount++; }
            }


        }

        public Job(string title, string provider, string description, int difficulty, Predicate<Tout> resultPredicate, Func<Job<Tin, Tout>, Tin> inputProvider, Payment<Tin, Tout>[] plan)
        {
            this.title = title;
            this.provider = provider;
            this.description = description;

            if (difficulty < 0 || difficulty > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty must be between 0 and 10 (inclusive)");
            }
            this.difficulty = difficulty;
            this.resultPredicate = resultPredicate;
            this.inputProvider = inputProvider;

            foreach (Payment<Tin, Tout> p in plan) { p.Assign(this); }

            if (plan.Any(x => (x.plan & Payment<Tin, Tout>.PlanType.Contract) != 0)) { throw new ArgumentException("Specified contract-only payment plan", nameof(plan)); }
            this.plan = plan;

        }
    }

    public abstract class ContractJob<Tin, Tout> : Job<Tin, Tout>
    {
        public int Iteration { get; protected set; }

        protected readonly int cycleLength = 0;

        public ContractJob(string title, string provider, string description, int difficulty, Predicate<Tout> resultPredicate, Func<Job<Tin, Tout>, Tin> inputProvider, Payment<Tin, Tout>[] plan, int cycleLength) : base(title, provider, description, difficulty, resultPredicate, inputProvider, plan)
        {
            this.cycleLength = cycleLength;
        }
    }

    public enum JobStatus
    {
        Incoming,
        Pending,
        Completed,

        IncomingContract,
        Contract
    }

    public class Condition<Tin, Tout>
    {
        protected Job<Tin, Tout> parent;
        protected readonly Func<Job<Tin, Tout>, string> stringify;

        protected readonly Predicate<Job<Tin, Tout>> evalutator;

        public override string ToString()
        {
            return stringify(parent);
        }

        public bool Evaluate()
        {
            return evalutator(parent);
        }

        public void Assign(Job<Tin, Tout> parent) { this.parent = parent; }
    }

    public readonly struct Reward
    {
        readonly int money;
        readonly ComputeNode[] nodes;
    }

    public class Payment<Tin, Tout>
    {
        [Flags]
        public enum PlanType
        {
            /// <summary>
            /// When this reward's conditions are met, you recieve the pay and the job is marked as completed
            /// </summary>
            OnCompletion = 0b1,

            /// <summary>
            /// Received immediatly after accepting the job. Must not have any associated conditions
            /// </summary>
            Upfront = 0b1 << 1,

            /// <summary>
            /// When the conditions are met, give the payout once. Does not end the job
            /// </summary>
            AtCertainPointOnce = 0b1 << 2,

            /// <summary>
            /// Recieved when the job is canceled. Can have conditions. Suggestion: Specifiy multiple to have different levels of payout depending on how much work was done
            /// </summary>
            Cancel = 0b1 << 3,

            /// <summary>
            /// Run at the end of every payment cycle, usually one month. Increments <see cref="Job.Iteration"/>
            /// </summary>
            PaymentCycle = 0b1 << 4,

            /// <summary>
            /// Every time the conditions are met, the pay is given. Can run more than once.
            /// </summary>
            AtCertainPoint = 0b1 << 5,


            Conditionless = Upfront,
            Contract = PaymentCycle

        }

        public enum PayType
        {
            Total = 0b_0000_1111_1111,
            PerPercentage = 0b_0000_1110_1100,
            PerResult = 0b_0001_1111_1111,
            PerCorrectResult = 0b_0010_1111_1111,
            PerExecution = 0b_0011_1111_1111,
            BlackBox = 0b_0100_1111_1111
        }

        protected Job<Tin, Tout> parent;
        protected Job<Tin, Tout> mirror; // Mirrors the parent job, put can be reset to store the values relevant for the payment (i.e. state at the last payment for cycled payments)

        public readonly PlanType plan;
        public readonly PayType payType;
        public readonly Reward reward;
        public readonly Condition<Tin, Tout>[] conditions;
        public readonly string overrideDescription;

        public Payment(PlanType plan, PayType payType, Reward reward, Condition<Tin, Tout>[] conditions)
        {
            this.plan = plan;
            this.payType = payType;
            this.reward = reward;
            this.conditions = conditions;
        }

        public Payment(PlanType plan, PayType payType, Reward reward, Condition<Tin, Tout>[] conditions, string overrideDescription)
        {
            this.plan = plan;
            this.payType = payType;
            this.reward = reward;
            this.conditions = conditions;
            this.overrideDescription = overrideDescription;
        }

        public void Assign(Job<Tin, Tout> parent)
        {
            this.parent = parent;
            this.mirror = parent;

            foreach (Condition<Tin, Tout> condition in this.conditions)
            {
                condition.Assign(parent);
            }
        }
    }
}