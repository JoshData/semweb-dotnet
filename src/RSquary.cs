using System;
using System.Collections;
using System.IO;
using System.Text;

using SemWeb;
using SemWeb.Stores;

namespace SemWeb.Query {
	public class RSquary : GraphMatch {
		
		// TODO: Optional statements
		// TODO: Grouping and disjunctions
		
		public static Entity qName = "http://purl.oclc.org/NET/rsquary/name";
		public static Entity qLimit = "http://purl.oclc.org/NET/rsquary/returnLimit";
		public static Entity qStart = "http://purl.oclc.org/NET/rsquary/returnStart";
		public static Entity qDistinctFrom = "http://purl.oclc.org/NET/rsquary/distinctFrom";
		public static Entity qOptional = "http://purl.oclc.org/NET/rsquary/optional";
		
		public RSquary(RdfReader query) :
			this(new MemoryStore(query),
				query.BaseUri == null ? null : new Entity(query.BaseUri),
				query.Variables, null) {
		}

		public RSquary(Store queryModel, Entity queryNode) : this(queryModel, queryNode, null, null) {
		}
		
		public RSquary(Store queryModel, Entity queryNode, IDictionary variableNames, IDictionary extraValueFilters) {
			// Find the query options
			if (queryNode != null) {
				ReturnStart = GetIntOption(queryModel, queryNode, qStart);
				ReturnLimit = GetIntOption(queryModel, queryNode, qLimit);
			}

			// Search the query for variable names
			foreach (Statement s in queryModel.Select(new Statement(null, qName, null))) {
				if (s.Object is Entity) throw new QueryException("Variable names must be literals.");
				SetVariableName(s.Subject, ((Literal)s.Object).Value);
			}
			
			if (variableNames != null) {
				foreach (DictionaryEntry entry in variableNames)
					SetVariableName((Entity)entry.Key, (string)entry.Value);
			}
			
			// Search the query for 'distinct' predicates between variables.
			foreach (Statement s in queryModel.Select(new Statement(null, qDistinctFrom, null))) {
				if (!(s.Object is Entity)) throw new QueryException("The distinctFrom predicate cannot have a literal as its object.");
				MakeDistinct(s.Subject, (Entity)s.Object);
			}
			
			// Add all statements except the query predicates and value filters into a
			// new store with just the statements relevant to the search.
			foreach (Statement s in queryModel.Select(Statement.All)) {
				if (IsQueryPredicate(s.Predicate)) continue;
				
				if (s.Predicate.Uri != null && extraValueFilters != null && extraValueFilters.Contains(s.Predicate.Uri)) {
					ValueFilterFactory f = (ValueFilterFactory)extraValueFilters[s.Predicate.Uri];
					AddValueFilter(s.Subject, f.GetValueFilter(s.Predicate.Uri, s.Object));
					continue;
				} else {
					ValueFilter f = ValueFilter.GetValueFilter(s.Predicate, s.Object);
					if (f != null) {
						AddValueFilter(s.Subject, f);
						continue;
					}
				}
				
				if (s.Meta == Statement.DefaultMeta)
					AddEdge(s);
				else if (queryNode != null && queryModel.Contains(new Statement(queryNode, qOptional, s.Meta)))
					AddOptionalEdge(s);
			}
		}
		
		private int GetIntOption(Store queryModel, Entity query, Entity predicate) {
			Resource[] rr = queryModel.SelectObjects(query, predicate);
			if (rr.Length == 0) return -1;
			Resource r = rr[0];
			if (r == null || !(r is Literal)) return -1;
			try {
				return int.Parse(((Literal)r).Value);
			} catch (Exception e) {
				throw new QueryException("Invalid integer value for <" + predicate + ">, '" + ((Literal)r).Value + "'.", e);
			}
		}		

		private bool IsQueryPredicate(Entity e) {
			if (e == qName) return true;
			if (e == qDistinctFrom) return true;
			if (e == qLimit) return true;
			if (e == qStart) return true;
			if (e == qOptional) return true;
			return false;
		}
	}

	public class SparqlXmlQuerySink : QueryResultSink {
		System.Xml.XmlWriter output;
		
		int blankNodeCounter = 0;
		Hashtable blankNodes = new Hashtable();
		
		private static System.Xml.XmlWriter GetWriter(System.IO.TextWriter writer) {
			System.Xml.XmlTextWriter w = new System.Xml.XmlTextWriter(writer);
			w.Formatting = System.Xml.Formatting.Indented;
			return w;
		}
		
		public SparqlXmlQuerySink(TextWriter output)
		 : this(GetWriter(output)) {
		}

		public SparqlXmlQuerySink(System.Xml.XmlWriter output) {
			this.output = output;
		}
		
		public override void Init(VariableBinding[] variables) {
			output.WriteStartElement("sparql");
			output.WriteAttributeString("xmlns", "http://www.w3.org/2001/sw/DataAccess/rf1/result");
			output.WriteStartElement("head");
			foreach (VariableBinding var in variables) {
				if (var.Name == null) continue;
				output.WriteStartElement("variable");
				output.WriteAttributeString("name", var.Name);
				output.WriteEndElement();
			}
			output.WriteEndElement(); // head
			output.WriteStartElement("results");
		}
		
		public override bool Add(VariableBinding[] result) {
			output.WriteStartElement("result");
			foreach (VariableBinding var in result) {
				if (var.Name == null) continue;
				
				output.WriteStartElement(var.Name);
				if (var.Target == null) {
					output.WriteAttributeString("bound", "false");
				} else if (var.Target.Uri != null) {
					output.WriteAttributeString("uri", var.Target.Uri);
				} else if (var.Target is Literal) {
					Literal literal = (Literal)var.Target;
					if (literal.DataType != null)
						output.WriteAttributeString("datatype", literal.DataType);
					if (literal.Language != null)
						output.WriteAttributeString("language", literal.Language);
					output.WriteString(literal.Value);				
				} else {
					string id;
					if (blankNodes.ContainsKey(var.Target))
						id = (string)blankNodes[var.Target];
					else {
						id = "r" + (++blankNodeCounter);
						blankNodes[var.Target] = id;
					}
					output.WriteAttributeString("bnodeid", id);
				}
				
				output.WriteEndElement();
			}
			output.WriteEndElement();
			
			return true;
		}
		
		public override void Finished() {
			output.WriteEndElement(); // results
			output.WriteEndElement(); // sparql
			output.Close();
		}
	}

}	

