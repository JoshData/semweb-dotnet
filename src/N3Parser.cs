using System;
using System.Collections;
using System.IO;
using System.Text;

using SemWeb;

namespace SemWeb.IO {

	public class N3Parser : RdfParser {
		Resource PrefixResource = new Literal("@prefix", null);
		
		TextReader sourcestream;
		NamespaceManager namespaces = new NamespaceManager();
		
		public N3Parser(TextReader source) {
			this.sourcestream = source;
		}
		
		public N3Parser(string sourcefile) {
			this.sourcestream = GetReader(sourcefile);
			BaseUri = sourcefile + "#";
		}

		private class MyReader {
			TextReader r;
			public MyReader(TextReader reader) { r = reader; }
			
			public int Line = 1;
			public int Col = 0;
			
			int[] peeked = new int[2];
			int peekCount = 0;
			
			public int Peek() {
				if (peekCount == 0) {
					peeked[0] = r.Read();
					peekCount = 1;
				}
				return peeked[0];
			}
			
			public int Peek2() {
				Peek();
				if (peekCount == 1) {
					peeked[1] = r.Read();
					peekCount = 2;
				}
				return peeked[1];
			}

			public int Read() {
				int c;
				
				if (peekCount > 0) {
					c = peeked[0];
					peeked[0] = peeked[1];
					peekCount--;
				} else {
					c = r.Read();
				}
				
				if (c == '\n') { Line++; Col = 0; }
				else { Col++; }
				
				return c;
			}
		}
		
		private struct Location {
			public readonly int Line, Col;
			public Location(int line, int col) { Line = line; Col = col; }
		}
		
		private struct ParseContext {
			public MyReader source;
			public Store store;
			public NamespaceManager namespaces;
			public Hashtable anonymous;
			public Entity meta;
			
			public Location Location { get { return new Location(source.Line, source.Col); } }
		}
		
		public override void Parse(Store store) {
			ParseContext context = new ParseContext();
			context.source = new MyReader(sourcestream);
			context.store = store;
			context.namespaces = namespaces;
			context.anonymous = new Hashtable();
			context.meta = Meta;
			
			while (ReadStatement(context)) { }
		}
		
		private bool ReadStatement(ParseContext context) {
			Location loc = context.Location;
			
			bool reverse;
			Resource subject = ReadResource(context, out reverse);
			if (subject == null) return false;
			if (reverse) OnError("is...of not allowed on a subject", loc);
			
			if ((object)subject == (object)PrefixResource) {
				loc = context.Location;
				string qname = ReadToken(context.source);
				if (!qname.EndsWith(":")) OnError("When using @prefix, the prefix identifier must end with a colon", loc);
				
				loc = context.Location;
				Resource uri = ReadResource(context, out reverse);
				if (uri == null) OnError("Expecting a URI", loc);
				if (reverse) OnError("is...of not allowed here", loc);
				namespaces.AddNamespace(uri.Uri, qname.Substring(0, qname.Length-1));
				
				loc = context.Location;
				char punc = ReadPunc(context.source);
				if (punc != '.')
					OnError("Expected a period but found '" + punc + "'", loc);
				return true;
			}
			
			char period = ReadPredicates(subject, context);
			loc = context.Location;
			if (period != '.' && period != '}')
				OnError("Expected a period but found '" + period + "'", loc);
			if (period == '}') return false;
			return true;
		}
		
		private char ReadPredicates(Resource subject, ParseContext context) {			
			char punctuation = ';';
			while (punctuation == ';')
				punctuation = ReadPredicate(subject, context);
			return punctuation;
		}
		
		private char ReadPredicate(Resource subject, ParseContext context) {
			bool reverse;
			Location loc = context.Location;
			Resource predicate = ReadResource(context, out reverse);
			if (predicate == null) OnError("Expecting a predicate", loc);
			if (predicate is Literal) OnError("Predicates cannot be literals", loc);
			
			char punctuation = ',';
			while (punctuation == ',') {
				ReadObject(subject, (Entity)predicate, context, reverse);
				loc = context.Location;
				punctuation = ReadPunc(context.source);
			}
			if (punctuation != '.' && punctuation != ';' && punctuation != ']' && punctuation != '}')
				OnError("Expecting a period, semicolon, comma, or close-bracket but found '" + punctuation + "'", loc);
			
			return punctuation;
		}
		
