Feature: Error Handling
  As a workflow engine
  I want to handle failures gracefully
  So that workflows can recover from errors

  @integration @smoke @errorhandling
  Scenario: Node execution failure
    Given I have a workflow with nodes "Task", "ErrorHandler", "SuccessHandler"
    And "Task" connects to "SuccessHandler" on Complete
    And "Task" connects to "ErrorHandler" on Fail
    And "Task" is configured to throw an exception
    When I start the workflow
    Then "Task" should fail with an error
    And NodeFailed event should fire for "Task"
    And "ErrorHandler" should execute
    And "SuccessHandler" should not execute
    And the error message should be captured in NodeInstance

  @integration @errorhandling
  Scenario: Retry policy execution
    Given I have a node "FlakyTask" with retry policy:
      | MaxAttempts | Delay | BackoffMultiplier |
      | 3           | 100ms | 2.0               |
    And "FlakyTask" fails on attempts 1 and 2, succeeds on attempt 3
    When I start the workflow
    Then "FlakyTask" should be attempted 3 times
    And there should be delays between retries
    And the final attempt should succeed
    And the workflow should complete successfully

  @integration @errorhandling
  Scenario: Retry exhausted triggers failure
    Given I have a node with retry policy MaxAttempts = 3
    And the node always fails
    When I start the workflow
    Then the node should be retried 3 times
    And after final retry failure, the node should fail permanently
    And OnFail connections should be triggered
    And the workflow should fail

  @integration @errorhandling
  Scenario: Circuit breaker pattern
    Given I have a node "UnreliableService" with circuit breaker:
      | FailureThreshold | Timeout  | FallbackNodeId |
      | 3                | 5 seconds | FallbackNode   |
    And "UnreliableService" fails 3 times in a row
    When the circuit breaker opens
    Then subsequent calls should not execute "UnreliableService"
    And "FallbackNode" should execute instead
    And after timeout expires, circuit should allow retry
    And success should close the circuit

  @integration @errorhandling
  Scenario: Compensation transaction execution
    Given I have a workflow with nodes "Step1", "Step2", "Step3"
    And "Step1" has compensation node "UndoStep1"
    And "Step2" has compensation node "UndoStep2"
    And "Step3" is configured to fail
    When I start the workflow
    Then "Step1" and "Step2" should complete successfully
    And "Step3" should fail
    And compensation should execute in reverse order: UndoStep2, UndoStep1
    And compensation results should be logged
    And the workflow should fail with original error preserved

  @integration @errorhandling @slow
  Scenario: Workflow timeout
    Given I have a workflow with timeout 500 milliseconds
    And the workflow contains long-running nodes
    When I start the workflow
    Then the workflow should be cancelled after 500 milliseconds
    And a TimeoutException should be thrown
    And the workflow status should be "Cancelled"
    And partial results should be preserved

  @integration @errorhandling
  Scenario: Graceful degradation with fallback nodes
    Given I have node "Primary" with fallback "Secondary"
    And "Primary" circuit breaker is open
    When I start the workflow
    Then "Primary" should not execute
    And "Secondary" should execute as fallback
    And the workflow should complete successfully
    And output should indicate fallback was used

  @integration @errorhandling
  Scenario: Dead letter queue threshold alerts
    Given I have a dead letter queue with max size 10
    And 10 messages have failed and been added to DLQ
    When an 11th message fails
    Then the oldest DLQ entry should be removed
    And the new message should be added
    And DLQ size should remain at 10

  @integration @errorhandling
  Scenario: Partial workflow recovery
    Given I have a workflow that fails at node "Step5"
    And nodes "Step1" through "Step4" completed successfully
    When I resume the workflow from checkpoint
    Then nodes "Step1" through "Step4" should not re-execute
    And execution should resume at "Step5"
    And the workflow should complete if "Step5" succeeds

  @integration @errorhandling
  Scenario: Exception details preserved
    Given I have a node that throws a specific exception type
    When the node executes and fails
    Then the exception message should be captured
    And the exception stack trace should be available
    And the exception type should be preserved
    And all details should be in NodeInstance error data

  @integration @errorhandling
  Scenario: Cascading failure prevention
    Given I have a workflow with error handlers
    And node "A" fails
    When error handler "HandleA" also fails
    Then the workflow should not enter infinite error handling
    And appropriate failure should be recorded
    And the workflow should fail gracefully

  @integration @errorhandling
  Scenario: Timeout on individual node
    Given I have a node with timeout 200 milliseconds
    And the node executes for 500 milliseconds
    When I start the workflow
    Then the node should be cancelled after 200 milliseconds
    And the node status should be "Cancelled"
    And timeout error should be recorded

  @integration @errorhandling
  Scenario: Retry with exponential backoff
    Given I have a node with retry policy:
      | MaxAttempts | InitialDelay | BackoffMultiplier |
      | 4           | 100ms        | 2.0               |
    When the node fails repeatedly
    Then retry delays should be approximately 100ms, 200ms, 400ms
    And total retry time should reflect exponential backoff
    And backoff should prevent thundering herd

  @integration @errorhandling
  Scenario: Selective retry based on exception type
    Given I have a node with retry policy that retries on TransientException
    And the policy does not retry on PermanentException
    When the node throws TransientException
    Then retries should be attempted
    When the node throws PermanentException
    Then no retries should be attempted
    And the node should fail immediately
