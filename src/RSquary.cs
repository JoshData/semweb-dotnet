using System;
using System.Collections;

using SemWeb;

namespace SemWeb.Query {
	public class QueryException : ApplicationException {
		public QueryException(string message, Exception cause) : base(message, cause) {
		}
	}
	
	public class RSquary {
		
		// TODO: Optional statements
		// TODO: Grouping and disjunctions
		
		public static Entity qSelect = "http://purl.oclc.org/NET/rsquary/select";
		public static Entity qVariable = "http://purl.oclc.org/NET/rsquary/variable";
		public static Entity qLimit = "http://purl.oclc.org/NET/rsquary/returnLimit";
		public static Entity qStart = "http://purl.oclc.org/NET/rsquary/returnStart";
		public static Entity qDistinctFrom = "http://purl.oclc.org/NET/rsquary/distinctFrom";
		
		KnowledgeModel model;
		MemoryStore store;
		Entity query;
		
		VariableBinding[] initialBindings;
		ArrayList[,] predicateFilters;
		ArrayList[] valueFilters;
		ArrayList[] variablesDistinct;
		
		int start = -1;
		int limit = -1;
		
		private class SearchState {
			public Hashtable cachedSelections = new Hashtable();
			public Store target;
			public QueryResultSink sink;
			public VariableBinding[] bindings;
			public int count = 0;
		}	
		
		private bool IsQueryPredicate(Entity e) {
			if (e == qSelect) return true;
			if (e == qVariable) return true;
			if (e == qDistinctFrom) return true;
			if (e == qLimit) return true;
			if (e == qStart) return true;
			return false;
		}
		
		public RSquary(Store queryModel, string queryUri) : this(queryModel, queryUri, null) {
		}
		
		public RSquary(Store queryModel, string queryUri, Hashtable extraValueFilters) {			
			model = new KnowledgeModel();
			store = new MemoryStore(model);
			model.Add(store);
			store.Import(queryModel);
			
			query = model.GetResource(queryUri);

			start = GetIntOption(qStart);
			limit = GetIntOption(qLimit);
			
			ArrayList variables = new ArrayList();
			variables.AddRange(SortSelections(query[qSelect], false));
			variables.AddRange(SortSelections(query[qVariable], true));
			
			initialBindings = (VariableBinding[])variables.ToArray(typeof(VariableBinding));
			
			// Get a list of all statements about each variable.
			predicateFilters = new ArrayList[initialBindings.Length,2];
			valueFilters = new ArrayList[initialBindings.Length];
			variablesDistinct = new ArrayList[initialBindings.Length];
			for (int i = 0; i < initialBindings.Length; i++) {
				Entity variable = initialBindings[i].Variable;

				valueFilters[i] = new ArrayList();
				
				for (int forward = 0; forward <= 1; forward++) {
					predicateFilters[i,forward] = model.Select(new Statement((forward==1) ? variable : null, null, (forward==1) ? null : variable)).Statements;
					
					// Remove any query predicates and value filters
					for (int z = 0; z < predicateFilters[i,forward].Count; z++) {
						Statement statement = (Statement)predicateFilters[i,forward][z];
						
						ValueFilter f = null;
						if (extraValueFilters != null)
							f = extraValueFilters[statement.Predicate.Uri] as ValueFilter;
						if (f == null)
							f = ValueFilter.GetValueFilter(statement.Predicate, (forward == 1) ? statement.Object : statement.Subject);
						
						if (f != null)
							valueFilters[i].Add(f);
							
						if (f != null || IsQueryPredicate(statement.Predicate)) {
							predicateFilters[i,forward].RemoveAt(z);
							z--;
						}
					}

				}
				
				variablesDistinct[i] = SymmetricSelect(variable, qDistinctFrom, model);
			}
		}
		
