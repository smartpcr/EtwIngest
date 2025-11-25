//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree.Example
{
    /// <summary>
    /// Example demonstrating the hierarchical ProgressTree library for Azure Stack deployment.
    /// Demonstrates both static tree construction (build tree first) and dynamic tree construction (add nodes during execution).
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== PROGRESSTREE WITH EXECUTEAFYNC & EVENTS ===");
            Console.WriteLine("Build tree with work functions, then call root.ExecuteAsync()");
            Console.WriteLine("Parent automatically drives children execution (Parallel/Sequential)");
            Console.WriteLine("Use events (OnStart, OnProgress, OnFinish, OnFail) to hook additional logic\n");

            var deployment = new ExecuteAsyncDeployment();
            await deployment.DeployAsync();

            Console.WriteLine("\n\n=== PROGRESSTREE WITH EFFECTIVE TIMING DEMO ===");
            Console.WriteLine("Demonstrates automatic execution mode detection and effective timing");
            Console.WriteLine("Parent starts at earliest child start, ends at latest child end\n");

            var timingDemo = new EffectiveTimingDemo();
            await timingDemo.RunAsync();
        }
    }
}
