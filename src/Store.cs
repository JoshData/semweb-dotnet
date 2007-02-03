using System;
#if !DOTNET2
using System.Collections;
#else
using System.Collections.Generic;
#endif
using System.Data;

using SemWeb.Util;

#if !DOTNET2
using SourceList = System.Collections.ArrayList;
using NamedSourceMap = System.Collections.Hashtable;
#else
using SourceList = System.Collections.Generic.List<SemWeb.SelectableSource>;
using NamedSourceMap = System.Collections.Generic.Dictionary<string, SemWeb.SelectableSource>;
#endif

namespace SemWeb {
	
	public class Store : StatementSource, StatementSink,
		SelectableSource, QueryableSource, StaticSource, ModifiableSource,
		IDisposable {
		
		// Static helper methods for creating data sources and sinks
		// from spec strings.
		
		public static StatementSource CreateForInput(string spec) {
			if (spec.StartsWith("rdfs+") || spec.StartsWith("euler+")) {
				StatementSource s = CreateForInput(spec.Substring(spec.IndexOf("+")+1));
				if (!(s is SelectableSource)) s = new MemoryStore(s);
				if (spec.StartsWith("rdfs+"))
					return new SemWeb.Inference.RDFS(s, (SelectableSource)s);
				if (spec.StartsWith("euler+"))
					return new SemWeb.Inference.Euler(s);
			}
			if (spec.StartsWith("debug+")) {
				StatementSource s = CreateForInput(spec.Substring(6));
				if (!(s is SelectableSource)) s = new MemoryStore(s);
				return new SemWeb.Stores.DebuggedSource((SelectableSource)s, System.Console.Error);
			}
			return (StatementSource)Create(spec, false);
		}		
		
		public static StatementSink CreateForOutput(string spec) {
			return (StatementSink)Create(spec, true);
		}
		
		private static object Create(string spec, bool output) {
			string[] multispecs = spec.Split('\n', '|');
			if (multispecs.Length > 1) {
				Store multistore = new Store();
				foreach (string mspec in multispecs) {
					object mstore = Create(mspec.Trim(), output);
					if (mstore is SelectableSource) {
						multistore.AddSource((SelectableSource)mstore);
					} else if (mstore is StatementSource) {
						MemoryStore m = new MemoryStore((StatementSource)mstore);
						multistore.AddSource(m);
					}
				}
				return multistore;
			}
		
			string type = spec;
			
			int c = spec.IndexOf(':');
			if (c != -1) {
				type = spec.Substring(0, c);
				spec = spec.Substring(c+1);
			} else {
				spec = "";
			}
			
			Type ttype;
			
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
						N3Writer ret = new N3Writer(spec); // turtle is default format
						switch (type) {
							case "nt": case "ntriples":
								ret.Format = N3Writer.Formats.NTriples;
								break;
						}
						return ret;
					} else {
						return new N3Reader(spec);
					}
				
				case "null":
					if (!output) throw new ArgumentException("The null sink does not support reading.");
					return new StatementCounterSink();
				
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
					ttype = Type.GetType(classtype);
					if (ttype == null)
						throw new NotSupportedException("The storage type in <" + classtype + "> could not be found.");
					return Activator.CreateInstance(ttype, new object[] { spec, table });
				/*case "bdb":
					return new SemWeb.Stores.BDBStore(spec);*/
				case "sparql-http":
					return new SemWeb.Remote.SparqlHttpSource(spec);
				case "class":
					ttype = Type.GetType(spec);
					if (ttype == null)
						throw new NotSupportedException("The class <" + spec + "> could not be found.");
					return Activator.CreateInstance(ttype);
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		// START OF ACTUAL STORE IMPLEMENTATION
		
		readonly Entity rdfType = new Entity("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");

		SourceList unnamedgraphs = new SourceList(); // a list of SelectableSources that aren't associated with graph URIs
		NamedSourceMap namedgraphs = new NamedSourceMap(); // a mapping from graph URIs to a selectable source that represents that graph
		
		SourceList allsources = new SourceList(); // a list of the sources in unnamed graphs and namedgraphs
		
		// GENERAL METHODS
		
		public Store() {
		}
		
		public Store(StatementSource source) {
			AddSource(new MemoryStore(source));
		}
		
		public Store(SelectableSource source) {
			AddSource(source);
		}

		public virtual void AddSource(SelectableSource store) {
			unnamedgraphs.Add(store);
			allsources.Add(store);
		}
		
		public virtual void AddSource(SelectableSource store, string uri) {
			namedgraphs[uri] = store;
			allsources.Add(store);
		}
		
		internal void AddSource2(SelectableSource store) {
			unnamedgraphs.Add(store);
			allsources.Add(store);
		}
		
		public void Write(System.IO.TextWriter writer) {
			using (RdfWriter w = new N3Writer(writer)) {
				Select(w);
			}
		}

		// INTERFACE IMPLEMENTATIONS and related methods
		
		// IDisposable
		
		public void Dispose() {
			foreach (object s in allsources)
				if (s is IDisposable)
					((IDisposable)s).Dispose();
		}
		
		// StatementSource
		
		public bool Distinct {
			get {
				if (allsources.Count > 1) return false;
				if (allsources.Count == 0) return true;
				return ((SelectableSource)allsources[0]).Distinct;
			}
		}
		
		public void Select(StatementSink result) {
			Select(Statement.All, result);
		}

		// SelectableSource
		
		private SelectableSource[] GetSources(ref Entity graph) {
			if (graph == null || namedgraphs.Count == 0)
				#if !DOTNET2
					return (SelectableSource[])allsources.ToArray(typeof(SelectableSource));
				#else
					return allsources.ToArray();
				#endif
			else if (graph == Statement.DefaultMeta)
				#if !DOTNET2
					return (SelectableSource[])unnamedgraphs.ToArray(typeof(SelectableSource));
				#else
					return unnamedgraphs.ToArray();
				#endif
			else if (graph.Uri != null && namedgraphs.ContainsKey(graph.Uri)) {
				graph = Statement.DefaultMeta;
				return new SelectableSource[] { (SelectableSource)namedgraphs[graph.Uri] };
			} else
				return null;
		}

		public bool Contains(Resource resource) {
			foreach (SelectableSource s in allsources)
				if (s.Contains(resource))
					return true;
			return false;
			/*return (resource is Entity && Contains(new Statement((Entity)resource, null, null, null)))
				|| (resource is Entity && Contains(new Statement(null, (Entity)resource, null, null)))
				|| (                      Contains(new Statement(null, null, resource, null)))
				|| (resource is Entity && Contains(new Statement(null, null, null, (Entity)resource)));*/
		}
		
		public bool Contains(Statement statement) {
			SelectableSource[] sources = GetSources(ref statement.Meta);
			if (sources == null) return false;
			foreach (SelectableSource s in sources)
				if (s.Contains(statement))
					return true;
			return false;
		}
			
		public static bool DefaultContains(SelectableSource source, Statement template) {
			StatementExistsSink sink = new StatementExistsSink();
			SelectFilter filter = new SelectFilter(template);
			filter.Limit = 1;
			source.Select(filter, sink);
			return sink.Exists;
		}
		
		public void Select(Statement template, StatementSink result) {
			SelectableSource[] sources = GetSources(ref template.Meta);
			if (sources == null) return;
			foreach (SelectableSource s in sources)
				s.Select(template, result);
		}
		
		public void Select(SelectFilter filter, StatementSink result) {
			Entity[] scanMetas = filter.Metas;
			if (scanMetas == null || namedgraphs.Count == 0) scanMetas = new Entity[] { null };
			foreach (Entity meta in scanMetas) {
				Entity meta2 = meta;
				SelectableSource[] sources = GetSources(ref meta2);
				if (sources == null) continue;
				filter.Metas = new Entity[] { meta2 };
				foreach (SelectableSource s in sources)
					s.Select(filter, result);
			}
		}
		
		public static void DefaultSelect(SelectableSource source, SelectFilter filter, StatementSink sink) {
			// This method should really be avoided...
			if (filter.LiteralFilters != null)
				sink = new SemWeb.Filters.FilterSink(filter.LiteralFilters, sink, source);
			foreach (Entity subject in filter.Subjects == null ? new Entity[] { null } : filter.Subjects)
			foreach (Entity predicate in filter.Predicates == null ? new Entity[] { null } : filter.Predicates)
			foreach (Resource objct in filter.Objects == null ? new Resource[] { null } : filter.Objects)
			foreach (Entity meta in filter.Metas == null ? new Entity[] { null } : filter.Metas)
				source.Select(new Statement(subject, predicate, objct, meta), sink);
		}		
		
		public SelectResult Select(Statement template) {
			return new SelectResult.Single(this, template);
		}
		
		public SelectResult Select(SelectFilter filter) {
			return new SelectResult.Multi(this, filter);
		}
		
		public Resource[] SelectObjects(Entity subject, Entity predicate) {
			if (predicate.Uri != null && predicate.Uri == NS.RDFS + "member") {
				ResourceCollector2 collector = new ResourceCollector2();
				Select(new Statement(subject, predicate, null, null), collector);
				return collector.GetItems();
			} else {
				ResSet resources = new ResSet();
				ResourceCollector collector = new ResourceCollector();
				collector.SPO = 2;
				collector.Table = resources;
				Select(new Statement(subject, predicate, null, null), collector);
				return resources.ToArray();
			}
		}
		public Entity[] SelectSubjects(Entity predicate, Resource @object) {
			ResSet resources = new ResSet();
			ResourceCollector collector = new ResourceCollector();
			collector.SPO = 0;
			collector.Table = resources;
			Select(new Statement(null, predicate, @object, null), collector);
			return resources.ToEntityArray();
		}
		class ResourceCollector : StatementSink {
			public ResSet Table;
			public int SPO;
			public bool Add(Statement s) {
				if (SPO == 0) Table.Add(s.Subject);
				if (SPO == 2) Table.Add(s.Object);
				return true;
			}
		}
		class ResourceCollector2 : StatementSink {
			System.Collections.ArrayList items = new System.Collections.ArrayList();
			ResSet other = new ResSet();
			public bool Add(Statement s) {
				if (s.Predicate.Uri == null || !s.Predicate.Uri.StartsWith(NS.RDF + "_")) {
					other.Add(s.Object);
				} else {
					string num = s.Predicate.Uri.Substring(NS.RDF.Length+1);
					try {
						int idx = int.Parse(num);
						items.Add(new Item(s.Object, idx));
					} catch {
						other.Add(s.Object);
					}
				}
				return true;
			}
			public Resource[] GetItems() {
				items.Sort();
				Resource[] ret = new Resource[items.Count + other.Count];
				int ctr = 0;
				foreach (Item item in items)
					ret[ctr++] = item.r;
				foreach (Resource item in other)
					ret[ctr++] = item;
				return ret;
			}
			class Item : IComparable {
				public Resource r;
				int idx;
				public Item(Resource r, int idx) { this.r = r; this.idx = idx; }
				public int CompareTo(object other) {
					return idx.CompareTo(((Item)other).idx);
				}
			}
		}
		
		// QueryableSource
		
		public SemWeb.Query.MetaQueryResult MetaQuery(Statement[] graph, SemWeb.Query.QueryOptions options) {
			// Special case for one wrapped data source that supports QueryableSource:
			if (allsources.Count == 1 && allsources[0] is QueryableSource)
				return ((QueryableSource)allsources[0]).MetaQuery(graph, options);
		
			SemWeb.Query.MetaQueryResult ret = DefaultMetaQuery(this, graph, options);
			
			// If the MultiStore will be falling back on the default Query implementation, then
			// return ret.  But if we can optimize the query by passing chunks of it
			// down to the data sources, then we unset the flag in ret that the default
			// implementation will be used.
			
			SemWeb.Query.GraphMatch.QueryPart[] chunks = ChunkQuery(graph, options);
			if (chunks != null && chunks.Length != graph.Length) // something was chunked
				ret.IsDefaultImplementation = false;
			
			return ret;
		}
		
		public void Query(Statement[] graph, SemWeb.Query.QueryOptions options, SemWeb.Query.QueryResultSink sink) {
			// Special case for one wrapped data source that supports QueryableSource:
			if (allsources.Count == 1 && allsources[0] is QueryableSource) {
				((QueryableSource)allsources[0]).Query(graph, options, sink);
				return;
			}

			// Chunk the query graph as best we can.
			SemWeb.Query.GraphMatch.QueryPart[] chunks = ChunkQuery(graph, options);
			
			// If we couldn't chunk the graph, or we got degenerate/useless chunking
			// (i.e. one chunk per statement), then just use the default GraphMatch implementation.
			if (chunks == null || chunks.Length == graph.Length) {
				Store.DefaultQuery(this, graph, options, sink);
				return;
			}
			
			SemWeb.Query.GraphMatch.RunGeneralQuery(chunks, options.VariableKnownValues, options.VariableLiteralFilters,
				0, options.Limit, sink);
		}
		
		private SemWeb.Query.GraphMatch.QueryPart[] ChunkQuery(Statement[] query, SemWeb.Query.QueryOptions options) {
			// MetaQuery the data sources to get their capabilities.
			SemWeb.Query.MetaQueryResult[] mq = new SemWeb.Query.MetaQueryResult[allsources.Count];
			for (int i = 0; i < allsources.Count; i++) {
				if (!(allsources[i] is QueryableSource))
					return null;
				mq[i] = ((QueryableSource)allsources[i]).MetaQuery(query, options);
				if (!mq[i].QuerySupported)
					return null;
			}
		
			System.Collections.ArrayList chunks = new System.Collections.ArrayList();
			
			int curSource = -1;
			System.Collections.ArrayList curStatements = new System.Collections.ArrayList();
			
			for (int j = 0; j < query.Length; j++) {
				if (curSource != -1) {
					// If we have a curSource and it definitively answers this
					// statement in the graph, include this statement in the
					// current chunk.
					if (mq[curSource].IsDefinitive != null && mq[curSource].IsDefinitive[j]) {
						curStatements.Add(query[j]);
						continue;
					}
					
					// If we have a curSource and no other source answers this
					// statement, also include this statement in the current chunk.
					bool foundOther = false;
					for (int i = 0; i < mq.Length; i++) {
						if (i == curSource) continue;
						if (mq[i].NoData != null && mq[i].NoData[j]) continue;
						foundOther = true;
						break;
					}
					if (!foundOther) {
						curStatements.Add(query[j]);
						continue;
					}
					
					// Some other source could possibly answer this statement,
					// so we complete the chunk we started.
					SemWeb.Query.GraphMatch.QueryPart c = new SemWeb.Query.GraphMatch.QueryPart(
						(Statement[])curStatements.ToArray(typeof(Statement)),
						(QueryableSource)allsources[curSource]
						);
					chunks.Add(c);
					
					curSource = -1;
					curStatements.Clear();
				}
			
				// Find a definitive source for this statement
				for (int i = 0; i < mq.Length; i++) {
					if (mq[i].IsDefinitive != null && mq[i].IsDefinitive[j]) {
						curSource = i;
						curStatements.Add(query[j]);
						break;
					}
				}
				if (curSource != -1) // found a definitive source
					continue;
					
				// See if only one source can answer this statement.
				// Also build a list of sources that can answer the
				// statement, so don't break out of this loop early.
				System.Collections.ArrayList answerables = new System.Collections.ArrayList();
				int findSource = -1;
				for (int i = 0; i < mq.Length; i++) {
					if (mq[i].NoData != null && mq[i].NoData[j]) continue;
					answerables.Add(allsources[i]);
					if (findSource == -1)
						findSource = i;
					else
						findSource = -2; // found a second source that can answer this
				}
				if (findSource >= 0) {
					curSource = findSource;
					curStatements.Add(query[j]);
					continue;
				}
				
				// More than one source can answer this, so make a one-statement chunk.
				SemWeb.Query.GraphMatch.QueryPart cc = new SemWeb.Query.GraphMatch.QueryPart(
					query[j],
					(QueryableSource[])answerables.ToArray(typeof(QueryableSource))
					);
				chunks.Add(cc);
			}

			if (curSource != -1) {
				SemWeb.Query.GraphMatch.QueryPart c = new SemWeb.Query.GraphMatch.QueryPart(
					(Statement[])curStatements.ToArray(typeof(Statement)),
					(QueryableSource)allsources[curSource]
					);
				chunks.Add(c);
			}
			
			return (SemWeb.Query.GraphMatch.QueryPart[])chunks.ToArray(typeof(SemWeb.Query.GraphMatch.QueryPart));
		}

		public static SemWeb.Query.MetaQueryResult DefaultMetaQuery(SelectableSource source, Statement[] graph, SemWeb.Query.QueryOptions options) {
			SemWeb.Query.MetaQueryResult ret = new SemWeb.Query.MetaQueryResult();
			
			ret.QuerySupported = true;
			ret.IsDefaultImplementation = true;
			
			ret.NoData = new bool[graph.Length];
			for (int i = 0; i < graph.Length; i++) {
				for (int j = 0; j < 4; j++) {
					Resource r = graph[i].GetComponent(j);
					
					if (r != null && !(r is Variable) && !source.Contains(r))
						ret.NoData[i] = true;
					
					if (r != null && r is Variable && options.VariableKnownValues != null && options.VariableKnownValues[(Variable)r] != null) {
						bool found = false;
						#if !DOTNET2
						foreach (Resource s in (ICollection)options.VariableKnownValues[(Variable)r]) {
						#else
						foreach (Resource s in (ICollection<Resource>)options.VariableKnownValues[(Variable)r]) {
						#endif
							if (source.Contains(s)) {
								found = true;
								break;
							}
						}
						if (!found) {
							ret.NoData[i] = true;
						}
					}
				}
			}
			
			return ret;
		}
		
		public static void DefaultQuery(SelectableSource source, Statement[] graph, SemWeb.Query.QueryOptions options, SemWeb.Query.QueryResultSink sink) {
			SemWeb.Query.GraphMatch q = new SemWeb.Query.GraphMatch();
			foreach (Statement s in graph)
				q.AddGraphStatement(s);
				
			q.ReturnLimit = options.Limit;
			
			if (options.VariableKnownValues != null) {
			#if !DOTNET2
			foreach (DictionaryEntry ent in options.VariableKnownValues)
				q.SetVariableRange((Variable)ent.Key, (ICollection)ent.Value);
			#else
			foreach (KeyValuePair<Variable,ICollection<Resource>> ent in options.VariableKnownValues)
				q.SetVariableRange(ent.Key, ent.Value);
			#endif
			}

			if (options.VariableLiteralFilters != null) {			
			#if !DOTNET2
			foreach (DictionaryEntry ent in options.VariableLiteralFilters)
				foreach (LiteralFilter filter in (ICollection)ent.Value)
					q.AddLiteralFilter((Variable)ent.Key, filter);
			#else
			foreach (KeyValuePair<Variable,ICollection<LiteralFilter>> ent in options.VariableLiteralFilters)
				foreach (LiteralFilter filter in ent.Value)
					q.AddLiteralFilter(ent.Key, filter);
			#endif
			}

			q.Run(source, sink);
		}

		// StaticSource
		
		public int StatementCount {
			get {
				int ret = 0;
				foreach (StatementSource s in allsources) {
					if (s is StaticSource)
						ret += ((StaticSource)s).StatementCount;
					else
						throw new InvalidOperationException("Not all data sources are support StatementCount.");
				}
				return ret;
			}
		}
		
		public Entity[] GetEntities() {
			ResSet h = new ResSet();
			foreach (StatementSource s in allsources) {
				if (s is StaticSource) {
					foreach (Resource r in ((StaticSource)s).GetEntities())
						h.Add(r);
				} else {
					throw new InvalidOperationException("Not all data sources support GetEntities.");
				}
			}
			return h.ToEntityArray();
		}
		
		public Entity[] GetPredicates() {
			ResSet h = new ResSet();
			foreach (StatementSource s in allsources) {
				if (s is StaticSource) {
					foreach (Resource r in ((StaticSource)s).GetPredicates())
						h.Add(r);
				} else {
					throw new InvalidOperationException("Not data sources support GetPredicates.");
				}
			}
			return h.ToEntityArray();
		}

		public Entity[] GetMetas() {
			ResSet h = new ResSet();
			foreach (StatementSource s in allsources) {
				if (s is StaticSource) {
					foreach (Resource r in ((StaticSource)s).GetMetas())
						h.Add(r);
				} else {
					throw new InvalidOperationException("Not all data sources support GetMetas.");
				}
			}
			return h.ToEntityArray();
		}

		public Entity[] GetEntitiesOfType(Entity type) {
			return SelectSubjects(rdfType, type);
		}
		
		public string GetPersistentBNodeId(BNode node) {
			foreach (SelectableSource source in allsources) {
				if (source is StaticSource) {
					string id = ((StaticSource)source).GetPersistentBNodeId(node);
					if (id != null) return id;
				}
			}
			return null;
		}
		
		public BNode GetBNodeFromPersistentId(string persistentId) {
			foreach (SelectableSource source in allsources) {
				if (source is StaticSource) {
					BNode node = ((StaticSource)source).GetBNodeFromPersistentId(persistentId);
					if (node != null) return node;
				}
			}
			return null;
		}
		
		
		// StatementSink

		bool StatementSink.Add(Statement statement) {
			Add(statement);
			return true;
		}
		
		public void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			// We don't know where to put it unless we are wrapping just one store.
			SelectableSource[] sources = GetSources(ref statement.Meta);
			if (sources == null || sources.Length != 1) throw new InvalidOperationException("I don't know which data source to put the statement into.");
			if (!(sources[0] is ModifiableSource)) throw new InvalidOperationException("The data source is not modifiable.");
			((ModifiableSource)sources[0]).Add(statement);
		}
		
		// ModifiableSource
		
		public void Clear() {
			if (allsources.Count > 0) throw new InvalidOperationException("The Clear() method is not supported when multiple data sources are added to a Store.");
			if (!(allsources[0] is ModifiableSource)) throw new InvalidOperationException("The data source is not modifiable.");
			((ModifiableSource)allsources[0]).Clear();
		}
		
		ModifiableSource[] GetModifiableSources(ref Entity graph) {
			SelectableSource[] sources = GetSources(ref graph);
			if (sources == null) return null;
			
			// check all are modifiable first
			foreach (SelectableSource source in sources)
				if (!(source is ModifiableSource)) throw new InvalidOperationException("Not all of the data sources are modifiable.");
				
			ModifiableSource[] sources2 = new ModifiableSource[sources.Length];
			sources.CopyTo(sources2, 0);
			return sources2;
		}

		public void Remove(Statement template) {
			ModifiableSource[] sources = GetModifiableSources(ref template.Meta);
			if (sources == null) return;
			
			foreach (ModifiableSource source in sources)
				source.Remove(template);
		}

		public virtual void Import(StatementSource source) {
			source.Select(this);
		}
		
		public void RemoveAll(Statement[] templates) {
			// Not tested...
			
			System.Collections.ArrayList metas = new System.Collections.ArrayList();
			foreach (Statement t in templates)
				if (!metas.Contains(t.Meta))
					metas.Add(t.Meta);
					
			foreach (Entity meta in metas) {
				Entity meta2 = meta;
				ModifiableSource[] sources = GetModifiableSources(ref meta2);
				if (sources == null) continue;
				
				StatementList templates2 = new StatementList();
				foreach (Statement t in templates) {
					if (t.Meta == meta) {
						Statement t2 = t;
						t2.Meta = meta2;
						templates2.Add(t2);
					}
				}
					
				foreach (ModifiableSource source in sources)
					source.RemoveAll(templates2);
			}
		}
		
		public void Replace(Entity find, Entity replacement) {
			foreach (SelectableSource source in allsources)
				if (!(source is ModifiableSource)) throw new InvalidOperationException("Not all of the data sources are modifiable.");

			foreach (ModifiableSource source in allsources)
				source.Replace(find, replacement);
		}
		
		public void Replace(Statement find, Statement replacement) {
			ModifiableSource[] sources = GetModifiableSources(ref find.Meta);
			if (sources == null) return;
				
			foreach (ModifiableSource source in sources)
				source.Replace(find, replacement);
		}

		public static void DefaultReplace(ModifiableSource source, Entity find, Entity replacement) {
			MemoryStore deletions = new MemoryStore();
			MemoryStore additions = new MemoryStore();
			
			source.Select(new Statement(find, null, null, null), deletions);
			source.Select(new Statement(null, find, null, null), deletions);
			source.Select(new Statement(null, null, find, null), deletions);
			source.Select(new Statement(null, null, null, find), deletions);
			
			foreach (Statement s in deletions) {
				source.Remove(s);
				additions.Add(s.Replace(find, replacement));
			}
			
			foreach (Statement s in additions) {
				source.Add(s);
			}
		}
		
		public static void DefaultReplace(ModifiableSource source, Statement find, Statement replacement) {
			source.Remove(find);
			source.Add(replacement);
		}
		
		
	}

