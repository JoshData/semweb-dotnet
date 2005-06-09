using System;
using System.Collections;
using System.IO;

using SemWeb;
using SemWeb.Stores;

namespace SemWeb.Query {
	public class QueryException : ApplicationException {
		public QueryException(string message) : base(message) {
		}
			
		public QueryException(string message, Exception cause) : base(message, cause) {
		}
	}
	
	public class QueryEngine {
		MemoryStore model;
		
		ArrayList setupVariablesSelect = new ArrayList();
		ArrayList setupVariablesSelectFirst = new ArrayList();
		ArrayList setupVariablesDistinct = new ArrayList();
		ArrayList setupValueFilters = new ArrayList();
		
		bool setup = false;
		
		Hashtable variableHash = new Hashtable();
		VariableBinding[] initialBindings;
		ArrayList[] predicateFilters;
		ArrayList[] valueFilters;
		ArrayList[] variablesDistinct;
		VariableNode rootVariable;			
		
		int start = -1;
		int limit = -1;
		
		public void Select(Entity entity) {
			SelectInternal(entity, setupVariablesSelect);
		}
		
		public void SelectFirst(Entity entity) {
			SelectInternal(entity, setupVariablesSelectFirst);
		}

		private void SelectInternal(Entity entity, ArrayList list) {
			if (entity.Uri == null) throw new QueryException("Anonymous nodes are automatically considered variables.");
			if (setupVariablesSelect.Contains(entity) || setupVariablesSelectFirst.Contains(entity)) return;
			list.Add(entity);
		}

		public void MakeDistinct(Entity a, Entity b) {
			SetupVariablesDistinct d = new SetupVariablesDistinct();
			d.a = a;
			d.b = b;
			setupVariablesDistinct.Add(d);
		}
		
		public void AddValueFilter(Entity entity, ValueFilter filter) {
			SetupValueFilter d = new SetupValueFilter();
			d.a = entity;
			d.b = filter;
			setupValueFilters.Add(d);
		}

		public void SetGraph(Store graph) {
			if (model != null) throw new ArgumentException("A graph has already been set for this query.");
			model = new MemoryStore(null);
			model.Import(graph);
		}
		
		public int ReturnStart { get { return start; } set { start = value; } }
		
		public int ReturnLimit { get { return limit; } set { limit = value; } }
		
		private class SetupVariablesDistinct {
			public Entity a, b;
		}
		private class SetupValueFilter {
			public Entity a;
			public ValueFilter b;
		}
		
		private class VariableNode {
			public QueryEngine Engine;
			
			public Resource Variable;
			public int BindingIndex;
			
			public Set Dependencies = new Set();
			
			public VariableNode Parent = null;
			public ArrayList Children = new ArrayList();

			public bool NoSelect;
			public int Depth;
			
			public Set Alternatives;
			public Hashtable PreCache = new Hashtable();		
			
			public bool ValueNotImportant {
				get {
					return Variable.Uri == null && Children.Count == 0;
				}
			}
			
			public bool Sees(VariableNode dependency) {
				if (this == dependency) return true;
				if (Parent == null || Parent == this) return false;
				if (Parent == dependency) return true;
				return Parent.Sees(dependency);
			}
			
			public void CopyBindings(VariableBinding[] source, VariableBinding[] dest) {
				dest[BindingIndex] = source[BindingIndex];
				foreach (VariableNode child in Children)
					child.CopyBindings(source, dest);
			}
			
			public void SetNoSelectAndDepth() {
				Depth = 0;
				
				// If any of the children of this node are selections,
				// then this node must be a selection.
				foreach (VariableNode child in Children) {
					child.SetNoSelectAndDepth();
					if (!child.NoSelect) NoSelect = false;
					if (child.Depth > Depth) Depth = child.Depth;
				}
				
				Depth++;
			}
			
