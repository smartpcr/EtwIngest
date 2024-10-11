//-------------------------------------------------------------------------------
// <copyright file="UnzipSteps.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Steps
{
    using FluentAssertions;
    using Libs;
    using Reqnroll;

    [Binding]
    public class UnzipSteps
    {
        private readonly ScenarioContext context;
        private readonly IReqnrollOutputHelper outputWriter;

        public UnzipSteps(ScenarioContext context, IReqnrollOutputHelper outputWriter)
        {
            this.context = context;
            this.outputWriter = outputWriter;
        }

        [Given("Given one or more zip files in folder {string}")]
        public void GivenGivenOneOrMoreZipFilesInFolder(string zipFolder)
        {
            Directory.Exists(zipFolder).Should().BeTrue();
            var zipFiles = Directory.GetFiles(zipFolder, "*.zip", SearchOption.TopDirectoryOnly);
            zipFiles.Should().NotBeNullOrEmpty();
            this.outputWriter.WriteLine($"total of {zipFiles.Length} zip files found");
            this.context.Set(zipFolder, "zipFolder");
        }

        [When("I extract zip files to collect etl files to folder {string}")]
        public void WhenIExtractZipFilesToCollectEtlFilesToFolder(string etlFolder)
        {
            if (!Directory.Exists(etlFolder))
            {
                Directory.CreateDirectory(etlFolder);
            }

            var zipFolder = this.context.Get<string>("zipFolder");
            var zipFiles = Directory.GetFiles(zipFolder, "*.zip", SearchOption.TopDirectoryOnly);
            foreach (var zipFile in zipFiles)
            {
                var unzipHelper = new UnzipHelper(zipFile, etlFolder, "etl");
                unzipHelper.Process();
            }
        }

        [Then("I should see all etl files in folder {string}")]
        public void ThenIShouldSeeAllEtlFilesInFolder(string etlFolder)
        {
            Directory.Exists(etlFolder).Should().BeTrue();
            var etlFiles = Directory.GetFiles(etlFolder, "*.etl", SearchOption.AllDirectories);
            etlFiles.Should().NotBeNullOrEmpty();
        }

    }
}
