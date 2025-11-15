// -----------------------------------------------------------------------
// <copyright file="NodeExecutionContextTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Contexts;

using ExecutionEngine.Contexts;
using FluentAssertions;

[TestClass]
public class NodeExecutionContextTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeEmptyCollections()
    {
        // Arrange & Act
        var context = new NodeExecutionContext();

        // Assert
        context.InputData.Should().NotBeNull();
        context.OutputData.Should().NotBeNull();
        context.LocalVariables.Should().NotBeNull();
        context.Metadata.Should().NotBeNull();
    }

    [TestMethod]
    public void InputData_CanStoreAndRetrieveValues()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        context.InputData["key1"] = "value1";
        context.InputData["key2"] = 42;

        // Assert
        context.InputData["key1"].Should().Be("value1");
        context.InputData["key2"].Should().Be(42);
        context.InputData.Should().HaveCount(2);
    }

    [TestMethod]
    public void OutputData_IsIndependentFromInputData()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.InputData["key"] = "input";

        // Act
        context.OutputData["key"] = "output";

        // Assert
        context.InputData["key"].Should().Be("input");
        context.OutputData["key"].Should().Be("output");
    }

    [TestMethod]
    public void LocalVariables_SupportsConcurrentAccess()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        context.LocalVariables["counter"] = 0;
        context.LocalVariables.TryGetValue("counter", out var value);

        // Assert
        value.Should().Be(0);
        context.LocalVariables.Should().ContainKey("counter");
    }

    [TestMethod]
    public void Metadata_CanStoreArbitraryData()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        context.Metadata["timestamp"] = DateTime.UtcNow;
        context.Metadata["nodeType"] = "CSharpTask";
        context.Metadata["version"] = 1;

        // Assert
        context.Metadata.Should().HaveCount(3);
        context.Metadata["nodeType"].Should().Be("CSharpTask");
    }

    [TestMethod]
    public void OutputData_CanBePopulatedFromComputations()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.InputData["x"] = 10;
        context.InputData["y"] = 20;

        // Act
        var x = (int)context.InputData["x"];
        var y = (int)context.InputData["y"];
        context.OutputData["sum"] = x + y;
        context.OutputData["product"] = x * y;

        // Assert
        context.OutputData["sum"].Should().Be(30);
        context.OutputData["product"].Should().Be(200);
    }

    [TestMethod]
    public void LocalVariables_ThreadSafe_ConcurrentWrites()
    {
        // Arrange
        var context = new NodeExecutionContext();
        var tasks = new List<Task>();

        // Act - Write from 10 threads concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                context.LocalVariables[$"key-{index}"] = index;
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        context.LocalVariables.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            context.LocalVariables[$"key-{i}"].Should().Be(i);
        }
    }
}