	public abstract class SelectResult : StatementSource, 
#if DOTNET2
	System.Collections.Generic.IEnumerable<Statement>
#else
	IEnumerable
#endif
	{
		internal Store source;
		MemoryStore ms;
		internal SelectResult(Store source) { this.source = source; }
		public bool Distinct { get { return source.Distinct; } }
		public abstract void Select(StatementSink sink);
#if DOTNET2
		System.Collections.Generic.IEnumerator<Statement> System.Collections.Generic.IEnumerable<Statement>.GetEnumerator() {
			return ((System.Collections.Generic.IEnumerable<Statement>)Buffer()).GetEnumerator();
		}
#endif
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return ((System.Collections.IEnumerable)Buffer()).GetEnumerator();
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
			SelectFilter filter;
			public Multi(Store source, SelectFilter filter)
				: base(source) {
				this.filter = filter;
			}
			public override void Select(StatementSink sink) {
				source.Select(filter, sink);
			}
		}
	}
}
		



/////// AUXILIARY STORE WRAPPERS /////////
	
namespace SemWeb.Stores {

	#if DOTNET2
	using System.Collections;
	#endif

	public abstract class SimpleSourceWrapper : SelectableSource {
	
		public virtual bool Distinct { get { return true; } }
		
		public virtual void Select(StatementSink sink) {
			// The default implementation does not return
			// anything for this call.
		}
		
