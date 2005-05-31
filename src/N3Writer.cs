using System;
using System.IO;

using SemWeb;

namespace SemWeb {
	public class N3Writer : RdfWriter {
		TextWriter writer;
		NamespaceManager ns;
		bool hasWritten = false;
		bool closed = false;
		
		string lastSubject = null, lastPredicate = null;
		
		long anonCounter = 0;
		
		bool ntriples = false;
		
		public N3Writer(string file) : this(file, null) { }
		
		public N3Writer(string file, NamespaceManager ns) : this(GetWriter(file), ns) { }

		public N3Writer(TextWriter writer) : this(writer, null) { }
		
		public N3Writer(TextWriter writer, NamespaceManager ns) {
			this.writer = writer; this.ns = ns;
		}
		
		public override NamespaceManager Namespaces { get { return ns; } }
		
		public bool NTriples { get { return ntriples; } set { ntriples = value; } }
		
		public override void WriteStatement(string subj, string pred, string obj) {
			WriteStatement2(URI(subj), URI(pred), URI(obj));
		}
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			WriteStatement2(URI(subj), URI(pred), literal.ToString());
		}
		
		public override string CreateAnonymousEntity() {
			return "_:anon" + (anonCounter++);
		}
			
		public override void Close() {
			base.Close();
			if (closed) return;
			if (hasWritten)
				writer.WriteLine(".");
			closed = true;
			hasWritten = false;
		}

		
		private string URI(string uri) {
			if (uri.StartsWith("_:anon")) return uri;
			if (BaseUri != null && uri.StartsWith(BaseUri)) {
				int len = BaseUri.Length;
				bool ok = true;
				for (int i = len; i < uri.Length; i++) {
					if (!char.IsLetterOrDigit(uri[i])) { ok = false; break; }
				}
				if (ok)
					return ":" + uri.Substring(len);
			}
			if (NTriples || ns == null) return "<" + uri + ">";
			return ns.Normalize(uri);
		}
		
		private void WriteLiteral(string expr) {
			writer.Write(expr);
			writer.Write(" ");
		}
		
		private void WriteStatement2(string subj, string pred, string obj) {
			closed = false;
			
			// Write the prefix directives at the beginning.
			if (!hasWritten && ns != null && !NTriples) {
				foreach (string prefix in ns.GetPrefixes()) {
					writer.Write("@prefix ");
					writer.Write(prefix);
					writer.Write(": <");
					writer.Write(ns.GetNamespace(prefix));
					writer.Write("> .\n");
				}
			}

			// Repeated subject.
			if (lastSubject != null && lastSubject == subj && !NTriples) {
				// Repeated predicate too.
				if (lastPredicate != null && lastPredicate == pred) {
					writer.Write(",\n\t\t");
					WriteThing(obj);
					
				// Just a repeated subject.
				} else {
					writer.Write(";\n\t");
					WriteThing(pred);
					WriteThing(obj);
					lastPredicate = pred;
				}
			
			// The subject became the object.  Abbreviate with
			// is...of notation.
			} else if (lastSubject != null && lastSubject == obj && !NTriples) {
				writer.Write(";\n\tis ");
				WriteThing(pred);
				writer.Write("of ");
				WriteThing(subj);
				lastPredicate = null;
			
			// Start a new statement.
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
