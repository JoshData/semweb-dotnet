using System;
using System.Collections;

using SemWeb;

namespace SemWeb.Stores {
	public class MemoryStore : Store, IEnumerable {
		Hashtable uriToResource = new Hashtable();
		
		ArrayList statements = new ArrayList();
		
		Hashtable statementsAboutSubject = new Hashtable();
		Hashtable statementsAboutObject = new Hashtable();
		
		public MemoryStore(KnowledgeModel model) : base(model) {
		}
		
		public MemoryStore(RdfParser parser, KnowledgeModel model) : this(model) {
			Import(parser);
		}

		public IList Statements { get { return ArrayList.ReadOnly(statements); } }
		  
		public override int StatementCount { get { return statements.Count; } }
		
		IEnumerator IEnumerable.GetEnumerator() {
			return statements.GetEnumerator();
		}
		
		public override void Clear() {
			uriToResource.Clear();
			statements.Clear();
			statementsAboutSubject.Clear();
			statementsAboutObject.Clear();
		}
		
		private ArrayList GetIndexArray(Hashtable from, Resource entity) {
			ArrayList ret = (ArrayList)from[entity];
			if (ret == null) {
				ret = new ArrayList();
				from[entity] = ret;
			}
			return ret;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			statements.Add(statement);
			GetIndexArray(statementsAboutSubject, statement.Subject).Add(statement);
			GetIndexArray(statementsAboutObject, statement.Object).Add(statement);
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
			GetIndexArray(statementsAboutSubject, statement.Subject).Remove(statement);
			GetIndexArray(statementsAboutObject, statement.Object).Remove(statement);
		}
		
		public override Entity[] GetAllEntities() {
			Hashtable h = new Hashtable();
			foreach (Statement s in Statements) {
				h[s.Subject] = h;
				h[s.Predicate] = h;
				if (s.Object is Entity) h[s.Object] = h;
				if (s.Meta != null) h[s.Meta] = h;
			}
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}
		
		public override Entity[] GetAllPredicates() {
			Hashtable h = new Hashtable();
			foreach (Statement s in Statements)
				h[s.Predicate] = h;
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
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
			return new Entity(Model);
		}
		
		private void ShorterList(ref IList list1, IList list2) {
			if (list2.Count < list1.Count)
				list1 = list2;
		}
		
		public override void Select(Statement template, StatementSink result) {
			IList source = statements;
			if (template.Subject != null) ShorterList(ref source, GetIndexArray(statementsAboutSubject, template.Subject));
			else if (template.Object != null) ShorterList(ref source, GetIndexArray(statementsAboutObject, template.Object));
			
			if (source == null) return;
			
			foreach (Statement statement in source) {
				if (template.Subject != null && !template.Subject.Equals(statement.Subject)) continue;
				if (template.Predicate != null && !template.Predicate.Equals(statement.Predicate)) continue;
				if (template.Object != null && !template.Object.Equals(statement.Object)) continue;
				if (!result.Add(statement)) return;
			}
		}

  }
}
