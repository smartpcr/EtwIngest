Feature: Unzip
	As a user,
	I want to be able to extract zip files,
	So that I can put all etl files in one folder.

@unzip
Scenario: Extract zip files
	Given Given one or more zip files in folder "C:\\zips"
	When I extract zip files to collect etl files to folder "C:\\etls"
	Then I should see all etl files in folder "C:\\etls"
