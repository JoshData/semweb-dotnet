using System;
using System.Collections;

using SemWeb;
using SemWeb.Query;
using SemWeb.Util;

namespace SemWeb.Algos {

	class SubtractionSource : SelectableSource {
		SelectableSource a, b;
		public SubtractionSource(SelectableSource a, SelectableSource b) {
			this.a = a;
			this.b = b;
		}
		public bool Contains(Statement template) {
			return Store.Contains(this, template);
		}
		public void Select(StatementSink sink) {
			Select(Statement.All, sink);
		}
		public void Select(Statement template, StatementSink sink) {
			a.Select(template, new Tester(b, sink));
		}
		public void Select(Entity[] subjects, Entity[] predicates, Resource[] objects, Entity[] metas, StatementSink sink) {
			a.Select(subjects, predicates, objects, metas, new Tester(b, sink));
		}
		class Tester : StatementSink {
			SelectableSource b;
			StatementSink c;
			public Tester(SelectableSource b, StatementSink c) { this.b = b; this.c = c;}
			public bool Add(Statement s) {
				if (b.Contains(s)) return true;
				return c.Add(s);
			}
		}
	}

	// This class makes a graph lean.
	public static class Lean {
		// A graph g is not lean if it can be decomposed
		// into a and b such that a entails b.  (where
		// 'decomposed' means a and b don't overlap
		// and their union is g.)
		// One graph a entails another graph b when:
		//   Let V be the set of variables, which is the
		//   set of blank nodes that are in b but not in a.
		//   Let M be a mapping from nodes to nodes taking
		//   nodes that aren't in V to themselves.
		//   Let M* be a mapping from graphs to graphs that
		//   maps a graph to the same graph except where 
		//   each node x is replaced by M(x).
		//   If there exists an M such that M*(b) is a
		//   subgraph of a, then a entails b.
		// Let a and b be a decomposition of g, and V be
		// the variables in b w.r.t. a (as defined above).

		// Assume a entails b.
		// |a| >= |b|.
		// Since a and b are nonoverlapping, every statement
		// in b must have a variable.  b therefore contains
		// all and only the statements in g that mention a
		// variable.  (If b had a statement without a variable,
		// M*(b) would still have that statement, so it could
		// not be a subgraph of a.)
		
		// Define a N-decomposition as a decomposition of
		//   a graph g into g1 and g2 such that the nodes of
		//   N each appear in either g1 or g2, but not both.
		//   In such a decomposition, there is no statement
		//   in g that mentions a node from N and g1 and
		//   also mention a node from N and g2.
		
		// Assume b has a V-decomposition into b1 and b2.
		// Then if a entails b, a entails b1 and a entails b2.
		// Thus, if b has a V-decomposition, b need not be
		// considered as its decomposed parts will be considered.
		
		// Define 'directly connected' as a relation between
		// two nodes and a graph that is true iff there is
		// a statement in the graph that mentions both nodes.
		// Define connected (generally) as a relation between
		// two nodes x and y, a graph g, and a set S that is true
		// iff x and y are directly connected in g or else there
		// exists another node z in S such that x and z are
		// connected and z and y are connected, in g with S.
		
		// If b has a V-decomposition, then V can be decomposed
		// into V1 and V2 and b can be decomposed into b1 and b2
		// such that all nodes in V1 appear in b1 and all nodes
		// in V2 appear in b2.  It can be seen that a node in
		// V1 cannot be connected to a node in V2 w.r.t. b and V.
		// Therefore iff every node in V is connected to every
		// other node in V, then b has no V-decomposition.
		
		// The only b's to consider are those whose variables V
		// are all connected to each other in b w.r.t. V.
		
		// The plan then is first to consider MSGs, and then
		// look at their subgraphs.
	
		public static void MakeLean(Store store) {
			MakeLean(store, null);
		}
	