		private void ReadObject(Resource subject, Entity predicate, ParseContext context, bool reverse) {
			bool reverse2;
			Location loc = context.Location;
			Resource value = ReadResource(context, out reverse2);
			if (value == null) OnError("Expecting a resource or literal object", loc);
			if (reverse2) OnError("is...of not allowed on objects", loc);
			
			loc = context.Location;
			if (!reverse) {
				if (subject is Literal) OnError("Subjects of statements cannot be literals", loc);			
				context.store.Add(new Statement((Entity)subject, predicate, value, context.meta));
			} else {
				if (value is Literal) OnError("A literal cannot be the object of a reverse-predicate statement", loc);
				context.store.Add(new Statement((Entity)value, predicate, subject, context.meta));
			}
		}
		
		private void ReadWhitespace(MyReader source) {
			while (true) {
				while (char.IsWhiteSpace((char)source.Peek()))
					source.Read();
				
				if (source.Peek() == '#') {
					while (true) {
						int c = source.Read();
						if (c == -1 || c == 10 || c == 13) break;
					}
					continue;
				}
				
				break;
			}
		}
		
		private char ReadPunc(MyReader source) {
			ReadWhitespace(source);
			int c = source.Read();
			if (c == -1)
				OnError("End of file expecting punctuation", new Location(source.Line, source.Col));
			return (char)c;
		}
		
		private string ReadToken(MyReader source) {
			ReadWhitespace(source);
			
			Location loc = new Location(source.Line, source.Col);
			
			int firstchar = source.Read();
			if (firstchar == -1)
				return "";
			
			StringBuilder b = new StringBuilder();
			b.Append((char)firstchar);

			if (firstchar == '<') {
				// This is a URI or the <= verb
				while (true) {
					int c = source.Read();
					if (c == -1) OnError("Unexpected end of stream within a token beginning with <", loc);
					b.Append((char)c);
					if (b.Length == 2 && c == '=')
						break; // the <= verb
					if (c == '>') // end of the URI
						break;
				}
			
			} else if (firstchar == '"') {
				// A string: ("""[^"\\]*(?:(?:\\.|"(?!""))[^"\\]*)*""")|("[^"\\]*(?:\\.[^"\\]*)*")
				// What kind of crazy regex is this??
				bool escaped = false;
				bool tripplequoted = false;
				while (true) {
					int c = source.Read();
					if (c == -1) OnError("Unexpected end of stream within a string", loc);
					
					if (b.Length == 1 && c == (int)'"' && source.Peek() == (int)'"') {
						tripplequoted = true;
						source.Read();
						continue;
					}
					
					if (!escaped && c == '\\')
						escaped = true;
					else if (escaped) {
						if (c == 'n') b.Append('\n');
						else if (c == 'r') b.Append('\r');
						else if (c == 't') b.Append('\t');
						else if (c == '\\') b.Append('\\');		
						else if (c == '"') b.Append('"');
						else if (c == '\'') b.Append('\'');
						else if (c == 'a') b.Append('\a');
						else if (c == 'b') b.Append('\b');
						else if (c == 'f') b.Append('\f');
						else if (c == 'v') b.Append('\v');
						else if (c == '\n') { }
						else if (c == '\r') { }
						else if (c == 'u' || c == 'U') {
							StringBuilder num = new StringBuilder();
							if (c == 'u')  {
								num.Append((char)source.Read()); // four hex digits
								num.Append((char)source.Read());
								num.Append((char)source.Read());
								num.Append((char)source.Read());
							} else {
								source.Read(); // two zeros
								source.Read();
								num.Append((char)source.Read()); // six hex digits
								num.Append((char)source.Read());
								num.Append((char)source.Read());
								num.Append((char)source.Read());
								num.Append((char)source.Read());
								num.Append((char)source.Read());
							}
							
							int unicode = int.Parse(num.ToString(), System.Globalization.NumberStyles.AllowHexSpecifier);
							b.Append((char)unicode); // is this correct?
							
						} else if (char.IsDigit((char)c) || c == 'x')
							OnError("Octal and hex byte-value escapes are deprecated and not supported", loc);
						else
							OnError("Unrecognized escape character: " + (char)c, loc);
						escaped = false;
					} else {
						b.Append((char)c);
						if (c == '"')
							break;
					}
				}
				
				if (tripplequoted) { // read the extra end quotes
					source.Read();
					source.Read();
				}

				// Strings can be suffixed with @langcode or ^^symbol (but not both?).
				if (b[0] == '"' && source.Peek() == '@') {
					b.Append((char)source.Read());
					while (char.IsLetterOrDigit((char)source.Peek()) || source.Peek() == (int)'-')
						b.Append((char)source.Read());
				} else if (b[0] == '"' && source.Peek() == '^' && source.Peek2() == '^') {
					b.Append((char)source.Read());
					b.Append((char)source.Read());
					b.Append(ReadToken(source)); 
				}

			} else if (char.IsLetter((char)firstchar) || firstchar == '?' || firstchar == '@' || firstchar == ':' || firstchar == '_') {
				// Something starting with @
				// A QName: ([a-zA-Z_][a-zA-Z0-9_]*)?:)?([a-zA-Z_][a-zA-Z0-9_]*)?
				// A variable: \?[a-zA-Z_][a-zA-Z0-9_]*
				while (true) {
					int c = source.Peek();
					if (c == -1 || (!char.IsLetterOrDigit((char)c) && c != '-' && c != '_' && c != ':')) break;					
					b.Append((char)source.Read());
				}
			
			} else if (char.IsDigit((char)firstchar) || firstchar == '+' || firstchar == '-') {
				while (true) {
					int ci = source.Peek();
					if (ci == -1) break;
					
					char c = (char)ci;
					if (char.IsWhiteSpace(c)) break;
					
					b.Append((char)source.Read());
				}
				
			} else if (firstchar == '=') {
				if (source.Peek() == (int)'>')
					b.Append((char)source.Read());
			
			} else if (firstchar == '[') {
				// The start of an anonymous node.

			} else if (firstchar == '{') {
				return "{";

			} else if (firstchar == '(') {
				return "(";
			} else if (firstchar == ')') {
				return ")";

			} else {
				while (true) {
					int c = source.Read();
					if (c == -1) break;
					if (char.IsWhiteSpace((char)c)) break;
					b.Append((char)c);
				}
				OnError("Invalid token: " + b.ToString(), loc);
			}
			
			return b.ToString();
		}
		
