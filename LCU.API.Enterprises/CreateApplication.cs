using Fathym;
using Fathym.API;
using Gremlin.Net.Driver;
using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
	}
}