		public static void MakeLean(Store store, StatementSink removed) {
			MSG.Graph[] msgs = MSG.FindMSGs(store);
			
			MemoryStore painted = new MemoryStore();
			MSG.PaintStatements(msgs, store, painted);
		
			foreach (MSG.Graph msgg in msgs) {
				// Load the MSG into memory.
				MemoryStore msg = new MemoryStore(painted.Select(new Statement(null, null, null, msgg.Meta)));
				msg.Replace(msgg.Meta, Statement.DefaultMeta);

				// Make this MSG lean.
				MemoryStore msgremoved = new MemoryStore();
				MakeLeanMSG(store, msg, msgg.GetBNodes(), msgremoved);
				
				// Whatever was removed from msg, remove it from the main graph.
				store.RemoveAll(msgremoved.ToArray());
				
				// And track what was removed.
				if (removed != null) msgremoved.Select(removed);
				
				// If this MSG is now (somehow) empty (shouldn't happen,
				// but one never knows), don't test for entailment.
				if (msg.StatementCount == 0) continue;

				// Remove this MSG if it is already entailed.
				
				// The GraphMatch will treat all blank nodes in
				// msg as variables.
				GraphMatch match = new GraphMatch(msg);
				QueryResultBufferSink sink = new QueryResultBufferSink();
				match.Run(new SubtractionSource(store, msg), sink);
				if (sink.Bindings.Count > 0) {
					// This MSG can be removed.
					store.RemoveAll(msg.ToArray());
					if (removed != null) msg.Select(removed);
				}
			}
		}
		
		private static void MakeLeanMSG(Store graph, Store msg, ICollection bnodecollection, StatementSink removed) {
			// For each of the 'connected' subgraphs of msg,
			// check if the store minus that subgraph
			// entails the subgraph.
			
			// If there is only one bnode in the MSG, then
			// there are no subgraphs to check.
			if (bnodecollection.Count == 1) return;
			
			// Get an array of bnodes in the msg
			ArrayList bnodes = new ArrayList();
			Hashtable bnodeindex = new Hashtable();
			foreach (Entity e in bnodecollection) {
				bnodeindex[e] = bnodes.Count;
				bnodes.Add(e);
			}
				
			// Build a connectivity map of the bnodes
			bool[,] connected;
			Connectivity.Build(msg, out connected, bnodeindex);
			
			// Find the mean connectivity of each node.
			int meancon = 0;
			for (int i = 0; i < bnodes.Count; i++)
				for (int j = 0; j < bnodes.Count; j++)
					if (connected[i,j])
						meancon++;
			meancon /= bnodes.Count;
			
			// If there's high connectivity, then (I think)
			// we want to just enumerate the 2^N possible
			// variable choices.  Otherwise, we expect there
			// to be far fewer than 2^N contiguous subgraphs,
			// so we can do a faster but more memory-intensive
			// enumeration.  What should the mean connectivity
			// threshold be?
			Permuter permuter;
			if (meancon < 5 && bnodes.Count > 5) {
				// We'll iterate using a memory-hogging algorithm
				// that uses the connectivity.
				permuter = new SearchingPermuter(connected);
			} else {
				// We'll iterate over the 2^N choices of variables.
				permuter = new ExponentialPermuter(bnodes.Count);
			}
			
			Console.WriteLine("\nNew MSG: {0}\n", permuter.GetType().ToString());
			
			// Set up an array for the permutations.
			// It would be better to organize this to
			// minimize the amount of change in the
			// variables set and the subgraph store
			// in each iteration.
			bool[] permute;
			int considered = 0;
			while ((permute = permuter.Next()) != null) {
				considered++;
				Console.WriteLine("  considered {0} subgraphs for a {1}-node MSG (2^N={2})", considered, bnodes.Count, Math.Pow(2, bnodes.Count));
				
				// Get all of the statements that mention
				// the bnodes marked as 'true' in this
				// permutation.
				
				ResSet variables = new ResSet();
				for (int i = 0; i < bnodes.Count; i++)
					if (permute[i])
						variables.Add((Entity)bnodes[i]);
			
				MemoryStore subgraph = new MemoryStore();
				msg.Select(new Sink(variables, subgraph));
				if (subgraph.StatementCount == 0) continue; // things may have been removed, meta entities, etc.
				//subgraph.Write(Console.Out);
				
				// Note that like below, 'msg' should really be 'graph'.
				if (subgraph.StatementCount > msg.StatementCount/2) continue;

				// Check if subgraph is entailed by (store minus subgraph)
				GraphMatch match = new GraphMatch(subgraph);
				
				// Bnodes that aren't variables in this subgraph
				// (false in this permutation) must be marked
				// as non-variables.
				for (int i = 0; i < bnodes.Count; i++)
					if (!permute[i])
						match.SetNonVariable((Entity)bnodes[i]);

				QueryResultBufferSink qsink = new QueryResultBufferSink();
				
				// The next line is wrong.  'msg' should be 'graph'.
				// But there's a bug that makes that not work.  Also,
				// since msg is in memory already, maybe we can forgo
				// making the graph completely lean and just see if
				// msg, rather than the whole graph, entails this subgraph.
				match.Run(new SubtractionSource(msg, subgraph), qsink);
				
				if (qsink.Bindings.Count > 0) {
					// This subgraph can be removed.
					msg.RemoveAll(subgraph.ToArray());
					
					// Track which statements were removed.
					if (removed != null) subgraph.Select(removed);
				}
				
			}
			
			Console.WriteLine("MSG: Considered {0} subgraphs for a {1}-node MSG (2^N={2})", considered, bnodes.Count, Math.Pow(2, bnodes.Count));
		}
		