			public void SortChildren() {
				// Sort the children of this node to put non-selections first,
				// since they are "purmutatively" simple, and then shorter
				// trees first.
				Children.Sort(new ChildComparer());
				foreach (VariableNode child in Children)
					child.SortChildren();
				Engine.predicateFilters[BindingIndex].Sort(new FilterComparer(this));
			}
			
			public void PruneAnonymousLeaves() {
				for (int i = 0; i < Children.Count; i++) {
					VariableNode child = (VariableNode)Children[i];
					child.PruneAnonymousLeaves();
					if (child.ValueNotImportant) {
						Children.RemoveAt(i);
						i--;
					}					
				}				
			}
			
			private class ChildComparer : IComparer {
				public int Compare(object a, object b) {
					bool an = ((VariableNode)a).NoSelect;
					bool bn = ((VariableNode)b).NoSelect;
					if (an && !bn) return -1;
					if (!an && bn) return 1;
					
					return ((VariableNode)a).Depth.CompareTo(((VariableNode)b).Depth);
				}
			}
			
			private class FilterComparer : IComparer {
				VariableNode node;
				public FilterComparer(VariableNode node) { this.node = node; }
				public int Compare(object a, object b) {
					return -Order((Statement)a).CompareTo(Order((Statement)b));
				}
				int Order(Statement s) {
					return Order(s.Subject) + Order(s.Predicate) + Order(s.Object);
				}
				int Order(Resource r) {
					if (r == node.Variable) return 0;
					if (r is Literal) return 1; // literals are always constant
					if (!node.Engine.variableHash.ContainsKey(r)) return 1; // constant
					if (node.Sees((VariableNode)node.Engine.variableHash[r])) return 1;
					return 0;
				}
			}

			public void Dump() { Dump(""); }
			public void Dump(string indent) {
				Console.Error.Write(indent + Variable);
				if (NoSelect)
					Console.Error.Write(" NoSelect");
				Console.Error.WriteLine();
				foreach (VariableNode child in Children)
					child.Dump(indent + " ");
			}
		}
		
		private class SearchState {
			public Hashtable cachedSelections = new Hashtable();
			public Store target;
			public QueryResultSink sink;
			public VariableBinding[] bindings;
		}	
		

