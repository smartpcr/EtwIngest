//-------------------------------------------------------------------------------
// <copyright file="UnzipHelper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Steps
{
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;

    public class UnzipHelper
    {
        private readonly HashSet<string> uniqueFiles = new();
        private readonly string zipFile;
        private readonly string outputFolder;
        private readonly string ext;

        public UnzipHelper(string zipFile, string outputFolder, string ext)
        {
            this.zipFile = zipFile;
            this.outputFolder = outputFolder;
            this.ext = ext;
        }

        public void Process()
        {
            // Open the zip file
            using ZipArchive archive = ZipFile.OpenRead(this.zipFile);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                // If the entry is a file
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    // If it's another ZIP file, recursively process it
                    if (entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        using var memoryStream = new MemoryStream();
                        entry.Open().CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        ProcessZipStream(memoryStream);
                    }
                    else if (entry.Name.EndsWith($".{this.ext}", StringComparison.OrdinalIgnoreCase))
                    {
                        // Process the regular file and add its hash if unique
                        using var fileStream = entry.Open();
                        string fileHash = ComputeFileHash(fileStream);
                        if (uniqueFiles.Add(fileHash))
                        {
                            Console.WriteLine($"Unique file found: {entry.FullName}");
                            var outputFilePath = Path.Combine(this.outputFolder, entry.Name);
                            using var outputFileStream = File.Create(outputFilePath);
                            fileStream.CopyTo(outputFileStream);
                            fileStream.Flush();
                        }
                    }
                }
            }
        }

        private void ProcessZipStream(Stream zipStream)
        {
            // Process a zip file from a stream (nested zip file)
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    if (entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        using var memoryStream = new MemoryStream();
                        entry.Open().CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        ProcessZipStream(memoryStream);
                    }
                    else if (entry.Name.EndsWith($".{this.ext}", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fileStream = entry.Open();
                        string fileHash = ComputeFileHash(fileStream);
                        if (uniqueFiles.Add(fileHash))
                        {
                            Console.WriteLine($"Unique file found: {entry.FullName}");
                            var outputFilePath = Path.Combine(this.outputFolder, entry.Name);
                            using var outputFileStream = File.Create(outputFilePath);
                            fileStream.CopyTo(outputFileStream);
                            fileStream.Flush();
                        }
                    }
                }
            }
        }

        private string ComputeFileHash(Stream fileStream)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
