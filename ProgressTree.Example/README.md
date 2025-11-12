# ProgressTree Example

This example demonstrates three approaches for building and executing hierarchical progress trees:

## 1. Static Tree Construction

Build the **entire tree structure** before execution starts. All nodes are created upfront, then work is executed on the pre-built tree.

**Use Case**: When you know the complete structure beforehand (e.g., fixed deployment steps, pre-defined workflow).

**Advantages**:
- Clear tree structure visible immediately
- All progress nodes created before any work starts
- Easier to reason about the complete workflow
- Manual control over execution flow

**Run this example:**
```bash
dotnet run -- --static
```

**Key characteristics:**
```csharp
// Create all nodes first
var preDeployment = root.AddChild(...);
var networkTask = preDeployment.AddChild(...);
var storageTask = preDeployment.AddChild(...);
var deployment = root.AddChild(...);
var node1Task = deployment.AddChild(...);
var node2Task = deployment.AddChild(...);

// Then execute on pre-built tree
await Task.WhenAll(
    CheckNetworkAsync(networkTask),
    CheckStorageAsync(storageTask));
```

## 2. Dynamic Tree Construction

Build tree nodes **during execution** as needed. Children are added dynamically as the execution progresses.

**Use Case**: When the tree structure depends on runtime conditions (e.g., variable number of items, conditional steps).

**Advantages**:
- Flexible - structure adapts to runtime conditions
- Nodes created just-in-time as needed
- Can add nodes based on intermediate results
- Interleaved construction and execution

**Run this example:**
```bash
dotnet run -- --dynamic
```

**Key characteristics:**
```csharp
// Create parent, then add children during execution
var preDeployment = root.AddChild(...);
await RunPreDeploymentChecksAsync(preDeployment); // Adds children internally

var deployment = root.AddChild(...);
await DeployToNodesAsync(deployment, nodeNames); // Adds children based on nodeNames
```

## 3. ExecuteAsync Pattern (NEW)

Build tree with **work functions attached**, then call `root.ExecuteAsync()`. Parent nodes automatically drive children execution based on `ExecutionMode`.

**Use Case**: Clean separation of tree structure definition from execution logic. Best when you want the tree to drive its own execution.

**Advantages**:
- **Parent drives children**: Parent automatically executes children based on ExecutionMode
- **Parallel execution**: Uses `Task.WhenAll()` for Parallel mode
- **Sequential execution**: Executes children one-by-one for Sequential mode
- **Cancellation support**: Built-in CancellationToken support
- **Cleaner code**: Declarative tree structure with attached behaviors

**Run this example:**
```bash
dotnet run -- --execute
```

**Key characteristics:**
```csharp
// Build tree with work functions
var preDeployment = root.AddChild(
    "pre-deployment",
    "Pre-deployment Checks",
    TaskType.Job,
    ExecutionMode.Parallel,  // Children will execute in parallel
    weight: 1.0);

// Attach work functions to leaf nodes
preDeployment.AddChild("network", "Network Connectivity",
    workFunc: CheckNetworkWorkFunc);
preDeployment.AddChild("storage", "Storage Validation",
    workFunc: CheckStorageWorkFunc);

// Execute entire tree - parent drives children execution
await root.ExecuteAsync(cancellationToken);
```

### ExecuteAsync Execution Flow:

1. **Leaf Nodes**: Execute the attached work function
2. **Parent Nodes with Parallel Mode**:
   ```csharp
   var childTasks = Children.Select(c => c.ExecuteAsync(ct));
   await Task.WhenAll(childTasks);
   ```
3. **Parent Nodes with Sequential Mode**:
   ```csharp
   foreach (var child in Children)
       await child.ExecuteAsync(ct);
   ```

## Run All Examples

To see all three approaches in action:
```bash
dotnet run
```

Or run specific combinations:
```bash
dotnet run -- --static --execute
```

## Key Differences

| Aspect | Static Construction | Dynamic Construction | ExecuteAsync Pattern |
|--------|-------------------|---------------------|---------------------|
| **When nodes created** | All upfront before execution | During execution as needed | Upfront with work functions |
| **Visibility** | Full tree visible immediately | Tree grows as execution progresses | Full tree visible immediately |
| **Execution control** | Manual (Task.WhenAll, etc.) | Manual in methods | Automatic by parent |
| **Flexibility** | Fixed structure | Adaptive structure | Fixed with behaviors |
| **Parallel/Sequential** | Manual Task.WhenAll/foreach | Manual Task.WhenAll/foreach | Automatic based on ExecutionMode |
| **Cancellation** | Manual passing | Manual passing | Built-in CancellationToken |
| **Best for** | Known workflows with manual control | Conditional workflows | Clean declarative trees |

## Execution Modes

The tree supports two execution modes that control how parents execute children:

- **Parallel (P)**: Children execute simultaneously using `Task.WhenAll()`
  - Duration = MAX of children durations
  - Example: Pre-deployment checks (network, storage, prereq run together)

- **Sequential (S)**: Children execute one after another
  - Duration = SUM of children durations
  - Example: Deployment phases (pre → deploy → post in order)

## Output

All examples show:
- Weighted progress calculation (Pre-deployment: 10%, Deployment: 80%, Post-deployment: 10%)
- Parallel vs Sequential execution modes (P = Parallel, S = Sequential)
- Duration tracking for all nodes
- Hierarchical tree structure with proper indentation
- Completion markers (✓) for finished tasks

Example output:
```
✓ Azure Stack Deployment (S 30.1s)                             100%
├── Pre-deployment Checks (P 18.4s)                            100%
│  ├── ✓ Network Connectivity: All endpoints reachable (0.9s) 100%
│  ├── ✓ Storage Validation: Storage ready (18.4s)            100%
│  ├── ✓ Prerequisites Check: All checks passed (18.4s)       100%
├── Deploy to Nodes (P 10.2s)                                  100%
│  ├── ✓ AzS-Node1: Deployment complete (10.2s)               100%
│  ├── ✓ AzS-Node2: Deployment complete (10.2s)               100%
│  ├── ✓ AzS-Node3: Deployment complete (10.2s)               100%
├── Post-deployment Validation (S 2.0s)                        100%
│  ├── ✓ Health Check: All services healthy (S 2.0s)          100%
│  │  ├── ✓ Portal service: Healthy (414ms)                   100%
│  │  ├── ✓ ARM service: Healthy (405ms)                      100%
│  │  ├── ✓ Storage service: Healthy (405ms)                  100%
│  │  ├── ✓ Compute service: Healthy (402ms)                  100%
│  │  ├── ✓ Network service: Healthy (405ms)                  100%
```

- **S** = Sequential execution (duration = sum of children)
- **P** = Parallel execution (duration = max of children)
- **✓** = Task completed

## API Summary

### AddChild with Work Function
```csharp
IProgressNode AddChild(
    string id,
    string description,
    TaskType taskType = TaskType.Job,
    ExecutionMode executionMode = ExecutionMode.Sequential,
    double maxValue = 100,
    double weight = 1.0,
    Func<IProgressNode, CancellationToken, Task>? workFunc = null)
```

### ExecuteAsync Method
```csharp
Task ExecuteAsync(CancellationToken cancellationToken = default)
```

Executes this node and its children:
- **Leaf nodes**: Executes the attached work function
- **Parent nodes**: Drives children execution based on ExecutionMode
  - Parallel: `Task.WhenAll(children)`
  - Sequential: Sequential foreach loop
- Automatically calls `Complete()` after execution
- Supports cancellation via CancellationToken
