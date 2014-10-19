using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor
{
	/// <summary>Based on https://github.com/Antaris/RazorEngine/blob/master/src/Core/RazorEngine.Core/AttributeValue.cs </summary>
	public class AttributeValue
	{
		/// <summary>Initializes a new instance of the <see cref="AttributeValue"/> class.</summary>
		/// <param name="prefix">The prefix.</param>
		/// <param name="value">The value.</param>
		/// <param name="literal">if set to <c>true</c> [literal].</param>
		public AttributeValue(PositionTagged<string> prefix, PositionTagged<object> value, bool literal)
		{
			Prefix = prefix;
			Value = value;
			Literal = literal;
		}

		/// <summary>Gets the prefix.</summary>
		/// <value>The prefix.</value>
		public PositionTagged<string> Prefix { get; private set; }
		/// <summary>Gets the value.</summary>
		/// <value>The value.</value>
		public PositionTagged<object> Value { get; private set; }
		/// <summary>Gets a value indicating whether this <see cref="AttributeValue"/> is literal.</summary>
		/// <value><c>true</c> if literal; otherwise, <c>false</c>.</value>
		public bool Literal { get; private set; }

		/// <summary>Froms the tuple.</summary>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		public static AttributeValue FromTuple(Tuple<Tuple<string, int>, Tuple<object, int>, bool> value) { return new AttributeValue(value.Item1, value.Item2, value.Item3); }

		/// <summary>Froms the tuple.</summary>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		public static AttributeValue FromTuple(Tuple<Tuple<string, int>, Tuple<string, int>, bool> value)
		{
			return new AttributeValue(value.Item1, new PositionTagged<object>(value.Item2.Item1, value.Item2.Item2), value.Item3);
		}

		/// <summary>
		/// Performs an implicit conversion from <see cref="Tuple{Tuple{System.String, System.Int32}, Tuple{System.Object, System.Int32}, System.Boolean}"/> to <see cref="AttributeValue"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator AttributeValue(Tuple<Tuple<string, int>, Tuple<object, int>, bool> value) { return FromTuple(value); }

		/// <summary>
		/// Performs an implicit conversion from <see cref="Tuple{Tuple{System.String, System.Int32}, Tuple{System.String, System.Int32}, System.Boolean}"/> to <see cref="AttributeValue"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator AttributeValue(Tuple<Tuple<string, int>, Tuple<string, int>, bool> value) { return FromTuple(value); }
	}
}
