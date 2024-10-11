Feature: EvtxParser
  As a user,
  I want to be able to parse an EVTX file,
  So that I can ingest them into kusto

@evtx
Scenario: parse evtx file
	Given a evtx file at "%HOME%\Downloads\hci\evtx\Event_Microsoft.AzureStack.LCMController.EventSource-Admin.EVTX"
	When I parse evtx file
	Then I should get 4475 evtx records
	| TimeStamp             | Level       | EventId | LogName                                              | Description                   |
  | 10/8/2024 11:24:22 PM | Information | 1       | Microsoft.AzureStack.LCMController.EventSource/Admin | The request is not supported. |
