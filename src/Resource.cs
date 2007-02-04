using System;
using System.Collections;
using System.Xml;

namespace SemWeb {
	
	public abstract class Resource :
#if DOTNET2
	IComparable<Resource>
#else
	IComparable
#endif
	{
		internal object ekKey, ekValue;
		internal ArrayList extraKeys;
		
		internal class ExtraKey {
			public object Key;
			public object Value; 
			public ExtraKey(object k, object v) { Key = k; Value = v; }
		}
		
		public abstract string Uri { get; }
		
		internal Resource() {
		}
		
		// These gets rid of the warning about overring ==, !=.
		// Since Entity and Literal override these, we're ok.
		public override bool Equals(object other) {
			return base.Equals(other);
		}
		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public static bool operator ==(Resource a, Resource b) {
			if ((object)a == null && (object)b == null) return true;
			if ((object)a == null || (object)b == null) return false;
			return a.Equals(b);
		}
		public static bool operator !=(Resource a, Resource b) {
			return !(a == b);
		}
		
		public object GetResourceKey(object key) {
			if (ekKey == key) return ekValue;
			if (extraKeys == null) return null;
			for (int i = 0; i < extraKeys.Count; i++) {
				Resource.ExtraKey ekey = (Resource.ExtraKey)extraKeys[i];
				if (ekey.Key == key)
					return ekey.Value;
			}
			return null;
		}
		public void SetResourceKey(object key, object value) {
			if (ekKey == null || ekKey == key) {
				ekKey = key;
				ekValue = value;
				return;
			}
			
			if (this is BNode) throw new InvalidOperationException("Only one resource key can be set for a BNode.");
		
			if (extraKeys == null) extraKeys = new ArrayList();
			
			foreach (Resource.ExtraKey ekey in extraKeys)
				if (ekey.Key == key) { ekey.Value = value; return; }
			
			Resource.ExtraKey k = new Resource.ExtraKey(key, value);
			extraKeys.Add(k);
		}

#if DOTNET2
		public int CompareTo(Resource other) {
#else
		public int CompareTo(object other) {
#endif
			// We'll make an ordering over resources.
			// First named entities, then bnodes, then literals.
			// Named entities are sorted by URI.
			// Bnodes by hashcode.
			// Literals by their value, language, datatype.
		
			Resource r = (Resource)other;
			if (Uri != null && r.Uri == null) return -1;
			if (Uri == null && r.Uri != null) return 1;
			if (this is BNode && r is Literal) return -1;
			if (this is Literal && r is BNode) return 1;
			
			if (Uri != null) return String.Compare(Uri, r.Uri, false, System.Globalization.CultureInfo.InvariantCulture);
			
			if (this is BNode) {
				BNode me = (BNode)this, o = (BNode)other;
				if (me.LocalName != null || o.LocalName != null) {
					if (me.LocalName == null) return -1;
					if (o.LocalName == null) return -1;
					int x = String.Compare(me.LocalName, o.LocalName, false, System.Globalization.CultureInfo.InvariantCulture);
					if (x != 0) return x;
				}
				return GetHashCode().CompareTo(r.GetHashCode());
			}

			if (this is Literal) {
				int x = String.Compare(((Literal)this).Value, ((Literal)r).Value, false, System.Globalization.CultureInfo.InvariantCulture);
				if (x != 0) return x;
				x = String.Compare(((Literal)this).Language, ((Literal)r).Language, false, System.Globalization.CultureInfo.InvariantCulture);
				if (x != 0) return x;
				x = String.Compare(((Literal)this).DataType, ((Literal)r).DataType, false, System.Globalization.CultureInfo.InvariantCulture);
				return x;
			}
			
			return 0; // unreachable
		}
	}
	
	public class Entity : Resource {
		private string uri;
		
		public Entity(string uri) {
			if (uri == null) throw new ArgumentNullException("To construct entities with no URI, use the BNode class.");
			if (uri.Length == 0) throw new ArgumentException("uri cannot be the empty string");
			this.uri = uri;
		}
		
		// For the BNode constructor only.
		internal Entity() {
		}
		
		public override string Uri {
			get {
				return uri;
			}
		}
		
		public static implicit operator Entity(string uri) { return new Entity(uri); }
		
		public override int GetHashCode() {
			if (uri == null) return base.GetHashCode(); // this is called from BNode.GetHashCode().
			return uri.GetHashCode();
		}
			
		public override bool Equals(object other) {
			if (!(other is Resource)) return false;
			if (object.ReferenceEquals(this, other)) return true;
			return ((Resource)other).Uri != null && ((Resource)other).Uri == Uri;
		}
		
		// Although these do the same as Resource's operator overloads,
		// having these plus the implict string conversion allows
		// these operators to work with entities and strings.

		public static bool operator ==(Entity a, Entity b) {
			if ((object)a == null && (object)b == null) return true;
			if ((object)a == null || (object)b == null) return false;
			return a.Equals(b);
		}
		public static bool operator !=(Entity a, Entity b) {
			return !(a == b);
		}
		
		public override string ToString() {
			return "<" + Uri + ">";
		}
	}
	
	public class BNode : Entity {
		string localname;
	
		public BNode() {
		}
		
		public BNode(string localName) {
			localname = localName;
			if (localname != null && localname.Length == 0) throw new ArgumentException("localname cannot be the empty string");
		}
		
		public string LocalName { get { return localname; } }

		public override int GetHashCode() {
			if (ekKey != null)
				return ekKey.GetHashCode() ^ ekValue.GetHashCode();
			
			// If there's no ExtraKeys info, then this
			// object is only equal to itself.  It's then safe
			// to use object.GetHashCode().
			return base.GetHashCode();
		}
			
		public override bool Equals(object other) {
			if (object.ReferenceEquals(this, other)) return true;
			if (!(other is BNode)) return false;
			
			object okKey = ((Resource)other).ekKey;
			object okValue = ((Resource)other).ekValue;
			
			return (ekKey != null && okKey != null)
				&& (ekKey == okKey)
				&& ekValue.Equals(okValue);
		}
		
		public override string ToString() {
			if (LocalName != null)
				return "_:" + LocalName;
			else
				return "_:bnode" + Math.Abs(GetHashCode());
		}
	}
	
	public class Variable : BNode {
		public Variable() : base() {
		}
		
		public Variable(string variableName) : base(variableName) {
		}
		
		public override string ToString() {
			if (LocalName != null)
				return "?" + LocalName;
			else
				return "?var" + Math.Abs(GetHashCode());
		}
	}

	public sealed class Literal : Resource { 
		private string value, lang, type;
		
		public Literal(string value) : this(value, null, null) {
		}
		
		public Literal(string value, string language, string dataType) {
		  if (value == null)
			  throw new ArgumentNullException("value");
		  this.value = string.Intern(value);
		  this.lang = language;
		  this.type = dataType;
		  
		  if (language != null && language.Length == 0) throw new ArgumentException("language cannot be the empty string");
		  if (dataType != null && dataType.Length == 0) throw new ArgumentException("dataType cannot be the empty string");
		}
		
		public static explicit operator Literal(string value) { return new Literal(value); }

		public override string Uri { get { return null; } }
		
		public string Value { get { return value; } }
		public string Language { get { return lang; } }
		public string DataType { get { return type; } }
		
		public override bool Equals(object other) {
			if (other == null) return false;
			if (!(other is Literal)) return false;
			Literal literal = (Literal)other;
			if (Value != literal.Value) return false;
			if (different(Language, literal.Language)) return false;
			if (different(DataType, literal.DataType)) return false;		
			return true;
		}
		
		private bool different(string a, string b) {
			if ((object)a == (object)b) return false;
			if (a == null || b == null) return true;
			return a != b;
		}
		
		public override int GetHashCode() {
			return Value.GetHashCode(); 
		 }
		
		public override string ToString() {
			System.Text.StringBuilder ret = new System.Text.StringBuilder();
			ret.Append('"');
			ret.Append(N3Writer.Escape(Value));
			ret.Append('"');
			
			if (Language != null) {
				ret.Append('@');
				ret.Append(N3Writer.Escape(Language));
			}
			
			if (DataType != null) {
				ret.Append("^^<");
				ret.Append(N3Writer.Escape(DataType));
				ret.Append(">");
			}
			return ret.ToString();
		}
		
		public static Literal Parse(string literal, NamespaceManager namespaces) {
			if (literal.Length < 2 || literal[0] != '\"') throw new FormatException("Literal value must start with a quote.");
			int quote = literal.LastIndexOf('"');
			if (quote <= 0) throw new FormatException("Literal value must have an end quote (" + literal + ")");
			string value = literal.Substring(1, quote-1);
			literal = literal.Substring(quote+1);
			
			value = value.Replace("\\\"", "\"");
			value = value.Replace("\\\\", "\\");
			
			string lang = null;
			string datatype = null;
			
			if (literal.Length >= 2 && literal[0] == '@') {
				int type = literal.IndexOf("^^");
				if (type == -1) lang = literal.Substring(1);
				else {
					lang = literal.Substring(1, type);
					literal = literal.Substring(type);
				}
			}
			
			if (literal.StartsWith("^^")) {
				if (literal.StartsWith("^^<") && literal.EndsWith(">")) {
					datatype = literal.Substring(3, literal.Length-4);
				} else {
					if (namespaces == null)
						throw new ArgumentException("No NamespaceManager was given to resolve the QName in the literal string.");
					datatype = namespaces.Resolve(literal.Substring(2));
				}
			}
			
			return new Literal(value, lang, datatype);
		}
		
		public object ParseValue() {
			string dt = DataType;
			if (dt == null || !dt.StartsWith(NS.XMLSCHEMA)) return Value;
			dt = dt.Substring(NS.XMLSCHEMA.Length);
			
			if (dt == "string" || dt == "normalizedString" || dt == "anyURI") return Value;
			if (dt == "boolean") return XmlConvert.ToBoolean(Value);
			if (dt == "decimal" || dt == "integer" || dt == "nonPositiveInteger" || dt == "negativeInteger" || dt == "nonNegativeInteger" || dt == "positiveInteger") return XmlConvert.ToDecimal(Value);
			if (dt == "float") return XmlConvert.ToSingle(Value);
			if (dt == "double") return XmlConvert.ToDouble(Value);
			if (dt == "duration") return XmlConvert.ToTimeSpan(Value);
			if (dt == "dateTime" || dt == "time" || dt == "date") return XmlConvert.ToDateTime(Value);
			if (dt == "long") return XmlConvert.ToInt64(Value);
			if (dt == "int") return XmlConvert.ToInt32(Value);
			if (dt == "short") return XmlConvert.ToInt16(Value);
			if (dt == "byte") return XmlConvert.ToSByte(Value);
			if (dt == "unsignedLong") return XmlConvert.ToUInt64(Value);
			if (dt == "unsignedInt") return XmlConvert.ToUInt32(Value);
			if (dt == "unsignedShort") return XmlConvert.ToUInt16(Value);
			if (dt == "unsignedByte") return XmlConvert.ToByte(Value);
			
			return Value;
		}
		
		public Literal Normalize() {
			if (DataType == null) return this;
			return new Literal(ParseValue().ToString(), Language, DataType);
		}
		
		public static Literal Create(bool value) {
			return new Literal(value ? "true" : "false", null, NS.XMLSCHEMA + "boolean");
		}
		
		public static Literal FromValue(float value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "float");
		}
		public static Literal FromValue(double value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "double");
		}
		public static Literal FromValue(byte value) {
			if (value <= 127)
				return new Literal(value.ToString(), null, NS.XMLSCHEMA + "byte");
			else
				return new Literal(value.ToString(), null, NS.XMLSCHEMA + "unsignedByte");
		}
		public static Literal FromValue(short value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "short");
		}
		public static Literal FromValue(int value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "int");
		}
		public static Literal FromValue(long value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "long");
		}
		public static Literal FromValue(sbyte value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "byte");
		}
		public static Literal FromValue(ushort value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "unsignedShort");
		}
		public static Literal FromValue(uint value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "unsignedInt");
		}
		public static Literal FromValue(ulong value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "unsignedLong");
		}
		public static Literal FromValue(Decimal value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "decimal");
		}
		public static Literal FromValue(bool value) {
			return new Literal(value ? "true" : "false", null, NS.XMLSCHEMA + "boolean");
		}
		public static Literal FromValue(string value) {
			return new Literal(value, null, NS.XMLSCHEMA + "string");
		}
		public static Literal FromValue(Uri value) {
			return new Literal(value.ToString(), null, NS.XMLSCHEMA + "anyURI");
		}
		public static Literal FromValue(DateTime value) {
			return FromValue(value, true, false);
		}
		public static Literal FromValue(DateTime value, bool withTime, bool isLocalTime) {
			if (withTime && isLocalTime)
				return new Literal(value.ToString("yyyy-MM-ddTHH\\:mm\\:ss.FFFFFFF0zzz"), null, NS.XMLSCHEMA + "dateTime");
			else if (withTime)
				return new Literal(value.ToString("yyyy-MM-ddTHH\\:mm\\:ss.FFFFFFF0"), null, NS.XMLSCHEMA + "dateTime");
			else
				return new Literal(value.ToString("yyyy-MM-dd"), null, NS.XMLSCHEMA + "date");
		}
		public static Literal FromValue(TimeSpan value) {
			return FromValue(value, false, false);
		}
		public static Literal FromValue(TimeSpan value, bool asTime, bool isLocalTime) {
			if (!asTime) {
				string ret = (value.Ticks >= 0 ? "P" : "-P");
				if (value.Days != 0) {
					ret += value.Days + "D";
					if (value.Hours != 0 || value.Minutes != 0 || value.Seconds != 0 || value.Milliseconds != 0)
						ret += "T";
				}
				if (value.Hours != 0) ret += value.Hours + "H";
				if (value.Minutes != 0) ret += value.Minutes + "M";
				if (value.Seconds != 0 || value.Milliseconds != 0) ret += (value.Seconds + value.Milliseconds/1000) + "S";
				return new Literal(ret, null, NS.XMLSCHEMA + "duration");
			} else if (isLocalTime) {
				return new Literal((DateTime.Today + value).ToString("HH\\:mm\\:ss.FFFFFFF0zzz"), null, NS.XMLSCHEMA + "time");
			} else {
				return new Literal((DateTime.Today + value).ToString("HH\\:mm\\:ss.FFFFFFF0"), null, NS.XMLSCHEMA + "time");
			}
		}
	}

	/*
	public abstract class LiteralFilter : Resource {
		public LiteralFilter() : base(null) { }
		
		public override string Uri { get { return null; } }
		
		public abstract bool Matches(Literal literal);
	}
	
	public interface SQLLiteralFilter {
		string GetSQLFunction();
	}
	
	public class LiteralNumericComparison : LiteralFilter, SQLLiteralFilter {
		double value;
		Op comparison;
		
		public LiteralNumericComparison(double value, Op comparison) {
			this.value = value; this.comparison = comparison;
		}
		
		public enum Op {
			Equal,
			NotEqual,
			GreaterThan,
			GreaterThanOrEqual,
			LessThan,
			LessThanOrEqual,
		}
		
		public override bool Matches(Literal literal) {
			double v;
			if (!double.TryParse(literal.Value, System.Globalization.NumberStyles.Any, null, out v)) return false;
			
			switch (comparison) {
				case Op.Equal: return v == value;
				case Op.NotEqual: return v != value;
				case Op.GreaterThan: return v > value;
				case Op.GreaterThanOrEqual: return v >= value;
				case Op.LessThan: return v < value;
				case Op.LessThanOrEqual: return v <= value;
				default: return false;
			}
		}
		
		public string GetSQLFunction() {
			switch (comparison) {
				case Op.Equal: return "literal = " + value;
				case Op.NotEqual: return "literal != " + value;
				case Op.GreaterThan: return "literal > " + value;
				case Op.GreaterThanOrEqual: return "literal >= " + value;
				case Op.LessThan: return "literal < " + value;
				case Op.LessThanOrEqual: return "literal <= " + value;
				default: return null;
			}
		}
	}
	
	public class LiteralStringComparison : LiteralFilter, SQLLiteralFilter {
		string value;
		Op comparison;
		
		public LiteralStringComparison(string value, Op comparison) {
			this.value = value; this.comparison = comparison;
		}
		
		public enum Op {
			Equal,
			NotEqual,
			GreaterThan,
			GreaterThanOrEqual,
			LessThan,
			LessThanOrEqual,
		}
		
		public override bool Matches(Literal literal) {
			string v = literal.Value;
			
			switch (comparison) {
				case Op.Equal: return v == value;
				case Op.NotEqual: return v != value;
				case Op.GreaterThan: return v.CompareTo(value) > 0;
				case Op.GreaterThanOrEqual: return v.CompareTo(value) >= 0;
				case Op.LessThan: return v.CompareTo(value) < 0;
				case Op.LessThanOrEqual: return v.CompareTo(value) <= 0;
				default: return false;
			}
		}
		
		public string GetSQLFunction() {
			switch (comparison) {
				case Op.Equal: return "literal = " + value;
				case Op.NotEqual: return "literal != " + value;
				case Op.GreaterThan: return "literal > " + value;
				case Op.GreaterThanOrEqual: return "literal >= " + value;
				case Op.LessThan: return "literal < " + value;
				case Op.LessThanOrEqual: return "literal <= " + value;
				default: return null;
			}
		}
	}
	*/
}

