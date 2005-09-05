using System;
using System.Collections;
using System.IO;

using SemWeb;
using SemWeb.Stores;

using name.levering.ryan.sparql.parser;
using name.levering.ryan.sparql.model;
using name.levering.ryan.sparql.common;

namespace SemWeb.Query {

	public class Sparql {

		name.levering.ryan.sparql.model.Query query;
	
		public Sparql(string query) {
			try {
				this.query = SPARQLParser.parse(new java.io.StringReader(query));
			} catch (ParseException e) {
				throw new Exception("SPARQL syntax error at: " + e.currentToken);
			}
			if (!(query is SelectQuery))
				throw new NotSupportedException("Only SELECT queries are supported at the moment.");
		}
	
		public void Query(QueryableSource source, QueryResultSink resultsink) {
			SelectQuery squery = (SelectQuery)query;
			RdfSourceWrapper sourcewrapper = new RdfSourceWrapper(source);
			RdfBindingSet results = squery.execute(sourcewrapper);
			
			java.util.List vars = results.getVariables();
			VariableBinding[] bindings = new VariableBinding[vars.size()];
			for (int i = 0; i < bindings.Length; i++) {
				Variable v = (Variable)vars.get(i);
				bindings[i] = new VariableBinding(new Entity(null), v.getName(), null);
			}
			resultsink.Init(bindings);
			
			java.util.Iterator iter = results.iterator();
			while (iter.hasNext()) {
				RdfBindingRow row = (RdfBindingRow)iter.next();
				for (int i = 0; i < bindings.Length; i++) {
					Variable v = (Variable)row.getVariables().get(i);
					bindings[i] = new VariableBinding(
						bindings[i].Variable, v.getName(),
						sourcewrapper.ToResource(row.getValue(v)));
				}
				resultsink.Add(bindings);
			}
			
			resultsink.Finished();
		}
	
		class RdfSourceWrapper : RdfSource, org.openrdf.model.ValueFactory {
			QueryableSource source;
			Hashtable bnodes = new Hashtable();
			
			public RdfSourceWrapper(QueryableSource source) {
				this.source = source;
			}
		
			public java.util.Iterator getDefaultStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object) {
				// What is this method supposed to do?
				return getStatements(subject, predicate, @object);
			}
			
