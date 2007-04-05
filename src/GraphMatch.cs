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
using BindingList = System.Collections.Generic.List<SemWeb.Query.VariableBindings>;
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
		VariableList distinguishedVars = new VariableList();
	
		public GraphMatch() {
		}
		
		public GraphMatch(RdfReader query) :
			this(new Store(query),
				query.BaseUri == null ? null : new Entity(query.BaseUri)) {
		}

		public GraphMatch(StatementSource queryModel) : this(new Store(queryModel), null) {
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
		
		public void SetDistinguishedVariable(Variable variable) {
			distinguishedVars.Add(variable);
		}
		
		public override void Run(SelectableSource targetModel, QueryResultSink result) {
			QueryPart[] parts = new QueryPart[graph.Count];
			for (int i = 0; i < graph.Count; i++)
				parts[i] = new QueryPart(graph[i], targetModel);
			
			RunGeneralQuery(parts, knownValues, litFilters, distinguishedVars.Count == 0 ? null : distinguishedVars, ReturnStart, ReturnLimit, false, result);
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
			public Variable[] Variables;
			public BindingList Rows;
		}
		
		internal static void RunGeneralQuery(QueryPart[] queryParts,
				VarKnownValuesType2 knownValues, LitFilterMap2 litFilters,
				#if !DOTNET2
				ICollection distinguishedVars,
				#else
				ICollection<Variable> distinguishedVars,
				#endif
				int returnStart, int returnLimit,
				bool allowQueryableSource,
				QueryResultSink result) {
				
			BindingSet bindings = new BindingSet();
			bindings.Variables = new Variable[0];
			bindings.Rows = new BindingList();
			bindings.Rows.Add(null); // we need a dummy row for the first intersection
			
			for (int iPart = 0; iPart < queryParts.Length; iPart++) {
				QueryPart part = queryParts[iPart];
			
				// Get the statements in the target model that match this aspect of the query graph.
				
				// get a list of values already found for each variable
				System.Collections.Hashtable foundValues = new System.Collections.Hashtable();
				foreach (Variable v in bindings.Variables)
					foundValues[v] = new ResSet();
				foreach (VariableBindings row in bindings.Rows) {
					if (row == null) continue; // null in first round
					for (int i = 0; i < row.Count; i++)
						((ResSet)foundValues[row.Variables[i]]).Add(row.Values[i]);
				}
				foreach (Variable v in bindings.Variables)
					foundValues[v] = ((ResSet)foundValues[v]).ToArray();
				
				// matches holds the bindings that match this part of the query
				BindingList matches;

				// vars holds an ordered list of variables found in this part of the query
				Variable[] vars;
				
				// Get a set of variables that we care about.  These are distinguished variables
				// in the query plus any variables that we will encounter in a future queryPart.
				// Any other variables are useless to us at this point and we will not do any
				// duplicate row tests based on them.
				ResSet interestingVariables = null;
				if (distinguishedVars != null) {
					interestingVariables = new ResSet();
					interestingVariables.AddRange(distinguishedVars);
					for (int jPart = iPart+1; jPart < queryParts.Length; jPart++) {
						foreach (Statement s in queryParts[jPart].Graph) {
							for (int jc = 0; jc < 4; jc++) {
								if (s.GetComponent(jc) is Variable)
									interestingVariables.Add(s.GetComponent(jc));
							}
						}
					}
				}
				
				// A QueryPart can either be:
				//	A single statement to match against one or more SelectableSources, or one or more QueryableSources
				//  A graph of statements to match against a single QueryableSource
				
				bool allSourcesQueryable = true;
				foreach (SelectableSource source in part.Sources)
					if (!(source is QueryableSource))
						allSourcesQueryable = false;
				
				if (!allowQueryableSource || !allSourcesQueryable) {
					Statement s = part.Graph[0];
					
					matches = new BindingList();
					VariableList varCollector = new VariableList();
					
					// get a list of variables in this part
					// the filter will have null components for variables, except
					// for variables with known values, we plug those values in
					SelectFilter f = new SelectFilter(s);
					for (int i = 0; i < 4; i++) {
						Resource r = s.GetComponent(i);
					
						if (r is Variable) {
							Variable v = (Variable)r;
							
							if (!varCollector.Contains(v))
								varCollector.Add(v);
							
							Resource[] values = null;
							#if DOTNET2
							if (foundValues.ContainsKey(v))
							#endif
								values = (Resource[])foundValues[v];
							if (values == null && 
							#if !DOTNET2
							knownValues[v] != null
							#else
							knownValues.ContainsKey(v)
							#endif
							)
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
					
					#if !DOTNET2
					if (s.Object is Variable && litFilters[(Variable)s.Object] != null)
						f.LiteralFilters = (LiteralFilter[])((LitFilterList)litFilters[(Variable)s.Object]).ToArray(typeof(LiteralFilter));
					#else
					if (s.Object is Variable && litFilters.ContainsKey((Variable)s.Object))
						f.LiteralFilters = ((LitFilterList)litFilters[(Variable)s.Object]).ToArray();
					#endif
					
					vars = new Variable[varCollector.Count];
					varCollector.CopyTo(vars, 0);

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
					
					// The distinguished variables for this part are any that are distinguished for
					// the query plus any that we need to tie these results to past and future
					// bindings (any variable used previously or used later and used in this query).
					if (distinguishedVars != null) {
						VariableList dvars = new VariableList();
						dvars.AddRange(distinguishedVars);
						for (int jPart = 0; jPart < queryParts.Length; jPart++) {
							if (jPart == iPart) continue;
							foreach (Statement s in queryParts[jPart].Graph) {
								for (int jc = 0; jc < 4; jc++) {
									if (s.GetComponent(jc) is Variable) // don't bother checking if it's actually used in this query part
										dvars.Add((Variable)s.GetComponent(jc));
								}
							}
						}
						opts.DistinguishedVariables = dvars;
					}
					
					vars = null;
					matches = null;
					foreach (QueryableSource source in part.Sources) {
						QueryResultBuffer partsink = new QueryResultBuffer();
						source.Query(part.Graph, opts, partsink);
						if (vars == null) {
							vars = new Variable[partsink.Variables.Length];
							partsink.Variables.CopyTo(vars, 0);	
							#if !DOTNET2
							matches = new BindingList(partsink.Bindings);
							#else
							matches = partsink.Bindings;
							#endif
						} else {
							// add in the bindings from this query, but the variables might
							// be in a different order this time
							foreach (VariableBindings b in partsink.Bindings) {
								Resource[] vals = new Resource[vars.Length];
								for (int i = 0; i < vars.Length; i++)
									vals[i] = b[vars[i]];
								VariableBindings c = new VariableBindings(vars, vals);
								matches.Add(c);
							}
						}
					}
					
					string qs = "";
					foreach (Statement s in part.Graph)
						qs += "\n\t" + s;

					result.AddComments("QUERY: " + qs + "  => " + matches.Count);
				}
				
				// Intersect the existing bindings with the new matches.
				
				// get a list of variables the old and new have in common
				int nCommonVars;
				int[,] commonVars = IntersectVariables(bindings.Variables, vars, out nCommonVars);
				
				BindingSet newbindings = new BindingSet();
				
				newbindings.Variables = new Variable[bindings.Variables.Length + vars.Length - nCommonVars];
				bindings.Variables.CopyTo(newbindings.Variables, 0);
				int ctr = bindings.Variables.Length;
				int[] newindexes = new int[vars.Length];
				for (int i = 0; i < vars.Length; i++) {
					if (Array.IndexOf(newbindings.Variables, vars[i]) == -1) {
						newbindings.Variables[ctr] = vars[i];
						newindexes[i] = ctr;
						ctr++;
					} else {
						newindexes[i] = -1;
					}
				}
				newbindings.Rows = new BindingList();
				
				int nMatches = 0;

				if (nCommonVars == 0) {
					// no variables in common, make a cartesian product of the bindings
					foreach (VariableBindings left in bindings.Rows) {
						for (int i = 0; i < matches.Count; i++) {
							VariableBindings right = (VariableBindings)matches[i];
							
							Resource[] newValues = new Resource[newbindings.Variables.Length];
							if (left != null) // null on first intersection
								left.Values.CopyTo(newValues, 0);
							right.Values.CopyTo(newValues, bindings.Variables.Length);
							
							nMatches++;
							if (!quickDupCheckIsDup(newbindings, newValues, nMatches, interestingVariables))
								newbindings.Rows.Add(new VariableBindings(newbindings.Variables, newValues));
						}
					}
				
				} else {
					// index the new matches by those variables
				
					System.Collections.Hashtable indexedMatches = new System.Collections.Hashtable();
					foreach (VariableBindings right in matches) {
						// traverse down the list of common variables making a tree
						// structure indexing the matches by their variable values
						System.Collections.Hashtable hash = indexedMatches;
						for (int i = 0; i < nCommonVars; i++) {
							Resource value = right.Values[commonVars[i, 1]];
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
					foreach (VariableBindings left in bindings.Rows) {
						System.Collections.Hashtable hash = indexedMatches;
						BindingList list = null;
						
						for (int i = 0; i < nCommonVars; i++) {
							Resource value = left.Values[commonVars[i, 0]];
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
							VariableBindings right = (VariableBindings)list[i];
							
							Resource[] newValues = new Resource[newbindings.Variables.Length];
							left.Values.CopyTo(newValues, 0);
							for (int j = 0; j < newindexes.Length; j++)
								if (newindexes[j] != -1)
									newValues[newindexes[j]] = right.Values[j];

							nMatches++;
							if (!quickDupCheckIsDup(newbindings, newValues, nMatches, interestingVariables))
								newbindings.Rows.Add(new VariableBindings(newbindings.Variables, newValues));
						}
					}
					
				}
				
				bindings = newbindings;
			}
			
			result.Init(bindings.Variables, false, false);
			
			int counter = 0;
			foreach (VariableBindings row in bindings.Rows) {
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
			Variable[] vars;
			
			public Filter(BindingList matches, Statement filter, Variable[] vars) { this.matches = matches; this.filter = filter; this.vars = vars; }
			public bool Add(Statement s) {
				if (filter.Subject == filter.Predicate && s.Subject != s.Predicate) return true;
				if (filter.Subject == filter.Object && s.Subject != s.Object) return true;
				if (filter.Subject == filter.Meta && s.Subject != s.Meta) return true;
				if (filter.Predicate == filter.Object && s.Predicate != s.Object) return true;
				if (filter.Predicate == filter.Meta && s.Predicate != s.Meta) return true;
				if (filter.Object == filter.Meta && s.Object != s.Meta) return true;
				
				Resource[] vals = new Resource[vars.Length];
				for (int i = 0; i < vars.Length; i++) {
					if ((object)vars[i] == (object)filter.Subject) vals[i] = s.Subject;
					else if ((object)vars[i] == (object)filter.Predicate) vals[i] = s.Predicate;
					else if ((object)vars[i] == (object)filter.Object) vals[i] = s.Object;
					else if ((object)vars[i] == (object)filter.Meta) vals[i] = s.Meta;
				}
				matches.Add(new VariableBindings(vars, vals));
				return true;
			}
		}
		
		static int[,] IntersectVariables(Variable[] leftVars, Variable[] rightVars, out int nCommonVars) {
			VariableList commonVars = new VariableList();
			foreach (Variable v in leftVars)
				if (Array.IndexOf(rightVars, v) != -1)
					commonVars.Add(v);
			
			int[,] ret = new int[commonVars.Count, 2];
			for (int i = 0; i < commonVars.Count; i++) {
				ret[i,0] = Array.IndexOf(leftVars, commonVars[i]);
				ret[i,1] = Array.IndexOf(rightVars, commonVars[i]);
			}
			
			nCommonVars = commonVars.Count;
			return ret;
		}
		
		static bool quickDupCheckIsDup(BindingSet newbindings, Resource[] newValues, int nMatches, ResSet interestingVariables) {
			// If there is a more than 10-to-1 ratio of rejected duplicates
			// to unique rows, then we check all rows.  Otherwise we check the first 100.

			bool isHighRejectRatio = newbindings.Rows.Count == 0 || (nMatches / newbindings.Rows.Count) > 10;
			for (int i = 0; i < newbindings.Rows.Count; i++) {
				if (i > 100 && !isHighRejectRatio) break;
				bool dup = true;
				for (int j = 0; j < newValues.Length; j++) {
					if (interestingVariables != null && !interestingVariables.Contains(newbindings.Variables[j])) continue;
					Resource left = ((VariableBindings)newbindings.Rows[i]).Values[j];
					Resource right = newValues[j];
					if ((object)left == null || (object)right == null) {
						if (!((object)left == null && (object)right == null))
							dup = false;
					} else if (!left.Equals(right)) {
						dup = false;
						break;
					}
				}
				if (dup)
					return true;
			}
			return false;
		}
	}
}
