Feature: EvtxParser
  As a user,
  I want to be able to parse an EVTX file,
  So that I can ingest them into kusto

@evtx
Scenario: parse evtx file
	Given a evtx file at "C:\\Users\\xiaodoli\\Downloads\\Event_Microsoft-Windows-FailoverClustering-Diagnostic.evtx"
	When I parse evtx file
	Then I should get the following events
