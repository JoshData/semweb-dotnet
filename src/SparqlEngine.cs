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
using VarKnownValuesType = System.Collections.Hashtable;
using VarKnownValuesList = System.Collections.ArrayList;
using LitFilterList = System.Collections.ArrayList;
using LitFilterMap = System.Collections.Hashtable;
#else
using VariableList = System.Collections.Generic.List<SemWeb.Variable>;
using VarKnownValuesType = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.Resource>>;
using VarKnownValuesList = System.Collections.Generic.List<SemWeb.Resource>;
using LitFilterList = System.Collections.Generic.List<SemWeb.LiteralFilter>;
using LitFilterMap = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.LiteralFilter>>;
#endif

namespace SemWeb.Query {

	public class SparqlEngine : SemWeb.Query.Query {
	
		private const string BNodePersistUri = "tag:taubz.for.net,2005:bnode_persist_uri/";

		string queryString;
		name.levering.ryan.sparql.parser.model.QueryNode query;
		ArrayList extFunctions = new ArrayList();
		
		public bool AllowPersistBNodes = false;
		
		string preferredMimeType = null;
		
		public enum QueryType {
			Ask,
			Construct,
			Describe,
			Select
		}
		
		/* CONSTRUCTORS */
		
		public SparqlEngine(TextReader query)
			: this(query.ReadToEnd()) {
		}
	
