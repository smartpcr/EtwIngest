# ProgressTree Quick Reference

## Three Callback Mechanisms

```
┌─────────────────────────────────────────────────────────────┐
│                     PROGRESSTREE CALLBACKS                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. EVENTS (Observer Pattern) - Monitor execution           │
│     • OnStart, OnProgress, OnFinish, OnFail                 │
│     • Multiple subscribers allowed                           │
│     • Fired automatically during execution                   │
│                                                              │
│  2. WORKFUNC (Execution Pattern) - Define work              │
│     • Func<IProgressNode, CancellationToken, Task>          │
│     • Single function per node                               │
│     • Called by ExecuteAsync()                               │
│                                                              │
│  3. RUNASYNC ACTION (Builder Pattern) - Build tree          │
│     • Func<IProgressNode, Task>                             │
│     • Builds tree structure                                  │
│     • One per RunAsync call                                  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Visual Flow Diagram

```
RunAsync(action)
    │
    ├──> ACTION EXECUTES: Build tree structure
    │         │
    │         ├──> root.AddChild("task1", workFunc: ...)
    │         ├──> root.AddChild("task2", workFunc: ...)
    │         │
    │         └──> Subscribe to events:
    │               task1.OnProgress += (n,m,v) => {...}
    │               task1.OnStart += (n) => {...}
    │               task1.OnFinish += (n) => {...}
    │
    └──> ExecuteAsync() called
              │
              ├──> OnStart EVENT fires
              │
              ├──> WORKFUNC executes
              │         │
              │         ├──> node.ReportProgress(25)
              │         │         └──> OnProgress EVENT fires
              │         │
              │         ├──> node.ReportProgress(50)
              │         │         └──> OnProgress EVENT fires
              │         │
              │         └──> node.ReportProgress(100)
              │                   └──> OnProgress EVENT fires
              │
              └──> OnFinish EVENT fires
```

## Code Pattern Comparison

### Pattern A: Event-Based Monitoring

```csharp
var task = root.AddChild("download", "Downloading file");

// Subscribe to monitor
task.OnProgress += (node, statusMessage, value) =>
{
    Console.WriteLine($"{statusMessage}: {value:F1}%");
};

// Work happens elsewhere
task.ReportProgress(50);  // Triggers event
```

**Use for:** Logging, metrics, notifications, UI updates

---

### Pattern B: Work Function Execution

```csharp
var task = root.AddChild("download", "Downloading file",
    workFunc: async (node, ct) =>  // THIS is the work
    {
        for (int i = 0; i <= 100; i += 10)
        {
            await DownloadChunk(i, ct);
            node.ReportProgress(i);
        }
    });

await task.ExecuteAsync();  // Calls workFunc
```

**Use for:** Defining actual work to be done

---

### Pattern C: Tree Construction

```csharp
await manager.RunAsync("Deployment", ExecutionMode.Sequential,
    async (root) =>  // THIS builds the tree
    {
        var task1 = root.AddChild(...);
        var task2 = root.AddChild(...);

        // Can execute manually or via ExecuteAsync
        await root.ExecuteAsync();
    });
```

**Use for:** Building dynamic tree structures

---

## Complete Example

```csharp
// PATTERN C: RunAsync action (tree builder)
await manager.RunAsync("File Processing", ExecutionMode.Sequential, async (root) =>
{
    var downloader = root.AddChild("download", "Download Files",
        // PATTERN B: workFunc (work executor)
        workFunc: async (node, ct) =>
        {
            for (int i = 1; i <= 10; i++)
            {
                await DownloadFileAsync(i, ct);
                node.ReportProgress(i * 10);
            }
        });

    // PATTERN A: Events (observers)
    downloader.OnProgress += (node, msg, val) =>
        Console.WriteLine($"Download: {val}%");

    downloader.OnFinish += (node) =>
        Console.WriteLine($"Downloaded in {node.DurationSeconds:F1}s");

    await root.ExecuteAsync();
});
```

---

## Decision Tree: Which Pattern to Use?

```
Do you need to observe/monitor progress changes?
├─ YES → Use OnProgress EVENT
│         task.OnProgress += (node, msg, val) => {...}
│
└─ NO ↓

Do you need to define the work a node does?
├─ YES → Use workFunc ACTION
│         workFunc: async (node, ct) => {...}
│
└─ NO ↓

Do you need to build the tree structure?
├─ YES → Use RunAsync action
│         RunAsync(..., async (root) => {...})
│
└─ NO → You probably need an event!
```

---

## Common Mistakes

### ❌ WRONG: Using events to define work

```csharp
var task = root.AddChild("process", "Processing");

// DON'T: Events are for observing, not executing work
task.OnProgress += (node, msg, val) =>
{
    // This won't define what the task does!
    node.ReportProgress(50);
};

await task.ExecuteAsync();  // Does nothing! No workFunc defined
```

### ✅ CORRECT: Use workFunc for work

```csharp
var task = root.AddChild("process", "Processing",
    workFunc: async (node, ct) =>  // Define work here
    {
        await DoWork(ct);
        node.ReportProgress(50);
    });

task.OnProgress += (node, msg, val) =>  // Monitor here
{
    Console.WriteLine($"Progress: {val}%");
};

await task.ExecuteAsync();  // Executes workFunc
```

---

### ❌ WRONG: Expecting workFunc to be event

```csharp
var task = root.AddChild("process", "Processing",
    workFunc: async (node, ct) => await DoWork(ct));

// DON'T: Can't add multiple workFuncs
task.workFunc += async (node, ct) => await DoMoreWork(ct);  // ERROR!
```

### ✅ CORRECT: Use events for multiple handlers

```csharp
var task = root.AddChild("process", "Processing",
    workFunc: async (node, ct) => await DoWork(ct));  // One workFunc

// DO: Add multiple event handlers
task.OnProgress += LogToConsole;
task.OnProgress += SendToMetrics;
task.OnProgress += UpdateUI;
```

---

## Cheat Sheet

| Need | Use | Example |
|------|-----|---------|
| Monitor progress | `OnProgress` event | `node.OnProgress += (n,m,v) => Log(v)` |
| Log when task starts | `OnStart` event | `node.OnStart += n => Log("Started")` |
| Log when task completes | `OnFinish` event | `node.OnFinish += n => Log("Done")` |
| Handle errors | `OnFail` event | `node.OnFail += (n,e) => Log(e)` |
| Define actual work | `workFunc` parameter | `workFunc: async (n,ct) => {...}` |
| Build tree structure | `RunAsync` action | `RunAsync("Root", async root => {...})` |
| Update progress | `ReportProgress()` | `node.ReportProgress(50)` |
| Change description | `Description` property | `node.Description = "Processing..."` |
| Execute tree | `ExecuteAsync()` | `await root.ExecuteAsync()` |

---

## Memory Aid

```
EVENT    = "Tell me when something happens"    (Observer)
WORKFUNC = "Do this work"                      (Executor)
ACTION   = "Build this structure"              (Builder)
```

**Events**: Many listeners, triggered automatically
**WorkFunc**: One function, executed on demand
**Action**: One callback, runs immediately
