using System;
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "xmascalendar/calendar/{calendar}/item/{day:int}")] HttpRequest req,
            [Blob("{calendar}/{day}", FileAccess.Read)] Stream item,
            string calendar, int day, ILogger log)
        {
            log.LogInformation($"C# HTTP trigger GET item processed a request for day {day}.");
            log.LogInformation($"Item position {item.Position}");
            log.LogInformation($"Item length {item.Length}");

            var resultStream = new MemoryStream();
            await item.CopyToAsync(resultStream);
            resultStream.Position = 0;
            
            return new FileStreamResult(resultStream, "application/octet-stream");
        }

        [FunctionName("SetItem")]
        public static async Task<IActionResult> SetCalendarItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "xmascalendar/calendar/{calendar}/item/{day:int}")] HttpRequest req,
            [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            await blobContainer.CreateIfNotExistsAsync();
            var blob = blobContainer.GetBlobClient($"{day}");
            if (blob.Exists())
            {
                return new OkObjectResult($"Already exists");;
            }
            else
            {
                await blob.UploadAsync(req.Body, overwrite: false);
                return new OkObjectResult($"Created");;
            }
        }

        [FunctionName("UpdateItem")]
        public static async Task<IActionResult> UpdateCalendarItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "xmascalendar/calendar/{calendar}/item/{day:int}")] HttpRequest req,
            [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            await blobContainer.CreateIfNotExistsAsync();
            var blob = blobContainer.GetBlobClient($"{day}");
            if (!blob.Exists())
            {
                return new OkObjectResult($"Item does not exists");;
            }
            else
            {
                await blob.UploadAsync(req.Body, overwrite: true);
                return new OkObjectResult($"Updated");;
            }
        }
    }
}