		public SparqlEngine(string query) {
			queryString = query;
			try {
				this.query = (name.levering.ryan.sparql.parser.model.QueryNode)SPARQLParser.parse(new java.io.StringReader(query));
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
		
		/* QUERY TYPE AND OUTPUT CONTROL PROPERTIES */
		
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
		
		public override string MimeType {
			get {
				if (preferredMimeType == null) {
					if (query is AskQuery)
						return SparqlXmlQuerySink.MimeType;
					if (query is ConstructQuery)
						return "application/rdf+xml";
					if (query is DescribeQuery)
						return "application/rdf+xml";
					if (query is SelectQuery)
						return SparqlXmlQuerySink.MimeType;
					throw new NotSupportedException("Query is of an unsupported type.");
				} else {
					return preferredMimeType;
				}
			}
			set {
				if ((query is AskQuery || query is SelectQuery) && value != SparqlXmlQuerySink.MimeType)
					throw new NotSupportedException("That MIME type is not supported for ASK or SELECT queries.");
				
				if (query is ConstructQuery || query is DescribeQuery) {
					// this throws if we don't recognize the type
					RdfWriter.Create(value, TextWriter.Null);
				}
				
				preferredMimeType = value;
			}
		}
		
		/* QUERY EXECUTION CONTROL METHODS */
		
		public void AddExternalFunction(RdfFunction function) {
			extFunctions.Add(function);
		}
		
		public override string GetExplanation() {
			return query.ToString();
		}
		
		/* QUERY EXECUTION METHODS */
	
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
			RdfSourceWrapper sourcewrapper = BindLogic(source);
			try {
				return q.execute(sourcewrapper);
			} catch (name.levering.ryan.sparql.common.QueryException e) {
				throw new QueryExecutionException("Error executing query: " + e.Message, e);
			}
		}
		
		public void Ask(SelectableSource source, TextWriter output) {
			bool result = Ask(source);
			System.Xml.XmlTextWriter w = new System.Xml.XmlTextWriter(output);
			w.Formatting = System.Xml.Formatting.Indented;
			w.WriteStartElement("sparql");
			w.WriteAttributeString("xmlns", "http://www.w3.org/2001/sw/DataAccess/rf1/result");
			w.WriteStartElement("head");
			w.WriteEndElement();
			w.WriteStartElement("boolean");
			w.WriteString(result ? "true" : "false");
			w.WriteEndElement();
			w.WriteEndElement();
			w.Flush();
		}
	
		public void Construct(SelectableSource source, StatementSink sink) {
			if (!(query is ConstructQuery))
				throw new InvalidOperationException("Only CONSTRUCT queries are supported by this method (" + query.GetType() + ").");
			ConstructQuery q = (ConstructQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);
			try {
				RdfGraph graph = q.execute(sourcewrapper);
				WriteGraph(graph, sourcewrapper, sink);
			} catch (name.levering.ryan.sparql.common.QueryException e) {
				throw new QueryExecutionException("Error executing query: " + e.Message, e);
			}
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
			using (RdfWriter w = RdfWriter.Create(MimeType, output))
				Construct(source, w);
		}

		public void Describe(SelectableSource source, StatementSink sink) {
			if (!(query is DescribeQuery))
				throw new InvalidOperationException("Only DESCRIBE queries are supported by this method (" + query.GetType() + ").");
			DescribeQuery q = (DescribeQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);
			try {
				RdfGraph graph = q.execute(sourcewrapper);
				WriteGraph(graph, sourcewrapper, sink);
			} catch (name.levering.ryan.sparql.common.QueryException e) {
				throw new QueryExecutionException("Error executing query: " + e.Message, e);
			}
		}

		public void Describe(SelectableSource source, TextWriter output) {
			using (RdfWriter w = RdfWriter.Create(MimeType, output))
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

		public override void Run(SelectableSource source, QueryResultSink resultsink) {
			if (!(query is SelectQuery))
				throw new InvalidOperationException("Only SELECT queries are supported by this method (" + query.GetType() + ").");

			// Perform the query
			SelectQuery squery = (SelectQuery)query;
			RdfSourceWrapper sourcewrapper = BindLogic(source);

			RdfBindingSet results;
			try {
				results = squery.execute(sourcewrapper);
			} catch (name.levering.ryan.sparql.common.QueryException e) {
				throw new QueryExecutionException("Error executing query: " + e.Message, e);
			}
			
			// Prepare binding objects
			java.util.List vars = results.getVariables();
			SparqlVariable[] svars = new SparqlVariable[vars.size()];
			SemWebVariable[] vars2 = new SemWebVariable[vars.size()];
			for (int i = 0; i < svars.Length; i++) {
				svars[i] = (SparqlVariable)vars.get(i);
				vars2[i] = new SemWebVariable(svars[i].getName());
			}
			
			// Initialize the result sink
			resultsink.Init(vars2); // set distinct and ordered
			
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
				
				Resource[] bindings = new Resource[vars2.Length];

				for (int i = 0; i < bindings.Length; i++) {
					Resource r = sourcewrapper.ToResource(row.getValue(svars[i]));
					r = sourcewrapper.Persist(r);
					bindings[i] = r;
				}

				resultsink.AddComments(sourcewrapper.GetLog());
				
				resultsink.Add(new VariableBindings(vars2, bindings));

				ctr2++;
				if (ctr2 >= ReturnLimit && ReturnLimit != -1) break;
			}
			
			resultsink.AddComments(sourcewrapper.GetLog());
			
			// Close the result sink.
			resultsink.Finished();
		}
		
		/* INTERNAL METHODS TO CONTROL QUERY EXECUTION */
		
		private RdfSourceWrapper BindLogic(SelectableSource source) {
			RdfSourceWrapper sourcewrapper = new RdfSourceWrapper(source, QueryMeta, this);
			
			MyLogicFactory logic = new MyLogicFactory();
			foreach (RdfFunction f in extFunctions)
				logic.registerExternalFunction(
					new URIWrapper(f.Uri),
					new ExtFuncWrapper(sourcewrapper, f));
			
			query.prepare(sourcewrapper, logic);
			
			return sourcewrapper;
		}
		
		class MyLogicFactory : name.levering.ryan.sparql.logic.StreamedLogic {
		    public override name.levering.ryan.sparql.model.logic.ConstraintLogic getGroupConstraintLogic(name.levering.ryan.sparql.model.data.GroupConstraintData data) {
        		return new RdfGroupLogic(data, new name.levering.ryan.sparql.logic.streamed.IndexedSetIntersectLogic());
    		}
		}
	
		class RdfSourceWrapper : AdvancedRdfSource,
				SPARQLValueFactory {
				
			public readonly SelectableSource source;
			Hashtable bnodes = new Hashtable();
			Entity QueryMeta;
			SparqlEngine sparql;
			
			System.Text.StringBuilder log = new System.Text.StringBuilder();
			
			public RdfSourceWrapper(SelectableSource source, Entity meta, SparqlEngine sparql) {
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
		
			private java.util.Iterator GetIterator(Statement statement, bool defaultGraph, int limit) {
				return GetIterator(statement.Subject == null ? null : new Entity[] { statement.Subject },
					statement.Predicate == null ? null : new Entity[] { statement.Predicate },
					statement.Object == null ? null : new Resource[] { statement.Object },
					statement.Meta == null ? null : new Entity[] { statement.Meta },
					null,
					defaultGraph,
					limit);
			}
			
			private java.util.Iterator GetIterator(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, object[] litFilters, bool defaultGraph, int limit) {
				if (subjects == null && predicates == null && objects == null && limit == -1)
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
				if (limit == 0)
					filter.Limit = 1;
				else if (limit > 0)
					filter.Limit = limit;

				return new StatementIterator(source, filter, this, defaultGraph && metas == null);
			}
			
		    /**
		     * Gets all the statements that come from the default graph and have a
		     * certain subject, predicate, and object. Any of the parameters can be
		     * null, in which case it assumes these are "wildcards" and all statements
		     * that match the remainding parameters will be returned.
     		 */ 
     		public java.util.Iterator getDefaultStatements (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI predicate, name.levering.ryan.sparql.common.Value @object) {
				return GetIterator( new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), QueryMeta), true, -1 );
			}

     		public java.util.Iterator getDefaultStatements (name.levering.ryan.sparql.common.Value[] subject, name.levering.ryan.sparql.common.Value[] predicate, name.levering.ryan.sparql.common.Value[] @object, object[] litFilters, int limit) {
				return GetIterator( ToEntities(subject), ToEntities(predicate), ToResources(@object), QueryMeta == null ? null : new Entity[] { QueryMeta }, litFilters, true, limit );
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
     		public java.util.Iterator getStatements (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI predicate, name.levering.ryan.sparql.common.Value @object) {
				return GetIterator(  new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), null), false, -1 );
			}
	
