using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IsolatedRazor.Interfaces;

namespace IsolatedRazor
{
	/// <summary>Represents an not encoded string.</summary>
	public class RawString : IEncodedString
	{
		private readonly string value;

		/// <summary>Initializes a new instance of the <see cref="RawString"/> class.</summary>
		/// <param name="value">The value.</param>
		public RawString(string value) { this.value = value; }

		/// <summary>Gets the encoded string.</summary>
		/// <returns>The encoded string.</returns>
		public string ToEncodedString() { return value ?? String.Empty; }

		/// <summary>Gets the string representation of this instance.</summary>
		/// <returns>The string representation of this instance.</returns>
		public override string ToString() { return ToEncodedString(); }
	}
}