		private void SetUp() {
			if (setup) return;
			setup = true;
			
			ArrayList variables = new ArrayList();
			
			// Get the list of variables			
			foreach (Entity v in setupVariablesSelect)
				variables.Add(new VariableBinding(v, null, false));
			foreach (Entity v in setupVariablesSelectFirst)
				variables.Add(new VariableBinding(v, null, true));
			
			// And all anonymous nodes in the graph
			Hashtable seenAnonNodes = new Hashtable();
			foreach (Statement s in model) {
				foreach (Resource r in new Resource[] { s.Subject, s.Predicate, s.Object }) {
					if (r.Uri != null || !(r is Entity) || seenAnonNodes.ContainsKey(r)) continue;
					seenAnonNodes[r] = r;
					variables.Add(new VariableBinding((Entity)r, null, true));
				}
			}
			
			initialBindings = (VariableBinding[])variables.ToArray(typeof(VariableBinding));
			
			foreach (VariableBinding b in initialBindings)
				variableHash[b.Variable] = b.Variable;
			
			// Get a list of all statements about each variable.
			
			predicateFilters = new ArrayList[initialBindings.Length];
			valueFilters = new ArrayList[initialBindings.Length];
			variablesDistinct = new ArrayList[initialBindings.Length];
			
			int[] constantFilters = new int[initialBindings.Length];
			
			for (int i = 0; i < initialBindings.Length; i++) {
				Entity variable = initialBindings[i].Variable;

				predicateFilters[i] = new ArrayList();
				
				for (int spo = 0; spo <= 2; spo++) {
					IList filters = model.Select(new Statement(
						spo == 0 ? variable : null,
						spo == 1 ? variable : null,
						spo == 2 ? variable : null)).Statements;
					
					foreach (Statement statement in filters) {
						predicateFilters[i].Add(statement);
						
						// Increment the count of filters based only on constants.
						if (spo == 0 && !variableHash.ContainsKey(statement.Predicate) && !variableHash.ContainsKey(statement.Object))
							constantFilters[i]++;
						if (spo == 1 && !variableHash.ContainsKey(statement.Subject) && !variableHash.ContainsKey(statement.Object))
							constantFilters[i]++;
						if (spo == 2 && !variableHash.ContainsKey(statement.Subject) && !variableHash.ContainsKey(statement.Predicate))
							constantFilters[i]++;
						
						// TODO: Give (inverse) functional properties (in the right direction)
						// an advantage, since they essentially turn a variable into a constant.
					}

				}
				
				// Get a list of distict variables from this one
				
				variablesDistinct[i] = new ArrayList();
				foreach (SetupVariablesDistinct d in setupVariablesDistinct) {
					if (d.a == variable) variablesDistinct[i].Add(d.b);
					if (d.b == variable) variablesDistinct[i].Add(d.a);
				}
				
				// Get a list of predicate filters for this one
				valueFilters[i] = new ArrayList();
				foreach (SetupValueFilter d in setupValueFilters) {
					if (d.a == variable) valueFilters[i].Add(d.b);
				}
			}
			
			// Create variable nodes for each variable.
			
			VariableNode[] variableNodes = new VariableNode[initialBindings.Length];
			for (int v = 0; v < initialBindings.Length; v++) {
				variableNodes[v] = new VariableNode();
				variableNodes[v].Engine = this;
				variableNodes[v].Variable = initialBindings[v].Variable;
				variableNodes[v].BindingIndex = v;
				variableNodes[v].NoSelect = initialBindings[v].var;
				variableHash[variableNodes[v].Variable] = variableNodes[v];
			}
			
			// Find the dependencies among the nodes.
			
			for (int v = 0; v < initialBindings.Length; v++) {
				foreach (Statement s in predicateFilters[v]) {
					if (s.Subject != initialBindings[v].Variable && variableHash.ContainsKey(s.Subject))
						variableNodes[v].Dependencies.Add( variableHash[s.Subject] );
					if (s.Predicate != initialBindings[v].Variable && variableHash.ContainsKey(s.Predicate))
						variableNodes[v].Dependencies.Add( variableHash[s.Predicate] );
					if (s.Object != initialBindings[v].Variable && variableHash.ContainsKey(s.Object))
						variableNodes[v].Dependencies.Add( variableHash[s.Object] );
				}
			}
			
			// Choose a root variable.  This should have the most number of
			// filters tied to constants (non-variable resources), with
			// ties going to selection variables.
			
			foreach (VariableNode v in variableNodes) {
				if (constantFilters[v.BindingIndex] == 0) continue;
				if (rootVariable == null) { rootVariable = v; continue; }
				if (constantFilters[rootVariable.BindingIndex] < constantFilters[v.BindingIndex])
					rootVariable = v;
				if (constantFilters[rootVariable.BindingIndex] == constantFilters[v.BindingIndex]
					 && initialBindings[rootVariable.BindingIndex].var && !initialBindings[v.BindingIndex].var)
					rootVariable = v;
			}
			if (rootVariable == null) {
				// No variables had any anchoring in the target model.
				// Root the tree on an arbitrary variable.
				rootVariable = variableNodes[0];
			}
			
			// Signifies the root node.
			rootVariable.Parent = rootVariable;
			
			// Hook the remaining variables into the tree somewhere
			
			for (int v = 1; v < initialBindings.Length; v++) {
				// Choose the next variable to place into the tree.
				// It will be the variable with the most number of
				// its dependencies already placed on the tree, with
				// ties going to selection variables.
				
				// TODO: Prioritize variables that are determined
				// through functional properties from variables on
				// the tree.
				
				VariableNode node = null;
				int dc = -1;
				foreach (VariableNode n in variableNodes) {
					if (n.Parent != null) continue;
					
					int c = 0;
					foreach (VariableNode dep in n.Dependencies.Items())
						if (dep.Parent != null)
							c++;
					
					if (node == null || c > dc) { node = n; dc = c; continue; }
					if (c == dc && ( 
						(initialBindings[node.BindingIndex].var && !initialBindings[n.BindingIndex].var)
						|| (constantFilters[node.BindingIndex] < constantFilters[n.BindingIndex])
						)) { node = n; dc = c; continue; }
				}
				
				// Find a place for this variable on the tree.  Place
				// it as a child of any of its dependencies, if that
				// dependency can see all of its other dependencies
				// that are already on the tree.
				
				foreach (VariableNode dep in node.Dependencies.Items()) {
					if (dep.Parent == null) continue;
					bool seesAll = true;
					foreach (VariableNode dep2 in node.Dependencies.Items()) {
						if (dep2.Parent == null) continue;
						if (!dep.Sees(dep2)) { seesAll = false; break; }
					}
					if (!seesAll) continue;
					
					node.Parent = dep;
					break;
				}
				if (node.Parent != null) continue; // done with this one
				
				// This variable is dependent on multiple nodes of the tree.
				// Find the first node it's dependent on, and then move
				// subsequent dependencies into subparts of that node.
				// This could be done better...
				VariableNode deproot = null;
				foreach (VariableNode dep in node.Dependencies.Items()) {
					if (dep.Parent == null) continue;
					if (deproot == null) { deproot = dep; continue; }
					
					// Move other dependencies within this node
					VariableNode p = dep;
					while (p.Parent != p) {
						if (deproot.Sees(p.Parent)) {
							p.Parent = deproot;
							deproot = p.Parent;
							break;
						}
						p = p.Parent;
					}
				}			
				node.Parent = deproot;
				if (node.Parent != null) continue; // done with this one
				
				if (node.Parent == null)
					throw new QueryException("Query variable " + node.Variable + " is not connected to the other entities in the query.");
			}
			
			// Find the children of each variable
			foreach (VariableNode v in variableNodes)
				if (v.Parent != v)
					v.Parent.Children.Add(v);
				
			// Determine which children
			rootVariable.SetNoSelectAndDepth();
			
			// Sort the order of children.  Children dependent through
			// functional properties (TODO), and children marked as
			// SelectFirst go first, because they will have one result,
			// which makes permutations on later children faster.  Children
			// heading shorter trees go first, but I don't remember why.
			rootVariable.SortChildren();
			
			// Anonymous nodes at the leaves of the dependency tree are
			// irrelevant. Prune them off.
			rootVariable.PruneAnonymousLeaves();
			
			//rootVariable.Dump();
		}
		
