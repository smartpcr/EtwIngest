# Events vs Actions in ProgressTree

This document explains the different callback mechanisms in ProgressTree: **Events** (like `OnProgress`) vs **Actions** (like `workFunc` and `RunAsync` action).

## Two Callback Patterns

### 1. Events (Observer Pattern)
Events are for **monitoring** and **observing** what happens during execution.

### 2. Actions/Funcs (Execution Pattern)
Actions/Funcs are for **defining the work** to be executed.

---

## Event Pattern: OnProgress

**Type**: `event ProgressNodeProgressEventHandler`

**Signature**:
```csharp
public delegate void ProgressNodeProgressEventHandler(
    IProgressNode node,
    string statusMessage,
    double value);
```

**Purpose**: Subscribe to be notified when progress changes (monitoring/observing)

**Usage**:
```csharp
var task = root.AddChild("processor", "Processing");

// Subscribe to the event (observer)
task.OnProgress += (node, statusMessage, value) =>
{
    // React to progress changes
    Console.WriteLine($"{statusMessage} - {value:F1}%");
};

// Later, during execution, the event fires automatically
task.ReportProgress(50);  // Triggers OnProgress event
```

**Characteristics**:
- ✅ **Multiple subscribers**: Many handlers can subscribe to the same event
- ✅ **Read-only**: Observers can't change the node's behavior
- ✅ **Automatic**: Fires automatically when progress/description changes
- ✅ **Side effects only**: Used for logging, metrics, notifications
- ❌ **Can't define work**: Only observes, doesn't execute the actual work

**Common Use Cases**:
- Logging progress to console/file
- Sending metrics to monitoring systems
- Updating external UI components
- Triggering alerts at thresholds
- Collecting performance data

---

## Action Pattern 1: workFunc

**Type**: `Func<IProgressNode, CancellationToken, Task>`

**Signature**:
```csharp
Func<IProgressNode, CancellationToken, Task> workFunc
```

**Purpose**: Define the actual work to be executed by this node

**Usage**:
```csharp
var task = root.AddChild(
    "processor",
    "Processing",
    workFunc: async (node, ct) =>  // This is the work function
    {
        // Define what this node actually does
        for (int i = 0; i <= 100; i += 10)
        {
            node.Description = $"Processing {i}%";
            node.ReportProgress(i);
            await Task.Delay(100, ct);
        }
    });

// Execute the work
await task.ExecuteAsync();  // Calls the workFunc internally
```

**Characteristics**:
- ✅ **Defines work**: This IS the actual work to be done
- ✅ **Receives node reference**: Can update progress, description, etc.
- ✅ **Cancellation support**: Receives CancellationToken
- ✅ **Async**: Returns Task for async operations
- ❌ **Single function**: Only one workFunc per node
- ❌ **Required to execute**: Without workFunc, leaf nodes do nothing

**Common Use Cases**:
- File processing logic
- API calls and data fetching
- Database operations
- Computation/calculation work
- Service deployment tasks

---

## Action Pattern 2: RunAsync Action

**Type**: `Func<IProgressNode, Task>`

**Signature**:
```csharp
Func<IProgressNode, Task> action
```

**Purpose**: Build the tree structure and optionally execute logic

**Usage**:
```csharp
await manager.RunAsync("Root Task", ExecutionMode.Sequential, async (root) =>
{
    // This action builds the tree structure
    var child1 = root.AddChild("child1", "Task 1");
    var child2 = root.AddChild("child2", "Task 2");

    // Can optionally execute work here too
    child1.ReportProgress(50);
    await Task.Delay(1000);
    child1.ReportProgress(100);
});
```

**Characteristics**:
- ✅ **Receives root node**: Gets the root to build tree structure
- ✅ **Tree construction**: Main purpose is to build the tree
- ✅ **Async**: Can execute async operations
- ✅ **Single root action**: One per RunAsync call
- ❌ **No cancellation token**: Doesn't receive CancellationToken

**Common Use Cases**:
- Building the progress tree structure
- Setting up node hierarchy
- Manual execution control (non-ExecuteAsync pattern)
- Dynamic tree construction based on conditions

---

## Complete Example: All Patterns Together

```csharp
using var manager = new ProgressTreeManager();

// ACTION PATTERN 2: RunAsync action (tree construction)
await manager.RunAsync("Deployment", ExecutionMode.Sequential, async (root) =>
{
    // Build tree structure here
    Console.WriteLine("Building deployment tree...");

    // EVENT PATTERN: Subscribe to root events
    root.OnStart += (node) =>
    {
        Console.WriteLine($"[EVENT] Deployment started: {node.Id}");
    };

    root.OnProgress += (node, statusMessage, value) =>
    {
        Console.WriteLine($"[EVENT] {statusMessage} - {value:F1}%");
    };

    root.OnFinish += (node) =>
    {
        Console.WriteLine($"[EVENT] Deployment completed in {node.DurationSeconds:F1}s");
    };

    // Add child with workFunc
    var preCheck = root.AddChild(
        "pre-check",
        "Pre-deployment Check",
        // ACTION PATTERN 1: workFunc (defines the actual work)
        workFunc: async (node, ct) =>
        {
            Console.WriteLine("[WORK] Starting pre-deployment checks...");
            for (int i = 0; i <= 100; i += 20)
            {
                node.Description = $"Checking... {i}%";
                node.ReportProgress(i);  // This triggers OnProgress event
                await Task.Delay(200, ct);
            }
            Console.WriteLine("[WORK] Pre-deployment checks complete");
        });

    // EVENT PATTERN: Subscribe to child events
    preCheck.OnProgress += (node, statusMessage, value) =>
    {
        if (value >= 50 && value < 55)
        {
            Console.WriteLine($"[EVENT] Halfway there! {statusMessage}");
        }
    };

    var deployment = root.AddChild(
        "deploy",
        "Deployment",
        workFunc: async (node, ct) =>
        {
            Console.WriteLine("[WORK] Deploying services...");
            for (int i = 0; i <= 100; i += 10)
            {
                node.ReportProgress(i);
                await Task.Delay(100, ct);
            }
            Console.WriteLine("[WORK] Deployment complete");
        });

    // Execute the tree
    await root.ExecuteAsync();

    Console.WriteLine("All done!");
});
```

