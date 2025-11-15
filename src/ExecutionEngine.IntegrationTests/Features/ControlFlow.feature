Feature: Control Flow
  As a workflow designer
  I want to use conditional branching and loops
  So that I can create dynamic workflows

  @integration @smoke
  Scenario: If-Else conditional execution
    Given I have an IfElse node "CheckValue" with condition "GetGlobal('count') > 5"
    And "CheckValue" connects to "HighValueHandler" via "TrueBranch"
    And "CheckValue" connects to "LowValueHandler" via "FalseBranch"
    And workflow variable "count" is set to <count>
    When I start the workflow
    Then only <executedNode> should execute
    And the node output should contain BranchTaken "<branch>"
    And the node output should contain ConditionResult <result>

    Examples:
      | count | executedNode     | branch      | result |
      | 10    | HighValueHandler | TrueBranch  | true   |
      | 3     | LowValueHandler  | FalseBranch | false  |

  @integration
  Scenario: If-Else with complex condition accessing multiple contexts
    Given I have an IfElse node with condition "(int)GetInput('age') >= 18 && GetGlobal('country') == 'US'"
    And the node receives input "age" with value <age>
    And workflow variable "country" is set to "<country>"
    When the IfElse node executes
    Then the condition should evaluate to <result>

    Examples:
      | age | country | result |
      | 21  | US      | true   |
      | 17  | US      | false  |
      | 21  | UK      | false  |

  @integration @smoke
  Scenario: ForEach collection iteration
    Given I have a ForEach node "ProcessFiles" with collection expression "GetGlobal('files')"
    And "ProcessFiles" connects to "ProcessFile" via "LoopBody" with trigger type "Next"
    And workflow variable "files" contains ["file1.txt", "file2.txt", "file3.txt"]
    When I start the workflow
    Then "ProcessFile" should execute 3 times
    And each iteration should receive a different file name
    And each iteration should receive the correct index (0, 1, 2)
    And the ForEach output should show ItemsProcessed = 3

  @integration
  Scenario: ForEach with LINQ expression
    Given I have a ForEach node with collection expression "GetGlobal('items').Cast<int>().Where(x => x > 5)"
    And workflow variable "items" contains [1, 3, 6, 8, 10, 12]
    When I start the workflow
    Then the loop body should execute 4 times
    And iterations should process values [6, 8, 10, 12]

  @integration @smoke
  Scenario: While loop with condition re-evaluation
    Given I have a While node "CounterLoop" with condition "GetGlobal('counter') < 5"
    And "CounterLoop" connects to "IncrementCounter" via "LoopBody" with trigger type "Next"
    And "IncrementCounter" connects back to "CounterLoop" on Complete
    And "IncrementCounter" increments workflow variable "counter"
    And workflow variable "counter" is set to 0
    When I start the workflow
    Then the loop should execute 5 times
    And the final value of "counter" should be 5
    And the While output should show IterationCount = 5

  @integration
  Scenario: While loop max iterations protection
    Given I have a While node with condition "true" and MaxIterations 10
    When I start the workflow
    Then the loop should stop after 10 iterations
    And the node should fail with "infinite loop" error
    And the workflow should fail

  @integration @smoke
  Scenario: Switch multi-way branching
    Given I have a Switch node "Router" with expression "(string)GetInput('status')"
    And "Router" has cases:
      | caseValue | portName    |
      | success   | SuccessPort |
      | failure   | FailurePort |
      | pending   | PendingPort |
    And "Router" connects to "HandleSuccess" via "SuccessPort"
    And "Router" connects to "HandleFailure" via "FailurePort"
    And "Router" connects to "HandlePending" via "PendingPort"
    And "Router" connects to "HandleDefault" via "Default"
    When the node receives input "status" with value "<status>"
    And I start the workflow
    Then only <executedNode> should execute
    And the output should contain MatchedCase "<matchedCase>"

    Examples:
      | status  | executedNode  | matchedCase |
      | success | HandleSuccess | success     |
      | failure | HandleFailure | failure     |
      | unknown | HandleDefault | Default     |

  @integration
  Scenario: Switch with expression evaluation
    Given I have a Switch node with expression "GetInput('score') >= 90 ? 'A' : GetInput('score') >= 70 ? 'B' : 'C'"
    And the node receives input "score" with value <score>
    When the Switch node executes
    Then the expression should evaluate to "<grade>"
    And the matched case should be "<grade>"

    Examples:
      | score | grade |
      | 95    | A     |
      | 75    | B     |
      | 60    | C     |

  @integration @slow
  Scenario: Nested loop iteration
    Given I have an outer ForEach node iterating over [1, 2, 3]
    And an inner ForEach node iterating over ["A", "B"]
    When I start the workflow
    Then the total executions should be 6
    And I should see combinations [(1,A), (1,B), (2,A), (2,B), (3,A), (3,B)]

  @integration
  Scenario: While loop with external variable modification
    Given I have a While node with condition "GetGlobal('flag') == true"
    And a loop body that sets "flag" to false after 3 iterations
    When I start the workflow
    Then the loop should execute exactly 3 times
    And the loop should terminate when flag becomes false

  @integration
  Scenario: ForEach with empty collection
    Given I have a ForEach node with an empty collection
    When I start the workflow
    Then the loop body should not execute
    And the ForEach should complete successfully
    And ItemsProcessed should be 0

  @integration
  Scenario: Branch convergence after If-Else
    Given I have an IfElse node with True and False branches
    And both branches connect to a common "Join" node
    When I start the workflow with condition = <condition>
    Then the "<executedBranch>" branch should execute
    And the "Join" node should execute after the branch
    And the workflow should complete successfully

    Examples:
      | condition | executedBranch |
      | true      | TrueBranch     |
      | false     | FalseBranch    |