		private int GetIntOption(Entity predicate) {
			Resource[] rr = query[predicate];
			if (rr.Length == 0) return -1;
			Resource r = rr[0];
			if (r == null || !(r is Literal)) return -1;
			try {
				return int.Parse(((Literal)r).Value);
			} catch (Exception e) {
				throw new QueryException("Invalid integer value for <" + predicate + ">, '" + ((Literal)r).Value + "'.", e);
			}
		}
		
		private ArrayList SortSelections(IEnumerable selections, bool var) {
			// Get the list of selections
			ArrayList items = new ArrayList();
			foreach (Resource s in selections) {
				if (!(s is Entity)) continue;
				items.Add(s);
			}
			
			Hashtable allvariables = new Hashtable();
			foreach (object o in query[qSelect]) allvariables[o] = o;
			foreach (object o in query[qVariable]) allvariables[o] = o;
			
			// Generate a matrix of dependencies
			bool[,] dep = new bool[items.Count, items.Count];
			bool[] ground = new bool[items.Count];
			for (int a = 0; a < items.Count; a++) {
				// Check whether the entity has a relation to an object in the target world.
				foreach (Statement s in model.Select(new Statement((Entity)items[a], null, null))) {
					if (IsQueryPredicate(s.Predicate)) continue;
					if (allvariables.ContainsKey(s.Object)) continue;
					ground[a] = true;
				}
				foreach (Statement s in model.Select(new Statement(null, null, (Entity)items[a]))) {
					if (IsQueryPredicate(s.Predicate)) continue;
					if (allvariables.ContainsKey(s.Subject)) continue;
					ground[a] = true;
				}
				
				// Check what other variables this entity has a relation with.
				for (int b = a+1; b < items.Count; b++) {
					StatementExistsSink2 esink = new StatementExistsSink2();
					model.Select(new Statement((Entity)items[a], null, (Entity)items[b]), esink);
					model.Select(new Statement((Entity)items[b], null, (Entity)items[a]), esink);
					if (esink.Exists)
						dep[a,b] = true;
				}
			}
			
			// Go through the permutations to find the ordering
			// that minimizes the number of variables with dependencies only to the right,
			// and especially such variables with no relations to non-variables.
			int[] order = new int[items.Count];
			int[] maxorder = new int[items.Count];
			bool[] used = new bool[items.Count];
			int maxsat = int.MinValue;
			SortSelectionsRecurse(used, order, 0, dep, ground, 0, maxorder, ref maxsat);
			
			ArrayList ret = new ArrayList();
			foreach (int index in maxorder)
				ret.Add(new VariableBinding((Entity)items[index], null, var));
			
			return ret;
		}
		
		private static int min(int a, int b) { return a < b ? a : b; }
		private static int max(int a, int b) { return a < b ? b : a; }
		
		private void SortSelectionsRecurse(bool[] used, int[] order, int index, bool[,] dep, bool[] ground, int satisfactions, int[] maxorder, ref int maxsat) {
			if (index == used.Length) { // base case
				if (satisfactions > maxsat) {
					order.CopyTo(maxorder, 0);
					maxsat = satisfactions;
				}
				return;
			}
			
			for (int i = 0; i < used.Length; i++) {
				if (used[i]) continue;
				
				int s = 0;
				for (int j = 0; j < index; j++)
					if (dep[min(i,order[j]),max(i,order[j])])
						s = 1; // if a dependency is met, this is good
				if (!ground[i] && s == 0)
					s = -1; // if no dependency is met and this isn't grounded to something in the target model, this is bad
				
				order[index] = i;
				used[i] = true;
				SortSelectionsRecurse(used, order, index+1, dep, ground, satisfactions+s, maxorder, ref maxsat);
				used[i] = false;
			}
		}
		
		private Resource GetBinding(Resource ent, int varIndex, bool reqEntity, SearchState state) {
			if (!(ent is Entity)) {
				if (reqEntity) return null;
				return ent;
			}
			for (int i = 0; i < state.bindings.Length; i++) {
				if (state.bindings[i].Variable.Equals(ent)) {
					if (i < varIndex && (!reqEntity || state.bindings[i].Target is Entity))
						return state.bindings[i].Target;
					else
						return null;
				}
			}
			return ent;
		}

