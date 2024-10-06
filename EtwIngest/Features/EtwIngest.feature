Feature: EtwIngest
    As a user,
    I want to be able to extract ETW events from a file,
    and infer its kusto table schema based on provider and event,
    and ingest the events into the kusto table.

	Background:
		Given kusto cluster uri "http://172.24.102.61:8080"
		And kusto database name "Dell"
		And kustainer volume mount from "c:\\kustodata" to "/kustodata"

    @parser
    Scenario: extract etl file
        Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_AzureStack.Compute.HostPluginWatchDog.2024-09-23.1.etl"
        When I parse etl file
        Then the result have the following events
          | ProviderName                                    | EventName                           |
          | MSNT_SystemTrace                                | EventTrace/PartitionInfoExtensionV2 |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | ManifestData                        |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | StartWatchDog/Start                 |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | ConfigFilesFound                    |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | StartWatchDog/Stop                  |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | ReadConfigFromStore                 |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | EnsureProcessStarted/Start          |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | ProcessStarted                      |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | EnsureProcessStarted/Stop           |
          | Microsoft-AzureStack-Compute-HostPluginWatchDog | FoundProcessAlreadyRunning          |

@schema
    Scenario: infer kusto schema
		Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_AzureStack.Compute.HostPluginWatchDog.2024-09-23.1.etl"
		When I infer schema for provider "Microsoft-AzureStack-Compute-HostPluginWatchDog" and event "EnsureProcessStarted/Stop"
		Then the result have the following schema
		  | ColumnName        | DataType | Nullable |
		  | TimeStamp         | datetime | false    |
		  | ProcessID         | int      | false    |
		  | ProcessName       | string   | false    |
		  | Level             | int      | false    |
		  | Opcode            | int      | false    |
		  | OpcodeName        | string   | false    |
		  | correlationVector | string   | true     |
		  | name              | string   | true     |
		  | executablePath    | string   | true     |
		  | arguments         | string   | true     |
		  | workingDirectory  | string   | true     |
		  | duration          | long     | true     |
		  | exception         | string   | true     |

@schema
	Scenario: ensure kusto table
		Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_AzureStack.Compute.HostPluginWatchDog.2024-09-23.1.etl"
		When I infer schema for provider "Microsoft-AzureStack-Compute-HostPluginWatchDog" and event "EnsureProcessStarted/Stop"
		Then Kusto table name should be "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop"
		When I ensure kusto table
		Then the table should be created with the following schema
		  | ColumnName        | DataType | Nullable |
		  | TimeStamp         | datetime | false    |
		  | ProcessID         | int      | false    |
		  | ProcessName       | string   | false    |
		  | Level             | int      | false    |
		  | Opcode            | int      | false    |
		  | OpcodeName        | string   | false    |
		  | correlationVector | string   | true     |
		  | name              | string   | true     |
		  | executablePath    | string   | true     |
		  | arguments         | string   | true     |
		  | workingDirectory  | string   | true     |
		  | duration          | long     | true     |
		  | exception         | string   | true     |

@extract
	Scenario: extract etl file to csv file
		Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_AzureStack.Compute.HostPluginWatchDog.2024-09-23.1.etl"
		When I parse etl file
		And I extract etl file to target folder "c:\\kustodata\\staging"
		Then I should generate the following csv files
		  | FileName                                                                           |
		  | ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2.csv                        |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData.csv               |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart.csv         |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound.csv           |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop.csv          |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore.csv        |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart.csv  |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted.csv             |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop.csv   |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning.csv |

@ingest
	Scenario: ingest etl files into kusto
		Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_AzureStack.Compute.HostPluginWatchDog.2024-09-23.1.etl"
		When I parse etl file
		And ensure kusto tables for all events are created
		Then the following kusto tables should be created
		| TableName                                                                      |
		| ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2                        |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData               |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart         |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop          |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore        |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart  |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted             |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop   |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning |
		When I extract etl file to target folder "c:\\kustodata\\staging"
		Then I should generate the following csv files
		  | FileName                                                                           |
		  | ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2.csv                        |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData.csv               |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart.csv         |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound.csv           |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop.csv          |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore.csv        |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart.csv  |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted.csv             |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop.csv   |
		  | ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning.csv |
		When I ingest etl files into kusto
		Then the following kusto tables should have the following records
		| TableName                                                                      | RecordCount |
		| ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2                        | 1           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData               | 1           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart         | 1           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound           | 584         |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop          | 1           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore        | 1138        |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart  | 1138        |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted             | 2           |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop   | 1138        |
		| ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning | 1136        |