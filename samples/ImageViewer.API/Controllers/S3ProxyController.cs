using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Amazon.S3;
using Amazon.S3.Model;
using ImageViewer.API.Models;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

namespace ImageViewer.API.Controllers
{
    /// <summary>
    /// ASP.NET Core controller acting as a S3 Proxy.
    /// </summary>
    [Route("api/[controller]")]
    public class S3ProxyController : ControllerBase
    {
        IAmazonS3 S3Client { get; set; }
        IAmazonSQS SQSClient { get; set; }
        ILogger Logger { get; set; }

        string BucketName { get; set; }
        string QueueUrl { get; set; }

        public S3ProxyController(IConfiguration configuration, ILogger<S3ProxyController> logger, IAmazonS3 s3Client, IAmazonSQS amazonSQS)
        {
            this.Logger = logger;
            this.S3Client = s3Client;
            this.SQSClient = amazonSQS;

            this.BucketName = configuration[Startup.AppS3BucketKey];
            this.QueueUrl = configuration[Startup.AppSQSUrlKey];
            if (string.IsNullOrEmpty(this.BucketName))
            {
                logger.LogCritical("Missing configuration for S3 bucket. The AppS3Bucket configuration must be set to a S3 bucket.");
                throw new Exception("Missing configuration for S3 bucket. The AppS3Bucket configuration must be set to a S3 bucket.");
            }

            if (string.IsNullOrEmpty(this.QueueUrl))
            {
                logger.LogCritical("Missing configuration for SQS url. The AppSQSName configuration must be set to a SQS url.");
                throw new Exception("Missing configuration for SQS url. The AppSQSName configuration must be set to a SQS url.");
            }

            logger.LogInformation($"Configured to use bucket {this.BucketName}");
        }

        [HttpGet]
        public async Task<JsonResult> Get()
        {
            var listResponse = await this.S3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = this.BucketName
            });

            try
            {
                this.Response.ContentType = "text/json";
                List<ImageModel> images = new List<ImageModel>();
                foreach (var obj in listResponse.S3Objects)
                {
                    var getTagsResponse = await this.S3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                    {
                        BucketName = this.BucketName,
                        Key = obj.Key
                    });
                    images.Add(new ImageModel
                    {
                        Key = obj.Key,
                        ETag = obj.ETag,
                        LastModified = obj.LastModified,
                        Size = obj.Size,
                        Tags = getTagsResponse.Tagging.Select(t => new ImageTag { Tag = t.Key, Value = t.Value })
                        .OrderByDescending(t => t.Value).ToList()
                    });
                }

                return new JsonResult(images);
            }
            catch (AmazonS3Exception e)
            {
                this.Response.StatusCode = (int)e.StatusCode;
                return new JsonResult(e.Message);
            }
        }

        [HttpGet("{key}")]
        public async Task Get(string key)
        {
            try
            {
                var getResponse = await this.S3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = this.BucketName,
                    Key = key
                });

                this.Response.ContentType = getResponse.Headers.ContentType;
                getResponse.ResponseStream.CopyTo(this.Response.Body);
            }
            catch (AmazonS3Exception e)
            {
                this.Response.StatusCode = (int)e.StatusCode;
                var writer = new StreamWriter(this.Response.Body);
                writer.Write(e.Message);
            }
        }

        [HttpPost("startUpload")]
        public ActionResult StartUpload([FromBody] ImageInfo image)
        {
            try
            {
                var pre = new GetPreSignedUrlRequest
                {
                    BucketName = this.BucketName,
                    Key = image.Name,
                    ContentType = image.ContentType,
                    Verb = HttpVerb.PUT,
                    Expires = DateTime.UtcNow.AddDays(1)
                };

                var url = this.S3Client.GetPreSignedURL(pre);
                Logger.LogInformation($"Upload URL is generated for object {image.Name} to bucket {this.BucketName}.");
                return Ok(new { url });
            }
            catch (AmazonS3Exception e)
            {
                return BadRequest(e);
            }
        }

        [HttpPost("endUpload")]
        public async Task<ActionResult> EndUpload([FromBody] ImageInfo image)
        {
            try
            {
                var messageRequest = new SendMessageRequest
                {
                    QueueUrl = this.QueueUrl,
                    MessageBody = JsonSerializer.Serialize(image)
                };

                await SQSClient.SendMessageAsync(messageRequest);

                Logger.LogInformation($"Upload SQS message is generated for object {image.Name}.");
                return Ok();
            }
            catch (AmazonSQSException e)
            {
                return BadRequest(e);
            }
        }

        [HttpPut("{key}")]
        public async Task Put(string key)
        {
            // Copy the request body into a seekable stream required by the AWS SDK for .NET.
            var seekableStream = new MemoryStream();
            await this.Request.Body.CopyToAsync(seekableStream);
            seekableStream.Position = 0;

            var putRequest = new PutObjectRequest
            {
                BucketName = this.BucketName,
                Key = key,
                InputStream = seekableStream
            };

            try
            {
                var response = await this.S3Client.PutObjectAsync(putRequest);
                Logger.LogInformation($"Uploaded object {key} to bucket {this.BucketName}. Request Id: {response.ResponseMetadata.RequestId}");
            }
            catch (AmazonS3Exception e)
            {
                this.Response.StatusCode = (int)e.StatusCode;
                var writer = new StreamWriter(this.Response.Body);
                writer.Write(e.Message);
            }
        }

        [HttpDelete("{key}")]
        public async Task Delete(string key)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                 BucketName = this.BucketName,
                 Key = key
            };

            try
            {
                var response = await this.S3Client.DeleteObjectAsync(deleteRequest);
                Logger.LogInformation($"Deleted object {key} from bucket {this.BucketName}. Request Id: {response.ResponseMetadata.RequestId}");
            }
            catch (AmazonS3Exception e)
            {
                this.Response.StatusCode = (int)e.StatusCode;
                var writer = new StreamWriter(this.Response.Body);
                writer.Write(e.Message);
            }
        }
    }
}