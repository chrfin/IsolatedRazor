using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor
{
	/// <summary>Based on https://github.com/Antaris/RazorEngine/blob/master/src/Core/RazorEngine.Core/PositionTagged.cs </summary>
	[DebuggerDisplay("({Position})\"{Value}\"")]
	public class PositionTagged<T>
	{
		/// <summary>Prevents a default instance of the <see cref="PositionTagged{T}"/> class from being created.</summary>
		private PositionTagged()
		{
			Position = 0;
			Value = default(T);
		}

		/// <summary>Initializes a new instance of the <see cref="PositionTagged{T}"/> class.</summary>
		/// <param name="value">The value.</param>
		/// <param name="offset">The offset.</param>
		public PositionTagged(T value, int offset)
		{
			Position = offset;
			Value = value;
		}

		/// <summary>Gets the position.</summary>
		/// <value>The position.</value>
		public int Position { get; private set; }
		/// <summary>Gets the value.</summary>
		/// <value>The value.</value>
		public T Value { get; private set; }

		/// <summary>Determines whether the specified <see cref="System.Object" }, is equal to this instance.</summary>
		/// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			PositionTagged<T> other = obj as PositionTagged<T>;
			return other != null &&
				   other.Position == Position &&
				   Equals(other.Value, Value);
		}

		/// <summary>Returns a hash code for this instance.</summary>
		/// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. </returns>
		public override int GetHashCode() { return Position.GetHashCode() ^ Value.GetHashCode(); }

		/// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString() { return Value.ToString(); }

		/// <summary>Performs an implicit conversion from <see cref="PositionTagged{T}"/> to <see cref="T"/>.</summary>
		/// <param name="value">The value.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator T(PositionTagged<T> value) { return value.Value; }

		/// <summary>Performs an implicit conversion from <see cref="Tuple{T, System.Int32}"/> to <see cref="PositionTagged{T}"/>.</summary>
		/// <param name="value">The value.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator PositionTagged<T>(Tuple<T, int> value) { return new PositionTagged<T>(value.Item1, value.Item2); }

		/// <summary>Implements the operator ==.</summary>
		/// <param name="left">The left.</param>
		/// <param name="right">The right.</param>
		/// <returns>The result of the operator.</returns>
		public static bool operator ==(PositionTagged<T> left, PositionTagged<T> right) { return Equals(left, right); }

		/// <summary>Implements the operator !=.</summary>
		/// <param name="left">The left.</param>
		/// <param name="right">The right.</param>
		/// <returns>The result of the operator.</returns>
		public static bool operator !=(PositionTagged<T> left, PositionTagged<T> right) { return !Equals(left, right); }
	}
}