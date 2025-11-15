# OnProgress Event Usage Examples

The `OnProgress` event is automatically triggered whenever:
1. **Progress value changes** (via `Value` property or `ReportProgress()`)
2. **Description changes**

## Event Signature

```csharp
public delegate void ProgressNodeProgressEventHandler(
    IProgressNode node,
    string statusMessage,
    double value);
```

**Parameters:**
- `node`: The node whose progress changed
- `statusMessage`: The current description/status message
- `value`: The current progress value (0-100 by default)

## When OnProgress Fires

```csharp
// Fires when you call ReportProgress()
node.ReportProgress(50);  // → OnProgress(node, description, 50)

// Fires when you set Value directly
node.Value = 75;  // → OnProgress(node, description, 75)

// Fires when you change Description
node.Description = "Processing...";  // → OnProgress(node, "Processing...", currentValue)
```

## Example 1: Logging Progress Changes

Track all progress changes to a log:

```csharp
await manager.RunAsync("Process Files", ExecutionMode.Sequential, async (root) =>
{
    var fileProcessor = root.AddChild("processor", "File Processor", workFunc: async (node, ct) =>
    {
        for (int i = 0; i <= 100; i += 10)
        {
            node.Description = $"Processing file {i}%";
            node.ReportProgress(i);
            await Task.Delay(100, ct);
        }
    });

    // Subscribe to progress changes
    fileProcessor.OnProgress += (node, statusMessage, value) =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {node.Id}: {statusMessage} - {value:F1}%");
    };

    await fileProcessor.ExecuteAsync();
});
```

**Output:**
```
[10:30:15] processor: Processing file 0% - 0.0%
[10:30:15] processor: Processing file 10% - 10.0%
[10:30:15] processor: Processing file 20% - 20.0%
...
```

## Example 2: Real-time Metrics Collection

Collect metrics for performance analysis:

```csharp
var progressMetrics = new List<(DateTime timestamp, string nodeId, double value)>();

await manager.RunAsync("Deployment", ExecutionMode.Parallel, async (root) =>
{
    var deployment = root.AddChild("deploy", "Deploy Services",
        ExecutionMode.Parallel, workFunc: async (node, ct) =>
    {
        for (int i = 0; i <= 100; i += 5)
        {
            node.ReportProgress(i);
            await Task.Delay(50, ct);
        }
    });

    // Track all progress updates with timestamps
    deployment.OnProgress += (node, statusMessage, value) =>
    {
        progressMetrics.Add((DateTime.Now, node.Id, value));
    };

    await deployment.ExecuteAsync();
});

// Analyze metrics
var totalUpdates = progressMetrics.Count;
var avgProgressRate = progressMetrics.Count > 1
    ? 100.0 / (progressMetrics.Last().timestamp - progressMetrics.First().timestamp).TotalSeconds
    : 0;

Console.WriteLine($"Total progress updates: {totalUpdates}");
Console.WriteLine($"Average progress rate: {avgProgressRate:F2}% per second");
```

## Example 3: External Progress Reporting

Send progress to an external system (webhook, database, etc.):

```csharp
public class ProgressReporter
{
    private readonly HttpClient httpClient = new HttpClient();

    public async Task ReportToWebhook(string nodeId, string status, double progress)
    {
        var payload = new
        {
            nodeId,
            status,
            progress,
            timestamp = DateTime.UtcNow
        };

        await httpClient.PostAsJsonAsync("https://api.example.com/progress", payload);
    }
}

// Usage
var reporter = new ProgressReporter();

await manager.RunAsync("Data Processing", ExecutionMode.Sequential, async (root) =>
{
    var processor = root.AddChild("data-processor", "Processing Data",
        workFunc: async (node, ct) =>
    {
        for (int i = 0; i <= 100; i += 10)
        {
            node.Description = $"Processing batch {i / 10 + 1}";
            node.ReportProgress(i);
            await Task.Delay(500, ct);
        }
    });

    // Send all progress updates to external webhook
    processor.OnProgress += async (node, statusMessage, value) =>
    {
        await reporter.ReportToWebhook(node.Id, statusMessage, value);
    };

    await processor.ExecuteAsync();
});
```

