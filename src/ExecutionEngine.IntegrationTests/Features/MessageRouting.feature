Feature: Message Routing
  As a workflow engine
  I want to route messages between nodes correctly
  So that workflow execution follows the defined graph

  @integration @smoke
  Scenario: Route completion message to downstream node
    Given I have a workflow with nodes "NodeA", "NodeB"
    And "NodeA" connects to "NodeB" on Complete
    When I start the workflow
    Then "NodeA" should send a NodeCompleteMessage
    And "NodeB" queue should receive the message
    And the message should contain "NodeA" execution context
    And "NodeB" should execute successfully

  @integration
  Scenario: Conditional message routing
    Given I have a workflow with nodes "Source", "Target1", "Target2"
    And "Source" connects to "Target1" on Complete with condition "GetInput('value') > 10"
    And "Source" connects to "Target2" on Complete with condition "GetInput('value') <= 10"
    And "Source" sets output "value" to <value>
    When I start the workflow
    Then only <executedNode> should execute

    Examples:
      | value | executedNode |
      | 15    | Target1      |
      | 5     | Target2      |
      | 10    | Target2      |

  @integration @parallel
  Scenario: Fan-out to multiple targets
    Given I have a workflow with nodes "Source", "T1", "T2", "T3"
    And "Source" connects to "T1" on Complete
    And "Source" connects to "T2" on Complete
    And "Source" connects to "T3" on Complete
    When I start the workflow
    Then all target queues should contain the message
    And the message routing count should be 3
    And all targets should execute successfully

  @integration
  Scenario: Source port based routing
    Given I have an IfElse node "Condition" with condition "GetGlobal('flag') == true"
    And "Condition" connects to "TrueHandler" via "TrueBranch" port
    And "Condition" connects to "FalseHandler" via "FalseBranch" port
    And workflow variable "flag" is set to <flag>
    When I start the workflow
    Then only <executedNode> should execute
    And the message should have SourcePort "<port>"

    Examples:
      | flag  | executedNode | port        |
      | true  | TrueHandler  | TrueBranch  |
      | false | FalseHandler | FalseBranch |

  @integration @errorhandling
  Scenario: Dead letter queue for failed messages
    Given I have a workflow with nodes "Source", "Target"
    And "Source" connects to "Target" on Complete
    And "Target" node queue is missing
    When I start the workflow
    And "Source" completes successfully
    Then the message should be added to dead letter queue
    And the DLQ entry should contain the failure reason
    And the DLQ entry should contain the original message

  @integration
  Scenario: Message routing with multiple message types
    Given I have a workflow with nodes "Task", "SuccessHandler", "FailureHandler"
    And "Task" connects to "SuccessHandler" on Complete
    And "Task" connects to "FailureHandler" on Fail
    When I start the workflow with "Task" configured to <result>
    Then <executedHandler> should execute
    And the message type should be <messageType>

    Examples:
      | result  | executedHandler | messageType |
      | success | SuccessHandler  | Complete    |
      | failure | FailureHandler  | Fail        |

  @integration
  Scenario: Disabled connection skips routing
    Given I have a workflow with nodes "Source", "Target1", "Target2"
    And "Source" connects to "Target1" on Complete
    And "Source" connects to "Target2" on Complete with IsEnabled set to false
    When I start the workflow
    Then only "Target1" should execute
    And "Target2" should not execute
    And the message should not be routed to "Target2"

  @integration
  Scenario: Router handles null or invalid contexts gracefully
    Given I have a workflow with nodes "Source", "Target"
    And "Source" connects to "Target" on Complete
    And "Source" produces a null execution context
    When I start the workflow
    Then the router should handle the null context
    And the workflow should not crash
    And appropriate error handling should occur
