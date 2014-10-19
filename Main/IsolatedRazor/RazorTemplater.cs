using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Razor;
using IsolatedRazor.Enumerations;
using IsolatedRazor.Interfaces;
using Microsoft.CSharp;
using Microsoft.VisualBasic;

namespace IsolatedRazor
{
	/// <summary>The base class to generate and render Razor templates.</summary>
	public class RazorTemplater : MarshalByRefObject, IDisposable
	{
		private const double SAFE_GUARD = 2007.1987;
		private const string TEMPLATE_CACHE_FILE = "IsolatedRazorTemplates.cache";

		private string templatePath;
		private string templateNamespace;
		private string defaultBaseClass;
		private bool persistTemplates;
		private bool isShadowCopied;

		private AppDomain appDomain = null;
		private RazorTemplater templater = null;
		private CodeDomProvider provider = null;
		private RazorTemplateEngine engine = null;

		private static TemplateCache templateCache = null;
		private static Dictionary<string, Assembly> assemblyCache = new Dictionary<string, Assembly>();

		/// <summary>Gets or sets the render timeout (in milliseconds).</summary>
		/// <value>The render timeout.</value>
		public int RenderTimeout { get; set; }

		/// <summary>Gets or sets the default namespaces.</summary>
		/// <value>The default namespaces.</value>
		public List<string> DefaultNamespaces { get; set; }

		/// <summary>Initializes a new instance of the <see cref="RazorTemplater" /> class.</summary>
		/// <param name="templateNamespace">The template namespace.</param>
		/// <param name="safeGuard">The safe guard to prevent external access.</param>
		/// <exception cref="System.ArgumentException">This constructor is for internal use only!</exception>
		public RazorTemplater(string templateNamespace, double safeGuard)
		{
			if (safeGuard != SAFE_GUARD)
				throw new ArgumentException("This constructor is for internal use only!");

			this.templateNamespace = templateNamespace;
		}

		/// <summary>Initializes a new instance of the <see cref="RazorTemplater" /> class.</summary>
		/// <param name="templateAssemblyPath">The template assembly path. This is the path where the generated templates are stored/cached.
		/// If shadow copy is enabled this path will be ignored and the shadow copy path will be used.</param>
		/// <param name="renderTimeout">The render timeout. This is the time in ms a template is allowed to render itself.</param>
		/// <param name="templateNamespace">The template namespace.</param>
		/// <param name="allowedDirectories">The directories the templates are allowed to read from.</param>
		/// <param name="baseType">Type of the template base class. Defaults to <see cref="TemplateBase" />.</param>
		/// <param name="defaultNamespaces">The default namespaces. Defaults to "System", "System.Collections.Generic", "System.Linq" and "System.Text".</param>
		/// <param name="language">The language. Defaults to C#.</param>
		/// <param name="sponsor">The sponsor to keep the object alive.</param>
		/// <param name="persistTemplates">If set to <c>true</c> the generated templates are persisted over multiple application runs. Otherwise they are deleted when disposing.</param>
		public RazorTemplater(string templateAssemblyPath, int renderTimeout = 5000, string templateNamespace = "IsolatedRazor.RazorTemplate", List<string> allowedDirectories = null,
			Type baseType = null, List<string> defaultNamespaces = null, RazorCodeLanguage language = null, ClientSponsor sponsor = null, bool persistTemplates = false)
		{
			RenderTimeout = renderTimeout;
			this.templateNamespace = templateNamespace;
			this.persistTemplates = persistTemplates;
			DefaultNamespaces = defaultNamespaces ?? new List<string>() { "System", "System.Collections.Generic", "System.Net", "System.Linq", "System.Text", "IsolatedRazor" };

			defaultBaseClass = (baseType ?? typeof(TemplateBase)).FullName;
			var host = new RazorEngineHost(language ?? new CSharpRazorCodeLanguage()) { DefaultNamespace = templateNamespace };
			DefaultNamespaces.ForEach(n => host.NamespaceImports.Add(n));
			engine = new RazorTemplateEngine(host);
			provider = host.CodeLanguage.LanguageName == "vb" ? (CodeDomProvider)new VBCodeProvider() : new CSharpCodeProvider();

			AppDomainSetup adSetup = new AppDomainSetup();
			if (AppDomain.CurrentDomain.SetupInformation.ShadowCopyFiles == "true")
			{
				isShadowCopied = true;
				templatePath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.CachePath, AppDomain.CurrentDomain.SetupInformation.ApplicationName);

				var shadowCopyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				if (shadowCopyDir.Contains("assembly"))
					shadowCopyDir = shadowCopyDir.Substring(0, shadowCopyDir.LastIndexOf("assembly"));

				var privatePaths = new List<string>();
				foreach (var assemblyLocation in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.StartsWith(shadowCopyDir)).Select(a => a.Location))
					privatePaths.Add(Path.GetDirectoryName(assemblyLocation));

