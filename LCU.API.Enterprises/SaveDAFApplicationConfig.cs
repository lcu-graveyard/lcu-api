using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs;
using Fathym.API;
using Fathym;
using System.Linq;
using Gremlin.Net.Process.Traversal;
using Gremlin.Net.Structure;

namespace LCU.API.Enterprises
{
    public static class SaveDAFApplicationConfig
    {
        [FunctionName("SaveDAFApplicationConfig")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("SaveDAFApplicationConfig processed a request.");

			var entGraphConfig = new LCUGraphConfig()
			{
				APIKey = Environment.GetEnvironmentVariable("LCU_GRAPH_API_KEY"),
				Host = Environment.GetEnvironmentVariable("LCU_GRAPH_HOST"),
				Database = Environment.GetEnvironmentVariable("LCU_GRAPH_DATABASE"),
				Graph = Environment.GetEnvironmentVariable("LCU_GRAPH")
			};

			string type = req.Query["type"];

			string apiKey = req.Query["apiKey"];

			var appGraph = new AppGraph(entGraphConfig);

			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

			var response = new BaseResponse();

			switch (type)
			{
				case "API":
					var apiConfig = requestBody.FromJSON<DAFApplicationConfiguration>();

					var apiResult = await appGraph.SaveDAFApplication(apiKey, apiConfig);

					var api = apiResult.JSONConvert<DAFAPIConfiguration>();

					response.Status = Status.Success;
					break;

				case "View":
					var viewConfig = requestBody.FromJSON<DAFApplicationConfiguration>();

					var viewResult = await appGraph.SaveDAFApplication(apiKey, viewConfig);

					var view = viewResult.JSONConvert<DAFViewConfiguration>();

					response.Status = Status.Success;
					break;

				default:
					response.Status = Status.GeneralError.Clone("The provided type is not supported.");
					break;
			}

			return new JsonResult(response, new JsonSerializerSettings());
		}
    }

public class AppGraph : ApplicationGraph
{
		public AppGraph(LCUGraphConfig config)
			: base(config)
		{

		}
		public override async Task<DAFApplicationConfiguration> SaveDAFApplication(string apiKey, DAFApplicationConfiguration config)
		{
			return await withG(async (client, g) =>
			{
				var existingQuery = g.V().HasLabel(AppGraphConstants.DAFAppVertexName)
						.Has("ApplicationID", config.ApplicationID)
						.Has("Registry", $"{apiKey}|{config.ApplicationID}");

				var existingResults = await Submit<DAFApplicationConfiguration>(existingQuery);

				var existingAppResult = existingResults.FirstOrDefault();

				var query = existingAppResult == null ? 
					g.AddV(AppGraphConstants.DAFAppVertexName)
						.Property("ApplicationID", config.ApplicationID)
						.Property("Registry", $"{apiKey}|{config.ApplicationID}") :
					g.V().HasLabel(AppGraphConstants.DAFAppVertexName)
						.HasId(existingAppResult.ID)
						.Has("ApplicationID", config.ApplicationID)
						.Has("Registry", $"{apiKey}|{config.ApplicationID}");

				if (config.Metadata.ContainsKey("BaseHref"))
				{
					query.Property("BaseHref", config.Metadata["BaseHref"])
						.Property("NPMPackage", config.Metadata["NPMPackage"])
						.Property("PackageVersion", config.Metadata["PackageVersion"]); 
				}
				else
				{
					query.Property("Host", config.Metadata["Host"])
						.Property("Path", config.Metadata["Path"])
						.Property("Security", config.Metadata["Security"]);
				}

				var appAppResults = await Submit<DAFApplicationConfiguration>(query);

				var appAppResult = appAppResults.FirstOrDefault();

				var appQuery = g.V().HasLabel(AppGraphConstants.AppVertexName)
					.HasId(config.ApplicationID)
					.Has("Registry", apiKey);

				var appResults = await Submit<Application>(appQuery);

				var appResult = appResults.FirstOrDefault();

				var edgeQueries = new[] {
					g.V(appResult.ID).AddE(AppGraphConstants.ProvidesEdgeName).To(g.V(appAppResult.ID)),
				};

				foreach (var edgeQuery in edgeQueries)
				{
					await Submit(edgeQuery);
				}

				return appAppResult;
			});
		}

	}
}
