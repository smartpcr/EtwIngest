// -----------------------------------------------------------------------
// <copyright file="AssemblyInitialize.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Serilog;
    using Serilog.Sinks.SystemConsole.Themes;

    /// <summary>
    /// Assembly-level test initialization to configure Dependency Injection and Logging.
    /// </summary>
    [TestClass]
    public static class AssemblyInitialize
    {
        /// <summary>
        /// Gets the service provider configured for the test assembly.
        /// </summary>
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        /// <summary>
        /// Initializes the test assembly with DI and Serilog colored console logging.
        /// </summary>
        /// <param name="context">The test context.</param>
        [AssemblyInitialize]
        public static void Setup(TestContext context)
        {
            // Configure Serilog with colored console output and method name in template
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}.{Method} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Setup Dependency Injection
            var services = new ServiceCollection();

            // Add logging with Serilog
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Build the service provider
            ServiceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Cleanup when all tests are complete.
        /// </summary>
        [AssemblyCleanup]
        public static void Cleanup()
        {
            Log.CloseAndFlush();
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
