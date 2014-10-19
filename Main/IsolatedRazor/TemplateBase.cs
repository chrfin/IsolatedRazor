using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using IsolatedRazor.Enumerations;
using IsolatedRazor.Interfaces;

namespace IsolatedRazor
{
	/// <summary>Base-template for rendering.</summary>
	/// <typeparam name="T">Type of the model.</typeparam>
	public abstract class TemplateBase : ITemplate
	{
		private StringBuilder buffer;

		/// <summary>Gets or sets the encoding.</summary>
		/// <value>The encoding.</value>
		public TemplateEncoding Encoding { get; set; }

		/// <summary>Gets or sets the layout.</summary>
		/// <value>The layout.</value>
		public string Layout { get; set; }

		/// <summary>Gets or sets the view bag.</summary>
		/// <value>The view bag.</value>
		public dynamic ViewBag { get; set; }

		/// <summary>Gets or sets the template resolver.</summary>
		/// <value>The template resolver.</value>
		public Func<string, ITemplate> TemplateResolver { get; set; }

		/// <summary>Gets or sets the sections.</summary>
		/// <value>The sections.</value>
		public IDictionary<string, Action> Sections { get; set; }

		/// <summary>Gets or sets the base URL used to resolve URLs in the template.</summary>
		/// <value>The base URL.</value>
		public string BaseUrl { get; set; }

		/// <summary>Gets a HTML new line.</summary>
		/// <value>The HTML new line.</value>
		public string HtmlNewLine { get { return "<br />"; } }

		/// <summary>Initializes a new instance of the <see cref="TemplateBase"/> class.</summary>
		protected TemplateBase()
		{
			Encoding = TemplateEncoding.Html;
			ViewBag = new DynamicViewBag();
			Sections = new Dictionary<string, Action>();
		}

		/// <summary>This method is required and have to be exactly as declared here.</summary>
		public abstract void Execute();

		/// <summary>Defines a section that can written out to a layout.</summary>
		/// <param name="name">The name of the section.</param>
		/// <param name="action">The delegate used to write the section.</param>
		public virtual void DefineSection(string name, Action action) { Sections.Add(name, action); }
		/// <summary>Determines if the section with the specified name has been defined.</summary>
		/// <param name="name">The section name.</param>
		public virtual bool IsSectionDefined(string name)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("The name of the section must be specified.");

			return Sections.ContainsKey(name) && Sections[name] != null;
		}

		/// <summary>Includes the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="model">The model or NULL if there is no model for the template.</param>
		public virtual IEncodedString Include(string name, object model = null)
		{
			var template = TemplateResolver(name);
			if (template == null)
				throw new ArgumentException(String.Format("No template found with the name \"{0}\".", name));

			if (model == null)
				return Raw(template.Render());

			var modelType = model.GetType();
			var templateType = template.GetType();
			Type templateModelType = null;

			if (templateType.IsGenericType)
			{
				templateModelType = templateType.GetGenericArguments()[0];
				if (templateModelType.IsAssignableFrom(modelType))
				{
					dynamic genericTemplate = template;
					genericTemplate.Model = model;
					return Raw((genericTemplate as ITemplate).Render());
				}
			}

			throw new ArgumentException(String.Format("The template does not support this model. Expected: {0}; Got: {1}", templateModelType == null ? "none" : templateModelType.Name, modelType.Name));
		}

		/// <summary>Returns the specified string as a raw string. This will ensure it is not encoded.</summary>
		/// <param name="rawString">The raw string to write.</param>
		public virtual IEncodedString Raw(string rawString) { return new RawString(rawString); }

		/// <summary>Resolves the specified path</summary>
		/// <param name="path">The path.</param>
		/// <returns>The resolved path.</returns>
		public virtual string ResolveUrl(string path) { return path.Replace("~", BaseUrl ?? String.Empty); }

		/// <summary>Writes the specified object to the result.</summary>
		/// <param name="value">The value to write.</param>
		public virtual void Write(object value)
		{
			if (value != null)
			{
				if (value is IEncodedString)
					buffer.Append(value);
				else
				{
					if (Encoding == TemplateEncoding.Raw)
						buffer.Append(value);
					else
						buffer.Append(WebUtility.HtmlEncode(value.ToString()));
				}
			}
		}

		/// <summary>Writes an attribute to the result.</summary>
		/// <param name="name">The name of the attribute.</param>
		/// <param name="prefix">The prefix.</param>
		/// <param name="suffix">The suffix.</param>
		/// <param name="values">The values.</param>
		public virtual void WriteAttribute(string name, PositionTagged<string> prefix, PositionTagged<string> suffix, params AttributeValue[] values)
		{
			bool first = true;
			bool wroteSomething = false;
			if (values.Length == 0)
			{
				// Explicitly empty attribute, so write the prefix and suffix
				WritePositionTaggedLiteral(prefix);
				WritePositionTaggedLiteral(suffix);
			}
			else
			{
				for (int i = 0; i < values.Length; i++)
				{
					AttributeValue attrVal = values[i];
					PositionTagged<object> val = attrVal.Value;

					bool? boolVal = null;
					if (val.Value is bool)
					{
						boolVal = (bool)val.Value;
					}

					if (val.Value != null && (boolVal == null || boolVal.Value))
					{
						string valStr = val.Value as string;
						if (valStr == null)
							valStr = val.Value.ToString();
						if (boolVal != null)
							valStr = name;

						if (first)
						{
							WritePositionTaggedLiteral(prefix);
							first = false;
						}
						else
							WritePositionTaggedLiteral(attrVal.Prefix);

						if (attrVal.Literal)
							WriteLiteral(valStr);
						else
							Write(valStr); // Write value

						wroteSomething = true;
					}
				}
				if (wroteSomething)
					WritePositionTaggedLiteral(suffix);
			}
		}
		/// <summary>Writes a <see cref="PositionTagged{string}" /> literal to the result.</summary>
		/// <param name="value">The value.</param>
		private void WritePositionTaggedLiteral(PositionTagged<string> value) { WriteLiteral(value.Value); }

