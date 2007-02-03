using System;
using System.Collections;
using System.IO;

using SemWeb;
using SemWeb.Stores;

using name.levering.ryan.sparql.parser;
using name.levering.ryan.sparql.parser.model;
using name.levering.ryan.sparql.model;
using name.levering.ryan.sparql.model.data;
using name.levering.ryan.sparql.common;
using name.levering.ryan.sparql.common.impl;
using name.levering.ryan.sparql.logic.expression;
using name.levering.ryan.sparql.logic.function;

using SemWebVariable = SemWeb.Variable;
using SparqlVariable = name.levering.ryan.sparql.common.Variable;
using ExpressionLogic = name.levering.ryan.sparql.model.logic.ExpressionLogic;

#if !DOTNET2
using VariableList = System.Collections.ArrayList;
using BindingList = System.Collections.ArrayList;
using VarKnownValuesType = System.Collections.Hashtable;
using VarKnownValuesList = System.Collections.ArrayList;
using LitFilterList = System.Collections.ArrayList;
using LitFilterMap = System.Collections.Hashtable;
#else
using VariableList = System.Collections.Generic.List<SemWeb.Variable>;
using BindingList = System.Collections.Generic.List<SemWeb.Query.VariableBinding[]>;
using VarKnownValuesType = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.Resource>>;
using VarKnownValuesList = System.Collections.Generic.List<SemWeb.Resource>;
using LitFilterList = System.Collections.Generic.List<SemWeb.LiteralFilter>;
using LitFilterMap = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.LiteralFilter>>;
#endif

namespace SemWeb.Query {

	public class Sparql : SemWeb.Query.Query {
	
		private const string BNodePersistUri = "tag:taubz.for.net,2005:bnode_persist_uri/";

		string queryString;
		name.levering.ryan.sparql.model.Query query;
		ArrayList extFunctions = new ArrayList();
		
		public bool AllowPersistBNodes = false;
		
		public enum QueryType {
			Ask,
			Construct,
			Describe,
			Select
		}
		
		public Sparql(TextReader query)
			: this(query.ReadToEnd()) {
		}
	
		public Sparql(string query) {
			queryString = query;
			try {
				this.query = SPARQLParser.parse(new java.io.StringReader(query));
				if (this.query is SelectQuery) {
					SelectQuery sq = (SelectQuery)this.query;
					ReturnLimit = sq.getLimit();
					ReturnStart = sq.getOffset();
				}
			} catch (TokenMgrError e) {
				throw new QueryFormatException("SPARQL syntax error at: " + e.Message);
			} catch (ParseException e) {
				throw new QueryFormatException("SPARQL syntax error: " + e.getMessage());
			}
			
			extFunctions.Add(new TestFunction());
			extFunctions.Add(new LCFunction());
			extFunctions.Add(new UCFunction());
		}
		
		public QueryType Type {
			get {
				if (query is AskQuery)
					return QueryType.Ask;
				if (query is ConstructQuery)
					return QueryType.Construct;
				if (query is DescribeQuery)
					return QueryType.Describe;
				if (query is SelectQuery)
					return QueryType.Select;
				throw new NotSupportedException("Query is of an unsupported type.");
			}
		}
		
		public void AddExternalFunction(RdfFunction function) {
			extFunctions.Add(function);
		}
	
		public override void Run(SelectableSource source, TextWriter output) {
			if (query is AskQuery)
				Ask(source, output);
			else if (query is ConstructQuery)
				Construct(source, output);
			else if (query is DescribeQuery)
				Describe(source, output);
			else if (query is SelectQuery)
				Select(source, output);
			else
				throw new NotSupportedException("Query is of an unsupported type.");
		}
	
		public bool Ask(SelectableSource source) {
			if (!(query is AskQuery))
				throw new InvalidOperationException("Only ASK queries are supported by this method (" + query.GetType() + ").");
			AskQuery q = (AskQuery)query;
			RdfSourceWrapper sourcewrapper =  BindLogic(source);
			return q.execute(sourcewrapper);
		}
		
		public void Ask(SelectableSource source, TextWriter output) {
			bool result = Ask(source);
			System.Xml.XmlTextWriter w = new System.Xml.XmlTextWriter(output);
			w.Formatting = System.Xml.Formatting.Indented;
			w.WriteStartElement("sparql");
			w.WriteAttributeString("xmlns", "http://www.w3.org/2001/sw/DataAccess/rf1/result");
			w.WriteStartElement("head");
			w.WriteEndElement();
			w.WriteStartElement("results");
			w.WriteStartElement("boolean");
			w.WriteString(result ? "true" : "false");
			w.WriteEndElement();
			w.WriteEndElement();
			w.WriteEndElement();
			w.Close();
		}
	
