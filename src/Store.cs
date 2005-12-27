using System;
using System.Collections;
using System.Data;

namespace SemWeb {
	
	public interface StatementSource {
		bool Distinct { get; }
		void Select(StatementSink sink);
	}
	
	public interface SelectableSource : StatementSource {
		bool Contains(Statement template);
		void Select(Statement template, StatementSink sink);
		void Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink sink);
		void Select(Entity[] subjects, Entity[] predicates, Entity[] metas, StatementSink sink, LiteralFilter[] literalFilters);
	}

	public interface QueryableSource {
		Entity[] FindEntities(Statement[] graph);
		void Query(Statement[] graph, SemWeb.Query.QueryResultSink sink);
	}
	
	public interface StatementSink {
		bool Add(Statement statement);
	}

	public interface ModifiableSource : StatementSink {
		void Clear();
		void Import(StatementSource source);
		void Remove(Statement template);
		void RemoveAll(Statement[] templates);
		void Replace(Entity find, Entity replacement);
		void Replace(Statement find, Statement replacement);
	}
	
	internal class StatementCounterSink : StatementSink {
		int counter = 0;
		
		public int StatementCount { get { return counter; } }
		
		public bool Add(Statement statement) {
			counter++;
			return true;
		}
	}

	internal class StatementExistsSink : StatementSink {
		bool exists = false;
		
		public bool Exists { get { return exists; } }
		
		public bool Add(Statement statement) {
			exists = true;
			return false;
		}
	}

	public abstract class Store : StatementSource, StatementSink,
		SelectableSource, QueryableSource, ModifiableSource,
		IDisposable {
		
		Entity rdfType;
		
		public static StatementSource CreateForInput(string spec) {
			return (StatementSource)Create(spec, false);
		}		
		
		public static StatementSink CreateForOutput(string spec) {
			return (StatementSink)Create(spec, true);
		}
		
		private static object Create(string spec, bool output) {
			string type = spec;
			
			int c = spec.IndexOf(':');
			if (c != -1) {
				type = spec.Substring(0, c);
				spec = spec.Substring(c+1);
			} else {
				spec = "";
			}
			
			switch (type) {
				case "mem":
					return new MemoryStore();
				case "xml":
					if (spec == "") throw new ArgumentException("Use: xml:filename");
					if (output) {
						return new RdfXmlWriter(spec);
					} else {
						return new RdfXmlReader(spec);
					}
				case "n3":
				case "ntriples":
				case "nt":
				case "turtle":
					if (spec == "") throw new ArgumentException("Use: format:filename");
					if (output) {
						N3Writer ret = new N3Writer(spec);
						switch (type) {
							case "nt": case "ntriples":
								ret.Format = N3Writer.Formats.NTriples;
								break;
							case "turtle":
								ret.Format = N3Writer.Formats.Turtle;
								break;
						}
						return ret;
					} else {
						return new N3Reader(spec);
					}
				/*case "file":
					if (spec == "") throw new ArgumentException("Use: format:filename");
					if (output) throw new ArgumentException("The FileStore does not support writing.");
					return new SemWeb.Stores.FileStore(spec);*/
				case "sqlite":
				case "mysql":
				case "postgresql":
					if (spec == "") throw new ArgumentException("Use: sqlite|mysql|postgresql:table:connection-string");
				
					c = spec.IndexOf(':');
					if (c == -1) throw new ArgumentException("Invalid format for SQL spec parameter (table:constring).");
					string table = spec.Substring(0, c);
					spec = spec.Substring(c+1);
					
					string classtype = null;
					if (type == "sqlite") {
						classtype = "SemWeb.Stores.SqliteStore, SemWeb.SqliteStore";
						spec = spec.Replace(";", ",");
					} else if (type == "mysql") {
						classtype = "SemWeb.Stores.MySQLStore, SemWeb.MySQLStore";
					} else if (type == "postgresql") {
						classtype = "SemWeb.Stores.PostgreSQLStore, SemWeb.PostgreSQLStore";
					}
					Type ttype = Type.GetType(classtype);
					if (ttype == null)
						throw new NotSupportedException("The storage type in <" + classtype + "> could not be found.");
					return Activator.CreateInstance(ttype, new object[] { spec, table });
				/*case "bdb":
					return new SemWeb.Stores.BDBStore(spec);*/
				case "sparql-http":
					return new SemWeb.Remote.SparqlHttpSource(spec);
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		protected Store() {
			rdfType = new Entity("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
		}
		
		void IDisposable.Dispose() {
			Close();
		}
		
		public virtual void Close() {
		}
		
		public abstract bool Distinct { get; }
		
		public abstract int StatementCount { get; }

		public abstract void Clear();

		public Entity[] GetEntitiesOfType(Entity type) {
			ArrayList entities = new ArrayList();
			
			IEnumerable result = Select(new Statement(null, rdfType, type));
			foreach (Statement s in result) {
				entities.Add(s.Subject);
			}
			
			return (Entity[])entities.ToArray(typeof(Entity));
		}
		
		bool StatementSink.Add(Statement statement) {
			Add(statement);
			return true;
		}
		
		public abstract void Add(Statement statement);
		
		public abstract void Remove(Statement statement);

		public virtual void Import(StatementSource source) {
			source.Select(this);
		}
		
		public void RemoveAll(Statement[] templates) {
			foreach (Statement t in templates)
				Remove(t);
		}
		
		public abstract Entity[] GetEntities();
		
		public abstract Entity[] GetPredicates();
		
		public abstract Entity[] GetMetas();
		
		public virtual bool Contains(Statement template) {
			return DefaultContains(this, template);
		}

		public static bool DefaultContains(SelectableSource source, Statement template) {
			StatementExistsSink sink = new StatementExistsSink();
			source.Select(template, sink);
			return sink.Exists;
		}
		
		public void Select(StatementSink result) {
			Select(Statement.All, result);
		}
		
		public abstract void Select(Statement template, StatementSink result);
		
		public abstract void Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink result);
		
		public virtual void Select(Entity[] subjects, Entity[] predicates, Entity[] metas, StatementSink sink, LiteralFilter[] literalFilters) {
			DefaultSelect(this, subjects, predicates, metas, sink, literalFilters);
		}
		
		public static void DefaultSelect(SelectableSource source, Entity[] subjects, Entity[] predicates, Entity[] metas, StatementSink sink, LiteralFilter[] literalFilters) {
			source.Select(subjects, predicates, (Resource[])null, metas, new FilterSink(source, sink, literalFilters));
		}

		private class FilterSink : StatementSink {
			SelectableSource source;
			StatementSink sink;
			LiteralFilter[] filters;
			public FilterSink(SelectableSource source, StatementSink sink, LiteralFilter[] filters) {
				this.source = source; this.sink = sink; this.filters = filters;
			}
			public bool Add(Statement statement) {
				if (!(statement.Object is Literal)) return true;
				foreach (LiteralFilter filter in filters)
					if (!filter.Filter((Literal)statement.Object, source))
						return true;
				return sink.Add(statement);
			}
		}
		
		public SelectResult Select(Statement template) {
			return new SelectResult.Single(this, template);
		}
		
		public SelectResult Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas) {
			return new SelectResult.Multi(this, subjects, predicates, objects, metas);
		}
		
		public Resource[] SelectObjects(Entity subject, Entity predicate) {
			Hashtable resources = new Hashtable();
			ResourceCollector collector = new ResourceCollector();
			collector.SPO = 2;
			collector.Table = resources;
			Select(new Statement(subject, predicate, null, null), collector);
			return (Resource[])new ArrayList(resources.Keys).ToArray(typeof(Resource));
		}
		public Entity[] SelectSubjects(Entity predicate, Resource @object) {
			Hashtable resources = new Hashtable();
			ResourceCollector collector = new ResourceCollector();
			collector.SPO = 0;
			collector.Table = resources;
			Select(new Statement(null, predicate, @object, null), collector);
			return (Entity[])new ArrayList(resources.Keys).ToArray(typeof(Entity));
		}
		class ResourceCollector : StatementSink {
			public Hashtable Table;
			public int SPO;
			public bool Add(Statement s) {
				if (SPO == 0) Table[s.Subject] = Table;
				if (SPO == 2) Table[s.Object] = Table;
				return true;
			}
		}
		
		public virtual void Replace(Entity find, Entity replacement) {
			MemoryStore deletions = new MemoryStore();
			MemoryStore additions = new MemoryStore();
			
			Select(new Statement(find, null, null, null), deletions);
			Select(new Statement(null, find, null, null), deletions);
			Select(new Statement(null, null, find, null), deletions);
			Select(new Statement(null, null, null, find), deletions);
			
			foreach (Statement s in deletions) {
				Remove(s);
				additions.Add(s.Replace(find, replacement));
			}
			
			foreach (Statement s in additions) {
				Add(s);
			}
		}
		
		public virtual void Replace(Statement find, Statement replacement) {
			Remove(find);
			Add(replacement);
		}
		
		public virtual Entity[] FindEntities(Statement[] filters) {
			return DefaultFindEntities(this, filters);
		}
		
		internal static Entity[] DefaultFindEntities(SelectableSource source, Statement[] filters) {
			Hashtable ents = new Hashtable();
			source.Select(filters[0], new FindEntitiesSink(ents, spom(filters[0])));
			for (int i = 1; i < filters.Length; i++) {
				Hashtable ents2 = new Hashtable();
				source.Select(filters[i], new FindEntitiesSink(ents2, spom(filters[i])));

				Hashtable ents3 = new Hashtable();
				if (ents.Count < ents2.Count) {
					foreach (Entity r in ents.Keys)
						if (ents2.ContainsKey(r))
							ents3[r] = r;
				} else {
					foreach (Entity r in ents2.Keys)
						if (ents.ContainsKey(r))
							ents3[r] = r;
				}
				ents = ents3;
			}
			
			ArrayList ret = new ArrayList();
			ret.AddRange(ents.Keys);
			return (Entity[])ret.ToArray(typeof(Entity));
		}
		
		private static int spom(Statement s) {
			if (s.Subject == null) return 0;
			if (s.Predicate == null) return 1;
			if (s.Object == null) return 2;
			if (s.Meta == null) return 3;
			throw new InvalidOperationException("A statement did not have a null field.");
		}
		
		private class FindEntitiesSink : StatementSink {
			Hashtable ents;
			int spom;
			public FindEntitiesSink(Hashtable ents, int spom) { this.ents = ents; this.spom = spom; }
			public bool Add(Statement s) {
				Entity e = null;
				if (spom == 0) e = s.Subject;
				if (spom == 1) e = s.Predicate;
				if (spom == 2) e = s.Object as Entity;
				if (spom == 3) e = s.Meta;
				if (e != null) ents[e] = ents;
				return true;
			}
		}
		
		public virtual void Query(Statement[] graph, SemWeb.Query.QueryResultSink sink) {
			SemWeb.Query.GraphMatch gm = new SemWeb.Query.GraphMatch();
			foreach (Statement s in graph)
				gm.AddEdge(s);
			gm.Run(this, sink);
		}
		
		public void Write(RdfWriter writer) {
			writer.Write(this);
		}
		
		public void Write(System.IO.TextWriter writer) {
			using (RdfWriter w = new N3Writer(writer)) {
				Write(w);
			}
		}
		
		protected object GetResourceKey(Resource resource) {
			return resource.GetResourceKey(this);
		}

		protected void SetResourceKey(Resource resource, object value) {
			resource.SetResourceKey(this, value);
		}
		
	}

	public abstract class SelectResult : StatementSource, IEnumerable {
		internal Store source;
		MemoryStore ms;
		internal SelectResult(Store source) { this.source = source; }
		public bool Distinct { get { return source.Distinct; } }
		public abstract void Select(StatementSink sink);
		public IEnumerator GetEnumerator() {
			return Buffer().Statements.GetEnumerator();
		}
		public long StatementCount { get { return Buffer().StatementCount; } }
		public MemoryStore Load() { return Buffer(); }
		public Statement[] ToArray() { return Load().ToArray(); }
		private MemoryStore Buffer() {
			if (ms != null) return ms;
			ms = new MemoryStore();
			ms.allowIndexing = false;
			Select(ms);
			return ms;
		}
		
		internal class Single : SelectResult {
			Statement template;
			public Single(Store source, Statement template) : base(source) {
				this.template = template;
			}
			public override void Select(StatementSink sink) {
				source.Select(template, sink);
			}
		}
		
		internal class Multi : SelectResult {
			Entity[] subjects, predicates, metas;
			Resource[] objects;
			public Multi(Store source, Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas)
				: base(source) {
				this.subjects = subjects;
				this.predicates = predicates;
				this.objects = objects;
				this.metas = metas;
			}
			public override void Select(StatementSink sink) {
				source.Select(subjects, predicates, objects, metas, sink);
			}
		}
	}
}

