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

#if !DOTNET2
using ResList = System.Collections.ICollection;
using LitFilterMap = System.Collections.Hashtable;
using LitFilterList = System.Collections.ArrayList;
#else
using ResList = System.Collections.Generic.ICollection<SemWeb.Resource>;
using LitFilterMap = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.LiteralFilter>>;
using LitFilterList = System.Collections.Generic.List<SemWeb.LiteralFilter>;
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
		
		public void SetVariableKnownValues(Variable variable, ResList knownValues) {
			if (VariableKnownValues == null)
			#if !DOTNET2
				VariableKnownValues = new Hashtable();
			#else
				VariableKnownValues = new Dictionary<Variable,ICollection<Resource>>();
			#endif
			
			VariableKnownValues[variable] = knownValues;
		}
		
		public void AddLiteralFilter(Variable variable, LiteralFilter filter) {
			if (VariableLiteralFilters == null)
				VariableLiteralFilters = new LitFilterMap();
			LitFilterList list = (LitFilterList)VariableLiteralFilters[variable];
			if (list == null) {
			 	list  = new LitFilterList();
				VariableLiteralFilters[variable] = list;
			}
			list.Add(filter);
		}
	}
	
	public struct MetaQueryResult {
		public bool QuerySupported;
		public bool[] NoData;
		public bool[] IsDefinitive;
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
		public virtual void Init(Variable[] variables, bool distinct, bool ordered) {
		}
		
		public abstract bool Add(VariableBindings result);

		public virtual void Finished() {
		}
		
		public virtual void AddComments(string comments) {
		}
	}
	
	public class QueryResultBuffer : QueryResultSink {
		Variable[] variables;

		#if !DOTNET2
		ArrayList bindings = new ArrayList();
		#else
		List<VariableBindings> bindings = new List<VariableBindings>();
		#endif

		public override void Init(Variable[] variables, bool distinct, bool ordered) {
			this.variables = new Variable[variables.Length];
			variables.CopyTo(this.variables, 0);
		}

		public override bool Add(VariableBindings result) {
			bindings.Add(result);
			return true;
		}
		
		public Variable[] Variables { get { return variables; } }

		#if !DOTNET2
		public IList Bindings { get { return bindings; } }
		#else
		public List<VariableBindings> Bindings { get { return bindings; } }
		#endif
	}
	
	public class VariableBindings {
		Variable[] vars;
		Resource[] vals;

		public VariableBindings(Variable[] vars, Resource[] vals) {
			this.vars = vars;
			this.vals = vals;
			if (vars.Length != vals.Length) throw new ArgumentException("Arrays do not have the same length.");
		}
		
		public int Count { get { return vars.Length; } }
		
		#if !DOTNET2
		public Variable[] Variables { get { return vars; } }
		public Resource[] Values { get { return vals; } }
		#else
		public IList<Variable> Variables { get { return vars; } }
		public IList<Resource> Values { get { return vals; } }
		#endif
		
		public Resource this[Variable variable] {
			get {
				for (int i = 0; i < vars.Length; i++)
					if (vars[i] == variable)
						return vals[i];
				throw new ArgumentException();
			}
		}

		public Resource this[string variableName] {
			get {
				for (int i = 0; i < vars.Length; i++)
					if (vars[i].LocalName != null && vars[i].LocalName == variableName)
						return vals[i];
				throw new ArgumentException();
			}
		}
		
		public Statement Substitute(Statement template) {
			// This may throw an InvalidCastException if a variable binds
			// to a literal but was used as the subject, predicate, or meta
			// of the template.
			for (int i = 0; i < vars.Length; i++) {
				if (vars[i] == template.Subject) template = new Statement((Entity)vals[i], template.Predicate, template.Object, template.Meta);
				if (vars[i] == template.Predicate) template = new Statement(template.Subject, (Entity)vals[i], template.Object, template.Meta);
				if (vars[i] == template.Object) template = new Statement(template.Subject, template.Predicate, vals[i], template.Meta);
				if (vars[i] == template.Meta) template = new Statement(template.Subject, template.Predicate, template.Object, (Entity)vals[i]);
			}
			return template;
		}
		
		public override string ToString() {
			String ret = "";
			for (int i = 0; i < vars.Length; i++) {
				ret += vars[i] + "=>" + vals[i] + "; ";
			}
			return ret;
		}
	}
}