		public void Query(Store target, QueryResultSink sink) {
			SearchState state = new SearchState();
			state.target = target;
			state.sink = sink;
			state.bindings = (VariableBinding[])initialBindings.Clone();
			
			bool found;
			Recurse(state, 0, out found);
		}
		
		public bool Test(Store target) {
			StopOnFirst sink = new StopOnFirst();
			Query(target, sink);
			return sink.Found;
		}		
		
		private bool Recurse(SearchState state, int varIndex, out bool found) {
			// Base case.
			if (varIndex == state.bindings.Length) {
				found = true;
				state.count++;
				if (state.count < start+1) return false;
				bool stop = !state.sink.Add(state.bindings);
				if (state.count >= limit && limit != -1) return true;				
				return stop;
			}
			
			Set resources = null;
			
			bool forward = true;
			while (true) {
				foreach (Statement s in predicateFilters[varIndex, forward ? 1 : 0]) {
					if (resources == null) {
						// Select all resources that match this statement
						Entity subj = forward ? null : (Entity)GetBinding(s.Subject, varIndex, true, state);
						Entity pred = (Entity)GetBinding(s.Predicate, varIndex, true, state);
						Resource obj = forward ? GetBinding(s.Object, varIndex, false, state) : null;
						Statement q = new Statement(subj, pred, obj);
						
						// The selection will also apply any value filters for this variable
						resources = SelectCache(q, forward, state, varIndex).Clone();
						
						// Apply distinctFrom restrictions to make sure the value of this
						// variable is not equal to the value of other variables, as specified.
						foreach (Resource r in variablesDistinct[varIndex]) {
							Resource rb = GetBinding(r, varIndex, false, state);
							if (rb != null) resources.Remove(rb);
						}
						
					} else {
						// Filter out resources that don't match the pattern
						Entity filterPredicate = (Entity)GetBinding(s.Predicate, varIndex, true, state);
						Resource filterObject = GetBinding(forward ? s.Object : s.Subject, varIndex, !forward, state);
						if (filterPredicate == null || filterObject == null) continue;
						
						// Find all items that satisfy the filter, and then intersect the sets.
						Set filter = SelectCache(new Statement(forward ? null : (Entity)filterObject, filterPredicate, forward ? filterObject : null), forward, state, -1);
						
						// Perform the loop on the items of the smaller set.
						Set smaller = (filter.Size() < resources.Size()) ? filter : resources;
						Set larger = (filter.Size() < resources.Size()) ? resources : filter;						
						foreach (Resource r in smaller.Items())
							if (!larger.Contains(r))
								smaller.Remove(r);							
						resources = smaller;
					}
				}
				
				if (forward) forward = false;
				else break;
			}
			
			found = false;
			
			if (resources == null) {
				// There were no statements about this variable.  Set it
				// to an anonymous node, since it may have any value at all.
				resources = new Set();
				resources.Add(new AnonymousNode());
			}
			
			foreach (Resource r in resources.Items()) {
				state.bindings[varIndex].Target = r;
				bool f;
				bool stop = Recurse(state, varIndex+1, out f);
				if (stop) return true;
				
				found |= f;
				if (state.bindings[varIndex].var && found) break;
			}
			
			return false;
		}
		
		private ArrayList SymmetricSelect(Entity e, Entity p, Store model) {
			ArrayList ret = new ArrayList();
			model.Select(new Statement(e, p, null), new PutInArraySink(false, ret));
			model.Select(new Statement(null, p, e), new PutInArraySink(true, ret));
			return ret;
		}
		
		private class SelectCacheKey {
			public Statement Q;
			public int VarIndex;
			