namespace SemWeb.Stores {

	public interface SupportsPersistableBNodes {
		string GetStoreGuid();
		string GetNodeId(BNode node);
		BNode GetNodeFromId(string persistentId);
	}

	public class MultiStore : Store {
		ArrayList stores = new ArrayList();
		Hashtable namedgraphs = new Hashtable();
		ArrayList allsources = new ArrayList();
		
		public MultiStore() { }
		
		public override bool Distinct { get { return false; } }
		
		public void Add(SelectableSource store) {
			stores.Add(store);
			allsources.Add(store);
		}
		
		public void Add(string uri, SelectableSource store) {
			namedgraphs[uri] = store;
			allsources.Add(store);
		}
		
		public void Add(RdfReader source) {
			MemoryStore store = new MemoryStore(source);
			Add(store);
		}
		
		public void Add(string uri, RdfReader source) {
			MemoryStore store = new MemoryStore(source);
			Add(uri, store);
		}
		
		public void Remove(SelectableSource store) {
			stores.Remove(store);
			allsources.Remove(store);
		}
		
		public void Remove(string uri) {
			allsources.Remove(namedgraphs[uri]);
			namedgraphs.Remove(uri);
		}
		
		public override int StatementCount {
			get {
				int ret = 0;
				foreach (StatementSource s in allsources) {
					if (s is Store)
						ret += ((Store)s).StatementCount;
					else
						throw new InvalidOperationException("Not all sources are stores supporting StatementCount.");
				}
				return ret;
			}
		}

