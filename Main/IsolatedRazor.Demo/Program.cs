using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Razor;
using IsolatedRazor;
using Microsoft.CSharp;
using Shared;

namespace IsolatedRazor.Demo
{
	class RazorTemplater : MarshalByRefObject
	{
		static void Main(string[] args)
		{
			RunAsync().Wait();
		}

		private static async Task RunAsync()
		{
			var cs = @"Data Source=.\SQLEXPRESS;Initial Catalog=Test;Integrated Security=True";
			var con = new System.Data.SqlClient.SqlConnection(cs);	// to load the assembly
			con.Dispose();

			UriBuilder uri = new UriBuilder(Assembly.GetEntryAssembly().CodeBase);
			var templatePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)), "Templates");

			string path = @"D:\\test.txt";
			string template = "<div>Hello @Model.Name</div><p>@System.IO.File.ReadAllText(\"" + path + "\")</p>";
			var model = new Person { Name = "CFI" };

			var timestamp = DateTime.Now;
			var stopWatch = Stopwatch.StartNew();
			using (var templater = new IsolatedRazor.RazorTemplater(templatePath))
			{
				stopWatch.Stop();
				Console.WriteLine("Templater created in ms: " + stopWatch.Elapsed.TotalMilliseconds);

				Console.WriteLine("=============================================================================");
				stopWatch = Stopwatch.StartNew();
				using (var service = new RazorEngine.Templating.IsolatedTemplateService())
				{
					Console.WriteLine("RazorEngine: " + service.Parse(template, model, null, String.Empty));
					stopWatch.Stop();
					Console.WriteLine("Time needed in ms: " + stopWatch.Elapsed.TotalMilliseconds);
					Console.WriteLine("=============================================================================");
				}

				await ExecuteTestAsync(timestamp, "Layout", async () =>
					{
						await templater.CompileAsync("MainLayout", "<section>@RenderBody()</section>", timestamp, typeof(LayoutBase)).ConfigureAwait(false);
						return "DONE";
					});

				await ExecuteTestAsync(timestamp, "No-Model", async () => await templater.ParseTemplateAsync("No-Model", "<div>Hello World!</div>", timestamp).ConfigureAwait(false));

				await ExecuteTestAsync(timestamp, "Simple", async () => await templater.ParseAsync("Simple", "<div>Hello @Model.Name! It is @DateTime.Now</div>", timestamp, model).ConfigureAwait(false));
				await ExecuteTestAsync(timestamp, "Simple - retry", async () => await templater.ParseAsync("Simple", "<div>Hello @Model.Name! It is @DateTime.Now</div>", timestamp, model).ConfigureAwait(false));
				await ExecuteTestAsync(timestamp, "Simple - retry", async () => await templater.ParseAsync("Simple", "<div>Hello @Model.Name! It is @DateTime.Now</div>", timestamp, model).ConfigureAwait(false));

				await ExecuteTestAsync(timestamp, "Simple with Layout",
					async () => await templater.ParseAsync("Simple", "@{ Layout = \"MainLayout\"; }<div>Hello @Model.Name with Layout!</div>", timestamp, model).ConfigureAwait(false));

				await ExecuteTestAsync(timestamp, "ViewBag", () =>
					{
						dynamic viewBag = new DynamicViewBag();
						viewBag.Message = "Hello from ViewBag!";
						return templater.ParseTemplateAsync("ViewBag", "<div>@ViewBag.Message</div>", timestamp, viewBag);
					});

				await ExecuteTestAsync(timestamp, "Local-Model",
					async () => await templater.ParseAsync("Local", "<div>Hello @Model.Message</div>", timestamp, new DemoModel() { Message = "Demo Model!" }).ConfigureAwait(false));

				Console.WriteLine("Press ENTER to run error cases:");
				Console.ReadLine();

				await ExecuteTestAsync(timestamp, "FileIO", async () => await templater.ParseAsync("FileIO", template, timestamp, model).ConfigureAwait(false));

				await ExecuteTestAsync(timestamp, "SQL",
					async () => await templater.ParseAsync("SQL", "@{ var con = new System.Data.SqlClient.SqlConnection(@\"" + cs + "\"); con.Open(); } State: @con.State", timestamp, model).ConfigureAwait(false));

				await ExecuteTestAsync(timestamp, "Task-Loop",
					async () => await templater.ParseAsync("Loop", "@{ System.Threading.Tasks.Task.Run(() => { while(true) { System.Threading.Thread.Sleep(100); System.Console.WriteLine(\"FAIL\"); } }); }",
						timestamp, model).ConfigureAwait(false));

				if (!System.Diagnostics.Debugger.IsAttached)
					await ExecuteTestAsync(timestamp, "Loop", async () => await templater.ParseAsync("Loop", "@while(true);", timestamp, model).ConfigureAwait(false));
				else
				{
					Console.WriteLine("Skipped timeout test - DO NOT run with attached debugger - timeout is disabled then -> dead-lock");
					Console.WriteLine("=============================================================================");
				}

				await ExecuteTestAsync(timestamp, "Error", async () => await templater.CompileAsync("Error", template + "@Model.DoNotExist", timestamp, typeof(TemplateBase<Person>)).ConfigureAwait(false));

				System.Threading.Thread.Sleep(250);
			}

			Console.WriteLine("DONE - press ENTER to exit");
			Console.ReadLine();
		}

		private static async Task ExecuteTestAsync(DateTime timestamp, string name, Func<Task<string>> test)
		{
			try
			{
				var stopWatch = Stopwatch.StartNew();
				Console.WriteLine(name + ": " + await test().ConfigureAwait(false));
				stopWatch.Stop();
				Console.WriteLine("Time needed in ms: " + stopWatch.Elapsed.TotalMilliseconds);
				Console.WriteLine("=============================================================================");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);

				if (ex.InnerException != null)
					Console.WriteLine(ex.InnerException.Message);
				Console.WriteLine("=============================================================================");
			}
		}
	}


	[Serializable]
	public class DemoModel
	{
		public string Message { get; set; }
	}
}