     		public java.util.Iterator getStatements (name.levering.ryan.sparql.common.Value[] subject, name.levering.ryan.sparql.common.Value[] predicate, name.levering.ryan.sparql.common.Value[] @object, object[] litFilters, int limit) {
				return GetIterator(  ToEntities(subject), ToEntities(predicate), ToResources(@object), null, litFilters, false, limit );
     		}
     		
		    /**
		     * Gets all the statements that come from a particular named graph and have
		     * a certain subject, predicate, and object. Any of the parameters can be
		     * null, in which case it assumes these are "wildcards" and all statements
		     * that match the remainding parameters will be returned.
		     */
     		public java.util.Iterator getStatements (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI predicate, name.levering.ryan.sparql.common.Value @object, name.levering.ryan.sparql.common.URI graph) {
				return GetIterator( new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph)), false, -1 );
			}
			
     		public java.util.Iterator getStatements (name.levering.ryan.sparql.common.Value[] subject, name.levering.ryan.sparql.common.Value[] predicate, name.levering.ryan.sparql.common.Value[] @object, name.levering.ryan.sparql.common.URI[] graph, object[] litFilters, int limit) {
				return GetIterator( ToEntities(subject), ToEntities(predicate), ToResources(@object), ToEntities(graph), litFilters, false, limit );
     		}
     		
			public name.levering.ryan.sparql.common.SPARQLValueFactory getValueFactory() {
				return this;
			}
			
			private bool has(Statement statement) {
				bool ret = source.Contains(statement);
				Log("CONTAINS: " + statement + " ("  + ret + ")");
				return ret;
			}
			
			public bool hasDefaultStatement (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI @predicate, name.levering.ryan.sparql.common.Value @object) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), QueryMeta));
			}
			
			public bool hasStatement (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI @predicate, name.levering.ryan.sparql.common.Value @object) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), null));
			}
	
			public bool hasStatement (name.levering.ryan.sparql.common.Value subject, name.levering.ryan.sparql.common.URI @predicate, name.levering.ryan.sparql.common.Value @object, name.levering.ryan.sparql.common.URI graph) {
				return has(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph)));
			}
			
			public Entity ToEntity(name.levering.ryan.sparql.common.Value ent) {
				if (ent == null) return null;
				if (ent is BNodeWrapper) return ((BNodeWrapper)ent).r;
				if (ent is URIWrapper) return ((URIWrapper)ent).r;
				if (ent is name.levering.ryan.sparql.common.BNode) {
					name.levering.ryan.sparql.common.BNode bnode = (name.levering.ryan.sparql.common.BNode)ent;
					Entity r = (Entity)bnodes[bnode.getID()];
					if (r == null) {
						r = new BNode();
						bnodes[bnode.getID()] = r;
					}
					return r;
				} else if (ent is name.levering.ryan.sparql.common.URI) {
					name.levering.ryan.sparql.common.URI uri = (name.levering.ryan.sparql.common.URI)ent;
					return new Entity(uri.getURI());
				} else {
					return null;
				}
			}
			
			public Resource ToResource(name.levering.ryan.sparql.common.Value value) {
				if (value == null) return null;
				if (value is LiteralWrapper) return ((LiteralWrapper)value).r;
				if (value is name.levering.ryan.sparql.common.Literal) {
					name.levering.ryan.sparql.common.Literal literal = (name.levering.ryan.sparql.common.Literal)value;
					return new Literal(literal.getLabel(), literal.getLanguage(), literal.getDatatype() == null ? null : literal.getDatatype().getURI());
				} else {
					return ToEntity(value);
				}
			}
			
			public Entity[] ToEntities(name.levering.ryan.sparql.common.Value[] ents) {
				if (ents == null) return null;
				ArrayList ret = new ArrayList();
				for (int i = 0; i < ents.Length; i++)
					if (!(ents[i] is name.levering.ryan.sparql.common.Literal))
						ret.Add( ToEntity(ents[i]) );
				return (Entity[])ret.ToArray(typeof(Entity));
			}
			public Resource[] ToResources(name.levering.ryan.sparql.common.Value[] ents) {
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
						ret[i] = ToResource((name.levering.ryan.sparql.common.Value)ents[i]);
				}
				return ret;
			}
	
			public name.levering.ryan.sparql.common.Value createValue(name.levering.ryan.sparql.common.Value value) {
				throw new NotImplementedException();
			}
			public name.levering.ryan.sparql.common.BNode createBNode(name.levering.ryan.sparql.common.BNode value) {
				throw new NotImplementedException();
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(name.levering.ryan.sparql.common.Literal value) {
				throw new NotImplementedException();
			}
			public name.levering.ryan.sparql.common.URI createURI(name.levering.ryan.sparql.common.URI value) {
				throw new NotImplementedException();
			}
			
			public name.levering.ryan.sparql.common.BNode createBNode() {
				return new BNodeWrapper(new BNode());
			}
			public name.levering.ryan.sparql.common.BNode createBNode(string id) {
				return new BNodeWrapper(new BNode(id));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(string value, string lang) {
				return new LiteralWrapper(new Literal(value, lang, null));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(string value, name.levering.ryan.sparql.common.URI datatype) {
				return new LiteralWrapper(new Literal(value, null, datatype.getURI()));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(string value) {
				return new LiteralWrapper(new Literal(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(float value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(double value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(byte value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(short value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(int value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(long value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.Literal createLiteral(bool value) {
				return new LiteralWrapper(Literal.FromValue(value));
			}
			public name.levering.ryan.sparql.common.URI createURI(string ns, string ln) {
				return createURI(ns + ln);
			}
			public name.levering.ryan.sparql.common.URI createURI(string uri) {
				return new URIWrapper(new Entity(uri));
			}
			public name.levering.ryan.sparql.common.Statement createStatement (name.levering.ryan.sparql.common.Resource subject, name.levering.ryan.sparql.common.URI @predicate, name.levering.ryan.sparql.common.Value @object) {
				return new Stmt(subject, predicate, @object); 
			}
			
			class Stmt : name.levering.ryan.sparql.common.Statement {
				name.levering.ryan.sparql.common.Resource subject;
				name.levering.ryan.sparql.common.URI predicate;
				name.levering.ryan.sparql.common.Value @object;
				public Stmt(name.levering.ryan.sparql.common.Resource subject, name.levering.ryan.sparql.common.URI @predicate, name.levering.ryan.sparql.common.Value @object) {
					this.subject = subject;
					this.predicate = predicate;
					this.@object = @object;
				}
				public name.levering.ryan.sparql.common.Resource getSubject() { return subject; }
				public name.levering.ryan.sparql.common.URI getPredicate() { return predicate; }
				public name.levering.ryan.sparql.common.Value getObject() { return @object; }
				public bool equals(object other) {
					name.levering.ryan.sparql.common.Statement s = (name.levering.ryan.sparql.common.Statement)other;
					return getSubject().Equals(s.getSubject())
						&& getPredicate().Equals(s.getPredicate())
						&& getObject().Equals(s.getObject());
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
				if (!r.Uri.StartsWith(SparqlEngine.BNodePersistUri)) return r;
				
				StaticSource spb = (StaticSource)source;
				string uri = r.Uri;
				string id = uri.Substring(SparqlEngine.BNodePersistUri.Length);
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
				return new Entity(SparqlEngine.BNodePersistUri + ":" + id);
			}
			
			public static name.levering.ryan.sparql.common.Value Wrap(Resource res, Hashtable cache) {
				if (cache.ContainsKey(res))
					return (name.levering.ryan.sparql.common.Value)cache[res];
				name.levering.ryan.sparql.common.Value value = Wrap(res);
				cache[res] = value;
				return value;
			}

			public static name.levering.ryan.sparql.common.Value Wrap(Resource res) {
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
			
			Hashtable cache = new Hashtable();
		
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
				return new GraphStatementWrapper(statements[curindex], cache);
			}
			
			public void remove() {
				throw new InvalidOperationException();
			}
		}
		
		class GraphStatementWrapper : GraphStatement {
			public readonly Statement s;
			name.levering.ryan.sparql.common.Value S;
			name.levering.ryan.sparql.common.URI P;
			name.levering.ryan.sparql.common.Value O;
			name.levering.ryan.sparql.common.URI G;
			
			public GraphStatementWrapper(Statement statement, Hashtable cache) {
				s = statement;
				S = RdfSourceWrapper.Wrap(s.Subject, cache);
				if (s.Predicate.Uri == null)
					throw new QueryExecutionException("Statement's predicate is a blank node.");
				P = RdfSourceWrapper.Wrap(s.Predicate, cache) as name.levering.ryan.sparql.common.URI;
				O = RdfSourceWrapper.Wrap(s.Object, cache);
				G = RdfSourceWrapper.Wrap(s.Meta, cache) as name.levering.ryan.sparql.common.URI;
			}
			
			public name.levering.ryan.sparql.common.URI getGraphName() { return G; }
			
			public name.levering.ryan.sparql.common.Value getSubject() { return S; }
	
			public name.levering.ryan.sparql.common.URI getPredicate() { return P; }
				
			public name.levering.ryan.sparql.common.Value getObject() { return O; }
		}
		
		class BNodeWrapper : java.lang.Object, name.levering.ryan.sparql.common.BNode {
			public BNode r;
			public BNodeWrapper(BNode res) { r = res; }
			public string getID() {
				if (r.LocalName != null) return r.LocalName;
				return r.GetHashCode().ToString();
			}
			public override bool equals(object other) {
				if (other is BNodeWrapper)
					return r.Equals(((BNodeWrapper)other).r);
				if (other is name.levering.ryan.sparql.common.BNode)
					return getID().Equals(((name.levering.ryan.sparql.common.BNode)other).getID());
				return false;
			}
			public override int hashCode() {
					if (r.LocalName != null) java.lang.String.instancehelper_hashCode(getID());
					return r.GetHashCode();
			}
			public object getNative() { return r; }
		}
	
		class URIWrapper : java.lang.Object, name.levering.ryan.sparql.common.URI {
			public Entity r;
			int hc;
			public URIWrapper(Entity res) { r = res; hc = java.lang.String.instancehelper_hashCode(r.Uri); }
			public string getURI() { return r.Uri; }
			public override string toString() { return r.Uri; }
			public override bool equals(object other) {
				if (other is URIWrapper)
					return r.Equals(((URIWrapper)other).r);
				else if (other is name.levering.ryan.sparql.common.URI)
					return r.Uri == ((name.levering.ryan.sparql.common.URI)other).getURI();
				else
					return false;
			}
			public override int hashCode() { return hc; }
			public object getNative() { return r.Uri; }
		}
	
		class LiteralWrapper : java.lang.Object, name.levering.ryan.sparql.common.Literal {
			public Literal r;
			int hc;
			public LiteralWrapper(Literal res) { r = res; hc = java.lang.String.instancehelper_hashCode(r.Value); }
			public name.levering.ryan.sparql.common.URI getDatatype() { if (r.DataType == null) return null; return new URIWrapper(r.DataType); }
			public string getLabel() { return r.Value; }
			public string getLanguage() { return r.Language; }
			public override bool equals(object other) {
				if (other is LiteralWrapper)
					return r.Equals(((LiteralWrapper)other).r);
				else if (other is name.levering.ryan.sparql.common.Literal)
					return r.Equals(GetLiteral((name.levering.ryan.sparql.common.Literal)other));
				return false;
			}
			public override int hashCode() { return hc; }
			static Literal GetLiteral(name.levering.ryan.sparql.common.Literal literal) {
				return new Literal(literal.getLabel(), literal.getLanguage(),
					literal.getDatatype() == null ? null
						: literal.getDatatype().getURI());
			}
			public object getNative() { return r; }
		}
		
		class ExtFuncWrapper : name.levering.ryan.sparql.logic.function.ExternalFunctionFactory, name.levering.ryan.sparql.logic.function.ExternalFunction {
			RdfSourceWrapper source;
			RdfFunction func;
			
			public ExtFuncWrapper(RdfSourceWrapper s, RdfFunction f) {
				source = s;
				func = f;
			}
			
			public name.levering.ryan.sparql.logic.function.ExternalFunction create(name.levering.ryan.sparql.model.logic.LogicFactory logicfactory, name.levering.ryan.sparql.common.SPARQLValueFactory valuefactory) {
				return this;
			}

			public name.levering.ryan.sparql.common.Value evaluate(name.levering.ryan.sparql.model.logic.ExpressionLogic[] arguments, name.levering.ryan.sparql.common.RdfBindingRow binding) {
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
		    	java.util.Map knownValues, java.util.Map knownFilters, int limit) {
		    	
		    	RdfSourceWrapper s = (RdfSourceWrapper)source;
		    	
		    	if (s.source is QueryableSource) {
		    		QueryableSource qs = (QueryableSource)s.source;
		    		QueryOptions opts = new QueryOptions();
		    		
		    		opts.Limit = 0;
		    		if (limit == 0)
		    			opts.Limit = 1;
		    		else if (limit > 0)
		    			opts.Limit = limit;
		    		
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
		    			if (!(graph[i].Subject is Variable) && !(graph[i].Predicate is Variable) && !(graph[i].Object is Variable))
		    				return null; // we could use Contains(), but we'll just abandon the Query() path altogether
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
		    		
		    		// too expensive to do...
		    		//if (!qs.MetaQuery(graph, opts).QuerySupported)
		    		//	return null; // TODO: We could also check if any part has NoData, we can abandon the query entirely 
		    		
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
		    	
				public override void Init(Variable[] variables) {
					java.util.ArrayList vars = new java.util.ArrayList();
					foreach (Variable b in variables)
						if (varMap[b] != null) // because of bad treatment of meta
							vars.add((SparqlVariable)varMap[b]);
					
					bindings = new RdfBindingSetImpl(vars);
				}
				
				public override bool Add(VariableBindings result) {
					RdfBindingRowImpl row = new RdfBindingRowImpl(bindings);
					for (int i = 0; i < result.Count; i++) {
						if (varMap[result.Variables[i]] == null) continue; // because of the bad treatment of meta
						row.addBinding( (SparqlVariable)varMap[result.Variables[i]], RdfSourceWrapper.Wrap(result.Values[i]) );
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
		    		
			    		if (knownValues != null && knownValues.get(expr) != null) {
				    		java.util.Set values = (java.util.Set)knownValues.get(expr);
	                        VarKnownValuesList values2 = new VarKnownValuesList();
				    		for (java.util.Iterator iter = values.iterator(); iter.hasNext(); ) {
				    			Resource r = src.ToResource((name.levering.ryan.sparql.common.Value)iter.next());
				    			if (r != null)
				    				values2.Add(r);
				    		}
				    		
				    		opts.VariableKnownValues[v] = values2;
				    	}
				    	
				    	if (!(expr is name.levering.ryan.sparql.common.BNode))
				    		((VariableList)opts.DistinguishedVariables).Add(v);
			    	}
		    		return v;
		    	}
		    	
	    		return entities ? src.ToEntity((name.levering.ryan.sparql.common.Value)expr) : src.ToResource((name.levering.ryan.sparql.common.Value)expr);
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
					name.levering.ryan.sparql.common.Literal val;
					
					object left = RemoveCast(b.getLeftExpression());
					object right = RemoveCast(b.getRightExpression());
					
					if (left is ASTVar && right is name.levering.ryan.sparql.common.Literal) {
						var = (SparqlVariable)left;
						val = (name.levering.ryan.sparql.common.Literal)right;
					} else if (right is ASTVar && left is name.levering.ryan.sparql.common.Literal) {
						var = (SparqlVariable)right;
						val = (name.levering.ryan.sparql.common.Literal)left;
						switch (comp) {
						case LiteralFilter.CompType.LT: comp = LiteralFilter.CompType.GE; break;
						case LiteralFilter.CompType.LE: comp = LiteralFilter.CompType.GT; break;
						case LiteralFilter.CompType.GT: comp = LiteralFilter.CompType.LE; break;
						case LiteralFilter.CompType.GE: comp = LiteralFilter.CompType.LT; break;
						}
					} else {
						return;
					}
					
					object parsedvalue = new Literal(val.getLabel(), null, val.getDatatype() == null ? null : val.getDatatype().getURI()).ParseValue();
					
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
		    			return new LiteralWrapper(new Literal((-doublevalue).ToString(), null, ((ASTLiteral)inside).getDatatype().getURI()));
		    		}
		    	}
		    	return node;
		    }
		}
		
		class TestFunction : RdfFunction {
			public override string Uri { get { return "http://taubz.for.net/code/semweb/test/function"; } }
			public override Resource Evaluate(Resource[] args) {
				return Literal.FromValue(args.Length == 2 && args[0].Equals(args[1]));
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
