Feature: EvtxParser
  As a user,
  I want to be able to parse an EVTX file,
  So that I can ingest them into kusto

  Background:
    Given kusto cluster uri "http://172.20.102.248:8080"
    And kusto database name "hci"
    And kustainer volume mount from "E:\\kustodata" to "/kustodata"

@evtx
Scenario: parse evtx file
	Given a evtx file at "%HOME%\Downloads\hci\evtx\Event_Microsoft.AzureStack.LCMController.EventSource-Admin.EVTX"
	When I parse evtx file
	Then I should get 4475 evtx records
	| TimeStamp             | Level       | EventId | LogName                                              | Description                   |
  | 10/8/2024 11:24:22 PM | Information | 1       | Microsoft.AzureStack.LCMController.EventSource/Admin | The request is not supported. |

@evtx
Scenario: parse multiple evtx files
  Given A zip file at "%HOME%\\Downloads\\hci.zip"
  When I extract "evtx" files from zip file to folder "%HOME%\\Downloads\\hci\\evtx"
  Then I should see the following "evtx" files in folder "%HOME%\\Downloads\\hci\\evtx"
    | FileName                                                        |
    | Event_Microsoft.AzureStack.LCMController.EventSource-Admin.evtx |
    | Event_Microsoft-Windows-WinRM-Operational.evtx                  |
    | Event_Microsoft-Windows-WMI-Activity-Operational.evtx           |
  When I parse evtx files in folder "%HOME%\\Downloads\\hci\\evtx"
  Then I should find 4475 distinct records in evtx files
  When I create table based on evtx record schema
  Then I should see following evtx kusto table
    | TableName     |
    | WindowsEvents |
  And kusto table "WindowsEvents" should have the following columns
  | ColumnName   | DataType |
  | TimeStamp    | datetime |
  | ProviderName | string   |
  | LogName      | string   |
  | MachineName  | string   |
  | EventId      | int      |
  | Level        | string   |
  | Opcode       | dynamic  |
  | Keywords     | string   |
  | ProcessId    | dynamic  |
  | Description  | string   |

@evtx
Scenario: parse evtx file into csv files
  Given a evtx file at "%HOME%\Downloads\hci\evtx\Event_Microsoft.AzureStack.LCMController.EventSource-Admin.EVTX"
  When I ingest evtx file to kusto
  Then the following kusto tables should have added records with expected counts
    | TableName     | RecordCount |
    | WindowsEvents | 1           |