		/// <summary>Writes the specified string to the result.</summary>
		/// <param name="literal">The literal to write.</param>
		public virtual void WriteLiteral(string literal) { buffer.Append(literal); }

		/// <summary>Renders the template and returns the result.</summary>
		public virtual string Render()
		{
			buffer = new StringBuilder();
			Execute();

			if (!String.IsNullOrWhiteSpace(Layout))
			{
				// Get the layout template.
				var layoutTemplate = TemplateResolver(Layout);

				if (layoutTemplate is ILayout)
				{
					var layout = layoutTemplate as ILayout;

					layout.Sections = Sections;
					layout.Body = buffer.ToString();

					return layout.Render();
				}
				else
					throw new ArgumentException("No matching layout found by the template resolver.");

			}

			return buffer.ToString();
		}
	}

	/// <summary>Base-template for rendering with models.</summary>
	/// <typeparam name="T">Type of the model.</typeparam>
	public abstract class TemplateBase<T> : TemplateBase, ITemplate<T>
	{
		/// <summary>Gets or sets the model.</summary>
		/// <value>The model.</value>
		public virtual T Model { get; set; }

		/// <summary>Includes the template with the specified name.</summary>
		/// <param name="cacheName">The name of the template type in cache.</param>
		/// <param name="model">The model or NULL if there is no model for the template.</param>
		/// <returns>The template writer helper.</returns>
		public override IEncodedString Include(string cacheName, object model = null) { return base.Include(cacheName, model ?? Model); }
	}

	public static class CommonExtensions
	{
		public const string INHERIT = "inherit";
		public const string BLOCK = "block";
		public const string INLINEBLOCK = "inline-block";

		public const string DISPLAY_NONE = "display:none; display:none !important;";	// to support Gmail and Outlook

		/// <summary>Returns a CSS displays value to hide the element if the value is not null or white space.</summary>
		/// <param name="value">The value.</param>
		public static string HideIfNullOrWhiteSpace(this string value) { return String.IsNullOrWhiteSpace(value) ? DISPLAY_NONE : String.Empty; }
		/// <summary>Returns a CSS displays value to show the element if the value is not null or white space or hides it otherwise.</summary>
		/// <param name="value">The value.</param>
		/// <param name="displayValue">The display value returned to show the element (use INHERIT, BLOCK or INLINEBLOCK).</param>
		public static string DisplayIfNotNullOrWhiteSpace(this string value, string displayValue = INHERIT) { return String.IsNullOrWhiteSpace(value) ? DISPLAY_NONE : String.Format("display: {0};", displayValue); }

		/// <summary>Returns a CSS displays value to hide the element if the value or any of the additional values are not null or white space.</summary>
		/// <param name="value">The value.</param>
		/// <param name="additionalValues">The additional values.</param>
		public static string HideIfNullOrWhiteSpace(this string value, params string[] additionalValues)
		{
			return String.IsNullOrWhiteSpace(value) && additionalValues.All(v => String.IsNullOrWhiteSpace(v)) ? DISPLAY_NONE : String.Empty;
		}
		/// <summary>Returns a CSS displays value to show the element if the value or any of the additional values are not null or white space or hides it otherwise.</summary>
		/// <param name="value">The value.</param>
		/// <param name="additionalValues">The additional values.</param>
		public static string DisplayIfNotNullOrWhiteSpace(this string value, params string[] additionalValues)
		{
			return String.IsNullOrWhiteSpace(value) && additionalValues.All(v => String.IsNullOrWhiteSpace(v)) ? DISPLAY_NONE : String.Format("display: {0};", INHERIT);
		}
		/// <summary>Returns a CSS displays value to show the element if the value or any of the additional values are not null or white space or hides it otherwise.</summary>
		/// <param name="value">The value.</param>
		/// <param name="additionalValues">The additional values.</param>
		/// <param name="displayValue">The display value returned to show the element (use INHERIT, BLOCK or INLINEBLOCK).</param>
		public static string DisplayIfNotNullOrWhiteSpace(this string value, string displayValue, params string[] additionalValues)
		{
			return String.IsNullOrWhiteSpace(value) && additionalValues.All(v => String.IsNullOrWhiteSpace(v)) ? DISPLAY_NONE : String.Format("display: {0};", displayValue);
		}

		/// <summary>Returns a CSS displays value to hide the element if both values are not equal.</summary>
		/// <param name="left">The left.</param>
		/// <param name="right">The right.</param>
		public static string HideIfNotEqual(this object left, object right) { return left == right ? String.Empty : DISPLAY_NONE; }
		/// <summary>Returns a CSS displays value to show the element if both values are equal or hides it otherwise.</summary>
		/// <param name="left">The left.</param>
		/// <param name="right">The right.</param>
		/// <param name="displayValue">The display value returned to show the element (use INHERIT, BLOCK or INLINEBLOCK).</param>
		public static string DisplayIfEqual(this object left, object right, string displayValue = INHERIT) { return left == right ? String.Format("display: {0};", displayValue) : DISPLAY_NONE; }
	}
}