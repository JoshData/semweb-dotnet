using System;
using System.Collections;

namespace SemWeb {
	public class MemoryStore : Store, IEnumerable {
		Hashtable uriToResource = new Hashtable();
		ArrayList statements = new ArrayList();
		
		Hashtable statementsAboutSubject = null;
		Hashtable statementsAboutObject = null;
		
		public MemoryStore(KnowledgeModel model) : base(model) {
		}
		
		public MemoryStore(RdfParser parser, KnowledgeModel model) : this(model) {
			Import(parser);
		}

		public ArrayList Statements { get { return statements; } }
		  
		public override int StatementCount { get { return statements.Count; } }
		
		IEnumerator IEnumerable.GetEnumerator() {
			return statements.GetEnumerator();
		}
		
		public override void Clear() {
			uriToResource.Clear();
			statements.Clear();
			statementsAboutSubject = null;
			statementsAboutObject = null;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			statements.Add(statement);
			statementsAboutSubject = null;
			statementsAboutObject = null;
		}
		
		public override bool Contains(Statement statement) {
			if (statement.AnyNull)
				throw new ArgumentNullException();
			StatementCounterSink sink = new StatementCounterSink();
			Select(statement, sink);
			return sink.StatementCount > 0;
		}
		
		public override void Remove(Statement statement) {
			statements.Remove(statement);
			statementsAboutSubject = null;
			statementsAboutObject = null;
		}
		
		public override Entity GetResource(string uri, bool create) {
			Entity ret = (Entity)uriToResource[uri];
			if (ret == null && create) {
				ret = new Entity(uri, Model);
				uriToResource[uri] = ret;
			}
			return ret;
		}
		
		public override Entity CreateAnonymousResource() {
			return new AnonymousNode(Model);
		}
		
		public override void Select(Statement template, StatementSink result) {
			if (statementsAboutSubject == null) {
				statementsAboutSubject = new Hashtable();
				foreach (Statement statement in statements) {
					ArrayList a = (ArrayList)statementsAboutSubject[statement.Subject];
					if (a == null) a = new ArrayList();
					a.Add(statement);
					statementsAboutSubject[statement.Subject] = a;
				}

				statementsAboutObject = new Hashtable();
				foreach (Statement statement in statements) {
					ArrayList a = (ArrayList)statementsAboutObject[statement.Object];
					if (a == null) a = new ArrayList();
					a.Add(statement);
					statementsAboutObject[statement.Object] = a;
				}
			}
			
			IList source = statements;
			if (template.Subject != null) source = (ArrayList)statementsAboutSubject[template.Subject];
			else if (template.Object != null) source = (ArrayList)statementsAboutObject[template.Object];
			
			if (source == null) return;
			
			foreach (Statement statement in source) {
				if (template.Subject != null && !template.Subject.Equals(statement.Subject)) continue;
				if (template.Predicate != null && !template.Predicate.Equals(statement.Predicate)) continue;
				if (template.Object != null) {
					if (template.Object is LiteralFilter) {
						if (!(statement.Object is Literal)) continue;
						LiteralFilter filter = (LiteralFilter)template.Object;
						if (!filter.Matches((Literal)statement.Object)) continue;
					} else {
						if (!template.Object.Equals(statement.Object)) continue;
					}
				}
				
				if (!result.Add(statement)) return;
			}
		}

  }
}
