using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImageViewer.Labeling
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private int _sleepInterval = 5000;
        private const int MessagesCount = 5;

        public const float DEFAULT_MIN_CONFIDENCE = 70f;

        public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";

        IAmazonS3 S3Client { get; }
        IAmazonSQS SQSClient { get; }

        IAmazonRekognition RekognitionClient { get; }

        float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

        private readonly string SQSUrl;
        private readonly string BucketName;

        HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            this.S3Client = new AmazonS3Client();
            this.RekognitionClient = new AmazonRekognitionClient();
            this.SQSClient = new AmazonSQSClient();

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
            this.SQSUrl = configuration.GetValue<string>("SQSUrl");
            this.BucketName = configuration.GetValue<string>("S3Bucket");


            if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
            {
                float value;
                if (float.TryParse(environmentMinConfidence, out value))
                {
                    this.MinConfidence = value;
                    _logger.LogInformation($"Setting minimum confidence to {this.MinConfidence}");
                }
                else
                {
                    _logger.LogWarning($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
                }
            }
            else
            {
                _logger.LogInformation($"Using default minimum confidence of {this.MinConfidence}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
               
                await Run(stoppingToken);
                await Task.Delay(_sleepInterval, stoppingToken);
            }
        }

        public async Task Run(CancellationToken stoppingToken)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = this.SQSUrl,
                    MaxNumberOfMessages = MessagesCount,
                    WaitTimeSeconds = 10
                };

                _logger.LogInformation($"Reading new messages");
                ReceiveMessageResponse result = await SQSClient.ReceiveMessageAsync(request, stoppingToken);
                foreach (Message message in result.Messages)
                {
                    ImageInfo image = JsonSerializer.Deserialize<ImageInfo>(message.Body);
                    await SetLabels(image);
                    await SQSClient.DeleteMessageAsync(this.SQSUrl, message.ReceiptHandle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }


        public async Task SetLabels(ImageInfo input)
        {

            if (!SupportedImageTypes.Contains(Path.GetExtension(input.Name)))
            {
                _logger.LogWarning($"Object {BucketName}:{input.Name} is not a supported image type");

            }

            _logger.LogInformation($"Looking for labels in image {BucketName}:{input.Name}");
            var detectResponses = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = MinConfidence,
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = BucketName,
                        Name = input.Name
                    }
                }
            });

            var tags = new List<Tag>();
            foreach (var label in detectResponses.Labels)
            {
                if (tags.Count < 10)
                {
                    _logger.LogInformation($"\tFound Label {label.Name} with confidence {label.Confidence}");
                    tags.Add(new Tag { Key = label.Name, Value = label.Confidence.ToString() });
                }
                else
                {
                    _logger.LogInformation($"\tSkipped label {label.Name} with confidence {label.Confidence} because the maximum number of tags has been reached");
                }
            }

            await this.S3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = BucketName,
                Key = input.Name,
                Tagging = new Tagging
                {
                    TagSet = tags
                }
            });
        }
    }
}