		private abstract class Permuter {
			public abstract bool[] Next();
		}
		
		private class SearchingPermuter : Permuter {
			// This is based on something I read.
			// We'll maintain a queue of connected
			// subgraphs to process.  The queue will
			// start with a one-node subgraph for each
			// bnode.  Then each time we process a
			// subgraph, we'll extend the graph by one
			// node every way we can and add all of those
			// new subgraphs into the queue -- unless we've
			// already processed the subgraph.  
		
			int n;
			bool[,] conn;
			Queue queue = new Queue();
			Hashtable processed = new Hashtable();
			
			public SearchingPermuter(bool[,] conn) {
				this.conn = conn;
				n = conn.GetLength(0);
				for (int i = 0; i < n; i++)
					QueueSubgraph(null, i);
			}
			
			void QueueSubgraph(Subgraph a, int b) {
				Subgraph s = new Subgraph();
				s.nodes = new bool[n];
				s.touching = new bool[n];
				if (a != null) {
					a.nodes.CopyTo(s.nodes, 0);
					a.touching.CopyTo(s.touching, 0);
				}
				s.nodes[b] = true;

				s.sum = unchecked((a != null ? a.sum : 0) + b);
				if (processed.ContainsKey(s)) return;
				
				for (int i = 0; i < n; i++)
					if (conn[b,i])
						s.touching[i] = true;
						
				processed[s] = processed;
				queue.Enqueue(s);
			}
			
			public override bool[] Next() {
				if (queue.Count == 0) return null;
				Subgraph s = (Subgraph)queue.Dequeue();
				
				// Create a new s for every node touching
				// s but not in s.
				for (int i = 0; i < n; i++)
					if (!s.nodes[i] && s.touching[i])
						QueueSubgraph(s, i);
				
				return s.nodes;
			}
			
			class Subgraph {
				public bool[] nodes;
				public bool[] touching;
				public int sum;
				
				public override int GetHashCode() { return sum; }
				public override bool Equals(object o) {
					Subgraph g = (Subgraph)o;
					for (int i = 0; i < nodes.Length; i++)
						if (nodes[i] != g.nodes[i])
							return false;
					return true;
				}
			}
		}
		
		private class ExponentialPermuter : Permuter {
			bool[] state;
			public ExponentialPermuter(int bnodecount) {
				state = new bool[bnodecount];
				state[0] = true; // don't need to do the first
								 // permutation with no variables
			}
			public override bool[] Next() {
				bool[] ret = (bool[])state.Clone();
				
				state[0] = !state[0];
				for (int i = 0; i < state.Length; i++) {
					if (state[i] == true) break;
					if (i == state.Length-1) {
						// We don't need to do the last
						// permutation with all true.
						return null;
					}
					state[i+1] = !state[i+1];
				}

				return ret;
			}
		}

