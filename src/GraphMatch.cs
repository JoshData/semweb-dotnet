using System;
using System.IO;

#if !DOTNET2
using System.Collections;
#else
using System.Collections.Generic;
#endif

using SemWeb;
using SemWeb.Filters;
using SemWeb.Stores;
using SemWeb.Util;

#if !DOTNET2
using VariableList = System.Collections.ArrayList;
using BindingList = System.Collections.ArrayList;
using VarKnownValuesType = System.Collections.Hashtable;
using VarKnownValuesType2 = System.Collections.IDictionary;
using LitFilterList = System.Collections.ArrayList;
using LitFilterMap = System.Collections.Hashtable;
using LitFilterMap2 = System.Collections.IDictionary;
#else
using VariableList = System.Collections.Generic.List<SemWeb.Variable>;
using BindingList = System.Collections.Generic.List<SemWeb.Query.VariableBinding[]>;
using VarKnownValuesType = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.Resource>>;
using VarKnownValuesType2 = System.Collections.Generic.IDictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.Resource>>;
using LitFilterList = System.Collections.Generic.List<SemWeb.LiteralFilter>;
using LitFilterMap = System.Collections.Generic.Dictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.LiteralFilter>>;
using LitFilterMap2 = System.Collections.Generic.IDictionary<SemWeb.Variable,System.Collections.Generic.ICollection<SemWeb.LiteralFilter>>;
#endif

namespace SemWeb.Query {

	public class GraphMatch : Query {
		private static Entity qLimit = "http://purl.oclc.org/NET/rsquary/returnLimit";
		private static Entity qStart = "http://purl.oclc.org/NET/rsquary/returnStart";
		private static Entity qDistinctFrom = "http://purl.oclc.org/NET/rsquary/distinctFrom";
		private static Entity qOptional = "http://purl.oclc.org/NET/rsquary/optional";
		
		StatementList graph = new StatementList();
		VarKnownValuesType2 knownValues = new VarKnownValuesType();
		LitFilterMap litFilters = new LitFilterMap();
	
		public GraphMatch() {
		}
		
		public GraphMatch(RdfReader query) :
			this(new MemoryStore(query),
				query.BaseUri == null ? null : new Entity(query.BaseUri)) {
		}

		public GraphMatch(Store queryModel) : this(queryModel, null) {
		}
		
		private GraphMatch(Store queryModel, Entity queryNode) {
			// Find the query options
			if (queryNode != null) {
				ReturnStart = GetIntOption(queryModel, queryNode, qStart);
				ReturnLimit = GetIntOption(queryModel, queryNode, qLimit);
			}

			foreach (Statement s in queryModel.Select(Statement.All)) {
				if (IsQueryPredicate(s.Predicate)) continue;
				
				if (s.Meta == Statement.DefaultMeta)
					AddGraphStatement(s);
				else
					throw new NotSupportedException("Subgraphs (meta statement relations) are not supported.");
			}
		}
		
		private int GetIntOption(Store queryModel, Entity query, Entity predicate) {
			Resource[] rr = queryModel.SelectObjects(query, predicate);
			if (rr.Length == 0) return -1;
			Resource r = rr[0];
			if (r == null || !(r is Literal)) return -1;
			try {
				return int.Parse(((Literal)r).Value);
			} catch (Exception) {
				return -1;
			}
		}		

		private bool IsQueryPredicate(Entity e) {
			if (e == qDistinctFrom) return true;
			if (e == qLimit) return true;
			if (e == qStart) return true;
			if (e == qOptional) return true;
			return false;
		}
		
		public override string GetExplanation() {
			string ret = "Query:\n";
			foreach (Statement s in graph)
				ret += " " + s + "\n";
			return ret;
		}
		
		public void AddGraphStatement(Statement statement) {
			graph.Add(statement);
		}
		
