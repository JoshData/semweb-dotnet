using System;
using System.Collections;
using System.IO;

namespace SemWeb {
	public abstract class RdfWriter : IDisposable, StatementSink {
		public abstract NamespaceManager Namespaces { get; }
		
		Hashtable anonymousNodes = new Hashtable();
		
		protected static TextWriter GetWriter(string dest) {
			if (dest == "-")
				return Console.Out;
			return new StreamWriter(dest);
		}
		
		public bool Add(Statement statement) {
			if (statement.AnyNull)
				throw new ArgumentNullException();
			
			string s = getUri(statement.Subject);
			string p = getUri(statement.Predicate);
			
			if (statement.Object is Literal) {
				Literal lit = (Literal)statement.Object;
				WriteStatementLiteral(s, p, lit.Value, lit.DataType, lit.Language);
			} else {
				string o = getUri((Entity)statement.Object);
				WriteStatement(s, p, o);
			}
			
			return true;
		}
		
		private string getUri(Entity e) {
			if (e.Uri != null) return e.Uri;
			if (anonymousNodes.ContainsKey(e)) return (string)anonymousNodes[e];
			string uri = CreateAnonymousNode();
			anonymousNodes[e] = uri;
			return uri;
		}
		
		public abstract void WriteStatement(string subj, string pred, string obj);
		
		public abstract void WriteStatementLiteral(string subj, string pred, string literal, string literalType, string literalLanguage);
		
		public abstract string CreateAnonymousNode();
		
		public abstract void Close();
		
		public void WriteStatementLiteral(string subj, string pred, string literal) {
			WriteStatementLiteral(subj, pred, literal, null);
		}
		
		public void WriteStatementLiteral(string subj, string pred, string literal, string literalType) {
			WriteStatementLiteral(subj, pred, literal, literalType, null);
		}
		
		public virtual void Dispose() {
		}
	}
}