		private Resource GetBinding(Resource ent, VariableNode currentNode, bool reqEntity, SearchState state) {
			if (!(ent is Entity)) {
				if (reqEntity) return null;
				return ent;
			}
			
			if (variableHash.ContainsKey(ent)) {
				while (currentNode.Parent != currentNode) {
					currentNode = currentNode.Parent;
					if (currentNode.Variable.Equals(ent)) { 
						if ((!reqEntity || state.bindings[currentNode.BindingIndex].Target is Entity))
							return state.bindings[currentNode.BindingIndex].Target;
						else
							return null;
					}
				}
				return null;
			}
			
			if (ent.Uri == null)
				throw new QueryException("An anonymous node in the query was not a variable.");

			return ent;
		}

		private bool GetAlternativesSet(Resource ent, VariableNode currentNode, out VariableNode outNode, out Set outSet) {
			if (variableHash.ContainsKey(ent)) {
				while (currentNode.Parent != currentNode) {
					currentNode = currentNode.Parent;
					if (currentNode.Variable.Equals(ent)) {
						outNode = currentNode;
						outSet = currentNode.Alternatives;
						return true;
					}
				}
			}
			
			outNode = null;
			outSet = null;
			return false;
		}

		public void Query(Store target, QueryResultSink sink) {
			SetUp();
			
			if (start != -1 || limit != -1)
				sink = new StartLimitSink(start, limit, sink);
			
			SearchState state = new SearchState();
			state.target = target;
			state.sink = sink;
			state.bindings = (VariableBinding[])initialBindings.Clone();
			
			// Send the query sink the list of variables.
			Entity[] varList = new Entity[initialBindings.Length];
			for (int i = 0; i < initialBindings.Length; i++)
				varList[i] = initialBindings[i].Variable;
			sink.Init(varList);
			
			bool emittedBinding;			
			Recurse(state, rootVariable, out emittedBinding);
			
			sink.Finished();
		}
		