## Example 4: Throttled Progress Updates

Only log progress at certain thresholds:

```csharp
await manager.RunAsync("Large Operation", ExecutionMode.Sequential, async (root) =>
{
    var operation = root.AddChild("op", "Processing", workFunc: async (node, ct) =>
    {
        for (int i = 0; i <= 1000; i++)
        {
            node.ReportProgress(i / 10.0);  // 0-100%
            await Task.Delay(10, ct);
        }
    });

    // Only log at 25%, 50%, 75%, 100% milestones
    var milestones = new HashSet<int> { 25, 50, 75, 100 };
    var logged = new HashSet<int>();

    operation.OnProgress += (node, statusMessage, value) =>
    {
        int milestone = ((int)value / 25) * 25;
        if (milestones.Contains(milestone) && !logged.Contains(milestone))
        {
            Console.WriteLine($"✓ Reached {milestone}% completion");
            logged.Add(milestone);
        }
    };

    await operation.ExecuteAsync();
});
```

**Output:**
```
✓ Reached 25% completion
✓ Reached 50% completion
✓ Reached 75% completion
✓ Reached 100% completion
```

## Example 5: Multi-Node Progress Tracking

Track progress across multiple nodes:

```csharp
var nodeProgress = new ConcurrentDictionary<string, double>();

await manager.RunAsync("Multi-Service Deployment", ExecutionMode.Parallel, async (root) =>
{
    var deployment = root.AddChild("deployment", "Deploy Services",
        ExecutionMode.Parallel);

    var services = new[] { "API", "Database", "Cache", "WebApp" };

    foreach (var serviceName in services)
    {
        var service = deployment.AddChild(
            $"deploy-{serviceName.ToLower()}",
            $"Deploy {serviceName}",
            workFunc: async (node, ct) =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    node.ReportProgress(i);
                    await Task.Delay(Random.Shared.Next(50, 200), ct);
                }
            });

        // Track each service's progress
        service.OnProgress += (node, statusMessage, value) =>
        {
            nodeProgress[node.Id] = value;

            // Calculate overall progress
            var overallProgress = nodeProgress.Values.Average();
            Console.WriteLine($"{serviceName}: {value:F0}% | Overall: {overallProgress:F1}%");
        };
    }

    await deployment.ExecuteAsync();
});
```

## Example 6: Progress-Based Conditional Logic

Trigger actions when progress reaches certain thresholds:

```csharp
await manager.RunAsync("Backup Operation", ExecutionMode.Sequential, async (root) =>
{
    var backup = root.AddChild("backup", "Creating Backup", workFunc: async (node, ct) =>
    {
        for (int i = 0; i <= 100; i++)
        {
            node.Description = $"Backing up... {i}%";
            node.ReportProgress(i);
            await Task.Delay(50, ct);
        }
    });

    bool halfwayAlertSent = false;
    bool almostDoneAlertSent = false;

    backup.OnProgress += (node, statusMessage, value) =>
    {
        // Alert at 50%
        if (value >= 50 && !halfwayAlertSent)
        {
            Console.WriteLine("⚠️  Backup is halfway complete");
            halfwayAlertSent = true;
        }

        // Alert at 90%
        if (value >= 90 && !almostDoneAlertSent)
        {
            Console.WriteLine("⚠️  Backup is almost complete, preparing finalization...");
            almostDoneAlertSent = true;
        }
    };

    await backup.ExecuteAsync();
});
```

## Key Points

1. **Automatic Triggering**: `OnProgress` fires automatically - you don't call it manually
2. **Both Value and Description**: Fires when either progress value OR description changes
3. **Current State**: Always receives the current absolute value, not deltas
4. **Thread-Safe**: Safe to use from async/parallel operations
5. **Multiple Subscribers**: Multiple handlers can subscribe to the same node's progress

## Comparison with Other Events

| Event | When It Fires | Use Case |
|-------|---------------|----------|
| `OnStart` | Once, when execution begins | Initialize resources, logging |
| `OnProgress` | Every time progress/description changes | Real-time monitoring, metrics |
| `OnFinish` | Once, when execution completes successfully | Cleanup, final logging |
| `OnFail` | Once, when execution fails with exception | Error handling, rollback |
