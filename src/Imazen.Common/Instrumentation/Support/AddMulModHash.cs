﻿namespace Imazen.Common.Instrumentation.Support
{
    readonly struct AddMulModHash : IHash
    {
        private static readonly ulong Prime = (ulong)(Math.Pow(2, 32) - 5.0);//(ulong)(Math.Pow(2,33) - 355.0);

        /// <summary>
        /// Seed the random number generator with a good prime, or you'll get poor distribution
        /// </summary>
        /// <param name="r"></param>
        public AddMulModHash(IRandomDoubleGenerator r)
        {
            this.r = r;
            this.a = (ulong)(r.NextDouble() * (Prime - 2)) + 1;
            this.b = (ulong)(r.NextDouble() * (Prime - 2)) + 1;
        }

        readonly ulong a;
        readonly ulong b;
        readonly IRandomDoubleGenerator r;
        public uint ComputeHash(uint value)
        {
            return (uint)((a * value + b) % Prime);
        }

        public IHash GetNext()
        {
            return new AddMulModHash(r);
        }

        public static AddMulModHash DeterministicDefault()
        {
            return new AddMulModHash(new MersenneTwister(1499840347));
        }
    }
}