			public override int GetHashCode() { return Q.GetHashCode(); }
			public override bool Equals(object other) {
				return ((SelectCacheKey)other).VarIndex == VarIndex && Q.Equals(((SelectCacheKey)other).Q);
			}
		}
		
		private Set SelectCache(Statement q, bool forward, SearchState state, int varIndex) {
			if (varIndex != -1 && valueFilters[varIndex].Count == 0)
				varIndex = -1;
			
			SelectCacheKey key = new SelectCacheKey();
			key.Q = q;
			key.VarIndex = varIndex;
			
			Set cached = (Set)state.cachedSelections[key];
			if (cached == null) {
				cached = new Set();
				state.target.Select(q, new PutInSet(forward, cached));
				
				// Do value filters
				if (varIndex != -1) {
					foreach (ValueFilter f in valueFilters[varIndex]) {
						foreach (Resource r in cached.Items()) {
							if (!(r is Literal) && f is LiteralValueFilter)
								cached.Remove(r);
							else if (!f.Filter(r, state.target))
								cached.Remove(r);
						}
					}
				}
							
				state.cachedSelections[key] = cached;
			}
			return cached;
		}
		
		private class PutInArraySink : StatementSink {
			Hashtable seen = new Hashtable();
			bool subject;
			ArrayList sink;
			
			public PutInArraySink(bool subject, ArrayList sink) {
				this.subject = subject; this.sink = sink;
			}

			public bool Add(Statement statement) {
				Resource r;
				if (subject) r = statement.Subject;
				else r = statement.Object;
				if (!(r.Uri != null || r is Literal) || !seen.ContainsKey(r)) {
					sink.Add(r);
					if ((r.Uri != null || r is Literal))
						seen[r] = r;
				}
				return true;
			}
		}
		
		private class PutInSet : StatementSink {
			bool subject;
			Set sink;
			
			public PutInSet(bool subject, Set sink) {
				this.subject = subject; this.sink = sink;
			}

			public bool Add(Statement statement) {
				if (subject) sink.Add(statement.Subject);
				else sink.Add(statement.Object);
				return true;
			}
		}

		private class StatementExistsSink2 : StatementSink {
			bool exists = false;
			
			public bool Exists { get { return exists; } }
			
			public bool Add(Statement statement) {
				if (statement.Predicate == qDistinctFrom) return true;
				exists = true;
				return false;
			}
		}
		
		private class Set {
			Hashtable store;
			ArrayList items;
			
			public Set() { store = new Hashtable(); }
			public Set(Set other) { store = (Hashtable)other.store.Clone(); }
			
			public void Add(Resource r) { store[r] = r; items = null; }
			public void Remove(Resource r) { store.Remove(r); items = null; }
			public bool Contains(Resource r) { return store.ContainsKey(r); }
			
			public int Size() { return store.Count; }
			
			public ICollection Items() {
				if (items == null) {
					items = new ArrayList();
					items.AddRange(store.Keys);
				}
				return items;
			}
			
			public Set Clone() { return new Set(this); }
		}
			
		private class StopOnFirst : QueryResultSink {
			public bool Found = false;
			public override bool Add(VariableBinding[] result) {
				Found = true;
				return false;
			}
		}
	
	}
	
	public class PrintQuerySink : QueryResultSink {
		public override bool Add(VariableBinding[] result) {
			foreach (VariableBinding var in result)
				Console.WriteLine(var.Variable + " ==> " + var.Target);
			Console.WriteLine();
			return true;
		}
	}
	
	public abstract class QueryResultSink {
		public abstract bool Add(VariableBinding[] result);
	}
	
	public struct VariableBinding {
		Entity v;
		Resource t;
		internal bool var;
		
		internal VariableBinding(Entity variable, Resource target, bool isvar) {
			v = variable;
			t = target;
			var = isvar;
		}
		
		public Entity Variable { get { return v; } set { v = value; } }
		public Resource Target { get { return t; } set { t = value; } }
	}
}	

