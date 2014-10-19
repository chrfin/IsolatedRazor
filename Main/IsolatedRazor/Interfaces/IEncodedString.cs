using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor.Interfaces
{
	/// <summary>Defines the required contract for implementing an encoded string.</summary>
	public interface IEncodedString
	{
		/// <summary>Gets the encoded string.</summary>
		/// <returns>The encoded string.</returns>
		string ToEncodedString();
	}
}