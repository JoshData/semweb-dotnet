using System;
using System.IO;

using SemWeb;

namespace SemWeb.IO {
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
		
		public override void PushMetaScope(string uri) {
			Close();
			WriteThing(URI(uri));
			writer.Write(" = { ");
			lastSubject = null;
			lastPredicate = null;
		}
		
		public override void PopMetaScope() {
			Close();
			WriteThing(" }\n");
			lastSubject = null;
			lastPredicate = null;
		}
		
		public override void WriteStatement(string subj, string pred, string obj) {
			WriteStatement2(URI(subj), URI(pred), URI(obj));
		}
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			WriteStatement2(URI(subj), URI(pred), literal.ToString());
		}
		
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
