using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IsolatedRazor.Interfaces;

namespace IsolatedRazor
{
	public abstract class LayoutBase : TemplateBase, ILayout
	{
		/// <summary>Gets or sets the body.</summary>
		/// <value>The body.</value>
		public string Body { get; set; }
		
		/// <summary>Renders the body.</summary>
		public IEncodedString RenderBody() { return Raw(Body); }

		/// <summary>Renders the section.</summary>
		/// <param name="name">The name.</param>
		/// <param name="isRequired">If set to <c>true</c> the section is required.</param>
		public IEncodedString RenderSection(string name, bool isRequired = true)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("The name of the section must be specified.");

			if ((Sections == null || !Sections.ContainsKey(name)) && isRequired)
				throw new ArgumentException(String.Format("No section has been defined with the name \"{0}\"", name));

			var action = Sections[name];
			if (action != null) 
				action();

			return Raw(String.Empty);
		}
	}
}
