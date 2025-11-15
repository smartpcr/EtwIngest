# ProgressTree

A powerful and flexible .NET library for creating hierarchical progress displays with automatic parent progress calculation using Spectre.Console.

## Features

- **Hierarchical Progress Tracking**: Manage complex multi-step operations with nested tasks
- **Automatic Progress Calculation**: Parent task progress automatically calculated from child tasks
- **Thread-Safe**: Uses concurrent collections for safe parallel task execution
- **Beautiful Console UI**: Built on Spectre.Console for rich, colored console output
- **Testable Design**: Clean interfaces make testing easy
- **Flexible API**: Simple to use yet powerful enough for complex scenarios

## Installation

```bash
dotnet add package ProgressTree
```

Or add a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\ProgressTree\ProgressTree.csproj" />
</ItemGroup>
```

## Quick Start

### Basic Usage

```csharp
using ProgressTree;

// Create a progress manager
using var manager = new ProgressTreeManager();

// Run your operation with progress tracking
await manager.RunAsync("My Operation", async () =>
{
    // Add tasks
    var task1 = manager.AddTask("task1", "Processing files");
    var task2 = manager.AddTask("task2", "Uploading results");

    // Update progress
    for (int i = 0; i <= 100; i += 10)
    {
        task1.Value = i;
        await Task.Delay(100);
    }

    // Mark complete
    task1.Complete();
    task2.Complete();
});
```

### Parallel Task Execution

```csharp
await manager.RunAsync("Parallel Operation", async () =>
{
    var task1 = manager.AddTask("task1", "Task 1");
    var task2 = manager.AddTask("task2", "Task 2");
    var task3 = manager.AddTask("task3", "Task 3");

    // Execute tasks in parallel
    await Task.WhenAll(
        DoWork(task1),
        DoWork(task2),
        DoWork(task3)
    );
});

async Task DoWork(IProgressNode task)
{
    for (int i = 0; i <= 100; i += 10)
    {
        task.Value = i;
        await Task.Delay(100);
    }
    task.Complete();
}
```

### Dynamic Task Description

```csharp
var task = manager.AddTask("deploy", "Deploying application");

// Update description as you progress
task.Description = "[yellow]Deploying[/]: Copying files...";
await CopyFiles();

task.Description = "[yellow]Deploying[/]: Configuring services...";
await ConfigureServices();

task.Description = "[green]✓ Deployed[/]: All services running";
task.Complete();
```

### Error Handling

```csharp
var task = manager.AddTask("risky", "Risky Operation");

try
{
    await DoRiskyWork(task);
    task.Complete();
}
catch (Exception ex)
{
    task.Fail($"Operation failed: {ex.Message}");
    throw;
}
```

## Complete Example

See `ProgressTree.Example` project for a full Azure Stack deployment example:

```csharp
public class AzureStackDeployment
{
    private IProgressTreeManager manager = new ProgressTreeManager();

    public async Task DeployAsync(List<string> nodeNames)
    {
        await manager.RunAsync("Azure Stack Deployment", async () =>
        {
            // Phase 1: Pre-deployment checks (parallel)
            var networkTask = manager.AddTask("network", "Network Connectivity");
            var storageTask = manager.AddTask("storage", "Storage Validation");
            var prereqTask = manager.AddTask("prereq", "Prerequisites Check");

            await Task.WhenAll(
                CheckNetworkAsync(networkTask),
                CheckStorageAsync(storageTask),
                CheckPrerequisitesAsync(prereqTask)
            );

            // Phase 2: Deploy to nodes
            foreach (var nodeName in nodeNames)
            {
                var deployTask = manager.AddTask($"deploy_{nodeName}", $"Deploy to {nodeName}");
                await DeployToNodeAsync(deployTask, nodeName);
            }

            // Phase 3: Post-deployment validation
            var healthTask = manager.AddTask("health", "Health Check");
            await PerformHealthCheckAsync(healthTask);
        });
    }
}
```

## API Reference

### IProgressTreeManager

```csharp
public interface IProgressTreeManager : IDisposable
{
    // Start the progress tracking session
    Task RunAsync(string rootDescription, Func<Task> action);

