using Godot;
using System;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Job
{
    public partial class Job : GodotObject
    {
        public readonly int Id;
        public readonly string Title;
        public readonly string Provider;
        public readonly string Description;

        public JobStatus Status { get; protected set; }

        protected readonly Predicate<Object> resultPredicate;

        public int ResultCount { get; protected set; }
        public int CorrectResultCount { get; protected set; }
        public int RunCount { get; protected set; }

        protected bool isContract;
        public int Iteration { get; protected set; }


        protected dynamic luaCode;

        protected readonly PaymentPlan plan;

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

        public void Update()
        {

        }

        public void InitalizeScript()
        {
            luaCode.init();
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

    public abstract class JobCondition
    {
        protected Job parent;

        public override abstract string ToString();

        public abstract bool Evaluate();
    }

    public readonly struct Reward
    {
        readonly int money;
        readonly int nodes;
    }

    public class PaymentPlan
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
            AtCertainPoint = 0b1 << 2,

            /// <summary>
            /// Recieved when the job is canceled. Can have conditions. Suggestion: Specifiy multiple to have different levels of payout depending on how much work was done
            /// </summary>
            Cancel = 0b1 << 3,

            /// <summary>
            /// Run at the end of every payment cycle, usually one month. Resets the result counters, essentially restarting the job per month. Increments <see cref="Job.Iteration"/>
            /// </summary>
            PaymentCycle = 0b1 << 4,


            Conditionless = Upfront

        }

        public enum PayType
        {
            Total = 0b_1111_1111,
            PerPercentage = 0b_1110_1100,
            PerResult = 0b_1111_1111,
            PerCorrectResult = 0b_1111_1111,
            BlackBox = 0b_1111_1111
        }
    }
}