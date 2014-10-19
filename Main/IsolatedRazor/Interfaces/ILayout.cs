using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor.Interfaces
{
	/// <summary>Defines a layout template.</summary>
	public interface ILayout : ITemplate
	{
		/// <summary>Gets or sets the body.</summary>
		/// <value>The body.</value>
		string Body { get; set; }

		/// <summary>Gets or sets the sections.</summary>
		/// <value>The sections.</value>
		IDictionary<string, Action> Sections { get; set; } 

		/// <summary>Renders the body.</summary>
		IEncodedString RenderBody();

		/// <summary>Renders the section with the specified name.</summary>
		/// <param name="name">The name of the section.</param>
		/// <param name="isRequired">Flag to specify whether the section is required.</param>
		/// <returns>The template writer helper.</returns>
		IEncodedString RenderSection(string name, bool isRequired = true);
	}
}