namespace SemWeb.Util.Bind {
	public class Any {
		Entity ent;
		Store model;
				
		public Any(Entity entity, Store model) {
			this.ent = entity;
			this.model = model;
		}
		
		public Entity Entity { get { return ent; } }
		public Store Model { get { return model; } }
		
		public string Uri { get { return ent.Uri; } }
		
		private Resource toRes(object value) {
			if (value == null) return null;
			if (value is Resource) return (Resource)value; // shouldn't happen
			if (value is string) return new Literal((string)value);
			if (value is Any) return ((Any)value).ent;
			throw new ArgumentException("value is not of a recognized type");
		}
		
		protected void AddValue(Entity predicate, object value, bool forward) {
			if (value == null) throw new ArgumentNullException("value");
			Resource v = toRes(value);
			if (!forward && !(v is Entity)) throw new ArgumentException("Cannot set this property to a literal value.");
			Statement add = new Statement(ent, predicate, v);
			if (!forward) add = add.Invert();
			model.Add(add);
		}
		protected void RemoveValue(Entity predicate, object value, bool forward) {
			if (value == null) throw new ArgumentNullException("value");
			Resource v = toRes(value);
			if (!forward && !(v is Entity)) throw new ArgumentException("Cannot set this property to a literal value.");
			Statement rem = new Statement(ent, predicate, v);
			if (!forward) rem = rem.Invert();
			model.Remove(rem);
		}
		
		protected void SetFuncProperty(Entity predicate, object value, bool forward) {
			Resource v = toRes(value);
			Statement search = new Statement(ent, predicate, null);
			Statement replace = new Statement(ent, predicate, v);
			if (!forward) {
				if (v != null && !(v is Entity)) throw new ArgumentException("Cannot set this property to a literal value.");
				search = search.Invert();
				replace = replace.Invert();
			}
			
			if (v != null) {
				foreach (Statement s in model.Select(search)) {
					model.Replace(s, replace);
					return;
				}
				model.Add(replace);
			} else {
				model.Remove(search);
			}
		}
		
		protected void SetNonFuncProperty(Entity predicate, object[] values, bool forward) {
			Statement search = new Statement(ent, predicate, null);
			if (!forward)
				search = search.Invert();
			
			model.Remove(search);
			if (values != null) {
				foreach (object value in values) {
					Resource v = toRes(value);
					if (v == null) throw new ArgumentNullException("element of values array");
					if (!forward && !(v is Entity)) throw new ArgumentException("Cannot set this property to a literal value.");
					Statement add = new Statement(ent, predicate, v);
					if (!forward) add = add.Invert();
					model.Add(add);
				}
			}
		}
	}
}
