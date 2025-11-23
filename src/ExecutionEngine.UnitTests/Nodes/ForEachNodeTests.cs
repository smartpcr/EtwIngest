// -----------------------------------------------------------------------
// <copyright file="ForEachNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using FluentAssertions;

[TestClass]
public class ForEachNodeTests
{
    [TestMethod]
    public async Task ExecuteAsync_ThreeItems_IteratesThreeTimes()
    {
        var node = new ForEachNode
        {
            CollectionExpression = "GetGlobal(\"items\")",
            ItemVariableName = "currentItem"
        };
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")",
            ItemVariableName = "currentItem"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<string> { "a", "b", "c" };
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["ItemsProcessed"].Should().Be(3);
        nodeContext.OutputData["TotalItems"].Should().Be(3);
        workflowContext.Variables["currentItem"].Should().Be("c");
        workflowContext.Variables["currentItemIndex"].Should().Be(2);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCollection_CompletesWithZeroItems()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<string>();
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["ItemsProcessed"].Should().Be(0);
    }

    [TestMethod]
    public async Task ExecuteAsync_IntegerArray_IteratesCorrectly()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"numbers\")",
            ItemVariableName = "num"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["numbers"] = new int[] { 1, 2, 3, 4, 5 };
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["ItemsProcessed"].Should().Be(5);
        workflowContext.Variables["num"].Should().Be(5);
        workflowContext.Variables["numIndex"].Should().Be(4);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinqExpression_IteratesFiltered()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "((IEnumerable<int>)GetGlobal(\"numbers\")).Where(n => n > 2)",
            ItemVariableName = "item"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["numbers"] = new int[] { 1, 2, 3, 4, 5 };
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["ItemsProcessed"].Should().Be(3);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCollectionExpression_ShouldFail()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = string.Empty
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("CollectionExpression cannot be null or empty");
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidExpression_ShouldFail()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "this is not valid C#"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("compilation failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_NonEnumerableResult_ShouldFail()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "42"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("did not return an IEnumerable");
    }

    [TestMethod]
    public async Task ExecuteAsync_NullCollectionResult_ShouldFail()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = null!;
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Collection expression evaluated to null");
    }

    [TestMethod]
    public void Initialize_WithConfiguration_SetsProperties()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression =  "GetGlobal(\"myList\")",
            ItemVariableName = "myItem",
        };

        node.Initialize(definition);

        node.CollectionExpression.Should().Be("GetGlobal(\"myList\")");
        node.ItemVariableName.Should().Be("myItem");
    }

    [TestMethod]
    public void Initialize_WithNullConfiguration_UsesDefaults()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
        };

        node.Initialize(definition);

        node.CollectionExpression.Should().BeEmpty();
        node.ItemVariableName.Should().Be("item");
    }

    [TestMethod]
    public void GetAvailablePorts_ShouldReturnLoopBodyPort()
    {
        var node = new ForEachNode();

        var ports = node.GetAvailablePorts();

        ports.Should().NotBeNull();
        ports.Should().HaveCount(1);
        ports.Should().Contain(ForEachNode.LoopBodyPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldSetSourcePort()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 1, 2, 3 };
        var nodeContext = new NodeExecutionContext();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        instance.SourcePort.Should().Be(ForEachNode.LoopBodyPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 1 };
        var nodeContext = new NodeExecutionContext();

        var eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_WithCancellation_ShouldCancel()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = Enumerable.Range(1, 1000).ToList();
        var nodeContext = new NodeExecutionContext();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var instance = await node.ExecuteAsync(workflowContext, nodeContext, cts.Token);

        instance.Status.Should().Be(NodeExecutionStatus.Cancelled);
        instance.ErrorMessage.Should().Contain("cancelled");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithProgressTracking_EmitsProgress()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 1, 2, 3, 4, 5 };
        var nodeContext = new NodeExecutionContext();

        var progressCount = 0;
        node.OnProgress += (sender, args) => progressCount++;

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        progressCount.Should().Be(5);
    }

    [TestMethod]
    public void NodeFactory_CreateForEachNode_ShouldSucceed()
    {
        var factory = new NodeFactory();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")",
        };

        var node = factory.CreateNode(definition);

        node.Should().NotBeNull();
        node.Should().BeOfType<ForEachNode>();
        node.NodeId.Should().Be("foreach-1");
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldEmitOnNextForEachIteration()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<string> { "a", "b", "c" };
        var nodeContext = new NodeExecutionContext();

        var nextEventCount = 0;
        var receivedIndices = new List<int>();
        var receivedItems = new List<object>();

        node.OnNext += (sender, args) =>
        {
            nextEventCount++;
            receivedIndices.Add(args.IterationIndex);
            if (args.IterationContext?.InputData.TryGetValue("item", out var item) == true)
            {
                receivedItems.Add(item);
            }
        };

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        nextEventCount.Should().Be(3);
        receivedIndices.Should().Equal(0, 1, 2);
        receivedItems.Should().Equal("a", "b", "c");
    }

    [TestMethod]
    public async Task ExecuteAsync_OnNextEvent_ShouldContainIterationContext()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"numbers\")",
            ItemVariableName = "num"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["numbers"] = new int[] { 10, 20, 30 };
        var nodeContext = new NodeExecutionContext();

        ExecutionEngine.Core.NodeNextEventArgs? lastEvent = null;
        node.OnNext += (sender, args) => lastEvent = args;

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        lastEvent.Should().NotBeNull();
        lastEvent!.IterationIndex.Should().Be(2);
        lastEvent.IterationContext.Should().NotBeNull();
        lastEvent.IterationContext!.InputData.Should().ContainKey("num");
        lastEvent.IterationContext.InputData["num"].Should().Be(30);
        lastEvent.IterationContext.InputData["numIndex"].Should().Be(2);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnNextEvent_ShouldContainMetadata()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 1, 2 };
        var nodeContext = new NodeExecutionContext();

        ExecutionEngine.Core.NodeNextEventArgs? firstEvent = null;
        node.OnNext += (sender, args) =>
        {
            if (firstEvent == null) firstEvent = args;
        };

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        firstEvent.Should().NotBeNull();
        firstEvent!.Metadata.Should().NotBeNull();
        firstEvent.Metadata!.Should().ContainKey("TotalItems");
        firstEvent.Metadata!["TotalItems"].Should().Be(2);
        firstEvent.Metadata!.Should().ContainKey("ItemValue");
        firstEvent.Metadata!["ItemValue"].Should().Be(1);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithChildNodeExecution_ShouldProcessEachItem()
    {
        var node = new ForEachNode();
        var definition = new ForEachNodeDefinition
        {
            NodeId = "foreach-1",
            CollectionExpression = "GetGlobal(\"items\")"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 5, 10, 15 };
        var nodeContext = new NodeExecutionContext();

        // Simulate child node execution that doubles each item
        var processedItems = new List<int>();
        node.OnNext += (sender, args) =>
        {
            if (args.IterationContext?.InputData.TryGetValue("item", out var item) == true)
            {
                var value = (int)item;
                var doubled = value * 2;
                processedItems.Add(doubled);

                // Child node would set its output
                args.IterationContext.OutputData["result"] = doubled;
            }
        };

        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        processedItems.Should().Equal(10, 20, 30);
    }
}
