## Execution Engine

### Overview

The Execution Engine is a workflow orchestration system that executes directed acyclic graphs (DAGs) representing business processes. The engine provides a flexible framework for defining, validating, executing, and monitoring complex workflows with support for control flow, error handling, and state persistence.

#### Key Features

- Graph-based workflow definition with nodes and edges
- Event-driven execution using channels for message passing
- Support for C# code and PowerShell script execution
- Control flow primitives (if-else, switch, loops)
- Subflow composition for workflow reusability
- State persistence and workflow lifecycle management (pause, resume, cancel)
- Progress tracking and error handling
- Graph validation to prevent infinite loops

### Documentation

The comprehensive design documentation has been split into focused documents for better readability:

#### Core Documentation

- **[design.md](design.md)** - Architecture and Design
  - Core concepts (execution plans, workflow instances, message queues)
  - Node architecture and execution lifecycle
  - Graph structure and edge definitions
  - Graph validation and cycle detection
  - Execution engine architecture
  - State persistence strategy

- **[implementation.md](implementation.md)** - Implementation Details
  - Testing strategy (unit and integration tests with BDD)
  - Phase-by-phase implementation roadmap
  - Detailed technical specifications for each component
  - Progress tracking and completion status

- **[usage_guide.md](usage_guide.md)** - Usage and Examples
  - Integration with EtwIngest workflows
  - Example workflow definitions
  - Node execution context flow patterns
  - Future enhancements and extensibility

### Quick Start

The execution engine is designed to orchestrate complex ETL workflows. Here's the basic workflow:

1. **Define your workflow** - Create a graph with nodes (tasks, control flow) and edges (dependencies)
2. **Validate the graph** - Ensure no cycles and proper node connections
3. **Execute** - Start a workflow instance with the execution engine
4. **Monitor** - Track progress via events and check execution status
5. **Manage state** - Pause, resume, or cancel workflows as needed

#### Example

1. definition

```yaml
# ===============================================================================
# Azure Stack Deployment Workflow (Restructured)
# ===============================================================================
# This workflow demonstrates the Azure Stack deployment process with:
# 1. Pre-deployment checks (container with sequential execution)
# 2. Node deployments (container with parallel subflows): Deploy to AzS-Node1, Node2, Node3
# 3. Post-deployment health checks (container with sequential execution)
# ===============================================================================

workflowId: azs-deployment
workflowName: Azure Stack Deployment
description: Complete Azure Stack deployment workflow with containers and subflows

defaultVariables:
  nodeName: "AzS-Node"

nodes:
  # ============================================================================
  # Phase 1: Pre-deployment Checks Container (Sequential Execution)
  # ============================================================================

  - nodeId: pre-deployment-checks
    nodeName: Pre-Deployment Checks
    runtimeType: Container
    configuration:
      ExecutionMode: "Sequential"
      ChildNodes:
        # Child 1: Network check
        - nodeId: check-network
          nodeName: Network Connectivity Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackPreCheckNode
          configuration:
            checkType: "network"

        # Child 2: Storage check
        - nodeId: check-storage
          nodeName: Storage Validation Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackPreCheckNode
          configuration:
            checkType: "storage"

        # Child 3: Prerequisites check
        - nodeId: check-prerequisites
          nodeName: Prerequisites Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackPreCheckNode
          configuration:
            checkType: "prerequisites"

      # Sequential connections: Network → Storage → Prerequisites
      ChildConnections:
        - sourceNodeId: check-network
          targetNodeId: check-storage
          triggerMessageType: Complete
          isEnabled: true

        - sourceNodeId: check-storage
          targetNodeId: check-prerequisites
          triggerMessageType: Complete
          isEnabled: true

  # ============================================================================
  # Phase 2: Deploy to Nodes Container (Parallel Subflows)
  # ============================================================================

  - nodeId: node-deployments
    nodeName: Node Deployments
    runtimeType: Container
    configuration:
      ExecutionMode: "Parallel"
      ChildNodes:
        # Child 1: Deploy to Node1
        - nodeId: deploy-node1
          nodeName: Deploy to AzS-Node1
          runtimeType: Subflow
          configuration:
            WorkflowFilePath: "Workflows/deploy_node.yaml"
            InputMappings:
              nodeName: "nodeName"
            OutputMappings:
              deploymentStatus: "node1DeploymentStatus"
              osVersion: "node1OsVersion"
              stampVersion: "node1StampVersion"

        # Child 2: Deploy to Node2
        - nodeId: deploy-node2
          nodeName: Deploy to AzS-Node2
          runtimeType: Subflow
          configuration:
            WorkflowFilePath: "Workflows/deploy_node.yaml"
            InputMappings:
              nodeName: "nodeName"
            OutputMappings:
              deploymentStatus: "node2DeploymentStatus"
              osVersion: "node2OsVersion"
              stampVersion: "node2StampVersion"

        # Child 3: Deploy to Node3
        - nodeId: deploy-node3
          nodeName: Deploy to AzS-Node3
          runtimeType: Subflow
          configuration:
            WorkflowFilePath: "Workflows/deploy_node.yaml"
            InputMappings:
              nodeName: "nodeName"
            OutputMappings:
              deploymentStatus: "node3DeploymentStatus"
              osVersion: "node3OsVersion"
              stampVersion: "node3StampVersion"

      # No internal connections - all deployments run in parallel
      ChildConnections: []

  # ============================================================================
  # Phase 3: Post-deployment Health Checks Container (Sequential Execution)
  # ============================================================================

  - nodeId: health-checks
    nodeName: Post-Deployment Health Checks
    runtimeType: Container
    configuration:
      ExecutionMode: "Sequential"
      ChildNodes:
        # Child 1: Portal health check
        - nodeId: health-portal
          nodeName: Portal Service Health Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackHealthCheckNode
          configuration:
            serviceName: "Portal"

        # Child 2: ARM health check
        - nodeId: health-arm
          nodeName: ARM Service Health Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackHealthCheckNode
          configuration:
            serviceName: "ARM"

        # Child 3: Storage health check
        - nodeId: health-storage
          nodeName: Storage Service Health Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackHealthCheckNode
          configuration:
            serviceName: "Storage"

        # Child 4: Compute health check
        - nodeId: health-compute
          nodeName: Compute Service Health Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackHealthCheckNode
          configuration:
            serviceName: "Compute"

      # Sequential connections: Portal → ARM → Storage → Compute
      ChildConnections:
        - sourceNodeId: health-portal
          targetNodeId: health-arm
          triggerMessageType: Complete
          isEnabled: true

        - sourceNodeId: health-arm
          targetNodeId: health-storage
          triggerMessageType: Complete
          isEnabled: true

        - sourceNodeId: health-storage
          targetNodeId: health-compute
          triggerMessageType: Complete
          isEnabled: true

# ============================================================================
# External Workflow Connections
# ============================================================================
connections:
  # ----------------------------------------------------------------------------
  # Phase 1 → Phase 2: Pre-deployment checks complete → Start node deployments
  # Pre-deployment checks container triggers node deployments container
  # ----------------------------------------------------------------------------

  - sourceNodeId: pre-deployment-checks
    targetNodeId: node-deployments
    triggerMessageType: Complete
    isEnabled: true

  # ----------------------------------------------------------------------------
  # Phase 2 → Phase 3: All node deployments complete → Start health checks
  # Node deployments container triggers health checks container
  # ----------------------------------------------------------------------------

  - sourceNodeId: node-deployments
    targetNodeId: health-checks
    triggerMessageType: Complete
    isEnabled: true

```

