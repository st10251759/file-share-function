using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Storage.Files.Shares;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System.Net;

namespace FileStorageFunction
{
    public class FileStorageFunction
    {
        private readonly ILogger<FileStorageFunction> _logger;
        public FileStorageFunction(ILogger<FileStorageFunction> logger)
        {
            _logger = logger;
        }

        [Function("UploadFileToShare")] // This attribute identifies the method as an Azure Function named "UploadFileToShare".
        public async Task<IActionResult> Run(
    // The method is triggered via an HTTP request, allowing GET and POST methods, with no specific route defined.
    [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            // Log the start of the file processing request.
            _logger.LogInformation("FileShareFunction processing a request for a file.");

            // Check if there are any files uploaded in the request.
            // The files are expected to be in the form-data section of the request body.
            if (req.Form.Files.Count == 0)
            {
                // If no files are uploaded, return a Bad Request (HTTP 400) response with a message indicating that no file was provided.
                return new BadRequestObjectResult("No file uploaded.");
            }

            // If a file is present, select the first file in the uploaded files list.
            var fileUpload = req.Form.Files[0];

            try
            {
                // Begin Azure File Share interaction using the Azure SDK to upload the file to Azure Storage.
                // Retrieve the connection string for Azure Storage from environment variables (for security reasons).
                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

                // Create a ShareClient instance to connect to the Azure File Share named "contractsshare".
                ShareClient share = new ShareClient(connectionString, "contractsshare");

                // Check if the specified file share exists in Azure Storage.
                // If the file share does not exist, return an Internal Server Error (HTTP 500) indicating the issue.
                if (!await share.ExistsAsync())
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                // Get a reference to the "uploads" directory within the file share using the ShareDirectoryClient.
                ShareDirectoryClient directory = share.GetDirectoryClient("uploads");

                // Create a ShareFileClient to represent the file being uploaded.
                // The file's name will be set to the uploaded file's name.
                ShareFileClient fileClient = directory.GetFileClient(fileUpload.FileName);

                // Open a read stream to the uploaded file so its contents can be read and uploaded to the file share.
                using (var stream = fileUpload.OpenReadStream())
                {
                    // Create the file in the Azure File Share with the specified file length (size).
                    await fileClient.CreateAsync(fileUpload.Length);

                    // Upload the file's contents in chunks (or ranges) to Azure.
                    // Here, the entire file is uploaded as one range, starting from byte 0 to the file's length.
                    await fileClient.UploadRangeAsync(new HttpRange(0, fileUpload.Length), stream);
                }

                // If the file upload is successful, return an OK (HTTP 200) response indicating the success.
                return new OkObjectResult("File uploaded successfully.");
            }
            catch (Exception ex)
            {
                // In case of any exceptions during the file upload process, log the error details for diagnostics.
                _logger.LogError(ex, "An error occurred during file upload.");

                // Return an Internal Server Error (HTTP 500) response to indicate that something went wrong.
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

    }

}