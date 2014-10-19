using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;

namespace IsolatedRazor
{
	/// <summary>Defines a dynamic view bag which can cross the AppDomain-boundary.</summary>
	[Serializable]
	public class DynamicViewBag : DynamicObject
	{
		private readonly IDictionary<string, object> values = new Dictionary<string, object>();

		/// <summary>Initializes a new instance of the <see cref="DynamicViewBag"/> class.</summary>
		/// <param name="parentViewBag">The parent view bag.</param>
		public DynamicViewBag(dynamic parentViewBag = null)
		{
			if (parentViewBag != null)
			{
				if (parentViewBag is DynamicViewBag)
				{
					foreach (var pair in (parentViewBag as DynamicViewBag).values)
						values.Add(pair);
				}
				else if (parentViewBag is IDictionary<string, object>)	// IDictionary<string, object> => ExpandoObject
				{
					foreach (var pair in (parentViewBag as IDictionary<string, object>))
						values.Add(pair);
				}
				else if (parentViewBag is DynamicObject)
				{
					var viewBag = parentViewBag as DynamicObject;
					foreach (var member in viewBag.GetDynamicMemberNames())
					{
						try { values[member] = ImpromptuInterface.Impromptu.InvokeGet(viewBag, member); }
						catch { }	// in case of RuntimebinderException -> we don't care - only load "available elements"
					}
				}
				else
					throw new ArgumentException("Unsupported dynamic type: " + (parentViewBag as object).GetType().Name, "parentViewBag");
			}
		}

		/// <summary>Returns the enumeration of all dynamic member names.</summary>
		/// <returns>A sequence that contains dynamic member names.</returns>
		public override IEnumerable<string> GetDynamicMemberNames() { return values.Keys; }

		/// <summary>
		/// Provides the implementation for operations that get member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject" /> class can override this method to specify dynamic behavior for operations such as getting a value for a property.
		/// </summary>
		/// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member on which the dynamic operation is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty) statement, where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject" /> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
		/// <param name="result">The result of the get operation. For example, if the method is called for a property, you can assign the property value to <paramref name="result" />.</param>
		/// <returns>
		/// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a run-time exception is thrown.)
		/// </returns>
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (values.ContainsKey(binder.Name))
			{
				result = values[binder.Name];
				return true;
			}

			result = null;
			return false;
		}

		/// <summary>
		/// Provides the implementation for operations that set member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject" /> class can override this method to specify dynamic behavior for operations such as setting a value for a property.
		/// </summary>
		/// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member to which the value is being assigned. For example, for the statement sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject" /> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
		/// <param name="value">The value to set to the member. For example, for sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject" /> class, the <paramref name="value" /> is "Test".</param>
		/// <returns>
		/// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown.)
		/// </returns>
		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (values.ContainsKey(binder.Name))
				values[binder.Name] = value;
			else
				values.Add(binder.Name, value);

