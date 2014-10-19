using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IsolatedRazor.Enumerations;

namespace IsolatedRazor.Interfaces
{
	/// <summary>Defines the required contract for implementing a template.</summary>
	public interface ITemplate
	{
		/// <summary>Gets or sets the encoding.</summary>
		/// <value>The encoding.</value>
		TemplateEncoding Encoding { get; set; }

		/// <summary>Gets or sets the layout.</summary>
		/// <value>The layout.</value>
		string Layout { get; set; }

		/// <summary>Gets or sets the view bag.</summary>
		/// <value>The view bag.</value>
		dynamic ViewBag { get; set; }

		/// <summary>Gets or sets the template resolver.</summary>
		/// <value>The template resolver.</value>
		Func<string, ITemplate> TemplateResolver { get; set; }
		
		/// <summary>Defines a section that can written out to a layout.</summary>
		/// <param name="name">The name of the section.</param>
		/// <param name="action">The delegate used to write the section.</param>
		void DefineSection(string name, Action action);
		/// <summary>Determines if the section with the specified name has been defined.</summary>
		/// <param name="name">The section name.</param>
		bool IsSectionDefined(string name);
		
		/// <summary>Includes the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="model">The model or NULL if there is no model for the template.</param>
		IEncodedString Include(string name, object model = null);
		
		/// <summary>Returns the specified string as a raw string. This will ensure it is not encoded.</summary>
		/// <param name="rawString">The raw string to write.</param>
		IEncodedString Raw(string rawString);
		
		/// <summary>Resolves the specified path</summary>
		/// <param name="path">The path.</param>
		/// <returns>The resolved path.</returns>
		string ResolveUrl(string path);

		/// <summary>Executes the compiled template.</summary>
		void Execute();

		/// <summary>Renders the template and returns the result.</summary>
		string Render();

		/// <summary>Writes the specified object to the result.</summary>
		/// <param name="value">The value to write.</param>
		void Write(object value);

		/// <summary>Writes the specified string to the result.</summary>
		/// <param name="literal">The literal to write.</param>
		void WriteLiteral(string literal);
	}

	/// <summary>Defines the required contract for implementing a template with a model.</summary>
	/// <typeparam name="T">Type of the model.</typeparam>
	public interface ITemplate<T> : ITemplate
	{
		/// <summary>Gets or sets the model.</summary>
		/// <value>The model.</value>
		T Model { get; set; }
	}
}