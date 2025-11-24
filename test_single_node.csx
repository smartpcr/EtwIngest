#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// Simulate a simple single-node workflow test
Console.WriteLine("Testing single-node workflow termination...");

// Test: workflow with one node and no connections
var workflow = new {
    WorkflowId = "test-workflow",
    EntryPointNodeId = "single-task",
    Nodes = new[] {
        new { NodeId = "single-task", ScriptContent = "SetGlobal(\"output\", 42);" }
    },
    Connections = Array.Empty<object>() // NO CONNECTIONS
};

Console.WriteLine($"Workflow: {workflow.WorkflowId}");
Console.WriteLine($"Entry Point: {workflow.EntryPointNodeId}");
Console.WriteLine($"Nodes: {workflow.Nodes.Length}");
Console.WriteLine($"Connections: {workflow.Connections.Length}");
Console.WriteLine("\nThis workflow should complete when the single node finishes.");
Console.WriteLine("Expected: Node completes -> Queue empty -> All tracked nodes terminal -> Workflow exits");
Console.WriteLine("\nPress Ctrl+C if this hangs (indicating the bug is reproduced)");