			return true;
		}

		/// <summary>Set a value in this instance of DynamicViewBag.</summary>
		/// <param name="propertyName">The property name through which this value can be get/set.</param>
		/// <param name="value">The value that will be assigned to this property name.</param>
		/// <exception cref="System.ArgumentNullException">The propertyName parameter may not be NULL.</exception>
		public void SetValue(string propertyName, object value)
		{
			if (propertyName == null)
				throw new ArgumentNullException("The propertyName parameter may not be NULL.");

			values[propertyName] = value;
		}

		/// <summary>Add a value to this instance of DynamicViewBag.</summary>
		/// <param name="propertyName">The property name through which this value can be get/set.</param>
		/// <param name="value">The value that will be assigned to this property name.</param>
		/// <exception cref="System.ArgumentNullException">The propertyName parameter may not be NULL.</exception>
		/// <exception cref="System.ArgumentException">Attempt to add duplicate value for the ' + propertyName + ' property.</exception>
		public void AddValue(string propertyName, object value)
		{
			if (propertyName == null)
				throw new ArgumentNullException("The propertyName parameter may not be NULL.");

			if (values.ContainsKey(propertyName) == true)
				throw new ArgumentException("Attempt to add duplicate value for the '" + propertyName + "' property.");

			values.Add(propertyName, value);
		}

		/// <summary>Adds values from the specified valueList to this instance of DynamicViewBag.</summary>
		/// <param name="valueList">A list of objects.  Each must have a public property of keyPropertyName.</param>
		/// <param name="keyPropertyName">The property name that will be retrieved for each object in the specified valueList
		/// and used as the key (property name) for the ViewBag.  This property must be of type string.</param>
		/// <exception cref="System.ArgumentNullException">
		/// Invalid NULL value in initializer list.
		/// or
		/// The keyPropertyName property must be of type string.
		/// </exception>
		/// <exception cref="System.ArgumentException">Attempt to add duplicate value for the ' + strKey + ' property.</exception>
		public void AddListValues(IList valueList, string keyPropertyName)
		{
			foreach (object value in valueList)
			{
				if (value == null)
					throw new ArgumentNullException("Invalid NULL value in initializer list.");

				Type type = value.GetType();
				object objKey = type.GetProperty(keyPropertyName);

				if (objKey.GetType() != typeof(string))
					throw new ArgumentNullException("The keyPropertyName property must be of type string.");

				string strKey = (string)objKey;

				if (values.ContainsKey(strKey) == true)
					throw new ArgumentException("Attempt to add duplicate value for the '" + strKey + "' property.");

				values.Add(strKey, value);
			}
		}

		/// <summary>Adds values from the specified valueDictionary to this instance of DynamicViewBag.</summary>
		/// <param name="valueDictionary">A dictionary of objects.  The Key of each item in the dictionary will be used
		/// as the key (property name) for the ViewBag.</param>
		/// <exception cref="System.ArgumentNullException">The Key in valueDictionary must be of type string.</exception>
		/// <exception cref="System.ArgumentException">Attempt to add duplicate value for the ' + strKey + ' property.</exception>
		public void AddDictionaryValues(IDictionary valueDictionary)
		{
			foreach (object objKey in valueDictionary.Keys)
			{
				if (objKey.GetType() != typeof(string))
					throw new ArgumentNullException("The Key in valueDictionary must be of type string.");

				string strKey = (string)objKey;

				if (values.ContainsKey(strKey) == true)
					throw new ArgumentException("Attempt to add duplicate value for the '" + strKey + "' property.");

				object value = valueDictionary[strKey];

				values.Add(strKey, value);
			}
		}

		/// <summary>Adds values from the specified valueDictionary to this instance of DynamicViewBag.</summary>
		/// <param name="valueDictionary">A generic dictionary of {string, object} objects.  The Key of each item in the
		/// dictionary will be used as the key (property name) for the ViewBag.</param>
		/// <exception cref="System.ArgumentException">Attempt to add duplicate value for the ' + strKey + ' property.</exception>
		/// <remarks>
		/// This method was intentionally not overloaded from AddDictionaryValues due to an ambiguous
		/// signature when the caller passes in a Dictionary&lt;string, object&gt; as the valueDictionary.
		/// This is because the Dictionary&lt;TK, TV&gt;() class implements both IDictionary and IDictionary&lt;TK, TV&gt;.
		/// A Dictionary&lt;string, ???&gt; (any other type than object) will resolve to AddDictionaryValues.
		/// This is specifically for a generic List&lt;string, object&gt;, which does not resolve to
		/// an IDictionary interface.
		/// </remarks>
		public void AddDictionaryValuesEx(IDictionary<string, object> valueDictionary)
		{
			foreach (string strKey in valueDictionary.Keys)
			{
				if (values.ContainsKey(strKey) == true)
					throw new ArgumentException("Attempt to add duplicate value for the '" + strKey + "' property.");

				object value = valueDictionary[strKey];

				values.Add(strKey, value);
			}
		}
	}
}
