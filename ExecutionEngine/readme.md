add design doc for execution engine:
1. execution plan is a graph that contains nodes and connectors.
2. execution of graph creates a workflow instance, it can be triggered by special node (timer, or manual)
3. graph execution have executionContext (dictionary of global variables, key-value), executionContext also contains message queue implemented by channels (use channel instead of event handler), these messages are strong-typed and produced by execution of each node
4. each node defines unit of execution with method "Task ExecuteAsync(CancellationToken)", node can produce the following events: OnStart, OnProgress(string status, int progressPercent) and following messages OnComplete, OnFail(Exception), messages are enqueued to a queue
5. OnComplete and OnFail can be connected to next node via channel subscription, these are edges within a graph, edge is one-way directed flow and defines dependency (or execution flow) from one node to another,
6. special node is subflow (workflow) that can reference and execute another graph
7. there are also special node behaves as control flow such as if-else, switch, while/foreach loop
8. graph can be validated by graph traverse, make sure there is no infinite loop
9. graph definition can be done in both json and yml file, execution of node can be both c# code, or powershell script
10. while graph is persisted to file, execution state of workflow instance are persisted to file, execution can be paused, resumed, cancelled