2. progress

```output
Workflow Execution (S 40.1s)                                        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
├── Workflow: azs-deployment (S 40.1s)                              ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  ├── ✓ pre-deployment-checks: Completed in 1.6s (S 23.0s)         ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ check-network: Completed in 0.5s (8.2s)                 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ check-storage: Completed in 0.5s (7.7s)                 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ check-prerequisites: Completed in 0.5s (7.1s)           ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  ├── ✓ node-deployments: Completed in 5.3s (P 14.3s)              ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ deploy-node1: Completed in 5.3s (S 14.1s)               ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node1/os-update: Completed in 1.8s (5.3s)     ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node1/stamp-update: Completed in 1.0s (4.2s)  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node1/sbe-update: Completed in 1.4s (2.8s)    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node1/mocarc-update: Completed in 1.0s (1.8s) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ deploy-node2: Completed in 5.2s (S 14.3s)               ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node2/os-update: Completed in 1.8s (5.3s)     ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node2/stamp-update: Completed in 1.0s (4.3s)  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node2/sbe-update: Completed in 1.4s (2.9s)    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node2/mocarc-update: Completed in 1.0s (1.9s) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ deploy-node3: Completed in 5.3s (S 14.1s)               ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node3/os-update: Completed in 1.8s (5.3s)     ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node3/stamp-update: Completed in 1.0s (4.2s)  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node3/sbe-update: Completed in 1.4s (2.8s)    ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  │  ├── ✓ deploy-node3/mocarc-update: Completed in 1.0s (1.8s) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  ├── ✓ health-checks: Completed in 1.8s (S 2.8s)                  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ health-portal: Completed in 0.4s (1.3s)                 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ health-arm: Completed in 0.4s (911ms)                   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ health-storage: Completed in 0.4s (474ms)               ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
│  │  ├── ✓ health-compute: Completed in 0.4s (31ms)                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 100%
```

See [usage_guide.md](usage_guide.md) for detailed examples and integration patterns.

### Architecture Highlights

The execution engine emphasizes:

- **Flexibility**: Support for C# and PowerShell scripts, dynamic node loading
- **Reliability**: Retry logic, dead letter queues, state persistence
- **Performance**: Lock-free circular buffers, parallel node execution
- **Observability**: Rich event model with progress tracking
- **Maintainability**: Clean abstractions, comprehensive test coverage

### Project Status

This execution engine is being integrated into the EtwIngest solution to provide automated, scheduled ETL processing with full workflow orchestration capabilities.

For detailed information, please refer to the documentation files linked above.
