# OnProgress Event vs Callback Action Pattern

## The Question: Event vs Action Callback?

You asked about **"OnProgress event"** and **"onProgressChanged action"**. Here's the key distinction:

### What EXISTS in ProgressTree

✅ **`OnProgress` EVENT** - C# event for observing progress changes

### What DOESN'T exist (but could be implemented)

❌ **`onProgressChanged` action** - A callback parameter pattern

---

## Pattern Comparison

### Current Implementation: Event Pattern

**How it works NOW:**

```csharp
public interface IProgressNode
{
    // Event-based pattern (CURRENT)
    event ProgressNodeProgressEventHandler? OnProgress;
}

// Usage:
var task = root.AddChild("process", "Processing");

// Subscribe to event (can have multiple subscribers)
task.OnProgress += (node, statusMessage, value) =>
{
    Console.WriteLine($"{statusMessage}: {value}%");
};

task.OnProgress += (node, statusMessage, value) =>
{
    logger.Log($"{statusMessage}: {value}%");
};

// Progress changes trigger all subscribers
task.ReportProgress(50);  // Both handlers above will fire
```

**Characteristics:**
- ✅ Multiple subscribers
- ✅ Standard C# event pattern
- ✅ Subscribe/unsubscribe with `+=` and `-=`
- ✅ Null-safe invocation with `?.Invoke()`

---

### Hypothetical Alternative: Callback Action Pattern

