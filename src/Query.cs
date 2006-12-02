using System;
using System.IO;

using SemWeb;
using SemWeb.Filters;
using SemWeb.Stores;
using SemWeb.Util;

#if !DOTNET2
using System.Collections;
#else
using System.Collections.Generic;
#endif

namespace SemWeb.Query {

	public struct QueryOptions {
		public int Limit; // 0 means no limit, otherwise the maximum number of results to give
		
		#if !DOTNET2
		public ICollection DistinguishedVariables; // if null, all variables are reported back in bindings; otherwise, a list of just the variables whose bindings are to be reported
		public IDictionary VariableKnownValues; // a map from variables to lists of values that the variable must be drawn from
		public IDictionary VariableLiteralFilters; // a map from variables to lists of literal value filters that its values must match
		#else
		public ICollection<Variable> DistinguishedVariables;
		public IDictionary<Variable,ICollection<Resource>> VariableKnownValues;
		public IDictionary<Variable,ICollection<LiteralFilter>> VariableLiteralFilters;
		#endif
	}
	
	public struct MetaQueryResult {
		public bool QuerySupported;
		public bool[] NoData;
		public bool[] IsDefinitive;
		public bool IsDefaultImplementation;
	}
	

	public class QueryFormatException : ApplicationException {
		public QueryFormatException(string message) : base(message) { }
		public QueryFormatException(string message, Exception cause) : base(message, cause) { }
	}

	public class QueryExecutionException : ApplicationException {
		public QueryExecutionException(string message) : base(message) { }
		public QueryExecutionException(string message, Exception cause) : base(message, cause) { }
	}
	
	public abstract class RdfFunction {
		public abstract string Uri { get; }
		public abstract Resource Evaluate(Resource[] args);	
	
	}

	public abstract class Query {
		int start = 0;
		int limit = -1;
		Entity queryMeta = null;
		
		public int ReturnStart { get { return start; } set { start = value; if (start < 0) start = 0; } }
		
		public int ReturnLimit { get { return limit; } set { limit = value; } }
		
		public Entity QueryMeta { get { return queryMeta; } set { queryMeta = value; } }
		
		public virtual void Run(SelectableSource source, TextWriter output) {
			Run(source, new SparqlXmlQuerySink(output));
		}

		public abstract void Run(SelectableSource source, QueryResultSink resultsink);

		public abstract string GetExplanation();
	}

	public abstract class QueryResultSink {
		public virtual void Init(VariableBinding[] variables, bool distinct, bool ordered) {
		}
		
		public abstract bool Add(VariableBinding[] result);

		public virtual void Finished() {
		}
		
		public virtual void AddComments(string comments) {
		}
	}
	
	internal class QueryResultBufferSink : QueryResultSink {
		#if !DOTNET2
		public ArrayList Bindings = new ArrayList();
		#else
		public List<VariableBinding[]> Bindings = new List<VariableBinding[]>();
		#endif
		public override bool Add(VariableBinding[] result) {
			VariableBinding[] clone = new VariableBinding[result.Length];
			result.CopyTo(clone, 0);
			Bindings.Add(clone);
			return true;
		}
	}

	public struct VariableBinding {
		Variable v;
		Resource t;
		
		public VariableBinding(Variable variable, Resource target) {
			v = variable;
			t = target;
		}
		
		public Variable Variable { get { return v; } set { v = value; } }
		public string Name { get { return v.LocalName; } }
		public Resource Target { get { return t; } set { t = value; } }

		public static Statement Substitute(VariableBinding[] variables, Statement template) {
			// This may throw an InvalidCastException if a variable binds
			// to a literal but was used as the subject, predicate, or meta
			// of the template.
			foreach (VariableBinding v in variables) {
				if (v.Variable == template.Subject) template = new Statement((Entity)v.Target, template.Predicate, template.Object, template.Meta);
				if (v.Variable == template.Predicate) template = new Statement(template.Subject, (Entity)v.Target, template.Object, template.Meta);
				if (v.Variable == template.Object) template = new Statement(template.Subject, template.Predicate, v.Target, template.Meta);
				if (v.Variable == template.Meta) template = new Statement(template.Subject, template.Predicate, template.Object, (Entity)v.Target);
			}
			return template;
		}
		
		internal static string ToString(VariableBinding[] bindings) {
			String ret = "";
			foreach (VariableBinding b in bindings) {
				ret += b.Variable + "=>" + b.Target + "; ";
			}
			return ret;
		}
	}
}