		public void Construct(SelectableSource source, StatementSink sink) {
			if (!(query is ConstructQuery))
				throw new InvalidOperationException("Only CONSTRUCT queries are supported by this method (" + query.GetType() + ").");
			ConstructQuery q = (ConstructQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);
			RdfGraph graph = q.execute(sourcewrapper);
			WriteGraph(graph, sourcewrapper, sink);
		}
		
		public NamespaceManager GetQueryPrefixes() {
			NamespaceManager ns = new NamespaceManager();
			java.util.Map prefixes = ((QueryData)query).getPrefixExpansions();
			for (java.util.Iterator i = prefixes.keySet().iterator(); i.hasNext(); ) {
				string prefix = (string)i.next();
				string uri = prefixes.get(prefix).ToString(); // surrounded in < >
				uri = uri.Substring(1, uri.Length-2); // not sure how to get this directly
				ns.AddNamespace(uri, prefix);
			}
			return ns;
		}
		
		void WriteGraph(RdfGraph graph, RdfSourceWrapper sourcewrapper, StatementSink sink) {
			if (sink is RdfWriter)
				((RdfWriter)sink).Namespaces.AddFrom(GetQueryPrefixes());
		
			java.util.Iterator iter = graph.iterator();
			while (iter.hasNext()) {
				GraphStatement stmt = (GraphStatement)iter.next();
				Statement s;
				if (stmt is GraphStatementWrapper) {
					s = ((GraphStatementWrapper)stmt).s;
				} else {
					s = new Statement(
						sourcewrapper.ToEntity(stmt.getSubject()),
						sourcewrapper.ToEntity(stmt.getPredicate()),
						sourcewrapper.ToResource(stmt.getObject()),
						stmt.getGraphName() == null ? Statement.DefaultMeta : sourcewrapper.ToEntity(stmt.getGraphName()));
				}
				
				if (s.AnyNull) continue; // unbound variable, or literal in bad position
				sink.Add(s);
			}
		}
		
		public void Construct(SelectableSource source, TextWriter output) {
			using (RdfWriter w = new N3Writer(output))
				Construct(source, w);
		}

		public void Describe(SelectableSource source, StatementSink sink) {
			if (!(query is DescribeQuery))
				throw new InvalidOperationException("Only DESCRIBE queries are supported by this method (" + query.GetType() + ").");
			DescribeQuery q = (DescribeQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);
			RdfGraph graph = q.execute(sourcewrapper);
			WriteGraph(graph, sourcewrapper, sink);
		}

		public void Describe(SelectableSource source, TextWriter output) {
			using (RdfWriter w = new N3Writer(output))
				Describe(source, w);
		}

		public void Select(SelectableSource source, TextWriter output) {
			Select(source, new SparqlXmlQuerySink(output));
		}

		public void Select(SelectableSource source, QueryResultSink sink) {
			if (!(query is SelectQuery))
				throw new InvalidOperationException("Only SELECT queries are supported by this method (" + query.GetType() + ").");
			Run(source, sink);
		}

		public override string GetExplanation() {
			return query.ToString();
		}
		
		private RdfSourceWrapper BindLogic(SelectableSource source) {
			RdfSourceWrapper sourcewrapper = new RdfSourceWrapper(source, QueryMeta, this);
			MyLogicFactory logic = new MyLogicFactory();
			foreach (RdfFunction f in extFunctions)
				logic.registerExternalFunction(
					new URIWrapper(f.Uri),
					new ExtFuncWrapper(sourcewrapper, f));
			query.setLogic(logic);
			return sourcewrapper;
		}
		
		public override void Run(SelectableSource source, QueryResultSink resultsink) {
			if (!(query is SelectQuery))
				throw new InvalidOperationException("Only SELECT queries are supported by this method (" + query.GetType() + ").");

			// Perform the query
			SelectQuery squery = (SelectQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);

			RdfBindingSet results;
			try {
				results = squery.execute(sourcewrapper);
			} catch (java.lang.Exception e) {
				throw new QueryExecutionException("Error executing query: " + e.Message, e);
			}
			
			// Prepare binding objects
			java.util.List vars = results.getVariables();
			VariableBinding[] bindings = new VariableBinding[vars.size()];
			SparqlVariable[] svars = new SparqlVariable[vars.size()];
			SemWebVariable[] vars2 = new SemWebVariable[vars.size()];
			for (int i = 0; i < bindings.Length; i++) {
				svars[i] = (SparqlVariable)vars.get(i);
				vars2[i] = new SemWebVariable(svars[i].getName());
				bindings[i] = new VariableBinding(vars2[i], null);
			}
			
			// Initialize the result sink
			resultsink.Init(bindings, false, false); // set distinct and ordered
			
			// Set the comments
			resultsink.AddComments(queryString + "\n");
			resultsink.AddComments(sourcewrapper.GetLog());

			// Iterate the bindings
			java.util.Iterator iter = results.iterator();
			long ctr = -1, ctr2 = 0;
			while (iter.hasNext()) {
				RdfBindingRow row = (RdfBindingRow)iter.next();

				// Since SPARQL processing may be lazy-delayed,
				// add any new comments that might have been logged.
				resultsink.AddComments(sourcewrapper.GetLog());

				ctr++;
			
				if (ctr < ReturnStart && ReturnStart != -1) continue;

				for (int i = 0; i < bindings.Length; i++) {
					Resource r = sourcewrapper.ToResource(row.getValue(svars[i]));
					r = sourcewrapper.Persist(r);
					bindings[i] = new VariableBinding(bindings[i].Variable, r);
				}

				resultsink.AddComments(sourcewrapper.GetLog());
				
				resultsink.Add(bindings);

				ctr2++;
				if (ctr2 >= ReturnLimit && ReturnLimit != -1) break;
			}
			
			resultsink.AddComments(sourcewrapper.GetLog());
			
			// Close the result sink.
			resultsink.Finished();
		}
		
