// -----------------------------------------------------------------------
// <copyright file="WorkflowLoader.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System;
using System.IO;

/// <summary>
/// Provides simplified API for loading and saving workflow definitions.
/// </summary>
public class WorkflowLoader
{
    private readonly WorkflowSerializer serializer;
    private readonly WorkflowValidator validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowLoader"/> class.
    /// </summary>
    public WorkflowLoader()
    {
        this.serializer = new WorkflowSerializer();
        this.validator = new WorkflowValidator();
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
        var workflow = this.serializer.LoadFromFile(filePath);

        if (validateOnLoad)
        {
            var validationResult = this.validator.Validate(workflow);
            if (!validationResult.IsValid)
            {
                var errorMessage = $"Workflow validation failed for '{filePath}':{Environment.NewLine}" +
                    string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException(errorMessage);
            }
        }

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
        if (validateBeforeSave)
        {
            var validationResult = this.validator.Validate(workflow);
            if (!validationResult.IsValid)
            {
                var errorMessage = $"Workflow validation failed:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException(errorMessage);
            }
        }

        this.serializer.SaveToFile(workflow, filePath);
    }

    /// <summary>
    /// Validates a workflow without loading from file.
    /// </summary>
    /// <param name="workflow">The workflow to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(WorkflowDefinition workflow)
    {
        return this.validator.Validate(workflow);
    }

    /// <summary>
    /// Loads a workflow from a file without validation.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>The loaded workflow definition.</returns>
    public WorkflowDefinition LoadWithoutValidation(string filePath)
    {
        return this.serializer.LoadFromFile(filePath);
    }
}