		public abstract bool Contains(Resource resource);

		public virtual bool Contains(Statement template) {
			template.Object = null; // reduce to another case (else there would be recursion)
			return Store.DefaultContains(this, template);
		}
		
		protected virtual void SelectAllSubject(Entity subject, StatementSink sink) {
		}

		protected virtual void SelectAllObject(Resource @object, StatementSink sink) {
		}

		protected virtual void SelectRelationsBetween(Entity subject, Resource @object, StatementSink sink) {
		}
		
		protected virtual void SelectAllPairs(Entity predicate, StatementSink sink) {
		}

		protected virtual void SelectSubjects(Entity predicate, Resource @object, StatementSink sink) {
		}

		protected virtual void SelectObjects(Entity subject, Entity predicate, StatementSink sink) {
		}

		public void Select(Statement template, StatementSink sink) {
			if (template.Meta != null && template.Meta != Statement.DefaultMeta) return;
			if (template.Predicate != null && template.Predicate.Uri == null) return;
			
			if (template.Subject == null && template.Predicate == null && template.Object == null) {
				Select(sink);
			} else if (template.Subject != null && template.Predicate != null && template.Object != null) {
				template.Meta = Statement.DefaultMeta;
				if (Contains(template))
					sink.Add(template);
			} else if (template.Predicate == null) {
				if (template.Subject == null)
					SelectAllObject(template.Object, sink); 
				else if (template.Object == null)
					SelectAllSubject(template.Subject, sink); 
				else
					SelectRelationsBetween(template.Subject, template.Object, sink);
			} else if (template.Subject != null && template.Object == null) {
				SelectObjects(template.Subject, template.Predicate, sink);
			} else if (template.Subject == null && template.Object != null) {
				SelectSubjects(template.Predicate, template.Object, sink);
			} else if (template.Subject == null && template.Object == null) {
				SelectAllPairs(template.Predicate, sink);
			}
		}
	