			public java.util.Iterator getStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object) {
				Statement statement = new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object));
				MemoryStore results = new MemoryStore();
				source.Select(statement, results);
				return new StatementIterator(results.ToArray());
			}
	
			public java.util.Iterator getStatements (org.openrdf.model.Value subject, org.openrdf.model.URI predicate, org.openrdf.model.Value @object, org.openrdf.model.URI graph) {
				Statement statement = new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph));
				MemoryStore results = new MemoryStore();
				source.Select(statement, results);
				return new StatementIterator(results.ToArray());
			}
			
			public org.openrdf.model.ValueFactory getValueFactory() {
				return this;
			}
			
			public bool hasDefaultStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				// What is this method supposed to do?
				return source.Contains(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object)));
			}
			
			public bool hasStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				return source.Contains(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object)));
			}
	
			public bool hasStatement (org.openrdf.model.Value subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object, org.openrdf.model.URI graph) {
				return source.Contains(new Statement(ToEntity(subject), ToEntity(predicate), ToResource(@object), ToEntity(graph)));
			}
	
			public Entity ToEntity(org.openrdf.model.Value ent) {
				if (ent == null) return null;
				if (ent is org.openrdf.model.BNode) {
					org.openrdf.model.BNode bnode = (org.openrdf.model.BNode)ent;
					Entity r = (Entity)bnodes[bnode.getID()];
					if (r == null) {
						r = new Entity(null);
						bnodes[bnode.getID()] = r;
					}
					return r;
				} else {
					org.openrdf.model.URI uri = (org.openrdf.model.URI)ent;
					return new Entity(uri.toString());
				}
			}
			
			public Resource ToResource(org.openrdf.model.Value value) {
				if (value == null) return null;
				if (value is org.openrdf.model.Literal) {
					org.openrdf.model.Literal literal = (org.openrdf.model.Literal)value;
					return new Literal(literal.getLabel(), literal.getLanguage(), literal.getDatatype() == null ? null : literal.getDatatype().toString());
				} else {
					return ToEntity(value);
				}
			}
	
			public org.openrdf.model.BNode createBNode() {
				return new BNodeWrapper(new Entity(null));
			}
			public org.openrdf.model.BNode createBNode(string id) {
				return new BNodeWrapper(new Entity(null));
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
			public org.openrdf.model.URI createURI(string ns, string ln) {
				return new URIWrapper(new Entity(ns + ln));
			}
			public org.openrdf.model.URI createURI(string uri) {
				return new URIWrapper(new Entity(uri));
			}
			public org.openrdf.model.Statement createStatement (org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
				return new Stmt(subject, predicate, @object); 
			}
			
			class Stmt : org.openrdf.model.Statement {
				org.openrdf.model.Resource subject;
				org.openrdf.model.URI predicate;
				org.openrdf.model.Value @object;
				public Stmt(org.openrdf.model.Resource subject, org.openrdf.model.URI @predicate, org.openrdf.model.Value @object) {
					this.subject = subject;
					this.predicate = predicate;
					this.@object = @object;
				}
				public org.openrdf.model.Resource getSubject() { return subject; }
				public org.openrdf.model.URI getPredicate() { return predicate; }
				public org.openrdf.model.Value getObject() { return @object; }
			}
		}
		
		class StatementIterator : java.util.Iterator {
			Statement[] statements;
			int curindex = -1;
			
			public StatementIterator(Statement[] statements) {
				this.statements = statements;
			}
			
			public bool hasNext() {
				return curindex + 1 < statements.Length;
			}
			
			public object next() {
				curindex++;
				return new GraphStatementWrapper(statements[curindex]);
			}
			
			public void remove() {
			}
		}
		
		class GraphStatementWrapper : GraphStatement {
			Statement s;
			public GraphStatementWrapper(Statement statement) {
				s = statement;
			}
			
			public org.openrdf.model.URI getGraphName() {
				return new URIWrapper(s.Meta);
			}
			
			public org.openrdf.model.Value getSubject() {
				if (s.Subject.Uri == null)
					return new BNodeWrapper(s.Subject);
				else
					return new URIWrapper(s.Subject);
			}
	
			public org.openrdf.model.URI getPredicate() {
				if (s.Predicate.Uri == null)
					throw new NotSupportedException("Statement's predicate is a blank node.");
				return new URIWrapper(s.Predicate);
			}
	
			public org.openrdf.model.Value getObject() {
				if (s.Object is Literal)
					return new LiteralWrapper((Literal)s.Object);
				else if (s.Object.Uri == null)
					return new BNodeWrapper((Entity)s.Object);
				else
					return new URIWrapper((Entity)s.Object);
			}
		}
		
		class BNodeWrapper : org.openrdf.model.BNode {
			public Entity r;
			public BNodeWrapper(Entity res) { r = res; }
			public string getID() { throw new NotSupportedException(); }
			public override bool Equals(object other) {
				return r.Equals(((BNodeWrapper)other).r);
			}
			public override int GetHashCode() { return r.GetHashCode(); } // just suppresses a compiler warning
		}
	
		class URIWrapper : org.openrdf.model.URI {
			public Entity r;
			public URIWrapper(Entity res) { r = res; }
			public string getLocalName() { return ""; }
			public string getNamespace() { return r.Uri; }
			public string toString() { return r.Uri; }
			public override bool Equals(object other) {
				return r.Equals(((URIWrapper)other).r);
			}
			public override int GetHashCode() { return r.GetHashCode(); } // just suppresses a compiler warning
		}
	
		class LiteralWrapper : org.openrdf.model.Literal {
			public Literal r;
			public LiteralWrapper(Literal res) { r = res; }
			public org.openrdf.model.URI getDatatype() { return new URIWrapper(r.DataType); }
			public string getLabel() { return r.Value; }
			public string getLanguage() { return r.Language; }
			public override bool Equals(object other) {
				return r.Equals(((LiteralWrapper)other).r);
			}
			public override int GetHashCode() { return r.GetHashCode(); } // just suppresses a compiler warning
		}
	}
}
