using System;
using System.Collections;
using System.IO;
using System.Text;

namespace SemWeb {

	public class N3Parser : RdfParser {
		Resource PrefixResource = new Literal("@prefix", null);
		
		TextReader sourcestream;
		NamespaceManager namespaces = new NamespaceManager();
		
		public N3Parser(TextReader source) {
			this.sourcestream = source;
		}
		
		public N3Parser(string sourcefile) {
			this.sourcestream = GetReader(sourcefile);
		}

		private class MyReader {
			TextReader r;
			public MyReader(TextReader reader) { r = reader; }
			
			public long Line = 1;
			public long Col = 0;
			
			public int Peek() { return r.Peek(); }
			
			public int Read() {
				int c = r.Read();

				if ((char)c == '\n' && (char)r.Peek() == '\r')
					r.Read();
				if ((char)c == '\r' && (char)r.Peek() == '\n')
					r.Read();
				if ((char)c == '\n' || c == '\r') { Line++; Col = 0; }
				else { Col++; }
				
				return c;
			}
		}
		
		public override void Parse(Store store) {
			Hashtable anonymous = new Hashtable();
			while (ReadStatement(new MyReader(sourcestream), store, namespaces, anonymous)) { }
		}
		
		private bool ReadStatement(MyReader source, Store Store, NamespaceManager namespaces, Hashtable anonymous) {
			Resource subject = ReadResource(source, Store, namespaces, anonymous);
			if (subject == null) return false;
			
			if ((object)subject == (object)PrefixResource) {
				string qname = ReadToken(source);
				if (!qname.EndsWith(":")) OnError("When using @prefix, the prefix identifier must end with a colon", source);
				Resource uri = ReadResource(source, Store, namespaces, anonymous);
				if (uri == null)
					OnError("Expecting a URI", source);
				namespaces.AddNamespace(uri.Uri, qname.Substring(0, qname.Length-1));
				char punc = ReadPunc(source);
				if (punc != '.')
					OnError("Expected a period", source);
				return true;
			}
			
			if (subject is Literal)
				OnError("Subjects of statements cannot be literals", source);
			
			char period = ReadPredicates((Entity)subject, source, Store, namespaces, anonymous);
			if (period != '.')
				OnError("Expected a period", source);
			return true;
		}
		
		private char ReadPredicates(Entity subject, MyReader source, Store Store, NamespaceManager namespaces, Hashtable anonymous) {			
			char punctuation = ';';
			while (punctuation == ';')
				punctuation = ReadPredicate(subject, source, Store, namespaces, anonymous);
			return punctuation;
		}
		
		private char ReadPredicate(Entity subject, MyReader source, Store Store, NamespaceManager namespaces, Hashtable anonymous) {
			Resource predicate = ReadResource(source, Store, namespaces, anonymous);
			if (predicate == null) OnError("Expecting a predicate", source);
			if (predicate is Literal)
				OnError("Statement predicates cannot be literals", source);
			
			char punctuation = ',';
			while (punctuation == ',') {
				ReadObject(subject, (Entity)predicate, source, Store, namespaces, anonymous);
				punctuation = ReadPunc(source);
			}
			if (punctuation != '.' && punctuation != ';' && punctuation != ']')
				OnError("Expecting a period, semicolon, comma, or close-bracket", source);
			
			return punctuation;
		}
		
		private void ReadObject(Entity subject, Entity predicate, MyReader source, Store Store, NamespaceManager namespaces, Hashtable anonymous) {
			Resource value = ReadResource(source, Store, namespaces, anonymous);
			if (value == null) OnError("Expecting a resource or literal object", source);
			Store.Add(new Statement(subject, predicate, value, Meta));
		}
		
		private void ReadWhitespace(MyReader source) {
			while (true) {
				while (char.IsWhiteSpace((char)source.Peek()))
					source.Read();
				
				if ((char)source.Peek() == '#') {
					while (true) {
						int c = source.Read();
						if (c == 0 || c == 10 || c == 13) break;
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
				OnError("End of file expecting punctuation", source);
			return (char)c;
		}
		
		private string ReadToken(MyReader source) {
			ReadWhitespace(source);
			StringBuilder b = new StringBuilder();
			int c;
			bool quoted = false;
			bool escaped = false;
			while ((c = source.Read()) != -1) {
				if (b.Length == 0 && (c == '\"' || c == '<')) quoted = true;
				else if (quoted && !escaped && (c == '\"' || c == '>')) quoted = false;
				
				if (!escaped) {
					if (c != '\\') b.Append((char)c);
				} else {
					if (c == 'n') c = '\n';
					b.Append((char)c);
				}
				 
				if (escaped) escaped = false;
				else escaped = (c == '\\');
				
				int next = source.Peek();
				if (b.Length > 1 && !quoted && (next == '.' || next == ',' || next == ';' || next == '[' || char.IsWhiteSpace((char)next)))
					break;
			}
			//Console.WriteLine(b.ToString());
			return b.ToString();
		}
		
		private Resource ReadResource(MyReader source, Store store, NamespaceManager namespaces, Hashtable anonymous) {
			string str = ReadToken(source);
			if (str == "")
				return null;
			
			if (str == "is" || str == "has" || str == "of")
				str = ReadToken(source);
			
			if (str.StartsWith("<") && str.EndsWith(">")) {
				string uri = str.Substring(1, str.Length-2);
				// relativize it
				return store.GetResource(uri);
			}
			
			if (str.StartsWith("\"")) {
				return Literal.Parse(str, store.Model);
			}
			
			if (str == "[") {
				// Embedded resource
				Entity ret = store.CreateAnonymousResource();
				char bracket = ReadPredicates(ret, source, store, namespaces, anonymous);
				if (bracket != ']')
					OnError("Expected a close bracket", source);
				return ret;
			}
			
			int colon = str.IndexOf(":");
			if (colon != -1) {
				string prefix = str.Substring(0, colon);
				if (prefix == "_") {
					Resource ret = (Resource)anonymous[str];
					if (ret == null) {
						ret = store.CreateAnonymousResource();
						anonymous[str] = ret;
					}
					return ret;
				} else {
					string ns = namespaces.GetNamespace(prefix);
					if (ns == null)
						OnError("Prefix is undefined: " + str, source);
					return store.GetResource( ns + str.Substring(colon+1) );
				}
			}
			
			if (str == "@prefix")
				return PrefixResource;
			
			if (str == "a")
				return store.GetResource( "http://www.w3.org/1999/02/22-rdf-syntax-ns#type" );
			if (str == "=") // ?
				return store.GetResource( "http://www.w3.org/2002/07/owl#sameAs" );
			
			try {
				double numval = double.Parse(str);
				return new Literal(numval.ToString(), store.Model);
			} catch (Exception e) {
			}
			
			OnError("Invalid token: " + str, source);
			return null;
		}
		
		private void OnError(string message, MyReader position) {
			throw new ParserException(message + ", line " + position.Line + " col " + position.Col);
		}
	}
}
