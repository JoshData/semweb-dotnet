using System;
using System.Collections;

namespace SemWeb {
	public abstract class Resource {
		KnowledgeModel model;
		
		protected Resource() {
			this.model = null;
		}
		
		protected Resource(KnowledgeModel model) {
			this.model = model;
		}
		
		public abstract string Uri { get; }
		
		public KnowledgeModel Model { get { return model; } }
		
		//public static explicit operator Resource(string uri) { return new Entity(uri); }
		
		public override string ToString() {
			if (Uri != null) return Uri;
			return "<anonymous>";
		}
		
		public Resource[] this[Entity relation] {
			get {
				return this[relation, true];
			}
		}
		public Resource[] this[string relation] {
			get {
				return this[relation, true];
			}
		}
		
		public Resource[] this[string relation, bool forward] {
			get {
				return this[new Entity(relation, Model), forward];
			}
		}
		
		public Resource[] this[Entity relation, bool forward] {
			get {
				if (Model == null)
					throw new InvalidOperationException("This resource is not associated with a model.");
				
				ArrayList entities = new ArrayList();
				Hashtable seen = new Hashtable();
				
				Statement template;
				if (forward) {
					if (!(this is Entity))
						throw new ArgumentException("Literals cannot be the subjects of statements.");
					template = new Statement((Entity)this, relation, null);
				} else
					template = new Statement(null, relation, this);
				
				MemoryStore result = model.Select(template);
				foreach (Statement s in result.Statements) {
					Resource obj;
					if (forward)
						obj = s.Object;
					else
						obj = s.Subject;
					
					if (seen.ContainsKey(obj)) continue;
					seen[obj] = seen;
					
					entities.Add(obj);
				}
				
				return (Resource[])entities.ToArray(typeof(Resource));
			}
		}		
	}
	
	public class Entity : Resource {
		// Don't use this field directly below.  Use only the
		// Uri property to allow derived classes to lazily load
		// the Uri on the first request.
		protected string uri;
		
		public Entity(string uri) : this(uri, null) { }
		
		public Entity(string uri, KnowledgeModel model) : base(model) { this.uri = uri; }
		
		public override string Uri { get { return uri; } }
		
		public static implicit operator Entity(string uri) { return new Entity(uri); }
		
		public override int GetHashCode() {
			if (Uri == null) return 0;
			return Uri.GetHashCode();
		}
			
		public override bool Equals(object other) {
			if (!(other is Resource)) return false;
			if ((object)this == other) return true;
			return ((Resource)other).Uri != null && ((Resource)other).Uri == Uri;
		}

		private bool Equals(string other) {
			if (Uri == null || other == null) return false;
			return other == Uri;
		}

		public static bool operator ==(Entity a, Entity b) {
			if (a == null && b == null) return true;
			if (a == null || b == null) return false;
			return a.Equals(b);
		}
		public static bool operator !=(Entity a, Entity b) {
			return !(a == b);
		}
	}
	
	public class AnonymousNode : Entity {
		public AnonymousNode() : this(null) { }
		public AnonymousNode(KnowledgeModel model) : base(null, model) { }
		
		public override string ToString() { return "_"; }
	}

	public class Literal : Resource { 
		private string value, lang, type;
		
		public Literal(string value) : this(value, null) { }
		
		public Literal(string value, KnowledgeModel model) : this(value, null, null, model) {
		}
		
		public Literal(string value, string language, string dataType) : this(value, language, dataType, null) {
		}
		
		public Literal(string value, string language, string dataType, KnowledgeModel model) : base(model) {
		  this.value = value;
		  this.lang = language;
		  this.type = dataType;
		  if (language != null && dataType != null)
			  throw new ArgumentException("A language and a data type cannot both be specified.");
		}
		
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
			ret.Append(Value);
			ret.Append('"');
			
			if (Language != null) {
				ret.Append('@');
				ret.Append(Language);
			}
			
			if (DataType != null) {
				ret.Append("^^<");
				ret.Append(DataType);
				ret.Append(">");
			}
			return ret.ToString();
		}
		
		public static Literal Parse(string literal, KnowledgeModel model) {
			if (!literal.StartsWith("\"")) throw new FormatException("Literal value must start with a quote.");
			int quote = literal.LastIndexOf('"');
			if (quote <= 0) throw new FormatException("Literal value must have an end quote (" + literal + ")");
			string value = literal.Substring(1, quote-1);
			literal = literal.Substring(quote+1);
			
			string lang = null;
			string datatype = null;
			
			if (literal.StartsWith("@")) {
				int type = literal.IndexOf("^^");
				if (type == -1) lang = literal.Substring(1);
				else {
					lang = literal.Substring(1, type);
					literal = literal.Substring(type);
				}
			}
			
			if (literal.StartsWith("^^<") && literal.EndsWith(">")) {
				datatype = literal.Substring(3, literal.Length-4);
			}
			
			return new Literal(value, lang, datatype, model);
		}
	}

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
}
