using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sylvan.BuildTools.Resources
{
	public class JsonDocument : JsonNode
	{
		public static JsonDocument Load(string jsonString)
		{
			var reader = new JsonReader(new StringReader(jsonString));
			return Load(reader);
		}

		public static JsonDocument Load(JsonReader reader)
		{
			var nodeStack = new Stack<JsonNode>();
			var doc = new JsonDocument();
			nodeStack.Push(doc);

			var propertyStack = new Stack<string>();
			JsonNode node = null;

			while (reader.Read()) {

				switch (reader.SyntaxKind) {
				case SyntaxKind.Comment:
				case SyntaxKind.None:
					continue;
				case SyntaxKind.ArrayStart:
					var arr = new JsonArray();
					arr.Start = reader.Start;
					nodeStack.Push(arr);
					continue;
				case SyntaxKind.ObjectStart:
					var obj = new JsonObject();
					obj.Start = reader.Start;
					nodeStack.Push(obj);
					continue;
				case SyntaxKind.PropertyName:
					propertyStack.Push(reader.PropertyName);

					continue;
				case SyntaxKind.ArrayEnd:
				case SyntaxKind.ObjectEnd:
					node = nodeStack.Pop();
					node.End = reader.End;
					break;
				case SyntaxKind.StringValue:
					node = new JsonString(reader.StringValue);
					SetLocation(reader, node);
					break;
				case SyntaxKind.DoubleValue:
					node = new JsonNumber(reader.FloatValue);
					SetLocation(reader, node);
					break;
				case SyntaxKind.IntegerValue:
					node = new JsonNumber(reader.IntegerValue);
					SetLocation(reader, node);
					break;
				case SyntaxKind.NullValue:
					node = new JsonNull();
					SetLocation(reader, node);
					break;
				case SyntaxKind.TrueValue:
					node = new JsonBoolean(true);
					SetLocation(reader, node);
					break;
				case SyntaxKind.FalseValue:
					node = new JsonBoolean(false);
					SetLocation(reader, node);
					break;
				}

				var parent = nodeStack.Peek();

				if (parent is JsonArray a) {
					a.Add(node);
				}
				else
				if (parent is JsonObject o) {
					var propertyName = propertyStack.Pop();
					o.Add(propertyName, node);
				}
				else
				if (parent is JsonDocument d) {
					d.RootNode = node;
					break;
				}
			}
			if (doc.RootNode != null) {
				doc.Start = doc.RootNode.Start;
				doc.End = doc.RootNode.End;
			}

			return doc;
		}

		static void SetLocation(JsonReader reader, JsonNode node)
		{
			node.Start = reader.Start;
			node.End = reader.End;
		}

		public JsonNode RootNode
		{
			get; private set;
		}

		public override JsonNodeType NodeType => JsonNodeType.Document;
	}

	public enum JsonNodeType
	{
		Document,
		Object,
		Array,
		String,
		Number,
		Boolean,
		Null,
	}

	public abstract class JsonNode
	{
		public abstract JsonNodeType NodeType { get; }

		public Location Start { get; internal set; }

		public Location End { get; internal set; }

		public JsonNode() { }

		internal JsonNode(JsonReader reader) : this(Location.Empty, Location.Empty)
		{
		}

		public JsonNode(Location start, Location end)
		{
			this.Start = start;
			this.End = end;
		}
	}

	public sealed class JsonObject : JsonNode, IDictionary<string, JsonNode>
	{
		Dictionary<string, JsonNode> members;

		public override JsonNodeType NodeType => JsonNodeType.Object;

		public ICollection<string> Keys => members.Keys;

		public ICollection<JsonNode> Values => members.Values;

		public int Count => members.Count;

		public bool IsReadOnly => true;

		JsonNode IDictionary<string, JsonNode>.this[string key]
		{
			get => this[key];
			set => throw new NotSupportedException();
		}

		internal JsonObject(Dictionary<string, JsonNode> members)
		{
			this.members = members;
		}

		internal JsonObject()
		{
			this.members = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
		}

		public JsonNode this[string key]
		{
			get
			{
				return this.members.TryGetValue(key, out JsonNode node) ? node : null;
			}
		}

		internal void Add(string name, JsonNode value)
		{
			this.members.Add(name, value);
		}

		void IDictionary<string, JsonNode>.Add(string key, JsonNode value)
		{
			throw new NotSupportedException();
		}

		public bool ContainsKey(string key)
		{
			return this.members.ContainsKey(key);
		}

		public bool Remove(string key)
		{
			throw new NotSupportedException();
		}

		public bool TryGetValue(string key, out JsonNode value)
		{
			return this.members.TryGetValue(key, out value);
		}

		public void Add(KeyValuePair<string, JsonNode> item)
		{
			throw new NotSupportedException();
		}

		public void Clear()
		{
			throw new NotSupportedException();
		}

		public bool Contains(KeyValuePair<string, JsonNode> item)
		{
			throw new NotSupportedException();
		}

		public void CopyTo(KeyValuePair<string, JsonNode>[] array, int arrayIndex)
		{
			throw new NotSupportedException();
		}

		public bool Remove(KeyValuePair<string, JsonNode> item)
		{
			throw new NotSupportedException();
		}

		public IEnumerator<KeyValuePair<string, JsonNode>> GetEnumerator()
		{
			return this.members.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}

	public sealed class JsonArray : JsonNode, IList<JsonNode>
	{
		List<JsonNode> elements;
		int count;

		public override JsonNodeType NodeType => JsonNodeType.Array;

		public JsonArray()
		{
			this.elements = new List<JsonNode>();
			this.count = 0;
		}

		public JsonNode this[int idx]
		{
			get
			{
				if (idx < 0 || idx >= count)
					throw new ArgumentOutOfRangeException(nameof(idx));
				return elements[idx];
			}
		}

		public int Count => this.elements.Count;

		public bool IsReadOnly => true;

		JsonNode IList<JsonNode>.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException();
		}

		internal void Add(JsonNode node)
		{
			this.elements.Add(node);
		}

		public int IndexOf(JsonNode item)
		{
			for (int i = 0; i < this.count; i++) {
				if (this[i].Equals(item)) {
					return i;
				}
			}
			return -1;
		}

		public void Insert(int index, JsonNode item)
		{
			throw new NotSupportedException();
		}

		public void RemoveAt(int index)
		{
			throw new NotSupportedException();
		}

		void ICollection<JsonNode>.Add(JsonNode item)
		{
			throw new NotSupportedException();
		}

		public void Clear()
		{
			throw new NotSupportedException();
		}

		public bool Contains(JsonNode item)
		{
			return this.IndexOf(item) != -1;
		}

		public void CopyTo(JsonNode[] array, int arrayIndex)
		{
			this.elements.CopyTo(array, arrayIndex);
		}

		public bool Remove(JsonNode item)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<JsonNode> GetEnumerator()
		{
			return this.elements.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}

	public sealed class JsonString : JsonNode
	{
		public string Value { get; }

		public JsonString(string value)
		{
			this.Value = value;
		}

		public override JsonNodeType NodeType => JsonNodeType.String;
	}

	public sealed class JsonNumber : JsonNode
	{
		bool isDouble;
		NumberValue value;

		public JsonNumber(double value)
		{
			this.isDouble = true;
			this.value = value;
		}

		public JsonNumber(long value)
		{
			this.isDouble = false;
			this.value = value;
		}

		public bool IsDouble => this.isDouble;

		public override JsonNodeType NodeType => JsonNodeType.Number;
	}

	public sealed class JsonBoolean : JsonNode
	{
		readonly bool value;

		public JsonBoolean(bool value)
		{
			this.value = value;
		}

		public override JsonNodeType NodeType => JsonNodeType.Boolean;
	}

	public sealed class JsonNull : JsonNode
	{
		public JsonNull()
		{
		}

		public override JsonNodeType NodeType => JsonNodeType.Null;
	}

	[StructLayout(LayoutKind.Explicit)]
	readonly struct NumberValue
	{
		[FieldOffset(0)]
		public readonly double DoubleValue;
		[FieldOffset(0)]
		public readonly long IntegerValue;

		public NumberValue(double value)
		{
			this = default;
			this.DoubleValue = value;
		}

		public NumberValue(long value)
		{
			this = default;
			this.IntegerValue = value;
		}

		public static implicit operator NumberValue(double value)
		{
			return new NumberValue(value);
		}

		public static implicit operator NumberValue(long value)
		{
			return new NumberValue(value);
		}
	}
}