**Output:**
```
Building deployment tree...
[EVENT] Deployment started: __root__
[WORK] Starting pre-deployment checks...
[EVENT] Checking... 0% - 0.0%
[EVENT] Checking... 20% - 20.0%
[EVENT] Checking... 40% - 40.0%
[EVENT] Halfway there! Checking... 60%
[EVENT] Checking... 60% - 60.0%
[EVENT] Checking... 80% - 80.0%
[EVENT] Checking... 100% - 100.0%
[WORK] Pre-deployment checks complete
[WORK] Deploying services...
[EVENT] Deployment - 10.0%
[EVENT] Deployment - 20.0%
...
[WORK] Deployment complete
[EVENT] Deployment completed in 5.2s
All done!
```

---

## Key Differences Summary

| Aspect | OnProgress Event | workFunc Action | RunAsync Action |
|--------|-----------------|-----------------|-----------------|
| **Purpose** | Monitor changes | Define work to execute | Build tree structure |
| **Type** | Event (observer) | Func (executor) | Func (builder) |
| **When called** | Automatically on changes | When ExecuteAsync runs | Immediately in RunAsync |
| **Multiple** | ✅ Many subscribers | ❌ One per node | ❌ One per RunAsync |
| **Can modify node** | ✅ Yes (side effects) | ✅ Yes (intended use) | ✅ Yes (tree building) |
| **Cancellation** | ❌ No | ✅ Yes (CancellationToken) | ❌ No |
| **Return value** | void | Task | Task |
| **Typical use** | Logging, metrics, UI | Actual work logic | Tree construction |

---

## When to Use Each

### Use OnProgress Event When:
- ✅ You want to **monitor** progress changes
- ✅ You need **multiple observers** (logging + metrics + UI)
- ✅ You want to **react** to progress without changing execution
- ✅ You're implementing **cross-cutting concerns** (logging, metrics)

**Example:**
```csharp
task.OnProgress += (node, msg, val) => logger.LogInfo($"{msg} - {val}%");
task.OnProgress += (node, msg, val) => metrics.Record(node.Id, val);
task.OnProgress += (node, msg, val) => webhookClient.SendProgress(node.Id, val);
```

### Use workFunc Action When:
- ✅ You're defining **what work the node does**
- ✅ You want the tree to **drive execution** (ExecuteAsync pattern)
- ✅ You need **cancellation support**
- ✅ You want **clean separation** between tree structure and work logic

**Example:**
```csharp
var processor = root.AddChild("proc", "File Processor",
    workFunc: async (node, ct) =>
    {
        foreach (var file in files)
        {
            await ProcessFileAsync(file, ct);
            node.ReportProgress(++processed * 100.0 / files.Count);
        }
    });
```

### Use RunAsync Action When:
- ✅ You're **building the tree structure**
- ✅ You want to **initialize** the tree
- ✅ You need **dynamic tree construction** based on runtime data
- ✅ You're using **manual execution** (not ExecuteAsync pattern)

**Example:**
```csharp
await manager.RunAsync("Process Files", ExecutionMode.Sequential, async (root) =>
{
    // Build dynamic tree based on files found
    var files = Directory.GetFiles(path);
    foreach (var file in files)
    {
        var fileNode = root.AddChild($"file-{Path.GetFileName(file)}",
                                     $"Process {file}");
    }
});
```

---

## Best Practice: Combine All Three

```csharp
await manager.RunAsync("Complete Workflow", ExecutionMode.Sequential, async (root) =>
{
    // PATTERN 2: Build tree structure
    var phase1 = root.AddChild("phase1", "Phase 1",
        // PATTERN 1: Define work
        workFunc: async (node, ct) =>
        {
            for (int i = 0; i <= 100; i += 10)
            {
                node.ReportProgress(i);
                await Task.Delay(50, ct);
            }
        });

    // PATTERN: Subscribe to events for monitoring
    phase1.OnProgress += (node, msg, val) =>
        Console.WriteLine($"Phase 1: {val:F0}%");

    phase1.OnFinish += (node) =>
        Console.WriteLine($"Phase 1 done in {node.DurationSeconds:F1}s");

    await root.ExecuteAsync();
});
```

This combines all three patterns for maximum flexibility and clean separation of concerns!
