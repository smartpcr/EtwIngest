// -----------------------------------------------------------------------
// <copyright file="WorkflowLoader.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides simplified API for loading and saving workflow definitions.
/// </summary>
public class WorkflowLoader
{
    private readonly ILogger<WorkflowLoader> logger;
    private readonly WorkflowSerializer serializer;
    private readonly WorkflowValidator validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowLoader"/> class.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider for DI-based logging.</param>
    public WorkflowLoader(IServiceProvider? serviceProvider = null)
    {
        // Get ILoggerFactory from service provider if available, otherwise use NullLoggerFactory
        var loggerFactory = serviceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;
        this.logger = loggerFactory.CreateLogger<WorkflowLoader>();

        this.serializer = new WorkflowSerializer();
        this.validator = new WorkflowValidator(serviceProvider);
    }

    /// <summary>
    /// Loads and validates a workflow definition from a file.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <param name="validateOnLoad">Whether to validate the workflow after loading.</param>
    /// <returns>The loaded workflow definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown if validation fails when validateOnLoad is true.</exception>
    public WorkflowDefinition Load(string filePath, bool validateOnLoad = true)
    {
        this.logger.LogDebug("Loading workflow from file: {FilePath}", filePath);
        var workflow = this.serializer.LoadFromFile(filePath);

        if (validateOnLoad)
        {
            var validationResult = this.validator.Validate(workflow);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    this.logger.LogError("Workflow validation error for '{FilePath}': {Error}", filePath, error);
                }

                var errorMessage = $"Workflow validation failed for '{filePath}':{Environment.NewLine}" +
                    string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException(errorMessage);
            }

            // Log warnings if any
            foreach (var warning in validationResult.Warnings)
            {
                this.logger.LogWarning("Workflow validation warning for '{FilePath}': {Warning}", filePath, warning);
            }
        }

        this.logger.LogInformation("Successfully loaded workflow '{WorkflowId}' from '{FilePath}'", workflow.WorkflowId, filePath);
        return workflow;
    }

    /// <summary>
    /// Saves a workflow definition to a file.
    /// </summary>
    /// <param name="workflow">The workflow to save.</param>
    /// <param name="filePath">The file path to save to.</param>
    /// <param name="validateBeforeSave">Whether to validate the workflow before saving.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation fails when validateBeforeSave is true.</exception>
    public void Save(WorkflowDefinition workflow, string filePath, bool validateBeforeSave = true)
    {
        this.logger.LogDebug("Saving workflow '{WorkflowId}' to file: {FilePath}", workflow?.WorkflowId, filePath);

        if (validateBeforeSave)
        {
            var validationResult = this.validator.Validate(workflow!);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    this.logger.LogError("Workflow validation error for '{WorkflowId}': {Error}", workflow?.WorkflowId, error);
                }

                var errorMessage = $"Workflow validation failed:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException(errorMessage);
            }

            // Log warnings if any
            foreach (var warning in validationResult.Warnings)
            {
                this.logger.LogWarning("Workflow validation warning for '{WorkflowId}': {Warning}", workflow?.WorkflowId, warning);
            }
        }

        this.serializer.SaveToFile(workflow!, filePath);
        this.logger.LogInformation("Successfully saved workflow '{WorkflowId}' to '{FilePath}'", workflow?.WorkflowId, filePath);
    }

    /// <summary>
    /// Validates a workflow without loading from file.
    /// </summary>
    /// <param name="workflow">The workflow to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(WorkflowDefinition workflow)
    {
        this.logger.LogDebug("Validating workflow '{WorkflowId}'", workflow?.WorkflowId);
        var result = this.validator.Validate(workflow!);

        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                this.logger.LogError("Workflow validation error for '{WorkflowId}': {Error}", workflow?.WorkflowId, error);
            }
        }

        foreach (var warning in result.Warnings)
        {
            this.logger.LogWarning("Workflow validation warning for '{WorkflowId}': {Warning}", workflow?.WorkflowId, warning);
        }

        return result;
    }

    /// <summary>
    /// Loads a workflow from a file without validation.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>The loaded workflow definition.</returns>
    public WorkflowDefinition LoadWithoutValidation(string filePath)
    {
        this.logger.LogDebug("Loading workflow from file without validation: {FilePath}", filePath);
        var workflow = this.serializer.LoadFromFile(filePath);
        this.logger.LogInformation("Loaded workflow '{WorkflowId}' from '{FilePath}' (validation skipped)", workflow.WorkflowId, filePath);
        return workflow;
    }
}
