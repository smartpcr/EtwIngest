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