		public override void Clear() {
			throw new InvalidOperationException("Clear is not a valid operation on a MultiStore.");
		}
		
		public override Entity[] GetEntities() {
			Hashtable h = new Hashtable();
			foreach (StatementSource s in allsources) {
				if (s is Store) {
					foreach (Resource r in ((Store)s).GetEntities())
						h[r] = h;
				} else {
					throw new InvalidOperationException("Not all sources are stores supporting GetEntities.");
				}
			}
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}
		
		public override Entity[] GetPredicates() {
			Hashtable h = new Hashtable();
			foreach (StatementSource s in allsources) {
				if (s is Store) {
					foreach (Resource r in ((Store)s).GetPredicates())
						h[r] = h;
				} else {
					throw new InvalidOperationException("Not all sources are stores supporting GetEntities.");
				}
			}
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}

		public override Entity[] GetMetas() {
			Hashtable h = new Hashtable();
			foreach (StatementSource s in allsources) {
				if (s is Store) {
					foreach (Resource r in ((Store)s).GetMetas())
						h[r] = h;
				} else {
					throw new InvalidOperationException("Not all sources are stores supporting GetEntities.");
				}
			}
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}

		public override void Add(Statement statement) { throw new InvalidOperationException("Add is not a valid operation on a MultiStore."); }
		