		private class Sink : StatementSink {
			ResSet variables;
			Store store;
			public Sink(ResSet variables, Store store) {
				this.variables = variables;
				this.store = store;
			}
			public bool Add(Statement s) {
				s.Meta = Statement.DefaultMeta;
				if (store.Contains(s)) return true;
				if (variables.Contains(s.Subject)
					|| variables.Contains(s.Predicate)
					|| variables.Contains(s.Object))
					store.Add(s);
				return true;
			}
		}
	}

	public static class MSG {

		// These methods find minimal self-contained graphs
		// in a graph by recursively expanding a subgraph.
	
		public static MemoryStore FindMSG(SelectableSource store, Entity node) {
			MemoryStore ret = new MemoryStore();
			FindMSG(store, node, ret);
			return ret;
		}
		
		public static void FindMSG(SelectableSource store, Entity node, Store msg) {
			if (node.Uri != null) throw new ArgumentException("node must be anonymous");
			
			ResSet nodesSeen = new ResSet();
			ResSet nodesToAdd = new ResSet();
			
			nodesToAdd.Add(node);
			
			while (nodesToAdd.Count > 0) {
				ResSet nodes = nodesToAdd;
				nodesToAdd = new ResSet();
				
				Sink sink = new Sink(msg, nodesToAdd);
				foreach (Entity n in nodes) {
					if (nodesSeen.Contains(n)) continue;
					nodesSeen.Add(n);
					store.Select(new Statement(n, null, null, null), sink);
					store.Select(new Statement(null, n, null, null), sink);
					store.Select(new Statement(null, null, n, null), sink);
				}
			}
		}
		
		private class Sink : StatementSink {
			Store msg;
			ResSet add;
			public Sink(Store msg, ResSet add) {
				this.msg = msg;
				this.add = add;
			}
			public bool Add(Statement s) {
				if (msg.Contains(s)) return true;
				msg.Add(s);
				if (s.Subject.Uri == null) add.Add(s.Subject);
				if (s.Predicate.Uri == null) add.Add(s.Predicate);
				if (s.Object is Entity && s.Object.Uri == null) add.Add(s.Object);
				return true;
			}
		}
		
		// This method finds all minimal self-contained graphs
		// by painting nodes colors.  (The colors happen to be
		// objects.)
		public static Graph[] FindMSGs(SelectableSource source) {
			FindMSGsSink sink = new FindMSGsSink();
			source.Select(Statement.All, sink);
			ArrayList graphs = new ArrayList();
			foreach (ArrayList a in sink.colors.Keys)
				graphs.Add(new Graph(source, a));
			return (Graph[])graphs.ToArray(typeof(Graph));
		}
		
		public class Graph : StatementSource {
			SelectableSource source;
			ResSet entities = new ResSet();
			Entity meta = new Entity(null);
			internal Graph(SelectableSource source, ArrayList entities) {
				this.source = source;
				foreach (Entity e in entities)
					this.entities.Add(e);
			}
			public bool Contains(Entity e) {
				return entities.Contains(e);
			}
			public ICollection GetBNodes() {
				return entities.Items;
			}
			public void Select(StatementSink s) {
				/*StatementList templates = new StatementList();
				foreach (Entity e in GetBNodes()) {
					templates.Add(new Statement(e, null, null, null));
					templates.Add(new Statement(null, e, null, null));
					templates.Add(new Statement(null, null, e, null));
				}
			
				MemoryStore m = new MemoryStore();
				m.checkForDuplicates = true;
				source.Select(templates, m);
				m.Select(s);*/
			
				source.Select(Statement.All, new Sink(this, s));
			}
			public Entity Meta { get { return meta; } }
			private class Sink : StatementSink {
				Graph g;
				StatementSink s;
				public Sink(Graph g, StatementSink s) {
					this.g = g;
					this.s = s;
				}
				public bool Add(Statement s) {
					if (g.Contains(s.Subject)
						|| g.Contains(s.Predicate)
						|| (s.Object is Entity && g.Contains((Entity)s.Object)))
						return this.s.Add(s);
					return true;
				}
			}
		}
		
