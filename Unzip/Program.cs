//-------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Unzip
{
    using System.IO.Compression;

    public class Program
    {
        static void Main(string[] args)
        {
            // Provide the path to the main zip file and the extraction path
            string zipFilePath = @"C:\zips\AzureStackLogs-20240927104305-SAC14-ERCS01.zip";
            string extractionPath = @"C:\etls";

            // Extract the zip file including nested zips and folders
            ExtractZipFile(zipFilePath, extractionPath);
        }

        static void ExtractZipFile(string zipFilePath, string extractionPath)
        {
            // Create the extraction directory if it doesn't exist
            if (!Directory.Exists(extractionPath))
            {
                Directory.CreateDirectory(extractionPath);
            }

            // Open the zip file
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractionPath, entry.FullName);

                // Normalize the directory structure (convert '/' to system directory separator)
                destinationPath = destinationPath.Replace("/", Path.DirectorySeparatorChar.ToString());

                // Check if it's a directory or file
                if (string.IsNullOrEmpty(entry.Name)) // It's a directory
                {
                    // Create the directory if it doesn't exist
                    if (!Directory.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                }
                else if (entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) // Nested ZIP file
                {
                    // Extract the nested ZIP file into a subdirectory
                    string nestedZipExtractionPath =
                        Path.Combine(extractionPath, Path.GetFileNameWithoutExtension(entry.Name));

                    // Ensure directory for nested zip extraction exists
                    if (!Directory.Exists(nestedZipExtractionPath))
                    {
                        Directory.CreateDirectory(nestedZipExtractionPath);
                    }

                    // Copy the nested ZIP file to a temporary location
                    string tempZipPath = Path.Combine(nestedZipExtractionPath, entry.Name);
                    entry.ExtractToFile(tempZipPath, overwrite: true);

                    // Recursively extract the nested ZIP file
                    ExtractZipFile(tempZipPath, nestedZipExtractionPath);

                    // Optionally, delete the extracted nested ZIP file after processing
                    File.Delete(tempZipPath);
                }
                else // It's a file
                {
                    // Ensure the directory for the file exists
                    string directoryPath = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Copy the file to the target location
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }
    }
}
