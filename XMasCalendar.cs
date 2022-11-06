using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace de.softwaremess.xmas.api
{
    public static class XMasCalendar
    {
        private static string connection = Environment.GetEnvironmentVariable("XmasCalendarStorage");

        [FunctionName("GetItem")]
        public static async Task<IActionResult> GetCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
            //[Blob("{calendar}/{day}", FileAccess.Read)] Stream item,
            string calendar, int day, ILogger log)
        {
            log.LogInformation($"C# HTTP trigger GET item processed a request for day {day}.");
            
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);
            if (!blobContainer.Exists())
            {
                return new NotFoundObjectResult($"Calendar {calendar} does not exist") ;
            }
            BlobClient blob =  blobContainer.GetBlobClient(day.ToString());
            if(!blob.Exists())
            {
                return new NotFoundObjectResult($"Item {day} does not exist") ;
            }
            var resultStream = new MemoryStream();
            blob.DownloadTo(resultStream);
            resultStream.Position = 0;
            
            return new FileStreamResult(resultStream, "application/octet-stream");
        }

        [FunctionName("SetItem")]
        public static async Task<IActionResult> SetCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
           // [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            log.LogInformation($"SetItem triggered calendar {calendar}, day {day}.");
            
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);
            
            IActionResult checkResult = CheckCalendarAccess(blobContainer, req);
            if(checkResult != null)
            {
                return checkResult;
            }

            BlobClient blob =  blobContainer.GetBlobClient(day.ToString());
            if(blob.Exists())
            {
                return new BadRequestObjectResult($"Item {day} already exist") ;
            }
            else
            {
                log.LogInformation($"Request header content-type {req.ContentType}.");
                var blobHttpHeaders = new BlobHttpHeaders();
                blobHttpHeaders.ContentType = req.ContentType;      
                await blob.UploadAsync(req.Body, blobHttpHeaders); //, blobHttpHeaders);
                try {
                var tags = new Dictionary<string, string>
                    {
                        { "Created", DateTime.UtcNow.ToString() }
                    };
                blob.SetTags(tags);  
                }
                catch(Exception ex)
                {
                    return new OkObjectResult(ex.Message);
                } 

                return new CreatedResult($"/calendar/{calendar}/item/{day}", day);
            }
        }

        [FunctionName("UpdateItem")]
        public static async Task<IActionResult> UpdateCalendarItem(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "calendar/{calendar}/item/{day:int}")] HttpRequest req,
           // [Blob("{calendar}", FileAccess.Write)] BlobContainerClient blobContainer,
            string calendar, int day, ILogger log)
        {
            log.LogInformation($"UpdateItem triggered calendar {calendar}, day {day}.");
            
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);
            
            IActionResult checkResult = CheckCalendarAccess(blobContainer, req);
            if(checkResult != null)
            {
                return checkResult;
            }

            BlobClient blob =  blobContainer.GetBlobClient(day.ToString());
            if(!blob.Exists())
            {
                return new NotFoundObjectResult($"Item {day} does not exist") ;
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
            
            string username = req.Headers["username"];
            if(username == null)
            {
                return new UnauthorizedResult(); 
            }
            
            BlobServiceClient blobServiceClient = new BlobServiceClient(connection);
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(calendar);

            if (blobContainer.Exists())
            {
                return new BadRequestObjectResult($"Calendar {calendar} already exists") ;
            }
            else
            {
                var options = new Dictionary<string, string>
                    {
                        { "Created", DateTime.UtcNow.ToString() },
                        { "Owner",  req.Headers["username"]}
                    };
                await blobContainer.CreateAsync(Azure.Storage.Blobs.Models.PublicAccessType.None, options);
                //return new OkObjectResult($"Created calendar {calendar}");
                return new CreatedResult($"/calendar/{calendar}", calendar);
            }
        }

        private static IActionResult CheckCalendarAccess(BlobContainerClient blobContainer, HttpRequest req)
        {
            string username = req.Headers["username"];
            if(username == null)
            {
                return new UnauthorizedResult(); 
            }
            if (!blobContainer.Exists())
            {
                return new NotFoundObjectResult($"Calendar does not exist") ;
            }
            string owner = blobContainer.GetProperties().Value.Metadata["Owner"];
            if(!owner.Equals(req.Headers["username"]))
            {
                //No access to calendar. Handle like non existend calendar.
                return new NotFoundObjectResult($"Calendar does not exist"); //new ObjectResult("Forbidden") {StatusCode = StatusCodes.Status403Forbidden };
            }

            return null;
        }
    }
}