		public bool Test(Store target) {
			StopOnFirst sink = new StopOnFirst();
			Query(target, sink);
			return sink.Found;
		}		
		
		private bool Recurse(SearchState state, VariableNode node, out bool emittedBinding) {
			Set resources = null;
			
			for (int sIndex = 0; sIndex < predicateFilters[node.BindingIndex].Count; sIndex++) { 
				Statement s = (Statement)predicateFilters[node.BindingIndex][sIndex];
				
				Store selectCache = null;
				
				int spo;
				if (s.Subject == node.Variable) spo = 0;
				else if (s.Predicate == node.Variable) spo = 1;
				else spo = 2;
				
				Statement q = new Statement(
					spo == 0 ? null : (Entity)GetBinding(s.Subject, node, true, state),
					spo == 1 ? null : (Entity)GetBinding(s.Predicate, node, true, state),
					spo == 2 ? null : GetBinding(s.Object, node, false, state));
				
				if (resources == null) {
					if (spo == 2 && node.NoSelect && q.Subject != null && q.Predicate != null) {
						VariableNode altNode;
						Set altSet;
						if (GetAlternativesSet(s.Subject, node, out altNode, out altSet)) {
							PreCacheKey key = new PreCacheKey(q.Subject, q.Predicate);
							Resource value = (Resource)altNode.PreCache[key];
							if (value == null) {
								// Run a combined select on all of the potential resources.
								ArrayList queries = new ArrayList();
								foreach (Resource r in altSet.Items()) {
									if (!(r is Entity)) continue;
									Statement q2 = new Statement((Entity)r, q.Predicate, null);
									queries.Add(q2);
								}
								
								state.target.Select((Statement[])queries.ToArray(typeof(Statement)), new PutInHash(0, 1, 2, altNode.PreCache));
							}
							
							value = (Resource)altNode.PreCache[key];
							resources = new Set();
							if (value != null)
								resources.Add(value);
						}
					}
					
					if (resources == null) {
						// Select all resources that match this statement
						// The selection will also apply any value filters for this variable
						resources = SelectCache(q, spo, state, selectCache, node).Clone();
					}
					
					// Apply distinctFrom restrictions to make sure the value of this
					// variable is not equal to the value of other variables, as specified.
					foreach (Resource r in variablesDistinct[node.BindingIndex]) {
						Resource rb = GetBinding(r, node, false, state);
						if (rb != null) resources.Remove(rb);
					}
					
				} else {
					int nullCount = ((q.Subject == null) ? 1 : 0) + ((q.Predicate == null) ? 1 : 0) + ((q.Object == null) ? 1 : 0);
					
					// Deal with queries with nothing yet known with a later variable.
					if (nullCount == 3)
						continue;
					
					if (nullCount >= 2) {
						// Run a combined select on all of the potential resources.
						ArrayList queries = new ArrayList();
						foreach (Resource r in resources.Items()) {
							if (spo != 2 && !(r is Entity)) continue;
							
							Statement q2 = new Statement(
								spo == 0 ? (Entity)r : q.Subject,
								spo == 1 ? (Entity)r : q.Predicate,
								spo == 2 ? r : q.Object);
							
							queries.Add(q2);
						}
						
						SelectPartialFilter selectFilter = new SelectPartialFilter(
							spo == 0 ? true : false,
							spo == 1 ? true : false,
							spo == 2 ? true : false,
							false);
							
						if (sIndex == predicateFilters[node.BindingIndex].Count-1 && node.NoSelect)
							selectFilter.SelectFirst = true;
	
						Set resources2 = new Set();
						state.target.Select((Statement[])queries.ToArray(typeof(Statement)), selectFilter, new PutInSet(spo, resources2));
						
						resources = resources2;
						
					} else {
						// Find all items that satisfy the filter, and then intersect the sets.
						Set filter = SelectCache(q, spo, state, selectCache, null);
						
						// Perform the loop on the items of the smaller set.
						Set smaller = (filter.Size() < resources.Size()) ? filter : resources;
						Set larger = (filter.Size() < resources.Size()) ? resources : filter;						
						foreach (Resource r in smaller.Items())
							if (!larger.Contains(r))
								smaller.Remove(r);							
						resources = smaller;
					}
				}
			}
			
			if (resources == null) {
				// There were no statements about this variable.  Set it
				// to an anonymous node, since it may have any value at all.
				// This is important because this will be invoked when
				// an anonymous variable node has no children.
				resources = new Set();
				resources.Add(new Entity((string)null));
			}
			
			node.Alternatives = resources;
			node.PreCache.Clear();
			
			emittedBinding = false;
			
			if (node.Children.Count == 0) {
				// This is the end of a tree branch.
				foreach (Resource r in resources.Items()) {
					emittedBinding = true;
					
					state.bindings[node.BindingIndex].Target = r;
					if (!state.sink.Add(state.bindings)) return true;
					
					if (node.NoSelect) break; // report only one binding for non-selective variables
				}
				return false;
				
			} if (node.Children.Count == 1) {
				// The node has one child, so simply recurse into the child.
				VariableNode child = (VariableNode)node.Children[0];
				//PrecacheSelect(node, resources, child, state);
				foreach (Resource r in resources.Items()) {
					state.bindings[node.BindingIndex].Target = r;
					
					bool eb;
					if (Recurse(state, child, out eb)) return true;
					emittedBinding |= eb;
					
					if (node.NoSelect && eb) break; // report only one binding for non-selective variables
				}
				
			} else {
				// Buffer the results of recursing into each child,
				// and then find the permutations of the buffered results.
				
				QueryResultSink oldsink = state.sink;
				
				ArrayList resourceItems = new ArrayList(resources.Items());
				
				bool[] filteredOut = new bool[resourceItems.Count];
				bool[] multipleResults = new bool[resourceItems.Count];
				BufferSink[,] buffers = new BufferSink[resourceItems.Count,node.Children.Count];
				
				for (int i = 0; i < node.Children.Count; i++) {
					VariableNode child = (VariableNode)node.Children[i];
					//PrecacheSelect(node, resources, child, state);
					
					for (int r = 0; r < resourceItems.Count; r++) {
						if (filteredOut[r]) continue;
						
						buffers[r,i] = new BufferSink();
						state.sink = buffers[r,i];
						
						state.bindings[node.BindingIndex].Target = (Resource)resourceItems[r];
						
						// If this is the last child, and all previous children found
						// only one binding for this resource, then we can short-circuit
						// the permutation process and emit the recursion on this child directly.
						if (i == node.Children.Count-1 && !multipleResults[r]) {
							state.sink = oldsink;
							
							// Copy over the bindings for the individual variables
							// set in the children of this node.
							state.bindings = (VariableBinding[])buffers[r,0].Bindings[0];							
							for (int c = 1; c < node.Children.Count-1; c++)
								((VariableNode)node.Children[c]).CopyBindings((VariableBinding[])buffers[r,c].Bindings[0], state.bindings);
							
							filteredOut[r] = true; // requires no more processing later
						}
						
						bool eb;
						if (Recurse(state, child, out eb)) return true;
						
						if (state.sink == oldsink) {
							emittedBinding |= eb;
							if (node.NoSelect && eb) return false; // report only one binding for non-selective variables
						}
						
						// If no new bindings were found for this resource,
						// the resource is filtered out for subsequent children.
						if (buffers[r,i].Bindings.Count == 0) {
							filteredOut[r] = true;
							resources.Remove(resourceItems[r]);
						}
						if (buffers[r,i].Bindings.Count > 1)
							multipleResults[r] = true;
					}
				}
				
				int[] counters = new int[node.Children.Count+1];
				
				for (int r = 0; r < resourceItems.Count; r++) {
					if (filteredOut[r]) continue;
					
					emittedBinding = true;
					
					// Permute through the combinations of variable bindings determined
					// by each child.  In 'good' queries, all but one child produces
					// only one variable binding per possible resource for this node.
					
					Array.Clear(counters, 0, counters.Length);
					
					while (counters[node.Children.Count] == 0) {
						VariableBinding[] binding = (VariableBinding[])buffers[r,0].Bindings[counters[0]];
						
						for (int c = 1; c < node.Children.Count; c++) {
							// Copy over the bindings for the individual variables
							// set in the children of this node.
							VariableNode child = (VariableNode)node.Children[c];
							child.CopyBindings((VariableBinding[])buffers[r,c].Bindings[counters[c]], binding);
						}

						oldsink.Add(binding);
						
						if (node.NoSelect) break; // report only one binding for non-selective variables
						
						// Increment the counters and carry.
						counters[0]++;
						for (int c = 0; c < node.Children.Count; c++) {
							if (counters[c] == buffers[r,c].Bindings.Count) {
								counters[c] = 0;
								counters[c+1]++;
							} else {
								break;
							}
						}
					}
				}
			}
			
			return false;
		}
		