		public void Select(SelectFilter filter, StatementSink sink) {
			Store.DefaultSelect(this, filter, sink);
		}
	}
	
	public class DebuggedSource : QueryableSource {
		SelectableSource source;
		System.IO.TextWriter output;
		
		public DebuggedSource(SelectableSource source, System.IO.TextWriter output) {
			this.source = source;
			this.output = output;
		}
		
		public bool Distinct { get { return source.Distinct; } }
		
		public void Select(StatementSink sink) {
			Select(Statement.All, sink);
		}
		
		public bool Contains(Resource resource) {
			output.WriteLine("CONTAINS: " + resource);
			return source.Contains(resource);
		}

		public bool Contains(Statement template) {
			output.WriteLine("CONTAINS: " + template);
			return source.Contains(template);
		}
		
		public void Select(Statement template, StatementSink sink) {
			output.WriteLine("SELECT: " + template);
			source.Select(template, sink);
		}
	
		public void Select(SelectFilter filter, StatementSink sink) {
			output.WriteLine("SELECT: " + filter);
			source.Select(filter, sink);
		}

		public virtual SemWeb.Query.MetaQueryResult MetaQuery(Statement[] graph, SemWeb.Query.QueryOptions options) {
			if (source is QueryableSource)
				return ((QueryableSource)source).MetaQuery(graph, options);
			else
				return new SemWeb.Query.MetaQueryResult(); // QuerySupported is by default false
		}

