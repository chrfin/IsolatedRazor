using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using Shared;

namespace WebRazorTest.Controllers
{
	public class HomeController : Controller
	{
		private string templatePath = HostingEnvironment.MapPath("~/App_Data/Temp/");

		public ActionResult Index()
		{
			return View();
		}

		public async Task<ActionResult> About()
		{
			var timestamp = DateTime.Now;
			using (var templater = new IsolatedRazor.RazorTemplater(templatePath, 60000))
			{
				ViewBag.Message = await templater.ParseAsync("test", "@Model.Name, your application description page. @DateTime.Now", timestamp, new Person() { Name = "CFI" });
			}

			return View();
		}

		public ActionResult Contact()
		{
			ViewBag.Message = "Your contact page.";
			return View();
		}
	}
}