		public static void PaintStatements(Graph[] msgs, StatementSource source, StatementSink sink) {
			Hashtable ents = new Hashtable();
			foreach (Graph g in msgs) {
				foreach (Entity e in g.GetBNodes()) {
					ents[e] = g.Meta;
				}
			}
			source.Select(new PaintMSGsSink(ents, sink));
		}
		
		class PaintMSGsSink : StatementSink {
			Hashtable ents = new Hashtable();
			StatementSink sink;
			public PaintMSGsSink(Hashtable ents, StatementSink sink) {
				this.ents = ents;
				this.sink = sink;
			}
			public bool Add(Statement s) {
				if (ents.ContainsKey(s.Subject)) s.Meta = (Entity)ents[s.Subject];
				else if (ents.ContainsKey(s.Predicate)) s.Meta = (Entity)ents[s.Predicate];
				else if (ents.ContainsKey(s.Object)) s.Meta = (Entity)ents[s.Object];
				else return true;
				return sink.Add(s);
			}
		}
		
		class FindMSGsSink : StatementSink {
			Hashtable bnodecolors = new Hashtable();
			public Hashtable colors = new Hashtable();
			public bool Add(Statement s) {
				// Get the color of any painted entity in the statement.
				int numcon = 0;
				ArrayList color = null;
				if (s.Subject.Uri == null) { Go1(s.Subject, ref color); numcon++; }
				if (s.Predicate.Uri == null) { Go1(s.Predicate, ref color); numcon++; }
				if (s.Object.Uri == null && s.Object is Entity) { Go1((Entity)s.Object, ref color); numcon++; }
				
				// If there isn't more than one blank node in the statement.
				if (numcon < 2)
					return true;
				
				// No nodes were colored yet, so pick a new color.
				if (color == null) {
					color = new ArrayList();
					colors[color] = color;
				}
				
				// Apply that color to all of the nodes.
				if (s.Subject.Uri == null) Go2(s.Subject, ref color);
				if (s.Predicate.Uri == null) Go2(s.Predicate, ref color);
				if (s.Object.Uri == null && s.Object is Entity) Go2((Entity)s.Object, ref color);
				
				return true;
			}
			void Go1(Entity e, ref ArrayList color) {
				if (color == null && bnodecolors.ContainsKey(e)) {
					color = (ArrayList)bnodecolors[e];
				}
			}
			void Go2(Entity e, ref ArrayList color) {
				if (bnodecolors.ContainsKey(e)) {
					ArrayList curcolor = (ArrayList)bnodecolors[e];
					if (curcolor != color) {
						// Everyone that has the color curcolor
						// has to switch to the color color.
						foreach (Entity e2 in curcolor)
							bnodecolors[e2] = color;
						color.AddRange(curcolor);
						colors.Remove(curcolor);
					}
				} else {
					bnodecolors[e] = color;
					color.Add(e);
				}
			}
		}
		
	}
	
	public static class Connectivity {
	
		public static void Build(StatementSource graph, out bool[,] connectivity, Hashtable indexes) {
			connectivity = new bool[indexes.Count, indexes.Count];
			graph.Select(new Sink(connectivity, indexes));
		}
		
		class Sink : StatementSink {
			bool[,] connectivity;
			Hashtable indexes;
			public Sink(bool[,] connectivity, Hashtable indexes) {
				this.connectivity = connectivity;
				this.indexes = indexes;
			}
			public bool Add(Statement st) {
				int s = indexes.ContainsKey(st.Subject) ? (int)indexes[st.Subject] : -1;
				int p = indexes.ContainsKey(st.Predicate) ? (int)indexes[st.Predicate] : -1;
				int o = indexes.ContainsKey(st.Object) ? (int)indexes[st.Object] : -1;
				if (s != -1 && p != -1) { connectivity[s,p]=true; connectivity[p,s]=true; }
				if (s != -1 && o != -1) { connectivity[s,o]=true; connectivity[o,s]=true; }
				if (p != -1 && o != -1) { connectivity[p,o]=true; connectivity[o,p]=true; }
				return true;
			}
		}
	}

}