		private ArrayList SymmetricSelect(Entity e, Entity p, Store model) {
			ArrayList ret = new ArrayList();
			model.Select(new Statement(e, p, null), new PutInArraySink(2, ret));
			model.Select(new Statement(null, p, e), new PutInArraySink(0, ret));
			return ret;
		}
		
		private class SelectCacheKey {
			public Statement Q;
			public SelectPartialFilter F;
			public VariableNode Var;
			
			public SelectCacheKey(Statement q, SelectPartialFilter f, VariableNode var) {
				Q = q;
				F = f;
				Var = var;
			}
			
			public override int GetHashCode() { return Q.GetHashCode(); }
			public override bool Equals(object other) {
				return ((SelectCacheKey)other).Var == Var && Q.Equals(((SelectCacheKey)other).Q)
				 && F.Equals(((SelectCacheKey)other).F);
			}
		}
		
		private Set SelectCache(Statement q, int spo, SearchState state, Store queryTarget, VariableNode var) {
			if (var != null && valueFilters[var.BindingIndex].Count == 0)
				var = null;
			
			//Console.WriteLine(q);
			
			//Console.WriteLine((queryTarget == null ? "SQL" : "MEM") + " : " + q + " (" + (var == null ? "no var" : var.Variable.ToString()) + ")");
			
			if (queryTarget == null)
				queryTarget = state.target;
			
			SelectPartialFilter f = new SelectPartialFilter(
				spo == 0 ? true : false,
				spo == 1 ? true : false,
				spo == 2 ? true : false,
				false
				);
			
			SelectCacheKey key = new SelectCacheKey(q, f, var);
			
			Set cached = (Set)state.cachedSelections[key];
			if (cached == null) {
				cached = new Set();
				queryTarget.Select(q, f, new PutInSet(spo, cached));
				
				// Do value filters
				if (var != null)
					ApplyValueFilters(cached, valueFilters[var.BindingIndex], state.target);
							
				state.cachedSelections[key] = cached;
			}
			
			return cached;
		}
		