		class MyLogicFactory : name.levering.ryan.sparql.logic.StreamedLogic {
		    public override name.levering.ryan.sparql.model.logic.ConstraintLogic getGroupConstraintLogic(name.levering.ryan.sparql.model.data.GroupConstraintData data) {
        		return new RdfGroupLogic(data, new name.levering.ryan.sparql.logic.streamed.IndexedSetIntersectLogic());
    		}
		}
	
		class RdfSourceWrapper : AdvancedRdfSource,
				org.openrdf.model.ValueFactory {
				
			public readonly SelectableSource source;
			Hashtable bnodes = new Hashtable();
			Entity QueryMeta;
			Sparql sparql;
			
			System.Text.StringBuilder log = new System.Text.StringBuilder();
			
			public RdfSourceWrapper(SelectableSource source, Entity meta, Sparql sparql) {
				this.source = source;
				QueryMeta = meta;
				this.sparql = sparql;
			}
			
			public void Log(string message) {
				log.Append(message);
				log.Append('\n');
			}
			
			public string GetLog() {
				string ret = log.ToString();
				log.Length = 0;
				return ret;
			}
		
			private java.util.Iterator GetIterator(Statement statement, bool defaultGraph) {
				return GetIterator(statement.Subject == null ? null : new Entity[] { statement.Subject },
					statement.Predicate == null ? null : new Entity[] { statement.Predicate },
					statement.Object == null ? null : new Resource[] { statement.Object },
					statement.Meta == null ? null : new Entity[] { statement.Meta },
					null,
					defaultGraph);
			}
			
			private java.util.Iterator GetIterator(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, object[] litFilters, bool defaultGraph) {
				if (subjects == null && predicates == null && objects == null)
					throw new QueryExecutionException("Query would select all statements in the store.");
				
				if (subjects != null) Depersist(subjects);
				if (predicates != null) Depersist(predicates);
				if (objects != null) Depersist(objects);
				if (metas != null) Depersist(metas);
				
				if (subjects != null && subjects.Length == 0) return new EmptyIterator();
				if (predicates != null && predicates.Length == 0) return new EmptyIterator();
				if (objects != null && objects.Length == 0) return new EmptyIterator();
				if (metas != null && metas.Length == 0) return new EmptyIterator();
				
				SelectFilter filter = new SelectFilter(subjects, predicates, objects, metas);
				if (litFilters != null) {
					filter.LiteralFilters = new LiteralFilter[litFilters.Length];
					for (int i = 0; i < litFilters.Length; i++)
						filter.LiteralFilters[i] = (LiteralFilter)litFilters[i];
				}

				return new StatementIterator(source, filter, this, defaultGraph && metas == null);
			}
			
		    /**
		     * Gets all the statements that come from the default graph and have a
		     * certain subject, predicate, and object. Any of the parameters can be
		     * null, in which case it assumes these are "wildcards" and all statements
		     * that match the remainding parameters will be returned.
     		 */ 
     		public java.util.Iterator getDefaultStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object) {
				return GetIterator( new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), QueryMeta), true );
			}

     		public java.util.Iterator getDefaultStatements (org.openrdf.model.Value[] subject, org.openrdf.model.Value[] predicate, org.openrdf.model.Value[] @object, object[] litFilters) {
				return GetIterator( ToEntities(subject), ToEntities(predicate), ToResources(@object), QueryMeta == null ? null : new Entity[] { QueryMeta }, litFilters, true );
     		}
			
		    /**
		     * Gets all the statements that come from any graph and have a certain
		     * subject, predicate, and object. Any of the parameters can be null, in
		     * which case it assumes these are "wildcards" and all statements that match
		     * the remainding parameters will be returned.
		     * 
		     * @param the subj the subject to match statements against
		     * @param pred the predicate to match statements against
		     * @param obj the object to match statements against
		     * @return an Iterator over the matching statements
		     */
     		public java.util.Iterator getStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object) {
				return GetIterator(  new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), null), false );
			}
	
     		public java.util.Iterator getStatements (org.openrdf.model.Value[] subject, org.openrdf.model.Value[] predicate, org.openrdf.model.Value[] @object, object[] litFilters) {
				return GetIterator(  ToEntities(subject), ToEntities(predicate), ToResources(@object), null, litFilters, false );
     		}
     		
		    /**
		     * Gets all the statements that come from a particular named graph and have
		     * a certain subject, predicate, and object. Any of the parameters can be
		     * null, in which case it assumes these are "wildcards" and all statements
		     * that match the remainding parameters will be returned.
		     */
     		public java.util.Iterator getStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object, org.openrdf.model.URI graph) {
				return GetIterator( new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph)), false );
			}
			
     		public java.util.Iterator getStatements (org.openrdf.model.Value[] subject, org.openrdf.model.Value[] predicate, org.openrdf.model.Value[] @object, org.openrdf.model.URI[] graph, object[] litFilters) {
				return GetIterator( ToEntities(subject), ToEntities(predicate), ToResources(@object), ToEntities(graph), litFilters, false );
     		}
     		
			public org.openrdf.model.ValueFactory getValueFactory() {
				return this;
			}
			
			private bool has(Statement statement) {
				bool ret = source.Contains(statement);
				Log("CONTAINS: " + statement + " ("  + ret + ")");
				return ret;
			}
			
			public bool hasDefaultStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), QueryMeta));
			}
			
			public bool hasStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), null));
			}
	
			public bool hasStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object, org.openrdf.model.URI graph) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph)));
			}
			
			public Entity ToEntity(org.openrdf.model.Value ent) {
				if (ent == null) return null;
				if (ent is BNodeWrapper) return ((BNodeWrapper)ent).r;
				if (ent is URIWrapper) return ((URIWrapper)ent).r;
				if (ent is org.openrdf.model.BNode) {
					org.openrdf.model.BNode bnode = (org.openrdf.model.BNode)ent;
					Entity r = (Entity)bnodes[bnode.getID()];
					if (r == null) {
						r = new BNode();
						bnodes[bnode.getID()] = r;
					}
					return r;
				} else if (ent is org.openrdf.model.URI) {
					org.openrdf.model.URI uri = (org.openrdf.model.URI)ent;
					return new Entity(uri.toString());
				} else {
					return null;
				}
			}
			
			public Resource ToResource(org.openrdf.model.Value value) {
				if (value == null) return null;
				if (value is LiteralWrapper) return ((LiteralWrapper)value).r;
				if (value is org.openrdf.model.Literal) {
					org.openrdf.model.Literal literal = (org.openrdf.model.Literal)value;
					return new Literal(literal.getLabel(), literal.getLanguage(), literal.getDatatype() == null ? null : literal.getDatatype().toString());
				} else {
					return ToEntity(value);
				}
			}
			
			public Entity[] ToEntities(org.openrdf.model.Value[] ents) {
				if (ents == null) return null;
				ArrayList ret = new ArrayList();
				for (int i = 0; i < ents.Length; i++)
					if (!(ents[i] is org.openrdf.model.Literal))
						ret.Add( ToEntity(ents[i]) );
				return (Entity[])ret.ToArray(typeof(Entity));
			}
			public Resource[] ToResources(org.openrdf.model.Value[] ents) {
				if (ents == null) return null;
				Resource[] ret = new Resource[ents.Length];
				for (int i = 0; i < ents.Length; i++)
					ret[i] = ToResource(ents[i]);
				return ret;
			}
			public Resource[] ToResources(name.levering.ryan.sparql.model.logic.ExpressionLogic[] ents, name.levering.ryan.sparql.common.RdfBindingRow binding) {
				if (ents == null) return null;
				Resource[] ret = new Resource[ents.Length];
				for (int i = 0; i < ents.Length; i++) {
					if (ents[i] is SparqlVariable)
						ret[i] = ToResource(binding.getValue((SparqlVariable)ents[i]));
					else
						ret[i] = ToResource((org.openrdf.model.Value)ents[i]);
				}
				return ret;
			}
	
			public org.openrdf.model.BNode createBNode() {
				return new BNodeWrapper(new BNode());
			}
			public org.openrdf.model.BNode createBNode(string id) {
				throw new Exception(id);
			}
			public org.openrdf.model.Literal createLiteral(string value, string lang) {
				return new LiteralWrapper(new Literal(value, lang, null));
			}
			public org.openrdf.model.Literal createLiteral(string value, org.openrdf.model.URI datatype) {
				return new LiteralWrapper(new Literal(value, null, datatype.toString()));
			}
			public org.openrdf.model.Literal createLiteral(string value) {
				return new LiteralWrapper(new Literal(value));
			}
			public org.openrdf.model.Literal createLiteral(float value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(double value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(byte value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(short value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(int value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(long value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.Literal createLiteral(bool value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public org.openrdf.model.URI createURI(string ns, string ln) {
				return createURI(ns + ln);
			}
			public org.openrdf.model.URI createURI(string uri) {
				return new URIWrapper(new Entity(uri));
			}
			public org.openrdf.model.Statement createStatement (org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				return new Stmt(subject, predicate, @object); 
			}
			public org.openrdf.model.Statement createStatement (org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object, org.openrdf.model.Resource graph) {
				return new Stmt(subject, predicate, @object, graph); 
			}
			
			class Stmt : org.openrdf.model.Statement {
				org.openrdf.model.Resource subject;
				org.openrdf.model.URI predicate;
				org.openrdf.model.Value @object;
				org.openrdf.model.Resource graph;
				public Stmt(org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
					this.subject = subject;
					this.predicate = predicate;
					this.@object = @object;
				}
				public Stmt(org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object, org.openrdf.model.Resource graph) {
					this.subject = subject;
					this.predicate = predicate;
					this.@object = @object;
					this.graph = graph;
				}
				public org.openrdf.model.Resource getSubject() { return subject; }
				public org.openrdf.model.URI getPredicate() { return predicate; }
				public org.openrdf.model.Value getObject() { return @object; }
				public org.openrdf.model.Resource getContext() { return graph; }
				public bool equals(object other) {
					org.openrdf.model.Statement s = (org.openrdf.model.Statement)other;
					return getSubject().Equals(s.getSubject())
						&& getPredicate().Equals(s.getPredicate())
						&& getObject().Equals(s.getObject())
						&& getContext().Equals(s.getContext());
				}
				public int hashCode() { return getSubject().GetHashCode(); }
			}
			
			public void Depersist(Resource[] r) {
				for (int i = 0; i < r.Length; i++)
					r[i] = Depersist(r[i]);
			}
			
			public Resource Depersist(Resource r) {
				if (r.Uri == null || !sparql.AllowPersistBNodes) return r;
				if (!(source is StaticSource)) return r;
				if (!r.Uri.StartsWith(Sparql.BNodePersistUri)) return r;
				
				StaticSource spb = (StaticSource)source;
				string uri = r.Uri;
				string id = uri.Substring(Sparql.BNodePersistUri.Length);
				BNode node = spb.GetBNodeFromPersistentId(id);
				if (node != null)
					return node;
				
				return r;
			}
			
			public Resource Persist(Resource r) {
				if (!(r is BNode) || !sparql.AllowPersistBNodes) return r;
				if (!(source is StaticSource)) return r;
				StaticSource spb = (StaticSource)source;
				string id = spb.GetPersistentBNodeId((BNode)r);
				if (id == null) return r;
				return new Entity(Sparql.BNodePersistUri + ":" + id);
			}
			
			public static org.openrdf.model.Value Wrap(Resource res) {
				if (res is Literal)
					return new LiteralWrapper((Literal)res);
				else if (res.Uri == null)
					return new BNodeWrapper((BNode)res);
				else
					return new URIWrapper((Entity)res);
			}
		}
		
		class EmptyIterator : java.util.Iterator {
			public bool hasNext() {
				return false;
			}
			
			public object next() {
				throw new InvalidOperationException();
			}
			
			public void remove() {
				throw new InvalidOperationException();
			}
		}

		class StatementIterator : java.util.Iterator {
			SelectableSource source;
			SelectFilter filter;
			RdfSourceWrapper wrapper;
			bool wantMetas;
		
			Statement[] statements;
			int curindex = -1;
			
			public StatementIterator(SelectableSource source, SelectFilter filter, RdfSourceWrapper wrapper, bool wantMetas) {
				this.source = source;
				this.filter = filter;
				this.wrapper = wrapper;
				this.wantMetas = wantMetas;
			}
			
			public bool hasNext() {
				if (statements == null) {
					System.DateTime start = System.DateTime.Now;

					MemoryStore results = new MemoryStore();
					StatementSink sink = results;
				
					if (!source.Distinct)
						sink = new SemWeb.Util.DistinctStatementsSink(results, !wantMetas);

					source.Select(filter, sink);
				
					wrapper.Log("SELECT: " + filter + " => " + results.StatementCount + " statements [" + (System.DateTime.Now-start) + "s]");
					
					statements = results.ToArray();
				}
				
				return curindex + 1 < statements.Length;
			}
			
			public object next() {
				curindex++;
				return new GraphStatementWrapper(statements[curindex]);
			}
			
			public void remove() {
				throw new InvalidOperationException();
			}
		}
		
		class GraphStatementWrapper : GraphStatement {
			public readonly Statement s;
			public GraphStatementWrapper(Statement statement) {
				s = statement;
			}
			
			public org.openrdf.model.URI getGraphName() {
				return new URIWrapper(s.Meta);
			}
			
			public org.openrdf.model.Value getSubject() {
				return RdfSourceWrapper.Wrap(s.Subject);
			}
	
			public org.openrdf.model.URI getPredicate() {
				if (s.Predicate.Uri == null)
					throw new QueryExecutionException("Statement's predicate is a blank node.");
				return new URIWrapper(s.Predicate);
			}
	
			public org.openrdf.model.Value getObject() {
				return RdfSourceWrapper.Wrap(s.Object);
			}
		}
		
		class BNodeWrapper : java.lang.Object, org.openrdf.model.BNode {
			public BNode r;
			public BNodeWrapper(BNode res) { r = res; }
			public string getID() { throw new NotSupportedException(); }
			public override bool equals(object other) {
				if (!(other is BNodeWrapper)) return false;
				return r.Equals(((BNodeWrapper)other).r);
			}
			public override int hashCode() { return r.GetHashCode(); }
		}
	
		class URIWrapper : java.lang.Object, org.openrdf.model.URI {
			public Entity r;
			public URIWrapper(Entity res) { r = res; }
			public string getLocalName() { return ""; }
			public string getNamespace() { return r.Uri; }
			string org.openrdf.model.URI.toString() { return r.Uri; }
			public override string toString() { return r.Uri; }
			public override bool equals(object other) {
				if (other is URIWrapper)
					return r.Equals(((URIWrapper)other).r);
				else if (other is org.openrdf.model.URI)
					return r.Uri == ((org.openrdf.model.URI)other).toString();
				else
					return false;
			}
			public override int hashCode() { return java.lang.String.instancehelper_hashCode(r.Uri); }
		}
	
		class LiteralWrapper : java.lang.Object, org.openrdf.model.Literal {
			public Literal r;
			public LiteralWrapper(Literal res) { r = res; }
			public org.openrdf.model.URI getDatatype() { if (r.DataType == null) return null; return new URIWrapper(r.DataType); }
			public string getLabel() { return r.Value; }
			public string getLanguage() { return r.Language; }
			public override bool equals(object other) {
				if (other is LiteralWrapper)
					return r.Equals(((LiteralWrapper)other).r);
				else if (other is org.openrdf.model.Literal)
					return r.Equals(GetLiteral((org.openrdf.model.Literal)other));
				return false;
			}
			public override int hashCode() { return java.lang.String.instancehelper_hashCode(r.Value); }
			static Literal GetLiteral(org.openrdf.model.Literal literal) {
				return new Literal(literal.getLabel(), literal.getLanguage(),
					literal.getDatatype() == null ? null
						: literal.getDatatype().toString());
			}
		}
		
		class ExtFuncWrapper : name.levering.ryan.sparql.logic.function.ExternalFunctionFactory, name.levering.ryan.sparql.logic.function.ExternalFunction {
			RdfSourceWrapper source;
			RdfFunction func;
			
			public ExtFuncWrapper(RdfSourceWrapper s, RdfFunction f) {
				source = s;
				func = f;
			}
			
			public name.levering.ryan.sparql.logic.function.ExternalFunction create(name.levering.ryan.sparql.model.logic.LogicFactory logicfactory, name.levering.ryan.sparql.common.impl.SPARQLValueFactory valuefactory) {
				return this;
			}

			public org.openrdf.model.Value evaluate(name.levering.ryan.sparql.model.logic.ExpressionLogic[] arguments, name.levering.ryan.sparql.common.RdfBindingRow binding) {
				try {
					Resource ret = func.Evaluate(source.ToResources(arguments, binding));
					return RdfSourceWrapper.Wrap(ret);
				} catch (Exception e) {
					throw new name.levering.ryan.sparql.logic.function.ExternalFunctionException(e); 
				}
			}
		}
		
		class RdfGroupLogic : name.levering.ryan.sparql.logic.AdvancedGroupConstraintLogic {
		    public RdfGroupLogic(name.levering.ryan.sparql.model.data.GroupConstraintData data, name.levering.ryan.sparql.model.logic.helper.SetIntersectLogic logic)
		    	: base(data, logic) {
		    }
		    
		    protected override RdfBindingSet runTripleConstraints(java.util.List tripleConstraints, RdfSource source,
		    	java.util.Collection defaultDatasets, java.util.Collection namedDatasets,
		    	java.util.Map knownValues, java.util.Map knownFilters) {
		    	
		    	if (tripleConstraints.size() <= 1) return null; // no need to optimize
		    	
		    	RdfSourceWrapper s = (RdfSourceWrapper)source;
		    	
		    	if (s.source is QueryableSource) {
		    		QueryableSource qs = (QueryableSource)s.source;
		    		QueryOptions opts = new QueryOptions();
		    		
		    		opts.DistinguishedVariables = new VariableList();

                    opts.VariableKnownValues = new VarKnownValuesType();
		    		
		    		Statement[] graph = new Statement[tripleConstraints.size()];
		    		Hashtable varMap1 = new Hashtable();
		    		Hashtable varMap2 = new Hashtable();
		    		for (int i = 0; i < tripleConstraints.size(); i++) {
		    			TripleConstraintData triple = tripleConstraints.get(i) as TripleConstraintData;
		    			if (triple == null) return null;
		    			
						graph[i] = new Statement(null, null, null, null); // I don't understand why this should be necessary for a struct, but I get a null reference exception otherwise (yet, that didn't happen initially)
		    			graph[i].Subject = ToRes(triple.getSubjectExpression(), knownValues, true, varMap1, varMap2, s, opts) as Entity;
		    			graph[i].Predicate = ToRes(triple.getPredicateExpression(), knownValues, true, varMap1, varMap2, s, opts) as Entity;
		    			graph[i].Object = ToRes(triple.getObjectExpression(), knownValues, false, varMap1, varMap2, s, opts);
		    			graph[i].Meta = new Variable(); // TODO
		    			if (graph[i].AnyNull) return new RdfBindingSetImpl();
		    			
		    			// Don't distinguish the meta variable for now.
		    			if (graph[i].Subject is Variable) ((ArrayList)opts.DistinguishedVariables).Add(graph[i].Subject);
		    			if (graph[i].Predicate is Variable) ((ArrayList)opts.DistinguishedVariables).Add(graph[i].Predicate);
		    			if (graph[i].Object is Variable) ((ArrayList)opts.DistinguishedVariables).Add(graph[i].Object);
		    		}

                    opts.VariableLiteralFilters = new LitFilterMap();
		    		foreach (DictionaryEntry kv in varMap1) {
		    			if (knownFilters != null && knownFilters.containsKey(kv.Key)) {
                            LitFilterList filters = new LitFilterList();
		    				for (java.util.Iterator iter = ((java.util.List)knownFilters.get(kv.Key)).iterator(); iter.hasNext(); )
		    					filters.Add((LiteralFilter)iter.next());
		    				opts.VariableLiteralFilters[(Variable)kv.Value] = filters;
		    			}
		    		}
		    		
		    		if (!qs.MetaQuery(graph, opts).QuerySupported)
		    			return null; // TODO: We could also check if any part has NoData, we can abandon the query entirely 
		    		
		    		QueryResultBuilder builder = new QueryResultBuilder();
		    		builder.varMap = varMap2;
		    		builder.source = s;
		    		qs.Query(graph, opts, builder);
		    		return builder.bindings;
		    	}
		    	
		    	return null;
		    }
		    
		    class QueryResultBuilder : QueryResultSink {
		    	public Hashtable varMap;
		    	public RdfSourceWrapper source;
		    	public RdfBindingSetImpl bindings;
		    	
				public override void Init(VariableBinding[] variables, bool distinct, bool ordered) {
					java.util.ArrayList vars = new java.util.ArrayList();
					foreach (VariableBinding b in variables)
						if (varMap[b.Variable] != null) // because of bad treatment of meta
							vars.add((SparqlVariable)varMap[b.Variable]);
					
					bindings = new RdfBindingSetImpl(vars);
					bindings.setDistinct(distinct);
					bindings.setOrdered(ordered);
				}
				
				public override bool Add(VariableBinding[] result) {
					RdfBindingRowImpl row = new RdfBindingRowImpl(bindings);
					for (int i = 0; i < result.Length; i++) {
						if (varMap[result[i].Variable] == null) continue; // because of the bad treatment of meta
						row.addBinding( (SparqlVariable)varMap[result[i].Variable], RdfSourceWrapper.Wrap(result[i].Target) );
					}
					bindings.addRow(row);
					return true;
				}

				public override void AddComments(string comments) {
					source.Log(comments);
				}
		    }
		    
		    Resource ToRes(object expr, java.util.Map knownValues, bool entities, Hashtable varMap1, Hashtable varMap2, RdfSourceWrapper src, QueryOptions opts) {
		    	if (expr is SparqlVariable) {
		    		Variable v;
		    		if (varMap1.ContainsKey(expr)) {
		    			v = (Variable)varMap1[expr];
		    		} else {
		    			v = new Variable(expr.ToString());
		    			varMap1[expr] = v;
		    			varMap2[v] = expr;
		    		}
		    		
		    		if (knownValues != null && knownValues.get(expr) != null) {
			    		java.util.Set values = (java.util.Set)knownValues.get(expr);
                        VarKnownValuesList values2 = new VarKnownValuesList();
			    		for (java.util.Iterator iter = values.iterator(); iter.hasNext(); ) {
			    			Resource r = src.ToResource((org.openrdf.model.Value)iter.next());
			    			if (r != null)
			    				values2.Add(r);
			    		}
			    		
			    		opts.VariableKnownValues[v] = values2;
			    	}
			    	
		    		return v;
		    	}
		    	
	    		return entities ? src.ToEntity((org.openrdf.model.Value)expr) : src.ToResource((org.openrdf.model.Value)expr);
		    }
		    
			protected override void extractLiteralFilters(name.levering.ryan.sparql.model.logic.ExpressionLogic node, java.util.Map literalFilters) {
				base.extractLiteralFilters(node, literalFilters);
			
				if (node is BinaryExpressionNode) {
					BinaryExpressionNode b = (BinaryExpressionNode)node;
					
					LiteralFilter.CompType comp;
					if (node is ASTEqualsNode)
						comp = LiteralFilter.CompType.EQ;
					else if (node is ASTNotEqualsNode)
						comp = LiteralFilter.CompType.NE;
					else if (node is ASTGreaterThanNode)
						comp = LiteralFilter.CompType.GT;
					else if (node is ASTGreaterThanEqualsNode)
						comp = LiteralFilter.CompType.GE;
					else if (node is ASTLessThanNode)
						comp = LiteralFilter.CompType.LT;
					else if (node is ASTLessThanEqualsNode)
						comp = LiteralFilter.CompType.LE;
					else
						return;
					
					SparqlVariable var;
					org.openrdf.model.Literal val;
					
					object left = RemoveCast(b.getLeftExpression());
					object right = RemoveCast(b.getRightExpression());
					
					if (left is ASTVar && right is org.openrdf.model.Literal) {
						var = (SparqlVariable)left;
						val = (org.openrdf.model.Literal)right;
					} else if (right is ASTVar && left is org.openrdf.model.Literal) {
						var = (SparqlVariable)right;
						val = (org.openrdf.model.Literal)left;
						switch (comp) {
						case LiteralFilter.CompType.LT: comp = LiteralFilter.CompType.GE; break;
						case LiteralFilter.CompType.LE: comp = LiteralFilter.CompType.GT; break;
						case LiteralFilter.CompType.GT: comp = LiteralFilter.CompType.LE; break;
						case LiteralFilter.CompType.GE: comp = LiteralFilter.CompType.LT; break;
						}
					} else {
						return;
					}
					
					object parsedvalue = new Literal(val.getLabel(), null, val.getDatatype() == null ? null : val.getDatatype().ToString()).ParseValue();
					
					LiteralFilter filter = LiteralFilter.Create(comp, parsedvalue);
					addLiteralFilter(var, filter, literalFilters);
				}
			}
		    
		    object RemoveCast(object node) {
		    	if (node is ASTFunctionCall) {
		    		string name = ((ASTFunctionCall)node).getName(null).ToString();
		    		if (name.StartsWith("http://www.w3.org/2001/XMLSchema#"))
		    			return RemoveCast(((ASTFunctionCall)node).getArguments().get(0));
		    	}
		    	if (node is ASTMinusNode) {
		    		object inside = RemoveCast(((ASTMinusNode)node).getExpression());
		    		if (inside is ASTLiteral) {
		    			string value = ((ASTLiteral)inside).getLabel();
		    			double doublevalue = double.Parse(value);
		    			return new LiteralWrapper(new Literal((-doublevalue).ToString(), null, ((ASTLiteral)inside).getDatatype().ToString()));
		    		}
		    	}
		    	return node;
		    }
		}
		
		class TestFunction : RdfFunction {
			public override string Uri { get { return "http://taubz.for.net/code/semweb/test/function"; } }
			public override Resource Evaluate(Resource[] args) {
				return Literal.Create(args.Length == 2 && args[0].Equals(args[1]));
			}
		}
		class LCFunction : RdfFunction {
			public override string Uri { get { return "http://taubz.for.net/code/semweb/test/lc"; } }
			public override Resource Evaluate(Resource[] args) {
				if (args.Length != 1 || !(args[0] is Literal)) throw new InvalidOperationException();
				return new Literal(((Literal)args[0]).Value.ToLower());
			}
		}
		class UCFunction : RdfFunction {
			public override string Uri { get { return "http://taubz.for.net/code/semweb/test/uc"; } }
			public override Resource Evaluate(Resource[] args) {
				if (args.Length != 1 || !(args[0] is Literal)) throw new InvalidOperationException();
				return new Literal(((Literal)args[0]).Value.ToUpper());
			}
		}
	}
	
}
