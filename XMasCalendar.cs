using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace de.softwaremess.xmas.api
{
    public static class XMasCalendar
    {
        [FunctionName("GetItem")]
        public static async Task<IActionResult> GetCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
            //[Blob("{calendar}/{day}", FileAccess.Read)] Stream item,
            string calendar, int day, ILogger log)
        {
            log.LogInformation($"C# HTTP trigger GET item processed a request for day {day}.");
            var connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);
            if (!blobContainer.Exists())
            {
                return new OkObjectResult($"Calendar {calendar} does not exist") ;
            }
            BlobClient blob =  blobContainer.GetBlobClient(day.ToString());
            if(!blob.Exists())
            {
                return new OkObjectResult($"Item {day} does not exist") ;
            }
            var resultStream = new MemoryStream();
            blob.DownloadTo(resultStream);
            resultStream.Position = 0;
            
            return new FileStreamResult(resultStream, "application/octet-stream");
        }

        [FunctionName("SetItem")]
        public static async Task<IActionResult> SetCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
            [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            var blob = blobContainer.GetBlobClient($"{day}");
            if (blob.Exists())
            {
                return new OkObjectResult($"Already exists");
            }
            else
            {
                await blob.UploadAsync(req.Body, overwrite: false);
                return new OkObjectResult($"Created");
            }
        }

        [FunctionName("UpdateItem")]
        public static async Task<IActionResult> UpdateCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
            [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            await blobContainer.CreateIfNotExistsAsync();
            var blob = blobContainer.GetBlobClient($"{day}");
            if (!blob.Exists())
            {
                return new OkObjectResult($"Item does not exists");
            }
            else
            {
                await blob.UploadAsync(req.Body, overwrite: true);
                return new OkObjectResult($"Updated");
            }
        }

        [FunctionName("CreateCalendar")]
        public static async Task<IActionResult> CreateCalendar(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "calendar/{calendar}")] HttpRequest req,
            string calendar, ILogger log)
        {
            log.LogInformation($"CreateCalendar triggered");
            var connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);

            if (blobContainer.Exists())
            {
                return new OkObjectResult($"Calendar {calendar} already exists") ;
            }
            else
            {
                var options = new Dictionary<string, string>
                    {
                        { "Created", DateTime.UtcNow.ToString() }
                    };
                await blobContainer.CreateAsync(Azure.Storage.Blobs.Models.PublicAccessType.None, options);
                return new OkObjectResult($"Created calendar {calendar}");
            }
        }
    }
}