		private void ApplyValueFilters(Set resources, ArrayList valueFilters, Store target) {
			foreach (ValueFilter f in valueFilters) {
				foreach (Resource r in resources.Items()) {
					if (!(r is Literal) && f is LiteralValueFilter)
						resources.Remove(r);
					else if (!f.Filter(r, target))
						resources.Remove(r);
				}
			}
		}
		
		private class PutInArraySink : StatementSink {
			Hashtable seen = new Hashtable();
			int spo;
			ArrayList sink;
			
			public PutInArraySink(int spo, ArrayList sink) {
				this.spo = spo; this.sink = sink;
			}

			public bool Add(Statement statement) {
				Resource r;
				if (spo == 0) r = statement.Subject;
				else if (spo == 1) r = statement.Predicate;
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
			int spo;
			Set sink;
			
			public PutInSet(int spo, Set sink) {
				this.spo = spo; this.sink = sink;
			}

			public bool Add(Statement statement) {
				if (spo == 0) sink.Add(statement.Subject);
				else if (spo == 1) sink.Add(statement.Predicate);
				else sink.Add(statement.Object);
				return true;
			}
		}

		private class PutInHash : StatementSink {
			int spo1, spo2, spo3;
			Hashtable cache;
			
			public PutInHash(int spo1, int spo2, int spo3, Hashtable cache) {
				this.spo1 = spo1; this.spo2 = spo2; this.spo3 = spo3; this.cache = cache;
			}