    // Add a new subtask
    IProgressNode AddTask(string id, string description, double maxValue = 100);

    // Get an existing task by ID
    IProgressNode? GetTask(string id);

    // Properties
    IProgressNode? RootTask { get; }
    int TaskCount { get; }
    int CompletedTaskCount { get; }
    double OverallProgress { get; }
}
```

### IProgressNode

```csharp
public interface IProgressNode
{
    string Id { get; }
    string Description { get; set; }
    double Value { get; set; }
    double MaxValue { get; set; }
    bool IsCompleted { get; }
    bool IsStarted { get; }

    void Increment(double amount);
    void Complete();
    void Fail(string errorMessage);
}
```

## Advanced Features

### Custom Max Values

```csharp
// Create a task with custom max value
var task = manager.AddTask("custom", "Custom Task", maxValue: 50);
task.Value = 25; // 50% complete
```

### Incremental Progress

```csharp
var task = manager.AddTask("incremental", "Processing items");
foreach (var item in items)
{
    await ProcessItem(item);
    task.Increment(100.0 / items.Count);
}
```

### Retrieving Tasks

```csharp
// Add task
manager.AddTask("task1", "Task 1");

// Retrieve later
var task = manager.GetTask("task1");
if (task != null)
{
    task.Value = 50;
}
```

## Testing

The library is fully testable with clean interfaces. See `ProgressTree.UnitTests` for examples:

```csharp
[TestMethod]
public async Task RunAsync_WithSingleTask_TracksProgress()
{
    using var manager = new ProgressTreeManager();

    await manager.RunAsync("Test", () =>
    {
        var task = manager.AddTask("task1", "Test Task");
        task.Value = 50;
        Assert.AreEqual(50, task.Value);
        task.Complete();
        Assert.IsTrue(task.IsCompleted);
        return Task.CompletedTask;
    });

    Assert.AreEqual(1, manager.CompletedTaskCount);
}
```

**Note**: Tests must run sequentially (not in parallel) due to Spectre.Console limitations with concurrent interactive displays.

## Spectre.Console Markup

The library supports Spectre.Console markup for rich formatting:

- `[blue]text[/]` - Blue text
- `[green]text[/]` - Green text
- `[yellow]text[/]` - Yellow text
- `[red]text[/]` - Red text
- `[bold]text[/]` - Bold text
- `[grey]text[/]` - Grey text

Example:

```csharp
task.Description = "[blue]Processing[/]: [grey]Step 1 of 10[/]";
task.Description = "[green]✓ Complete[/]: [grey]All done[/]";
task.Description = "[red]✗ Failed[/]: [grey]Error occurred[/]";
```

## Requirements

- .NET 8.0 or later
- Spectre.Console 0.53.0 or later

## License

Copyright (c) Microsoft Corp. All rights reserved.

## Contributing

Contributions are welcome! Please ensure all tests pass and add new tests for new functionality.

```bash
dotnet test ProgressTree.UnitTests/ProgressTree.UnitTests.csproj
```

## Example Output

```
✓   root   (S 2.2s)                               ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
├── ✓ parallel group (P 1.0s)                     ━━━━━━━━━━━━━━━━━━━                        100%
│   ├── ✓ task1 (1.0s)                            ━━━━━━━━━━━━━━━━━━━                        100%
│   ├── ✓ task2 (624ms)                           ━━━━━━━━━━━                                100%
│   └── ✓ task3 (1.0s)                            ━━━━━━━━━━━━━━━━━━━                        100%
└── ✓ sequential group (S 1.2s)                                      ━━━━━━━━━━━━━━━━━━━━━━  100%
    ├── ✓ step1 (415ms)                                              ━━━━━━━                 100%
    ├── ✓ step2 (403ms)                                                     ━━━━━━━          100%
    └── ✓ step3 (405ms)                                                             ━━━━━━━  100%
```