		public void Query(Statement[] graph, SemWeb.Query.QueryOptions options, SemWeb.Query.QueryResultSink sink) {
			output.WriteLine("QUERY:");
			foreach (Statement s in graph)
				output.WriteLine("\t" + s);
			if (options.VariableKnownValues != null) {
				#if !DOTNET2
				foreach (System.Collections.DictionaryEntry ent in options.VariableKnownValues)
				#else
				foreach (KeyValuePair<Variable,ICollection<Resource>> ent in options.VariableKnownValues)
				#endif
					output.WriteLine("\twhere " + ent.Key + " in " + ToString((ICollection)ent.Value));	
			}
			if (source is QueryableSource) 
				((QueryableSource)source).Query(graph, options, sink);
			else
				throw new NotSupportedException("Underlying source " + source + " is not a QueryableSource.");
		}
		
		string ToString(ICollection resources) {
			ArrayList s = new ArrayList();
			foreach (Resource r in resources)
				s.Add(r.ToString());
			return String.Join(",", (string[])s.ToArray(typeof(string)));
		}
	}
	
	public class CachedSource : SelectableSource {
		SelectableSource source;

		Hashtable containsresource = new Hashtable();
		StatementMap containsstmtresults = new StatementMap();
		StatementMap selectresults = new StatementMap();
		Hashtable selfilterresults = new Hashtable();
	
