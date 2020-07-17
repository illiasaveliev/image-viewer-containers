using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImageViewer.API
{
    public class Startup
    {
        public const string AppS3BucketKey = "AppS3Bucket";
        public const string AppSQSUrlKey = "AppSQSUrl";
        private const string CorsPolicyName = "CorsPolicy";

        public Startup(IWebHostEnvironment env)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public static IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            string[] corsOrigins = {
    Configuration.GetSection("WebappRedirectUrl").Value,
    Configuration.GetSection("AdditionalCorsOrigins").Value,
};
            services.AddCors(options =>
            {
                options.AddPolicy(
                    CorsPolicyName,
                    builder => builder.WithOrigins(corsOrigins.Where(co => !string.IsNullOrEmpty(co)).ToArray())
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .WithExposedHeaders("Content-Disposition"));
            });

            services.AddControllers();

            // Add S3 to the ASP.NET Core dependency injection framework.
            services.AddAWSService<Amazon.S3.IAmazonS3>();
            services.AddAWSService<Amazon.SQS.IAmazonSQS>();
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors(CorsPolicyName);

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