		SelectableSource[] GetSources(Entity graph) {
			if (graph == null || namedgraphs.Count == 0)
				return (SelectableSource[])allsources.ToArray(typeof(SelectableSource));
			else if (graph == Statement.DefaultMeta)
				return (SelectableSource[])stores.ToArray(typeof(SelectableSource));
			else if (graph.Uri != null && namedgraphs.ContainsKey(graph.Uri))
				return new SelectableSource[] { (SelectableSource)namedgraphs[graph.Uri] };
			else
				return null;
		}
		
		public override bool Contains(Statement statement) {
			SelectableSource[] sources = GetSources(statement.Meta);
			if (sources == null) return false;
			foreach (SelectableSource s in sources)
				if (s.Contains(statement))
					return true;
			return false;
		}
			
		public override void Remove(Statement statement) { throw new InvalidOperationException("Remove is not a valid operation on a MultiStore."); }
		
		public override void Select(Statement template, StatementSink result) {
			SelectableSource[] sources = GetSources(template.Meta);
			if (sources == null) return;
			foreach (SelectableSource s in sources)
				s.Select(template, result);
		}
		
		public override void Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink result) {
			if (metas == null || namedgraphs.Count == 0) metas = new Entity[] { null };
			foreach (Entity meta in metas) {
				SelectableSource[] sources = GetSources(meta);
				if (sources == null) continue;
				foreach (SelectableSource s in sources)
					s.Select(subjects, predicates, objects, metas, result);
			}
		}

		public override void Replace(Entity a, Entity b) { throw new InvalidOperationException("Replace is not a valid operation on a MultiStore."); }
		
		public override void Replace(Statement find, Statement replacement) { throw new InvalidOperationException("Replace is not a valid operation on a MultiStore."); }
		
	}
	
}