		public CachedSource(SelectableSource s) { source = s; }
	
		public bool Distinct { get { return source.Distinct; } }
		
		public void Select(StatementSink sink) {
			Select(Statement.All, sink);
		}

		public bool Contains(Resource resource) {
			if (source == null) return false;
			if (!containsresource.ContainsKey(resource))
				containsresource[resource] = source.Contains(resource);
			return (bool)containsresource[resource];
		}

		public bool Contains(Statement template) {
			if (source == null) return false;
			if (!containsstmtresults.ContainsKey(template))
				containsstmtresults[template] = source.Contains(template);
			return (bool)containsstmtresults[template];
		}
		
		public void Select(Statement template, StatementSink sink) {
			if (source == null) return;
			if (!selectresults.ContainsKey(template)) {
				MemoryStore s = new MemoryStore();
				source.Select(template, s);
				selectresults[template] = s;
			}
			((MemoryStore)selectresults[template]).Select(sink);
		}
	
		public void Select(SelectFilter filter, StatementSink sink) {
			if (source == null) return;
			if (!selfilterresults.ContainsKey(filter)) {
				MemoryStore s = new MemoryStore();
				source.Select(filter, s);
				selfilterresults[filter] = s;
			}
			((MemoryStore)selfilterresults[filter]).Select(sink);
		}

	}
	