				adSetup.ApplicationBase = shadowCopyDir;
				adSetup.PrivateBinPath = String.Join(";", privatePaths);
			}
			else
			{
				isShadowCopied = false;
				templatePath = templateAssemblyPath;
				adSetup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
				adSetup.PrivateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
			}

			if (templateCache == null)
			{
				var path = Path.Combine(templatePath, TEMPLATE_CACHE_FILE);
				if (persistTemplates && File.Exists(path))
				{
					using (var filestream = File.Open(path, FileMode.Open))
					{
						var formatter = new BinaryFormatter();
						templateCache = (TemplateCache)formatter.Deserialize(filestream);
					}
				}
				else
					templateCache = new TemplateCache();
			}

			Directory.CreateDirectory(templatePath);

			PermissionSet permSet = new PermissionSet(PermissionState.None);
			permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));					// run the code
			permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.RemotingConfiguration));		// remoting lifetime (sponsor)
			permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read, templatePath));				// read templates
			permSet.AddPermission(new ReflectionPermission(ReflectionPermissionFlag.RestrictedMemberAccess));	// support dynamic

			if(allowedDirectories != null)
				allowedDirectories.ForEach(dir => permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read, dir)));

			appDomain = AppDomain.CreateDomain("razorDomain", null, adSetup, permSet);

			ObjectHandle handle = Activator.CreateInstanceFrom(appDomain, typeof(RazorTemplater).Assembly.ManifestModule.FullyQualifiedName, typeof(RazorTemplater).FullName,
				false, BindingFlags.CreateInstance, null, new object[] { templateNamespace, SAFE_GUARD }, null, null);
			templater = (RazorTemplater)handle.Unwrap();

			if (sponsor == null)
				sponsor = new ClientSponsor(TimeSpan.FromMinutes(1));
			sponsor.Register(templater);
		}
		/// <summary>Finalizes an instance of the <see cref="RazorTemplater"/> class.</summary>
		~RazorTemplater() { Dispose(); }

		/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
		public void Dispose()
		{
			if (appDomain != null)
			{
				try { AppDomain.Unload(appDomain); }
				catch { }
				appDomain = null;
			}
			if(templateCache != null)
			{
				if (persistTemplates)
				{
					var path = Path.Combine(templatePath, TEMPLATE_CACHE_FILE);
					using(var filestream = File.Open(path, FileMode.Create))
					{
						var formatter = new BinaryFormatter();
						formatter.Serialize(filestream, templateCache);
					}
				}
				else
				{
					foreach (var tpl in templateCache.CachedNames)
					{
						var file = templateCache.Get(tpl, template: null, timestamp: null);
						if (File.Exists(file))
							File.Delete(file);
					}
				}
			}
		}

		/// <summary>Deletes the template from the cache so it will be regenerated if parsed again.</summary>
		/// <param name="name">The name.</param>
		public void DeleteTemplate(string name)
		{
			string file = templateCache.Get(name, template: null, timestamp: null);
			if (!String.IsNullOrWhiteSpace(file))
			{
				if (File.Exists(file))
					File.Delete(file);
				templateCache.Remove(name);
			}
		}

		/// <summary>Parses the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		public async Task<string> ParseTemplateAsync(string name, string template, DateTime timestamp, DynamicViewBag viewBag = null, List<string> additionalNamespaces = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var assemblyPath = await CompileAsync(name, template, timestamp, defaultBaseClass, additionalNamespaces).ConfigureAwait(false);
			return Render(name, assemblyPath, viewBag);
		}
		/// <summary>Parses the template with the specified name.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp.</param>
		/// <param name="model">The model.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="baseType">Type of the template base class. Defaults to <see cref="TemplateBase" />.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		public async Task<string> ParseAsync<T>(string name, string template, DateTime timestamp, T model, DynamicViewBag viewBag = null, Type baseType = null, List<string> additionalNamespaces = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var assemblyPath = await CompileAsync(name, template, timestamp, baseType ?? typeof(TemplateBase<T>), additionalNamespaces).ConfigureAwait(false);
			return Render(name, assemblyPath, model, viewBag);
		}
		/// <summary>Parses the template with the specified name.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp.</param>
		/// <param name="model">The model.</param>
		/// <param name="baseTypeName">Type of the template base class as its FQDN. Defaults to the default base class.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		public async Task<string> ParseAsync<T>(string name, string template, DateTime timestamp, T model, string baseTypeName, DynamicViewBag viewBag = null, List<string> additionalNamespaces = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var assemblyPath = await CompileAsync(name, template, timestamp, baseTypeName ?? typeof(TemplateBase<T>).FullName, additionalNamespaces).ConfigureAwait(false);
			return Render(name, assemblyPath, model, viewBag);
		}

		/// <summary>Compiles the specified layout and stores it in the cache with the given name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <param name="baseType">Type of the template base class. Defaults to <see cref="LayoutBase" />.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		/// <param name="generatedCodePath">If not NULL the generated code will be saved to this path.</param>
		/// <returns>The path to the generated assembly.</returns>
		public Task<string> CompileLayoutAsync(string name, string template, DateTime timestamp, Type baseType = null, List<string> additionalNamespaces = null, string generatedCodePath = null)
		{
			return CompileAsync(name, template, timestamp, baseType ?? typeof(LayoutBase), additionalNamespaces, generatedCodePath);
		}
		/// <summary>Compiles the specified template and stores it in the cache with the given name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <param name="baseType">Type of the template base class. Defaults to the default base class.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		/// <param name="generatedCodePath">If not NULL the generated code will be saved to this path.</param>
		/// <returns>The path to the generated assembly.</returns>
		public Task<string> CompileAsync(string name, string template, DateTime timestamp, Type baseType = null, List<string> additionalNamespaces = null, string generatedCodePath = null)
		{
			return CompileAsync(name, template, timestamp, baseType == null ? defaultBaseClass : baseType.FullName, additionalNamespaces, generatedCodePath);
		}
		/// <summary>Compiles the specified template and stores it in the cache with the given name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <param name="baseTypeName">Type of the template base class as its FQDN. Defaults to the default base class.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		/// <param name="generatedCodePath">If not NULL the generated code will be saved to this path.</param>
		/// <returns>The path to the generated assembly.</returns>
		/// <exception cref="System.ObjectDisposedException">RazorTemplater already disposed.</exception>
		/// <exception cref="CompilerException">The supplied template is not valid.</exception>
		public Task<string> CompileAsync(string name, string template, DateTime timestamp, string baseTypeName = null, List<string> additionalNamespaces = null, string generatedCodePath = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cachedTemplate = templateCache.Get(name, template, timestamp);
			if (!String.IsNullOrWhiteSpace(cachedTemplate))
				return Task.FromResult(cachedTemplate);

			return Task.Run(() =>
			{
				engine.Host.DefaultBaseClass = baseTypeName ?? defaultBaseClass;
				engine.Host.DefaultClassName = ClassName(name);
				if (additionalNamespaces != null)
					additionalNamespaces.ForEach(n => engine.Host.NamespaceImports.Add(n));

				GeneratorResults razorTemplate = engine.GenerateCode(new StringReader(template));

				if (!String.IsNullOrWhiteSpace(generatedCodePath))
					SaveGeneratedCode(razorTemplate, generatedCodePath);
				else if (isShadowCopied)
					SaveGeneratedCode(razorTemplate, Path.Combine(templatePath, engine.Host.DefaultClassName + ".cs"));

				var outputName = name;
				foreach (char c in System.IO.Path.GetInvalidFileNameChars())
					outputName = outputName.Replace(c, '_');

				var compilerParameters = new CompilerParameters()
				{
					GenerateExecutable = false,
					IncludeDebugInformation = false,
					OutputAssembly = Path.Combine(templatePath, outputName + ".dll"),
					CompilerOptions = "/target:library /optimize /define:RAZORTEMPLATE"
				};

				var assemblies = AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => !a.IsDynamic && File.Exists(a.Location))
					.GroupBy(a => a.GetName().Name).Select(g => g.First(y => y.GetName().Version == g.Max(x => x.GetName().Version))) // group assemblies on FullName to avoid loading duplicate assemblies
					.Select(a => a.Location);
				compilerParameters.ReferencedAssemblies.AddRange(assemblies.ToArray());

				var compiledAssembly = provider.CompileAssemblyFromDom(compilerParameters, razorTemplate.GeneratedCode);
				templater.RemoveFromAssemblyCache(compiledAssembly.PathToAssembly);

				if (additionalNamespaces != null)
				{
					engine.Host.NamespaceImports.Clear();
					DefaultNamespaces.ForEach(n => engine.Host.NamespaceImports.Add(n));
				}

				if (compiledAssembly.Errors.Count > 0)
				{
					var message = String.Empty;
					foreach (var error in compiledAssembly.Errors)
					{
						if (error is CompilerError)
						{
							var compilerError = error as CompilerError;
							message += compilerError.ErrorText + Environment.NewLine;
						}
						else
							message += error.ToString() + Environment.NewLine;
					}

					throw new CompilerException(message, compiledAssembly.Errors);
				}

				var assemblyPath = compiledAssembly.PathToAssembly;
				templateCache.Set(name, assemblyPath, template, timestamp);

				return assemblyPath;
			});
		}
		/// <summary>Compiles the specified templates and stores it in the cache with the given group name.</summary>
		/// <param name="groupName">The group name.</param>
		/// <param name="templates">The template group.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <param name="baseTypeName">Type of the template base class as its FQDN. Defaults to the default base class.</param>
		/// <param name="additionalNamespaces">The additional namespaces.</param>
		/// <param name="generatedCodePath">If not NULL the generated code will be saved to this path.</param>
		/// <returns>The path to the generated assembly.</returns>
		/// <exception cref="System.ObjectDisposedException">RazorTemplater already disposed.</exception>
		/// <exception cref="CompilerException">The supplied template is not valid.</exception>
		public Task<string> CompileAsync(string groupName, Dictionary<string, string> templates, DateTime timestamp, string baseTypeName = null,
			List<string> additionalNamespaces = null, string generatedCodePath = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cachedTemplate = templateCache.Get(groupName, templates, timestamp);
			if (!String.IsNullOrWhiteSpace(cachedTemplate))
				return Task.FromResult(cachedTemplate);

			return Task.Run(() =>
			{
				engine.Host.DefaultBaseClass = baseTypeName ?? defaultBaseClass;
				if (additionalNamespaces != null)
					additionalNamespaces.ForEach(n => engine.Host.NamespaceImports.Add(n));

				var razorTemplates = new List<GeneratorResults>();
				foreach (var template in templates)
				{
					engine.Host.DefaultClassName = ClassName(groupName + "_" + template.Key);

					var razorTemplate = engine.GenerateCode(new StringReader(template.Value));
					razorTemplates.Add(razorTemplate);

					if (!String.IsNullOrWhiteSpace(generatedCodePath))
						SaveGeneratedCode(razorTemplate, generatedCodePath + String.Format(".{0}.cs", engine.Host.DefaultClassName));
					else if (isShadowCopied)
						SaveGeneratedCode(razorTemplate, Path.Combine(templatePath, engine.Host.DefaultClassName + ".cs"));
				}

				var outputName = groupName;
				foreach (char c in System.IO.Path.GetInvalidFileNameChars())
					outputName = outputName.Replace(c, '_');

				var compilerParameters = new CompilerParameters()
				{
					GenerateExecutable = false,
					IncludeDebugInformation = false,
					OutputAssembly = Path.Combine(templatePath, outputName + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".dll"),	// date-time to avoid accessing locked files
					CompilerOptions = "/target:library /optimize /define:RAZORTEMPLATE"
				};

				var assemblies = AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => !a.IsDynamic && File.Exists(a.Location))
					.GroupBy(a => a.GetName().Name).Select(g => g.First(y => y.GetName().Version == g.Max(x => x.GetName().Version))) // group assemblies on FullName to avoid loading duplicate assemblies
					.Select(a => a.Location);
				compilerParameters.ReferencedAssemblies.AddRange(assemblies.ToArray());

				var compiledAssembly = provider.CompileAssemblyFromDom(compilerParameters, razorTemplates.Select(t => t.GeneratedCode).ToArray());
				templater.RemoveFromAssemblyCache(compiledAssembly.PathToAssembly);

				if (additionalNamespaces != null)
				{
					engine.Host.NamespaceImports.Clear();
					DefaultNamespaces.ForEach(n => engine.Host.NamespaceImports.Add(n));
				}

				if (compiledAssembly.Errors.Count > 0)
				{
					var message = String.Empty;
					foreach (var error in compiledAssembly.Errors)
					{
						if (error is CompilerError)
						{
							var compilerError = error as CompilerError;
							message += compilerError.ErrorText + Environment.NewLine;
						}
						else
							message += error.ToString() + Environment.NewLine;
					}

					throw new CompilerException(message, compiledAssembly.Errors);
				}

				var assemblyPath = compiledAssembly.PathToAssembly;
				templateCache.Set(groupName, assemblyPath, templates, timestamp);

				return assemblyPath;
			});
		}

		/// <summary>Saves the generated code.</summary>
		/// <param name="razorTemplate">The razor template.</param>
		/// <param name="path">The path.</param>
		private void SaveGeneratedCode(GeneratorResults razorTemplate, string path)
		{
			using (var writer = new StreamWriter(path, false, Encoding.UTF8))
			{
				provider.GenerateCodeFromCompileUnit(razorTemplate.GeneratedCode, writer, new CodeGeneratorOptions());
			}
		}

		/// <summary>Renders the template with specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="template">The template. NULL to ignore.</param>
		/// <param name="timestamp">The time-stamp of the template. NULL to ignore.</param>
		public string RenderTemplate(string name, DynamicViewBag viewBag = null, string template = null, DateTime? timestamp = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cachedTemplate = templateCache.Get(name, template, timestamp);
			if (String.IsNullOrWhiteSpace(cachedTemplate))
				throw new ArgumentException(String.Format("No matching cached template with the name \"{0}\".", name));

			return Render(name, cachedTemplate, viewBag);
		}
		/// <summary>Renders the template with specified name.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="name">The name.</param>
		/// <param name="model">The model.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="template">The template. NULL to ignore.</param>
		/// <param name="timestamp">The time-stamp of the template. NULL to ignore.</param>
		public string Render<T>(string name, T model, DynamicViewBag viewBag = null, string template = null, DateTime? timestamp = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cachedTemplate = templateCache.Get(name, template, timestamp);
			if (String.IsNullOrWhiteSpace(cachedTemplate))
				throw new ArgumentException(String.Format("No matching cached template with the name \"{0}\".", name));

			return Render(name, cachedTemplate, model, viewBag);
		}
		/// <summary>Renders the template with specified name from the specified group.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="groupName">Name of the group.</param>
		/// <param name="name">The name.</param>
		/// <param name="model">The model.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="templates">The templates.</param>
		/// <param name="timestamp">The time-stamp of the template. NULL to ignore.</param>
		public string Render<T>(string groupName, string name, T model, DynamicViewBag viewBag = null, Dictionary<string, string> templates = null, DateTime? timestamp = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cachedTemplate = templateCache.Get(groupName, templates, timestamp);
			if (String.IsNullOrWhiteSpace(cachedTemplate))
				throw new ArgumentException(String.Format("No matching cached template with the name \"{0}\".", name));

			return Render(groupName + "_" + name, cachedTemplate, model, viewBag);
		}

		/// <summary>Renders the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <exception cref="TaskCanceledException">Timeout</exception>
		private string Render(string name, string assemblyPath, DynamicViewBag viewBag = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cts = new CancellationTokenSource();
			cts.CancelAfter(RenderTimeout);
			Thread worker = null;
			var task = Task.Run<string>((Func<string>)(() =>
			{
				worker = Thread.CurrentThread;
				return templater.RenderTemplateInternal(assemblyPath, engine.Host.DefaultNamespace + "." + ClassName(name), viewBag, templateCache);
			}));

			while (!task.IsCompleted && !cts.Token.IsCancellationRequested
#if DEBUG
				|| !task.IsCompleted && System.Diagnostics.Debugger.IsAttached
#endif
				)
			{
				Thread.Sleep(1);
			}

			if (!task.IsCompleted)
			{
				worker.Abort();
				throw new TaskCanceledException("Timeout");
			}

			return task.Result;
		}
		/// <summary>Renders the template with the specified name.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="name">The name.</param>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <param name="model">The model.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <exception cref="TaskCanceledException">Timeout</exception>
		private string Render<T>(string name, string assemblyPath, T model, DynamicViewBag viewBag = null)
		{
			if (appDomain == null)
				throw new ObjectDisposedException("RazorTemplater");

			var cts = new CancellationTokenSource();
			cts.CancelAfter(RenderTimeout);
			Thread worker = null;
			var task = Task.Run<string>((Func<string>)(() =>
			{
				worker = Thread.CurrentThread;
				return templater.RenderTemplateInternal(assemblyPath, engine.Host.DefaultNamespace + "." + ClassName(name), model, viewBag, templateCache);
			}));

			while (!task.IsCompleted && !cts.Token.IsCancellationRequested
#if DEBUG
				|| !task.IsCompleted && System.Diagnostics.Debugger.IsAttached
#endif
				)
			{
				Thread.Sleep(1);
			}

			if (!task.IsCompleted)
			{
				worker.Abort();
				throw new TaskCanceledException("Timeout");
			}

			return task.Result;
		}

		/// <summary>Renders the template.</summary>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <param name="typeName">Name of the type.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="templateCache">The template cache.</param>
		/// <param name="encoding">The encoding.</param>
		internal string RenderTemplateInternal(string assemblyPath, string typeName, DynamicViewBag viewBag, TemplateCache templateCache, TemplateEncoding encoding = TemplateEncoding.Html)
		{
			var template = PrepareTemplate(assemblyPath, typeName, viewBag, templateCache, encoding);
			return template.Render();
		}
		/// <summary>Renders the template with the given model.</summary>
		/// <typeparam name="T">Type of the model.</typeparam>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <param name="typeName">Name of the type.</param>
		/// <param name="model">The model.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="templateCache">The template cache.</param>
		/// <param name="encoding">The encoding.</param>
		internal string RenderTemplateInternal<T>(string assemblyPath, string typeName, T model, DynamicViewBag viewBag, TemplateCache templateCache, TemplateEncoding encoding = TemplateEncoding.Html)
		{
			var template = PrepareTemplate(assemblyPath, typeName, viewBag, templateCache, encoding) as ITemplate<T>;
			template.Model = model;

			return template.Render();
		}

		/// <summary>Prepares the template.</summary>
		/// <param name="assemblyPath">The assembly path.</param>
		/// <param name="typeName">Name of the type.</param>
		/// <param name="viewBag">The view bag.</param>
		/// <param name="templateCache">The template cache.</param>
		/// <param name="encoding">The encoding.</param>
		private TemplateBase PrepareTemplate(string assemblyPath, string typeName, DynamicViewBag viewBag, TemplateCache templateCache, TemplateEncoding encoding = TemplateEncoding.Html)
		{
			var type = GetTemplateType(assemblyPath, typeName);
			var templateInstance = Activator.CreateInstance(type);
			if (templateInstance is TemplateBase)
			{
				var template = (TemplateBase)templateInstance;

				template.Encoding = encoding;
				template.ViewBag = viewBag;
				template.TemplateResolver = tpl =>
					{
						string tplAssemblyPath = templateCache.Get(tpl, template: null, timestamp: null);
						if (String.IsNullOrWhiteSpace(tplAssemblyPath))
							return null;

						var layoutType = GetTemplateType(tplAssemblyPath, templateNamespace + "." + tpl);
						return Activator.CreateInstance(layoutType) as ITemplate;
					};

				return template;
			}

			throw new ArgumentException("Invalid template type!");
		}

		/// <summary>Gets the type for the template.</summary>
		/// <param name="assemblyPath">Path to the assembly.</param>
		/// <param name="typeName">Name of the type.</param>
		private Type GetTemplateType(string assemblyPath, string typeName)
		{
			var type = Type.GetType(String.Format("{0}, {1}", typeName, Path.GetFileNameWithoutExtension(assemblyPath)));
			if (type != null)
				return type;

			Assembly assembly;
			if (assemblyCache.ContainsKey(assemblyPath))
				assembly = assemblyCache[assemblyPath];
			else
			{
				assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
				assemblyCache[assemblyPath] = assembly;
			}

			return assembly.GetType(typeName);
		}

		/// <summary>Removes the assembly from the assembly cache.</summary>
		/// <param name="assemblyPath">The assembly path.</param>
		private void RemoveFromAssemblyCache(string assemblyPath)
		{
			if (assemblyCache.ContainsKey(assemblyPath))
				assemblyCache.Remove(assemblyPath);
		}

		/// <summary>Gets a valid class name from the template name.</summary>
		/// <param name="name">The template name.</param>
		private string ClassName(string name)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < name.Length; i++)
			{
				builder.Append(Char.IsLetterOrDigit(name[i]) ? name[i] : '_');
			}

			name = builder.ToString();
			if (!Char.IsLetter(name[0]))
				name = "C" + name;

			return name;
		}
	}
}