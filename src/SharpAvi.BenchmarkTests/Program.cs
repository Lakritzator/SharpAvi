using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace SharpAvi.BenchmarkTests
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var job = Job.Default.With(Platform.X64);
            job.Run.MaxWarmupIterationCount = 20;
            job.Run.MaxIterationCount = 20;
            job.Run.UnrollFactor = 100;
            job.Run.InvocationCount = 1000;
            
            var config = DefaultConfig.Instance.With(job);
            BenchmarkRunner.Run<CapturePerformance>(config);
            Console.ReadLine();
        }
    }
}