	internal class DecoupledStatementSource : StatementSource {
		StatementSource source;
		int minbuffersize = 2000;
		int maxbuffersize = 10000;
		
		bool bufferWanted = false;
		System.Threading.AutoResetEvent bufferMayAcquire = new System.Threading.AutoResetEvent(false);
		System.Threading.AutoResetEvent bufferReleased = new System.Threading.AutoResetEvent(false);

		System.Threading.Thread sourceThread;
		
		StatementList buffer = new StatementList();
		bool sourceFinished = false;
		
		public DecoupledStatementSource(StatementSource source) {
			this.source = source;
		}
		
		public bool Distinct { get { return source.Distinct; } }

		public void Select(StatementSink sink) {
			bufferWanted = false;
			
			sourceThread = new System.Threading.Thread(Go);
			sourceThread.Start();
			
			while (true) {
				bufferWanted = true;
				if (!sourceFinished) bufferMayAcquire.WaitOne(); // wait until we can have the buffer
				bufferWanted = false;
				
				Statement[] statements = buffer.ToArray();
				buffer.Clear();
				
				bufferReleased.Set(); // notify that we don't need the buffer anymore

				if (sourceFinished && statements.Length == 0) break;
				
				foreach (Statement s in statements)
					sink.Add(s);
			}
		}
		
		private void Go() {
			source.Select(new MySink(this));
			sourceFinished = true;
			bufferMayAcquire.Set(); // for the last batch
		}
		
		private void SourceAdd(Statement s) {
			if ((bufferWanted && buffer.Count > minbuffersize) || buffer.Count >= maxbuffersize) {
				bufferMayAcquire.Set();
				bufferReleased.WaitOne();
			}
			buffer.Add(s);
		}
		private void SourceAdd(Statement[] s) {
			if ((bufferWanted && buffer.Count > minbuffersize) || buffer.Count >= maxbuffersize) {
				bufferMayAcquire.Set();
				bufferReleased.WaitOne();
			}
			foreach (Statement ss in s)
				buffer.Add(ss);
		}
		
		private class MySink : StatementSink {
			DecoupledStatementSource x;
			public MySink(DecoupledStatementSource x) { this.x = x; }
			public bool Add(Statement s) {
				x.SourceAdd(s);
				return true;
			}
			public bool Add(Statement[] s) {
				x.SourceAdd(s);
				return true;
			}
		}
	}
}
