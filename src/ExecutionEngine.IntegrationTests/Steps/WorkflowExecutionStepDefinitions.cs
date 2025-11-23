// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionStepDefinitions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.IntegrationTests.Steps
{
    using ExecutionEngine.Enums;
    using ExecutionEngine.Nodes.Definitions;
    using ExecutionEngine.Workflow;
    using Reqnroll;

    [Binding]
    public class WorkflowExecutionStepDefinitions
    {
        private readonly ScenarioContext context;
        private readonly IReqnrollOutputHelper outputWriter;
        private WorkflowDefinition workflowDefinition;

        public WorkflowExecutionStepDefinitions(ScenarioContext context, IReqnrollOutputHelper outputWriter)
        {
            this.context = context;
            this.outputWriter = outputWriter;
            this.workflowDefinition = new WorkflowDefinition()
            {
                WorkflowName = "TestWorkflow",
                Description = "Test workflow",
                Nodes = new List<NodeDefinition>(),
                Connections = new List<NodeConnection>()
            };
        }

        [Given("I have a workflow with nodes {string}, {string}, {string}")]
        public void GivenIHaveAWorkflowWithNodes(string nodeA, string nodeB, string nodeC)
        {
            // create enumerable with node names
            var nodeNames = new[] { nodeA, nodeB, nodeC };
            foreach (var nodeName in nodeNames)
            {
                this.workflowDefinition.Nodes.Add(new NoopNodeDefinition()
                {
                    NodeId = nodeName,
                    NodeName = nodeName,
                });
            }
        }

        [Given("{string} connects to {string} on Complete")]
        public void GivenConnectsToOnComplete(string sourceNode, string targetNode)
        {
            this.workflowDefinition.Connections.Add(new NodeConnection()
            {
                SourceNodeId = sourceNode,
                TargetNodeId = targetNode,
                TriggerMessageType = MessageType.Complete
            });
        }

        [When("I start the workflow")]
        public void WhenIStartTheWorkflow()
        {
            throw new PendingStepException();
        }

        [Then("the workflow should complete successfully")]
        public void ThenTheWorkflowShouldCompleteSuccessfully()
        {
            throw new PendingStepException();
        }

        [Then("nodes should execute in order {string}, {string}, {string}")]
        public void ThenNodesShouldExecuteInOrder(string nodeA, string nodeB, string nodeC)
        {
            throw new PendingStepException();
        }

        [Then("all nodes should have status {string}")]
        public void ThenAllNodesShouldHaveStatus(string completed)
        {
            throw new PendingStepException();
        }

        [Then("the workflow duration should be greater than zero")]
        public void ThenTheWorkflowDurationShouldBeGreaterThanZero()
        {
            throw new PendingStepException();
        }

        [Given("I have a workflow with nodes {string}, {string}, {string}, {string}, {string}")]
        public void GivenIHaveAWorkflowWithNodes(string node1, string node2, string node3, string node4, string node5)
        {
            throw new PendingStepException();
        }

        [Then("{string}, {string}, {string} should execute in parallel")]
        public void ThenShouldExecuteInParallel(string parallelA, string parallelB, string parallelC)
        {
            throw new PendingStepException();
        }

        [Then("{string} should execute after all parallel nodes complete")]
        public void ThenShouldExecuteAfterAllParallelNodesComplete(string join)
        {
            throw new PendingStepException();
        }

        [Given("I have a workflow with nodes {string}, {string}, {string}, {string}, {string}, {string}")]
        public void GivenIHaveAWorkflowWithNodes(string start, string p1, string p2, string join, string sequential, string end)
        {
            throw new PendingStepException();
        }


        [Then("{string}, {string} should execute in parallel")]
        public void ThenShouldExecuteInParallel(string p0, string p1)
        {
            throw new PendingStepException();
        }

        [Then("{string} should wait for both {string} and {string}")]
        public void ThenShouldWaitForBothAnd(string join, string p1, string p2)
        {
            throw new PendingStepException();
        }

        [Then("{string} should execute after {string}")]
        public void ThenShouldExecuteAfter(string laterNode, string earlierNode)
        {
            throw new PendingStepException();
        }

        [Given("I have a workflow with nodes {string}, {string}, {string}, {string}")]
        public void GivenIHaveAWorkflowWithNodes(string p0, string p1, string nodeA, string nodeB)
        {
            throw new PendingStepException();
        }


        [Then("{string} and {string} should be identified as entry points")]
        public void ThenAndShouldBeIdentifiedAsEntryPoints(string p0, string p1)
        {
            throw new PendingStepException();
        }

        [Then("both entry points should receive initial trigger")]
        public void ThenBothEntryPointsShouldReceiveInitialTrigger()
        {
            throw new PendingStepException();
        }

        [Then("all target nodes should receive the completion message")]
        public void ThenAllTargetNodesShouldReceiveTheCompletionMessage()
        {
            throw new PendingStepException();
        }

        [Then("all targets should execute in parallel")]
        public void ThenAllTargetsShouldExecuteInParallel()
        {
            throw new PendingStepException();
        }

        [Given("I have a valid acyclic workflow")]
        public void GivenIHaveAValidAcyclicWorkflow()
        {
            throw new PendingStepException();
        }

        [Then("no infinite loops should occur")]
        public void ThenNoInfiniteLoopsShouldOccur()
        {
            throw new PendingStepException();
        }

        [Then("all reachable nodes should execute")]
        public void ThenAllReachableNodesShouldExecute()
        {
            throw new PendingStepException();
        }

        [Given("I have a long-running workflow")]
        public void GivenIHaveALong_RunningWorkflow()
        {
            throw new PendingStepException();
        }

        [When("I cancel the workflow after {int} milliseconds")]
        public void WhenICancelTheWorkflowAfterMilliseconds(int p0)
        {
            throw new PendingStepException();
        }

        [Then("the workflow status should be {string}")]
        public void ThenTheWorkflowStatusShouldBe(string cancelled)
        {
            throw new PendingStepException();
        }

        [Then("some nodes may not have executed")]
        public void ThenSomeNodesMayNotHaveExecuted()
        {
            throw new PendingStepException();
        }

        [Then("executed nodes should have their state preserved")]
        public void ThenExecutedNodesShouldHaveTheirStatePreserved()
        {
            throw new PendingStepException();
        }
    }
}
