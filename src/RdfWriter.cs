using System;
using System.Collections;
using System.IO;

namespace SemWeb {
	public abstract class RdfWriter : IDisposable, StatementSink {
		public abstract NamespaceManager Namespaces { get; }
		
		Hashtable anonymousNodes = new Hashtable();
		
		internal static TextWriter GetWriter(string dest) {
			if (dest == "-")
				return Console.Out;
			return new StreamWriter(dest);
		}
		
		bool StatementSink.Add(Statement statement) {
			Add(statement);
			return true;
		}
		
		public void Add(Statement statement) {
			if (statement.AnyNull)
				throw new ArgumentNullException();
			
			string s = getUri(statement.Subject);
			string p = getUri(statement.Predicate);
			
			if (statement.Object is Literal) {
				Literal lit = (Literal)statement.Object;
				WriteStatement(s, p, lit);
			} else {
				string o = getUri((Entity)statement.Object);
				WriteStatement(s, p, o);
			}
		}
		
		private string getUri(Entity e) {
			if (e.Uri != null) return e.Uri;
			if (anonymousNodes.ContainsKey(e)) return (string)anonymousNodes[e];
			string uri = CreateAnonymousNode();
			anonymousNodes[e] = uri;
			return uri;
		}
		
		public virtual void PushMetaScope(string uri) { }
		
		public virtual void PopMetaScope() { }
		
		public abstract void WriteStatement(string subj, string pred, string obj);
		
		public abstract void WriteStatement(string subj, string pred, Literal literal);
		
		public abstract string CreateAnonymousNode();
		
		public abstract void Close();
		
		public virtual void Dispose() {
		}
	}
}
