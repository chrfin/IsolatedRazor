using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor
{
	public class CompilerException : Exception
	{
		/// <summary>Gets the errors.</summary>
		/// <value>The errors.</value>
		public CompilerErrorCollection Errors { get; private set; }

		/// <summary>Initializes a new instance of the <see cref="CompilerException" /> class.</summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="errors">The errors.</param>
		public CompilerException(string message, CompilerErrorCollection errors) : base(message) { Errors = errors; }
	}
}