**How it COULD work (but doesn't):**

```csharp
public interface IProgressNode
{
    // Callback action pattern (HYPOTHETICAL - NOT IN CODE)
    Action<IProgressNode, string, double>? onProgressChanged { get; set; }
}

// Usage:
var task = root.AddChild("process", "Processing");

// Set callback (can only have ONE callback)
task.onProgressChanged = (node, statusMessage, value) =>
{
    Console.WriteLine($"{statusMessage}: {value}%");
};

// Can't add another one - this REPLACES the previous one
task.onProgressChanged = (node, statusMessage, value) =>
{
    logger.Log($"{statusMessage}: {value}%");  // Console logging is now GONE
};

// Progress changes trigger the callback
task.ReportProgress(50);  // Only logger fires, console handler was replaced
```

**Characteristics:**
- ❌ Single callback only (gets replaced)
- ✅ Simpler syntax (property vs event)
- ❌ Can't have multiple handlers
- ❌ Not standard C# pattern for notifications

---

## Why Events Are Better for Progress Monitoring

### Problem with Callback Actions

```csharp
// CALLBACK ACTION PATTERN (not recommended)
var task = root.AddChild("download", "Downloading");

// First subscriber: Console logging
task.onProgressChanged = (n, m, v) => Console.WriteLine($"Progress: {v}%");

// Second subscriber: Metrics
// OOPS! This replaces the console logging
task.onProgressChanged = (n, m, v) => metrics.Record("download", v);

// Third subscriber: UI update
// OOPS! This replaces the metrics
task.onProgressChanged = (n, m, v) => uiProgressBar.Value = v;

// Result: Only UI updates, no console logging or metrics! 😞
```

### Solution with Events

```csharp
// EVENT PATTERN (current implementation)
var task = root.AddChild("download", "Downloading");

// First subscriber: Console logging
task.OnProgress += (n, m, v) => Console.WriteLine($"Progress: {v}%");

// Second subscriber: Metrics
task.OnProgress += (n, m, v) => metrics.Record("download", v);

// Third subscriber: UI update
task.OnProgress += (n, m, v) => uiProgressBar.Value = v;

// Result: All three handlers fire! 😊
```

---

## Side-by-Side Comparison

### Scenario: Multiple Observers

#### Event Pattern (Current ✅)

```csharp
var processor = root.AddChild("proc", "Processing Files");

// Multiple independent observers
processor.OnProgress += LogToConsole;
processor.OnProgress += SendToMetrics;
processor.OnProgress += UpdateDashboard;
processor.OnProgress += NotifyWebhook;

// All four handlers are called
processor.ReportProgress(50);
```

#### Callback Action Pattern (Hypothetical ❌)

```csharp
var processor = root.AddChild("proc", "Processing Files");

// Can only have one callback
processor.onProgressChanged = LogToConsole;
processor.onProgressChanged = SendToMetrics;      // Replaces LogToConsole
processor.onProgressChanged = UpdateDashboard;    // Replaces SendToMetrics
processor.onProgressChanged = NotifyWebhook;      // Replaces UpdateDashboard

// Only the last one is called
processor.ReportProgress(50);  // Only NotifyWebhook fires

// To have multiple, you'd need to manually chain:
processor.onProgressChanged = (n, m, v) =>
{
    LogToConsole(n, m, v);
    SendToMetrics(n, m, v);
    UpdateDashboard(n, m, v);
    NotifyWebhook(n, m, v);
};  // Ugly and manual!
```

---

## When You MIGHT Use Callback Actions

Callback actions (properties with Action/Func) are useful when:

### ✅ Good Use Case: Single Responsibility Callbacks

```csharp
// AddChild already uses this pattern!
public IProgressNode AddChild(
    string id,
    string description,
    Func<IProgressNode, CancellationToken, Task>? workFunc = null)  // <-- Callback action
```

**Why it works here:**
- Each node has ONE work function (single responsibility)
- You don't need multiple work functions
- It defines behavior, not observes it

### ❌ Bad Use Case: Progress Monitoring

```csharp
// Don't use callback actions for monitoring
public interface IProgressNode
{
    Action<double>? onProgressChanged { get; set; }  // BAD for monitoring
}
```

**Why it doesn't work:**
- You often need multiple observers (logging, metrics, UI)
- Each observer should be independent
- Events are the standard pattern for this

---

## Practical Example: Both Patterns Together

The ProgressTree library uses BOTH patterns appropriately:

```csharp
await manager.RunAsync("Data Processing", ExecutionMode.Sequential, async (root) =>
{
    var processor = root.AddChild(
        "processor",
        "Processing Data",

        // CALLBACK ACTION: Define the work (single function)
        workFunc: async (node, ct) =>
        {
            for (int i = 0; i <= 100; i += 10)
            {
                await ProcessBatch(i, ct);
                node.ReportProgress(i);
            }
        });

    // EVENTS: Observe the work (multiple handlers)
    processor.OnProgress += (node, msg, val) =>
        Console.WriteLine($"Console: {val}%");

    processor.OnProgress += (node, msg, val) =>
        logger.LogInformation($"Log: {val}%");

    processor.OnProgress += (node, msg, val) =>
        metrics.RecordGauge("progress", val);

    await processor.ExecuteAsync();
});
```

**Result:**
- ✅ ONE work function defines what to do
- ✅ MULTIPLE event handlers observe what's happening
- ✅ Clean separation of concerns

---

## Summary Table

| Aspect | OnProgress Event | Hypothetical onProgressChanged Action |
|--------|------------------|--------------------------------------|
| **Pattern** | C# Event | Property with Action delegate |
| **Syntax** | `+=` / `-=` | `=` assignment |
| **Multiple handlers** | ✅ Yes, unlimited | ❌ No, only one (or manual chaining) |
| **Standard pattern** | ✅ Yes, for notifications | ⚠️ Sometimes, for single callbacks |
| **Use in ProgressTree** | ✅ OnProgress, OnStart, OnFinish, OnFail | ✅ workFunc (for defining work) |
| **Best for** | Observing/monitoring | Defining behavior/work |
| **Current implementation** | ✅ Exists | ❌ Doesn't exist |

---

## Answer to Your Question

### OnProgress Event (EXISTS)

**What it is:** A C# event that fires automatically when progress changes

**How to use:**
```csharp
task.OnProgress += (node, statusMessage, value) =>
{
    // Your handler code
    Console.WriteLine($"{statusMessage}: {value}%");
};
```

**When it fires:**
- When `ReportProgress(value)` is called
- When `Value` property is set
- When `Description` property changes

**Purpose:** Monitor and observe progress changes

---

### onProgressChanged Action (DOESN'T EXIST)

There is no `onProgressChanged` action/callback parameter in ProgressTree.

**If you want something like this:**

You can create a wrapper:

```csharp
public void AddProgressCallback(
    IProgressNode node,
    Action<IProgressNode, string, double> callback)
{
    // Convert action callback to event subscription
    node.OnProgress += (n, m, v) => callback(n, m, v);
}

// Usage:
AddProgressCallback(task, (node, msg, val) =>
{
    Console.WriteLine($"{msg}: {val}%");
});
```

But this is just wrapping the event - the event pattern is better!

---

## Recommendation

✅ **Use `OnProgress` event** (current implementation)
- Multiple subscribers
- Standard C# pattern
- Flexible and powerful

❌ **Don't create an `onProgressChanged` action**
- Less flexible
- Not the C# standard for notifications
- Would be redundant with the event

**The event pattern is the right choice for progress monitoring!**