		#if !DOTNET2
		public void SetVariableRange(Variable variable, ICollection range) {
		#else
		public void SetVariableRange(Variable variable, ICollection<Resource> range) {
		#endif
			knownValues[variable] = range;
		}
		
		public void AddLiteralFilter(Variable variable, LiteralFilter filter) {
			if (litFilters[variable] == null)
				litFilters[variable] = new LitFilterList();
			((LitFilterList)litFilters[variable]).Add(filter);
		}
		
		public override void Run(SelectableSource targetModel, QueryResultSink result) {
			QueryPart[] parts = new QueryPart[graph.Count];
			for (int i = 0; i < graph.Count; i++)
				parts[i] = new QueryPart(graph[i], targetModel);
			
			RunGeneralQuery(parts, knownValues, litFilters, ReturnStart, ReturnLimit, result);
		}
		
		internal struct QueryPart {
			public readonly Statement[] Graph;
			public readonly SelectableSource[] Sources;
			
			public QueryPart(Statement s, SelectableSource src) {
				Graph = new Statement[] { s };
				Sources = new SelectableSource[] { src };
			}

			public QueryPart(Statement s, SelectableSource[] sources) {
				Graph = new Statement[] { s };
				Sources = sources;
			}

			public QueryPart(Statement[] graph, QueryableSource src) {
				Graph = graph;
				Sources = new SelectableSource[] { src };
			}
		}
		
		struct BindingSet {
			public VariableList Variables;
			public BindingList Rows;
		}
		
		internal static void RunGeneralQuery(QueryPart[] queryParts,
				VarKnownValuesType2 knownValues, LitFilterMap2 litFilters,
				int returnStart, int returnLimit,
				QueryResultSink result) {
				
			BindingSet bindings = new BindingSet();
			bindings.Variables = new VariableList();
			bindings.Rows = new BindingList();
			bindings.Rows.Add(null); // we need a dummy row for the first intersection
			
			foreach (QueryPart part in queryParts) {
				// Get the statements in the target model that match this aspect of the query graph.
				
				// get a list of values already found for each variable
				System.Collections.Hashtable foundValues = new System.Collections.Hashtable();
				foreach (Variable v in bindings.Variables)
					foundValues[v] = new ResSet();
				foreach (VariableBinding[] row in bindings.Rows) {
					if (row == null) continue; // null in first round
					foreach (VariableBinding b in row)
						((ResSet)foundValues[b.Variable]).Add(b.Target);
				}
				foreach (Variable v in bindings.Variables)
					foundValues[v] = ((ResSet)foundValues[v]).ToArray();
				
				// matches holds the bindings that match this part of the query
				BindingList matches;

				// vars holds an ordered list of variables found in this part of the query
				VariableList vars;
				
				// A QueryPart can either be:
				//	A single statement to match against one or more SelectableSources
				//  A graph of statements to match against a single QueryableSource
				
				if (part.Graph.Length == 1) {
					Statement s = part.Graph[0];
					
					matches = new BindingList();
					vars = new VariableList();
					
					// get a list of variables in this part
					// the filter will have null components for variables, except
					// for variables with known values, we plug those values in
					SelectFilter f = new SelectFilter(s);
					for (int i = 0; i < 4; i++) {
						Resource r = s.GetComponent(i);
					
						if (r is Variable) {
							Variable v = (Variable)r;
							
							if (!vars.Contains(v))
								vars.Add(v);
							
							Resource[] values = (Resource[])foundValues[v];
							if (values == null && knownValues[v] != null)
								#if !DOTNET2
								values = (Resource[])new ArrayList((ICollection)knownValues[v]).ToArray(typeof(Resource));
								#else
								values = new List<Resource>(knownValues[v]).ToArray();
								#endif
							
							if (values == null) {
								f.SetComponent(i, null);
							} else if (i != 2) {
								bool fail = false;
								f.SetComponent(i, ToEntArray(values, ref fail));
								if (fail) return;
							} else {
								f.SetComponent(i, values);
							}
						}
					}
					
					if (s.Object is Variable && litFilters[(Variable)s.Object] != null)
					#if !DOTNET2
						f.LiteralFilters = (LiteralFilter[])((LitFilterList)litFilters[(Variable)s.Object]).ToArray(typeof(LiteralFilter));
					#else
						f.LiteralFilters = ((LitFilterList)litFilters[(Variable)s.Object]).ToArray();
					#endif

					// get the matching statements; but if a variable was used twice in s,
					// filter out matching statements that don't respect that (since that info
					// was lost in the SelectFilter).
					foreach (SelectableSource source in part.Sources)
						source.Select(f, new Filter(matches, s, vars));
						
					result.AddComments("SELECT: " + f + " => " + matches.Count);
				
				} else {
					
					// build a query
					
					QueryOptions opts = new QueryOptions();
					
					if (knownValues != null) {
						foreach (Variable v in knownValues.Keys)
							#if !DOTNET2
							opts.SetVariableKnownValues(v, (System.Collections.ICollection)knownValues[v]);
							#else
							opts.SetVariableKnownValues(v, knownValues[v]);
							#endif
					}
					foreach (Variable v in foundValues.Keys)
						if (foundValues[v] != null)
							opts.SetVariableKnownValues(v, (Resource[])foundValues[v]);
					foreach (Variable v in litFilters.Keys)
						if (litFilters[v] != null)
							foreach (LiteralFilter f in (System.Collections.ICollection)litFilters[v]) 
								opts.AddLiteralFilter(v, f);
					
					QueryResultBufferSink partsink = new QueryResultBufferSink();
					((QueryableSource)part.Sources[0]).Query(part.Graph, opts, partsink);
					
					vars = partsink.Variables;
					matches = partsink.Bindings;
					
					string qs = "";
					foreach (Statement s in part.Graph)
						qs += "\n\t" + s;

					result.AddComments("QUERY: " + qs + "  => " + matches.Count);
				}
				
				// Intersect the existing bindings with the new matches.
				
				// get a list of variables the old and new have in common
				int[,] commonVars = IntersectVariables(bindings.Variables, vars);
				int nCommonVars = commonVars.Length/2; // because Length is number of elements, in both dimensions
				
				BindingSet newbindings = new BindingSet();
				newbindings.Variables = new VariableList();
				newbindings.Variables.AddRange(bindings.Variables);
				foreach (Variable v in vars)
					if (!newbindings.Variables.Contains(v))
						newbindings.Variables.Add(v);
				newbindings.Rows = new BindingList();

				if (nCommonVars == 0) {
					// no variables in common, make a cartesian product of the bindings
					
					foreach (VariableBinding[] left in bindings.Rows) {
						for (int i = 0; i < matches.Count; i++) {
							VariableBinding[] right = (VariableBinding[])matches[i];
							
							VariableBinding[] row = new VariableBinding[newbindings.Variables.Count];
							if (left != null) left.CopyTo(row, 0); // null on first intersection
							right.CopyTo(row, bindings.Variables.Count);
							newbindings.Rows.Add(row);
						}
					}
				
				} else {
					// map the new variables in the right side to indexes in new bindings
					int[] newindexes = new int[vars.Count];
					int newvarindex = bindings.Variables.Count;
					for (int i = 0; i < vars.Count; i++) {
						if (bindings.Variables.Contains(vars[i]))
							newindexes[i] = -1;
						else
							newindexes[i] = newvarindex++;
					}
				
					// index the new matches by those variables
				
					System.Collections.Hashtable indexedMatches = new System.Collections.Hashtable();
					foreach (VariableBinding[] right in matches) {
						// traverse down the list of common variables making a tree
						// structure indexing the matches by their variable values
						System.Collections.Hashtable hash = indexedMatches;
						for (int i = 0; i < nCommonVars; i++) {
							Resource value = right[commonVars[i, 1]].Target;
							if (i < nCommonVars - 1) {
								if (hash[value] == null)
									hash[value] = new System.Collections.Hashtable();
								hash = (System.Collections.Hashtable)hash[value];
							} else {
								if (hash[value] == null)
									hash[value] = new BindingList();
								BindingList list = (BindingList)hash[value];
								list.Add(right);
							}
						}
					}
					
					// for each existing binding, find all of the new matches
					// that match the common variables, by traversing the index tree
					foreach (VariableBinding[] left in bindings.Rows) {
						System.Collections.Hashtable hash = indexedMatches;
						BindingList list = null;
						
						for (int i = 0; i < nCommonVars; i++) {
							Resource value = left[commonVars[i, 0]].Target;
							if (hash[value] == null) break;
							if (i < nCommonVars - 1)
								hash = (System.Collections.Hashtable)hash[value];
							else
								list = (BindingList)hash[value];
						}
						
						// tree traversal didn't go to the end, meaning there was
						// no corresponding match (with the same common variable values)
						if (list == null) continue;
					
						for (int i = 0; i < list.Count; i++) {
							VariableBinding[] right = (VariableBinding[])list[i];
							
							VariableBinding[] row = new VariableBinding[newbindings.Variables.Count];
							left.CopyTo(row, 0);
							for (int j = 0; j < newindexes.Length; j++)
								if (newindexes[j] != -1)
									row[newindexes[j]] = right[j];

							newbindings.Rows.Add(row);
						}
					}
					
				}
				
				bindings = newbindings;
			}
			
			VariableBinding[] initrow = new VariableBinding[bindings.Variables.Count];
			for (int i = 0; i < bindings.Variables.Count; i++)
				initrow[i] = new VariableBinding((Variable)bindings.Variables[i], null);
			
			result.Init(initrow, false, false);
			
			int counter = 0;
			foreach (VariableBinding[] row in bindings.Rows) {
				counter++;
				if (returnStart > 0 && counter < returnStart) continue;
				
				if (!result.Add(row))
					break;

				if (returnLimit > 0 && counter >= returnLimit) break;
			}
			
			result.Finished();
		}

		static Entity[] ToEntArray(Resource[] res, ref bool fail) {
			ResSet ents = new ResSet();
			foreach (Resource r in res)
				if (r is Entity)
					ents.Add(r);
			if (ents.Count == 0) { fail = true; return null; }
			return ents.ToEntityArray();
		}
		
		class Filter : StatementSink {
			BindingList matches;
			Statement filter;
			VariableList vars;
			
			public Filter(BindingList matches, Statement filter, VariableList vars) { this.matches = matches; this.filter = filter; this.vars = vars; }
			public bool Add(Statement s) {
				if (filter.Subject == filter.Predicate && s.Subject != s.Predicate) return true;
				if (filter.Subject == filter.Object && s.Subject != s.Object) return true;
				if (filter.Subject == filter.Meta && s.Subject != s.Meta) return true;
				if (filter.Predicate == filter.Object && s.Predicate != s.Object) return true;
				if (filter.Predicate == filter.Meta && s.Predicate != s.Meta) return true;
				if (filter.Object == filter.Meta && s.Object != s.Meta) return true;
				
				VariableBinding[] row = new VariableBinding[vars.Count];
				for (int i = 0; i < vars.Count; i++) {
					row[i] = new VariableBinding((Variable)vars[i], null);
					if ((object)vars[i] == (object)filter.Subject) row[i].Target = s.Subject;
					else if ((object)vars[i] == (object)filter.Predicate) row[i].Target = s.Predicate;
					else if ((object)vars[i] == (object)filter.Object) row[i].Target = s.Object;
					else if ((object)vars[i] == (object)filter.Meta) row[i].Target = s.Meta;
				}
				matches.Add(row);
				return true;
			}
		}
		
		static int[,] IntersectVariables(VariableList leftVars, VariableList rightVars) {
			VariableList commonVars = new VariableList();
			foreach (Variable v in leftVars)
				if (rightVars.Contains(v))
					commonVars.Add(v);
			
			int[,] ret = new int[commonVars.Count, 2];
			for (int i = 0; i < commonVars.Count; i++) {
				ret[i,0] = leftVars.IndexOf(commonVars[i]);
				ret[i,1] = rightVars.IndexOf(commonVars[i]);
			}
			
			return ret;
		}
	}
}
