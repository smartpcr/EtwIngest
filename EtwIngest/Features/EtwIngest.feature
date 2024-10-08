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


@ingest
	Scenario: ingest a large etl files into kusto
		Given etl file "C:\\Users\\xiaodoli\\Downloads\\SAC14-S1-N01_HostAgent.SDNDiagnosticsTrace.2024-09-25.12.etl"
		When I parse etl file
		And ensure kusto tables for all events are created
		Then total of 745 kusto tables should be created, including
		| TableName                                                                                                  |
		| ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2                                                    |
		| ETL-Microsoft-Windows-Services.ServiceConfigChange                                                         |
		| ETL-Microsoft-Windows-TCPIP.Ndkpi_Arm_Cq                                                                   |
		| ETL-Microsoft-Windows-TCPIP.TcpSwsAvoidanceBegin                                                           |
		| ETL-Microsoft-AzureStack-CloudEngine.PowerShellRemoteStepStop                                              |
		| ETL-Microsoft-AzureStack-Roles-KB-EventSource.LiveUpdateKbStart                                            |
		| ETL-Microsoft-AzureStack-Infrastructure-ServiceSettings-Sdk.OperationStart                                 |
		| ETL-Microsoft-Windows-Sysprep.RunRegistryDllsStart                                                         |
		| ETL-Microsoft-AzureStack-CloudEngine.ActionPlanStart                                                       |
		| ETL-Microsoft-AzureStack-Common-Infrastructure-HostModel-ServiceFabricHost.InitializeStateSerializersStart |
		| ETL-Microsoft-AzureStack-Usage.UsageQueryRequest                                                           |
		| ETL-Microsoft-AzureStack-Infrastructure-Health-Refresher.RefreshCycleStop                                  |
		| ETL-Microsoft-AzureStack-UsageAdminHealthProbe.EndHealthProbe                                              |
		| ETL-Microsoft-Windows-OobeLdr.OobeLdrProcessUnattend                                                       |
		| ETL-Microsoft-AzureStack-Infrastructure-Health-RunnerService.InvalidOldResult                              |
		| ETL-Microsoft-Windows-Windeploy.LaunchandwaitforexternalprocessStart                                       |
		| ETL-Microsoft-AzureStack-Infrastructure-Health-WindowsServiceHost.ServiceAssemblyFindResult                |
		| ETL-Microsoft-AzureStack-Common-Infrastructure-Http.ClientRequestStop                                      |
		| ETL-Microsoft-AzureStack-Infrastructure-Health-HealthStorePlugin.FaultSubStateMachine                      |
		When I extract etl file to target folder "c:\\kustodata\\staging"
		Then I should generate the 88 csv files that include the following
		  | FileName                                                                                    |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.AddNicRedirectionRules.csv |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ApplyingState.csv          |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.CertValidated.csv          |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ChannelClosedCallback.csv  |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.InfrastructureRules.csv    |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.NoActiveUpdates.csv        |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.NoOvsdbRules.csv           |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.OvsdbDbg.csv               |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.PluginEvent.csv            |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ProcessingUpdate.csv       |
		  | ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.SchedulingUpdate.csv       |
		When I ingest etl files into kusto
		Then the following kusto tables should have the following records
		| TableName                                                                                                 | RecordCount |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.TimeoutThreadStatistics                      | 3355        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.SchedulingUpdate                 | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.ReconcilePortsCallBackEndStop                   | 113         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.ProcessingUpdate                 | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.DnsProxyOnUpdate                             | 6           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.OvsdbDbg                                 | 3376        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.MeterDeleteSeqData                           | 11          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.OnCountersTimerCallback                      | 4           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.GetUsageByVsidCompleted                      | 11          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.NoOvsdbRules                             | 10          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.VfpPort                                      | 51          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VSwitchPlugin.CertValidated                             | 51          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.VfpSwitch                                    | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.NoActiveUpdates                              | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.RetrievedSwitchCache                         | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.PortState                        | 16          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.OvsdbDbg                         | 3375        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.OnMeterTimerCallback                         | 14          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.NoInfrastructureRules            | 8           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.AddOrUpdateGatewayTunnelConfigTriggersGSD | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.PluginEvent                              | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.ReconcilePorts                                  | 113         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.VmmsPid                                   | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.PortStateCheck                                  | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ApplyingPortSettings                         | 51          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VSwitchPlugin.ChannelClosedCallback                     | 51          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.OvsdbSwitch                                  | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.AddOrUpdateGatewayTunnelConfigurations    | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.AppliedState                                 | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.OvsdbDbg                                     | 3383        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.NoOvsdbRules                     | 26          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ReusingCompartment                           | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.InfrastructureRules                      | 8           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.PortState                                    | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.NoActiveUpdates                          | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.PluginEvent                      | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.RemovingGroup                                | 96          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.NoPaIsolationId                           | 6           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.VmmsPid                                      | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.ChangingPortState                               | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.OvsdbDbg                                        | 7266        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.ReconcilePortsCallBackStartStart                | 113         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.Update                                   | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ProcessingUpdate                             | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ApplyingSwitchSettings                       | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.BuildingRdidRules                            | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.UpdatingOvsdbPorts                           | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.UpdateCompleted                          | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.DnsSocketPool                                | 3355        |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ChannelClosedCallback                    | 32          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.AddGatewayCommon                          | 6           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.PreservingOvsdbSwitch                        | 3           |
		| ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2                                                   | 1           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.SchedulingUpdate                         | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.PolicyApplied                                   | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.AddGateway                                | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.UpdatedHealthState                        | 751         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.UpdateHandler                                | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.MeterConvertWcfCounters                      | 11          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.PreservingOvsdbPort                          | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.PluginEvent                                  | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.CertValidated                                | 43          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.SchedulingUpdate                             | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.MultiCompartmentNetworkProxyOnUpdate         | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ApplyingDrPortSettings                       | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ProcessingUpdate                         | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.Service.PluginNotification                              | 24          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.ApplyingState                            | 26          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.AllTunnelsOnGatewayRemoved                | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.UpdatedOvsdbPorts                            | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.UpdateCompleted                  | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.BuildingState                                | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.RemovingLayer                                | 336         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.Update                           | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ChannelClosedCallback                        | 43          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ProcessedPrMeterDelta                        | 11          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.OnProcessUnsolicitedRouterAdvertisement      | 6           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.HealthMonitorTimerCallbackEnd             | 751         |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.BuiltState                                   | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.CertValidated                             | 9           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.ApplyingState                    | 26          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.CertValidated                            | 32          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.ServiceInsertionPlugin.NoActiveUpdates                  | 2           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.ChannelClosedCallback                     | 9           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.StartGatewayMonitoring                    | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.AddNicRedirectionRules                   | 80          |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.GatewayPlugin.Error                                     | 3           |
		| ETL-Microsoft.Windows.NetworkController.HostAgent.VNetPlugin.ApplyingState                                | 3           |
