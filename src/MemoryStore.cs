using System;
using System.Collections;

#if DOTNET2
using System.Collections.Generic;
#endif

using SemWeb;
using SemWeb.Stores;
using SemWeb.Util;

namespace SemWeb {
	public class MemoryStore : Store, SupportsPersistableBNodes
#if !DOTNET2
, IEnumerable
#else
, IEnumerable<Statement>
#endif
	{
	
		StatementList statements;
		
		Hashtable statementsAboutSubject = new Hashtable();
		Hashtable statementsAboutObject = new Hashtable();
		
		bool isIndexed = false;
		internal bool allowIndexing = true;
		bool checkForDuplicates = false;
		bool distinct = true;
		
		string guid = null;
		Hashtable pbnodeToId = null;
		Hashtable pbnodeFromId = null;
		
		long stamp = 0;
		
		public MemoryStore(bool checkForDuplicates) {
			statements = new StatementList();
			this.checkForDuplicates = checkForDuplicates;
		}
		
		public MemoryStore() : this(false) {
		}

		public MemoryStore(StatementSource source) : this() {
			Import(source);
		}
		
		public MemoryStore(Statement[] statements) : this() {
			#if !DOTNET2
				this.statements = new StatementList(statements);
			#else
				this.statements.AddRange(statements);
			#endif
		}

		public Statement[] ToArray() {
			return statements.ToArray();
		}

		public override bool Distinct { get { return distinct; } }
		
		public override int StatementCount { get { return statements.Count; } }
		
		public Statement this[int index] {
			get {
				return statements[index];
			}
		}
		
		// This isn't strictly necessary since Store implements these
		// via a call to Select(), but it can't hurt to just go
		// directly to the underlying array.
		#if !DOTNET2
		IEnumerator IEnumerable.GetEnumerator() {
		#else
		IEnumerator<Statement> IEnumerable<Statement>.GetEnumerator() {
		#endif
			return statements.GetEnumerator();
		}
		
		private void AvdanceStamp() {
			stamp = unchecked(stamp+1);
		}
		
		public override void Clear() {
			statements.Clear();
			statementsAboutSubject.Clear();
			statementsAboutObject.Clear();
			distinct = true;
			AvdanceStamp();
		}
		
		private StatementList GetIndexArray(Hashtable from, Resource entity) {
			StatementList ret = (StatementList)from[entity];
			if (ret == null) {
				ret = new StatementList();
				from[entity] = ret;
			}
			return ret;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			if (checkForDuplicates && Contains(statement)) return;
			statements.Add(statement);
			if (isIndexed) {
				GetIndexArray(statementsAboutSubject, statement.Subject).Add(statement);
				GetIndexArray(statementsAboutObject, statement.Object).Add(statement);
			}
			if (!checkForDuplicates) distinct = false;
			AvdanceStamp();
		}
		
		public override void Import(StatementSource source) {
			bool newDistinct = checkForDuplicates || ((StatementCount==0) && source.Distinct);
			base.Import(source); // distinct set to false if !checkForDuplicates
			distinct = newDistinct;
			AvdanceStamp();
		}
		
		public override void Remove(Statement statement) {
			if (statement.AnyNull) {
				for (int i = 0; i < statements.Count; i++) {
					Statement s = (Statement)statements[i];
					if (statement.Matches(s)) {
						statements.RemoveAt(i); i--;
						if (isIndexed) {
							GetIndexArray(statementsAboutSubject, s.Subject).Remove(s);
							GetIndexArray(statementsAboutObject, s.Object).Remove(s);
						}
					}
				}
			} else {
				statements.Remove(statement);
				if (isIndexed) {
					GetIndexArray(statementsAboutSubject, statement.Subject).Remove(statement);
					GetIndexArray(statementsAboutObject, statement.Object).Remove(statement);
				}
			}
			AvdanceStamp();
		}
		
		public override Entity[] GetEntities() {
			ResSet h = new ResSet();
			for (int i = 0; i < statements.Count; i++) {
				Statement s = (Statement)statements[i];
				h.Add(s.Subject);
				h.Add(s.Predicate);
				if (s.Object is Entity) h.Add(s.Object);
				if (s.Meta != Statement.DefaultMeta) h.Add(s.Meta);
			}
			return h.ToEntityArray();
		}
		
		public override Entity[] GetPredicates() {
			ResSet h = new ResSet();
			for (int i = 0; i < statements.Count; i++) {
				Statement s = (Statement)statements[i];
				h.Add(s.Predicate);
			}
			return h.ToEntityArray();
		}

		public override Entity[] GetMetas() {
			ResSet h = new ResSet();
			for (int i = 0; i < statements.Count; i++) {
				Statement s = (Statement)statements[i];
				h.Add(s.Meta);
			}
			return h.ToEntityArray();
		}

		private void ShorterList(ref StatementList list1, StatementList list2) {
			if (list2.Count < list1.Count)
				list1 = list2;
		}
		
		public override StatementSource Select(Statement template) {
			StatementList source = statements;
			
			if (template == Statement.All)
				return new MemoryStoreIterator1(template, source, this);
			
			// The first time select is called, turn indexing on for the store.
			// TODO: Perform this index in a background thread if there are a lot
			// of statements.
			if (!isIndexed && allowIndexing) {
				isIndexed = true;
				for (int i = 0; i < StatementCount; i++) {
					Statement statement = this[i];
					GetIndexArray(statementsAboutSubject, statement.Subject).Add(statement);
					GetIndexArray(statementsAboutObject, statement.Object).Add(statement);
				}
			}
			
			if (template.Subject != null) ShorterList(ref source, GetIndexArray(statementsAboutSubject, template.Subject));
			else if (template.Object != null) ShorterList(ref source, GetIndexArray(statementsAboutObject, template.Object));
			
			if (source == null) return new EmptyStatementIterator();
			
			return new MemoryStoreIterator1(template, source, this);
		}
		
		private class MemoryStoreIterator1 : StatementIterator {
			Statement template;
			StatementList source;
			MemoryStore store;
			long stamp;
			int index;
			Statement current;
			public MemoryStoreIterator1(Statement template, StatementList source, MemoryStore store) {
				this.template = template;
				this.source = source;
				this.store = store;
				stamp = store.stamp;
				index = -1;
			}
			
			void HasStoreChanged() {
				if (stamp != store.stamp)
					throw new InvalidOperationException("The contents of the MemoryStore has changed since Select was called.");
			}
			
			public override bool Distinct { get { return store.Distinct; } }
			public override bool MoveNext() {
				HasStoreChanged();
				while (index < source.Count-1) {
					++index;
					current = source[index];
					if (template.Matches(current))
						return true;
				}
				return false;
			}
			public override Statement Current { get { return current; } }
		}

		public override StatementSource Select(SelectFilter filter) {
			// TODO: Use the index if possible.
			return new MemoryStoreIterator2(filter, this);
		}

		private class MemoryStoreIterator2 : StatementIterator {
			SelectFilter filter;
			ResSet s, p, o, m;
			MemoryStore store;
			long stamp;
			int index;
			Statement current;
			public MemoryStoreIterator2(SelectFilter filter, MemoryStore store) {
				this.filter = filter;
				this.store = store;
				stamp = store.stamp;
				index = -1;

				s = filter.Subjects == null ? null : new ResSet(filter.Subjects);
				p = filter.Predicates == null ? null : new ResSet(filter.Predicates);
				o = filter.Objects == null ? null : new ResSet(filter.Objects);
				m = filter.Metas == null ? null : new ResSet(filter.Metas);
			}
			
			void HasStoreChanged() {
				if (stamp != store.stamp)
					throw new InvalidOperationException("The contents of the MemoryStore has changed since Select was called.");
			}
			
			public override bool Distinct { get { return store.Distinct; } }
			public override bool MoveNext() {
				HasStoreChanged();
				while (index < store.StatementCount-1) {
					++index;
					current = store[index];

					if (s != null && !s.Contains(current.Subject)) continue;
					if (p != null && !p.Contains(current.Predicate)) continue;
					if (o != null && !o.Contains(current.Object)) continue;
					if (m != null && !m.Contains(current.Meta)) continue;
					if (filter.LiteralFilters != null && !LiteralFilter.MatchesFilters(current.Object, filter.LiteralFilters, store)) continue;
					return true;
				}
				return false;
			}
			public override Statement Current { get { return current; } }
		}

		public override void Replace(Entity a, Entity b) {
			MemoryStore removals = new MemoryStore();
			MemoryStore additions = new MemoryStore();
			foreach (Statement statement in statements) {
				if ((statement.Subject != null && statement.Subject == a) || (statement.Predicate != null && statement.Predicate == a) || (statement.Object != null && statement.Object == a) || (statement.Meta != null && statement.Meta == a)) {
					removals.Add(statement);
					additions.Add(statement.Replace(a, b));
				}
			}
			RemoveAll(removals.ToArray());
			Import(additions);
			AvdanceStamp();
		}
		
		public override void Replace(Statement find, Statement replacement) {
			if (find.AnyNull) throw new ArgumentNullException("find");
			if (replacement.AnyNull) throw new ArgumentNullException("replacement");
			if (find == replacement) return;
			
			foreach (Statement match in new MemoryStore(Select(find))) {
				Remove(match);
				Add(replacement);
				break; // should match just one statement anyway
			}
		}

		string SupportsPersistableBNodes.GetStoreGuid() {
			if (guid == null) guid = Guid.NewGuid().ToString("N");;
			return guid;
		}
		
		string SupportsPersistableBNodes.GetNodeId(BNode node) {
			if (pbnodeToId == null) {
				pbnodeToId = new Hashtable();
				pbnodeFromId = new Hashtable();
			}
			if (pbnodeToId.ContainsKey(node)) return (string)pbnodeToId[node];
			string id = pbnodeToId.Count.ToString();
			pbnodeToId[node] = id;
			pbnodeFromId[id] = node;
			return id;
		}
		
		BNode SupportsPersistableBNodes.GetNodeFromId(string persistentId) {
			if (pbnodeFromId == null) return null;
			return (BNode)pbnodeFromId[persistentId];
		}
	}
}
