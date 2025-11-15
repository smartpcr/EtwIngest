// -----------------------------------------------------------------------
// <copyright file="OutputHelper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace EtwIngest.Steps
{
    using System.Runtime.CompilerServices;
    using Reqnroll;

    public static class OutputHelper
    {
        public static void WriteError(
            this IReqnrollOutputHelper outputHelper,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            outputHelper.WriteLine($"ERROR: {DateTime.UtcNow:u} [{filePath}.{memberName}.{lineNumber}] {message}");
            Console.ResetColor();
        }

        public static void WriteWarning(
            this IReqnrollOutputHelper outputHelper,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            outputHelper.WriteLine($"WARN: {DateTime.UtcNow:u} [{filePath}.{memberName}.{lineNumber}] {message}");
            Console.ResetColor();
        }

        public static void WriteInfo(
            this IReqnrollOutputHelper outputHelper,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            outputHelper.WriteLine($"INFO: {DateTime.UtcNow:u} [{filePath}.{memberName}.{lineNumber}] {message}");
            Console.ResetColor();
        }

        public static void WriteVerbose(
            this IReqnrollOutputHelper outputHelper,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            outputHelper.WriteLine($"INFO: {DateTime.UtcNow:u} [{filePath}.{memberName}.{lineNumber}] {message}");
            Console.ResetColor();
        }
    }
}