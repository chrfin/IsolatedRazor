using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsolatedRazor
{
	[Serializable]
	public class TemplateCache
	{
		private Dictionary<string, TemplateCacheItem> cache = new Dictionary<string, TemplateCacheItem>();

		/// <summary>Gets the cached names.</summary>
		/// <value>The cached names.</value>
		public IEnumerable<string> CachedNames { get { return cache.Keys; } }

		/// <summary>Sets the specified template in the cache using the given name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="templateAssemblyPath">The template assembly path.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		public void Set(string name, string templateAssemblyPath, string template, DateTime timestamp)
		{
			cache[name] = new TemplateCacheItem()
			{
				Timestamp = timestamp,
				HashCode = template.GetHashCode(),
				Value = templateAssemblyPath
			};
		}
		/// <summary>Sets the specified template in the cache using the given name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="templateAssemblyPath">The template assembly path.</param>
		/// <param name="templates">The template group.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		public void Set(string name, string templateAssemblyPath, Dictionary<string, string> templates, DateTime timestamp)
		{
			cache[name] = new TemplateCacheItem()
			{
				Timestamp = timestamp,
				HashCode = GetHashCode(templates),
				Value = templateAssemblyPath
			};
		}

		/// <summary>Gets the templates assembly path from the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="template">The template.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <returns>NULL if the template is not cached or did change.</returns>
		public string Get(string name, string template, DateTime? timestamp)
		{
			TemplateCacheItem result;
			if (cache.TryGetValue(name, out result))
				return (!timestamp.HasValue || result.Timestamp == timestamp.Value)
					&& (String.IsNullOrWhiteSpace(template) || result.HashCode == template.GetHashCode()) ? result.Value : null;

			return null;
		}
		/// <summary>Gets the templates assembly path from the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		/// <param name="templates">The template group.</param>
		/// <param name="timestamp">The time-stamp the template (NOT the assembly) was created.</param>
		/// <returns>NULL if the template is not cached or did change.</returns>
		public string Get(string name, Dictionary<string, string> templates, DateTime? timestamp)
		{
			TemplateCacheItem result;
			if (cache.TryGetValue(name, out result))
				return (!timestamp.HasValue || result.Timestamp == timestamp.Value)
					&& (templates == null || result.HashCode == GetHashCode(templates)) ? result.Value : null;

			return null;
		}

		/// <summary>Returns a hash code for this template group.</summary>
		/// <param name="templates">The templates.</param>
		/// <returns>A hash code for this template group, suitable for use in hashing algorithms and data structures like a hash table. </returns>
		private static int GetHashCode(Dictionary<string, string> templates)
		{
			int hashCode = 0;
			templates.ToList().ForEach(t => hashCode = String.IsNullOrWhiteSpace(t.Value) ? hashCode : (hashCode ^ t.Key.GetHashCode() ^ t.Value.GetHashCode()));
			return hashCode;
		}

		/// <summary>Removes the template with the specified name.</summary>
		/// <param name="name">The name.</param>
		public bool Remove(string name) { return cache.Remove(name); }
	}

	[Serializable]
	public class TemplateCacheItem
	{
		/// <summary>Gets or sets the time-stamp.</summary>
		/// <value>The time-stamp.</value>
		public DateTime Timestamp { get; set; }
		/// <summary>Gets or sets the hash code.</summary>
		/// <value>The hash code.</value>
		public int HashCode { get; set; }

		/// <summary>Gets or sets the value.</summary>
		/// <value>The value.</value>
		public string Value { get; set; }
	}
}
