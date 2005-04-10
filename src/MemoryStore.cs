using System;
using System.Collections;

using SemWeb;

namespace SemWeb.Stores {
	public class MemoryStore : Store, IEnumerable {
		ArrayList statements = new ArrayList();
		
		Hashtable statementsAboutSubject = new Hashtable();
		Hashtable statementsAboutObject = new Hashtable();
		
		public MemoryStore() {
		}
		
		public MemoryStore(RdfParser parser) {
			Import(parser);
		}

		public IList Statements { get { return ArrayList.ReadOnly(statements); } }
		  
		public override int StatementCount { get { return statements.Count; } }
		
		IEnumerator IEnumerable.GetEnumerator() {
			return statements.GetEnumerator();
		}
		
		public override void Clear() {
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

		public override Entity CreateAnonymousEntity() {
			return new Entity((string)null);
		}
		
		private void ShorterList(ref IList list1, IList list2) {
			if (list2.Count < list1.Count)
				list1 = list2;
		}
		
		public override void Select(Statement template, SelectPartialFilter partialFilter, StatementSink result) {
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

		public override void Select(Statement[] templates, SelectPartialFilter partialFilter, StatementSink result) {
			foreach (Statement t in templates)
				Select(t, result);
		}

		public override void Replace(Entity a, Entity b) {
			foreach (Statement statement in statements) {
				if (statement.Subject == a || statement.Predicate == a || statement.Object == a || statement.Meta == a) {
					Remove(statement);
					Add(new Statement(
						statement.Subject == a ? b : a,
						statement.Predicate == a ? b : a,
						statement.Object == a ? b : a,
						statement.Meta == a ? b : a
						));
				}
			}
		}
	}
}