		private Resource ReadResource(ParseContext context, out bool reverse) {
			Location loc = context.Location;
			
			Resource res = ReadResource2(context, out reverse);
			
			ReadWhitespace(context.source);
			while (context.source.Peek() == '!' || context.source.Peek() == '^' || (context.source.Peek() == '.' && context.source.Peek2() != -1 && char.IsLetter((char)context.source.Peek2())) ) {
				int pathType = context.source.Read();
				
				bool reverse2;
				loc = context.Location;
				Resource path = ReadResource2(context, out reverse2);
				if (reverse || reverse2) OnError("is...of is not allowed in path expressions", loc);
				if (!(path is Entity)) OnError("A path expression cannot be a literal", loc);
				
				Entity anon = context.store.CreateAnonymousResource();
				
				Statement s;
				if (pathType == '!' || pathType == '.') {
					if (!(res is Entity)) OnError("A path expression cannot contain a literal: " + res, loc);
					s = new Statement((Entity)res, (Entity)path, anon, context.meta);
				} else {
					s = new Statement(anon, (Entity)path, res, context.meta);
				}
				
				context.store.Add( s );
				
				res = anon;

				ReadWhitespace(context.source);
			}
				
			return res;
		}			

		private Resource ReadResource2(ParseContext context, out bool reverse) {
			reverse = false;
			
			Location loc = context.Location;
			
			string str = ReadToken(context.source);
			if (str == "")
				return null;
			
			// @ Keywords

			if (str == "@prefix")
				return PrefixResource;
			
			if (str.StartsWith("@"))
				OnError("The " + str + " directive is not supported", loc);

			// Standard Keywords
			// TODO: Turn these off with @keywords
			
			if (str == "a")
				return context.store.GetResource( "http://www.w3.org/1999/02/22-rdf-syntax-ns#type" );
			if (str == "=") // ?
				return context.store.GetResource( "http://www.w3.org/2002/07/owl#sameAs" );
			if (str == "=>") // ?
				return context.store.GetResource( "http://www.w3.org/2000/10/swap/log#implies" );
			if (str == "<=") // ?
				OnError("The <= predicate is not supported (because I don't know what it translates to)", loc);

			if (str == "has") // ignore this token
				str = ReadToken(context.source);
			
			if (str == "is") {
				// Reverse predicate
				str = ReadToken(context.source);
				reverse = true;
				string of = ReadToken(context.source);
				if (of != "of") OnError("Expecting token 'of' but found '" + of + "'", loc);
			}
			
			// URI
			
			if (str.StartsWith("<") && str.EndsWith(">")) {
				string uri = str.Substring(1, str.Length-2);
				if (uri.StartsWith("#")) {
					if (BaseUri == null)
						OnError("The document contains a relative URI but no BaseUri was specified", loc);
					uri = BaseUri + uri;
				}
				// relativize it
				return context.store.GetResource(uri);
			}
			
			// STRING LITERAL
			
			if (str.StartsWith("\"")) {
				return Literal.Parse(str, context.store.Model, context.namespaces);
			}
			
			// NUMERIC LITERAL
			
			// In Turtle, numbers are restricted to [0-9]+, and are datatyped xsd:integer.
			double numval;
			if (double.TryParse(str, System.Globalization.NumberStyles.Any, null, out numval))
				return new Literal(numval.ToString(), context.store.Model);
			
			// VARIABLE
			
			if (str[0] == '?') {
				// TODO: Preserve the mapping.
				Entity variable = context.store.CreateAnonymousResource();
				return variable;
			}
			
			// QNAME

			int colon = str.IndexOf(":");
			if (colon != -1) {
				string prefix = str.Substring(0, colon);
				if (prefix == "_") {
					Resource ret = (Resource)context.anonymous[str];
					if (ret == null) {
						ret = context.store.CreateAnonymousResource();
						context.anonymous[str] = ret;
					}
					return ret;
				} else if (prefix == "") {
					if (BaseUri == null)
						OnError("The document contains a relative URI but no BaseUri was specified", loc);
					return context.store.GetResource( BaseUri + str.Substring(colon+1) );
				} else {
					string ns = context.namespaces.GetNamespace(prefix);
					if (ns == null)
						OnError("Prefix is undefined: " + str, loc);
					return context.store.GetResource( ns + str.Substring(colon+1) );
				}
			}
			
			// ANONYMOUS
			
			if (str == "[") {
				Entity ret = context.store.CreateAnonymousResource();
				char bracket = ReadPredicates(ret, context);
				if (bracket != ']')
					OnError("Expected a close bracket but found '" + bracket + "'", loc);
				return ret;
			}
			
			// LIST
			
			if (str == "(") {
				// A list
				Entity ent = null;
				while (true) {
					bool rev2;
					Resource res = ReadResource(context, out rev2);
					if (res == null)
						break;
					
					if (ent == null) {
						ent = context.store.CreateAnonymousResource();
					} else {
						Entity sub = context.store.CreateAnonymousResource();
						context.store.Add(new Statement(ent, "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest", sub, context.meta));
						ent = sub;
					}
					
					context.store.Add(new Statement(ent, "http://www.w3.org/1999/02/22-rdf-syntax-ns#first", res, context.meta));					
				}
				if (ent == null) // No list items.
					ent = context.store.GetResource("http://www.w3.org/1999/02/22-rdf-syntax-ns#nil"); // according to Turtle spec
				else
					context.store.Add(new Statement(ent, "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest", (Entity)"http://www.w3.org/1999/02/22-rdf-syntax-ns#nil", context.meta));
				return ent;
			}
			
			if (str == ")")
				return null; // Should I use a more precise end-of-list return value?
			
			// REIFICATION
			
			if (str == "{") {
				// Embedded resource
				ParseContext newcontext = context;
				newcontext.meta = context.store.CreateAnonymousResource();
				while (ReadStatement(newcontext)) { }
				ReadWhitespace(context.source);
				if (context.source.Peek() == '}') context.source.Read();
				return newcontext.meta;
			}
			
			// NOTHING MATCHED
			
			OnError("Invalid token: " + str, loc);
			return null;
		}
		
		private void OnError(string message, Location position) {
			throw new ParserException(message + ", line " + position.Line + " col " + position.Col);
		}
	
	}
}