			public bool Add(Statement statement) {
				Resource a, b, c;
				if (spo1 == 0) a = statement.Subject;
				else if (spo1 == 1) a = statement.Predicate;
				else a = statement.Object;
				if (spo2 == 0) b = statement.Subject;
				else if (spo2 == 1) b = statement.Predicate;
				else b = statement.Object;
				if (spo3 == 0) c = statement.Subject;
				else if (spo3 == 1) c = statement.Predicate;
				else c = statement.Object;
				cache[new PreCacheKey(a,b)] = c;
				return true;
			}
		}

		private class Set {
			Hashtable store;
			ArrayList items;
			
			public Set() { store = new Hashtable(); }
			public Set(Set other) { store = (Hashtable)other.store.Clone(); }
			
			public void Add(object r) { store[r] = r; items = null; }
			public void Remove(object r) { store.Remove(r); items = null; }
			public bool Contains(object r) { return store.ContainsKey(r); }
			
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
			public override void Init(Entity[] variables) { }
			public override void Finished() { }
		}
	
		private class BufferSink : QueryResultSink {
			public ArrayList Bindings = new ArrayList();
			public override bool Add(VariableBinding[] result) {
				Bindings.Add(result.Clone());
				return true;
			}
			public override void Init(Entity[] variables) { }
			public override void Finished() { }
		}

		private class StartLimitSink : QueryResultSink {
			int start, limit;
			QueryResultSink sink;
			int counter = 0;
			public StartLimitSink(int start, int limit, QueryResultSink sink) { this.start = start; this.limit = limit; this.sink = sink; }
			public override bool Add(VariableBinding[] result) {
				counter++;
				if (counter >= start || start == -1) sink.Add(result);
				if (counter == limit) return false;
				return true;
			}
			public override void Init(Entity[] variables) { sink.Init(variables); }
			public override void Finished() { sink.Finished(); }
		}
		
		private class PreCacheKey {
			Resource s, p;
			public PreCacheKey(Resource subject, Resource predicate) { s = subject; p = predicate; }
			public override bool Equals(object other) {
				return ((PreCacheKey)other).s.Equals(s) && ((PreCacheKey)other).p.Equals(p);
			}
			public override int GetHashCode() {
				return s.GetHashCode() ^ p.GetHashCode();
			}
		}
	}
	
	public abstract class QueryResultSink {
		public abstract void Init(Entity[] variables);
		public abstract bool Add(VariableBinding[] result);
		public abstract void Finished();
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
		
		public Entity Variable { get { return v; } }
		public Resource Target { get { return t; } internal set { t = value; } }
	}
}	

