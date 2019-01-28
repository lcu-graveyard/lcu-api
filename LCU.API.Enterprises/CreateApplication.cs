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
	public static class CreateApplication
	{
		[FunctionName("CreateApplication")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			log.LogInformation("CreateApplication function processed a request.");

			var entGraphConfig = new LCUGraphConfig()
			{
				APIKey = Environment.GetEnvironmentVariable("LCU_GRAPH_API_KEY"),
				Host = Environment.GetEnvironmentVariable("LCU_GRAPH_HOST"),
				Database = Environment.GetEnvironmentVariable("LCU_GRAPH_DATABASE"),
				Graph = Environment.GetEnvironmentVariable("LCU_GRAPH")
			};

			var appGraph = new ApplicationGraph(entGraphConfig);

			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

			var app = requestBody.FromJSON<Application>();

			app = await appGraph.Create(app);

			var response = new BaseResponse<Application>();

			if (app != null)
			{
				response.Model = app;

				response.Status = Status.Success;
			}
			else
			{
				response.Status = Status.GeneralError.Clone("The application was not created.");
			}

			return new JsonResult(response, new JsonSerializerSettings());
		}

		//public class AppGraph : ApplicationGraph
		//{
		//	public AppGraph(LCUGraphConfig config)
		//		: base(config)
		//	{ }

		//	public override async Task<Application> Create(Application application)
		//	{
		//		return await withG(async (client, g) =>
		//		{
		//			var addQuery = g.AddV(AppGraphConstants.AppVertexName)
		//				.Property("Container", application.Container)
		//				.Property("Host", application.Host)
		//				.Property("IsReadOnly", application.IsReadOnly)
		//				.Property("Name", application.Name)
		//				.Property("PathRegex", application.PathRegex)
		//				.Property("Priority", application.Priority)
		//				//.Property("QueryRegex", application.QueryRegex)
		//				//.Property("UserAgentRegex", application.UserAgentRegex)
		//				.Property("EnterprisePrimaryAPIKey", application.EnterprisePrimaryAPIKey)
		//				.Property("Registry", application.EnterprisePrimaryAPIKey);

		//			var appResults = await Submit<Application>(addQuery);

		//			var appResult = appResults.FirstOrDefault();

		//			var entQuery = g.V().HasLabel(AppGraphConstants.EnterpriseVertexName)
		//				.Has("Registry", application.EnterprisePrimaryAPIKey)
		//				.Has("PrimaryAPIKey", application.EnterprisePrimaryAPIKey);

		//			var entResults = await Submit<Enterprise>(entQuery);

		//			var entResult = entResults.FirstOrDefault();

		//			var edgeQueries = new[] {
		//			g.V(entResult.ID).AddE(AppGraphConstants.OwnsEdgeName).To(g.V(appResult.ID)),
		//			g.V(entResult.ID).AddE(AppGraphConstants.ManagesEdgeName).To(g.V(appResult.ID))
		//		};

		//			foreach (var edgeQuery in edgeQueries)
		//			{
		//				await Submit(edgeQuery);
		//			}

		//			return appResult;
		//		});
		//	}
		//}
	}
}
