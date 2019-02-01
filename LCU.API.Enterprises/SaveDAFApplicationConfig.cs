using Fathym;
using Fathym.API;
using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

			string apiKey = req.Query["apiKey"];

			var appGraph = new AppGraph(entGraphConfig);

			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

			var response = new BaseResponse();

			var appConfig = requestBody.FromJSON<DAFApplicationConfiguration>();

			var appResult = await appGraph.SaveDAFApplication(apiKey, appConfig);

			response.Status = Status.Success;

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
						.HasId(config.ID)
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

				query = query.Property("Priority", config.Metadata["Priority"].As<int>());

				if (config.Metadata.ContainsKey("BaseHref"))
				{
					query.Property("BaseHref", config.Metadata["BaseHref"])
						.Property("NPMPackage", config.Metadata["NPMPackage"])
						.Property("PackageVersion", config.Metadata["PackageVersion"]);
				}
				else if (config.Metadata.ContainsKey("APIRoot"))
				{
					query.Property("APIRoot", config.Metadata["APIRoot"])
						.Property("InboundPath", config.Metadata["InboundPath"])
						.Property("Methods", config.Metadata["Methods"])
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
