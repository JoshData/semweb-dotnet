using System;
using System.IO;

namespace SemWeb {
	public class N3Writer : RdfWriter {
		TextWriter writer;
		NamespaceManager ns;
		bool hasWritten = false;
		bool closed = false;
		
		string lastSubject = null, lastPredicate = null;
		
		long anonCounter = 0;
		
		public N3Writer(string file) : this(file, null) { }
		
		public N3Writer(string file, NamespaceManager ns) : this(GetWriter(file), ns) { }

		public N3Writer(TextWriter writer) : this(writer, null) { }
		
		public N3Writer(TextWriter writer, NamespaceManager ns) {
			if (ns == null)
				ns = new NamespaceManager();
			this.writer = writer; this.ns = ns;
		}
		
		public override NamespaceManager Namespaces { get { return ns; } }
		
		public override void WriteStatement(string subj, string pred, string obj) {
			WriteStatement2(URI(subj), URI(pred), URI(obj));
		}
		
		public override void WriteStatementLiteral(string subj, string pred, string literal, string literalType, string literalLanguage) {
			WriteStatement2(URI(subj), URI(pred), "\"" + Escape(literal) + "\"" + lang(literalLanguage) + type(literalType));
		}
		
		private string Escape(string str) {
			return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
		}
		
		private string lang(string lang) { if (lang == null) return null; return "@" + lang; }
		private string type(string type) { if (type == null) return null; return "^^" + URI(type); }
		
		public override string CreateAnonymousNode() {
			return "_:anon" + (anonCounter++);
		}
		
		public override void Dispose() {
			Close();
		}
		
		public override void Close() {
			if (closed) return;
			if (hasWritten)
				writer.WriteLine(".");
			closed = true;
			hasWritten = false;
		}

		
		private string URI(string uri) {
			if (uri.StartsWith("_:anon")) return uri;
			return ns.Normalize(uri);
		}
		
		private void WriteLiteral(string expr) {
			writer.Write(expr);
			writer.Write(" ");
		}
		
		private void WriteStatement2(string subj, string pred, string obj) {
			closed = false;

			if (lastSubject != null && lastSubject == subj) {
				if (lastPredicate != null && lastPredicate == pred) {
					writer.Write(",\n\t\t");
					WriteThing(obj);
				} else {
					writer.Write(";\n\t");
					WriteThing(pred);
					WriteThing(obj);
					lastPredicate = pred;
				}
			} else {
				if (hasWritten)
					writer.Write(".\n");
					
				WriteThing(subj);
				WriteThing(pred);
				WriteThing(obj);
				
				lastSubject = subj;
				lastPredicate = pred;
			}
			
			hasWritten = true;
		}
		
		private void WriteThing(string text) {
			writer.Write(text);
			writer.Write(" ");
		}
	}
}
