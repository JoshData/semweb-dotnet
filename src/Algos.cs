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
		public void Select(Statement template, StatementSink sink) {
			a.Select(template, new Tester(b, sink));
		}
		public void Select(Statement[] templates, StatementSink sink) {
			a.Select(templates, new Tester(b, sink));
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
			foreach (MSG.Graph msgg in MSG.FindMSGs(store)) {
				// Load the MSG into memory.
				MemoryStore msg = new MemoryStore(msgg);

				// Make this MSG lean.
				MemoryStore msgremoved = new MemoryStore();
				MakeLeanMSG(store, msg, msgremoved);
				store.RemoveAll(msgremoved);
				if (removed != null) msgremoved.Select(removed);
				if (msg.StatementCount == 0) continue;

				// Remove this MSG if it is already entailed.
				
				// The GraphMatch will treat all blank nodes in
				// msg as variables.
				GraphMatch match = new GraphMatch(msg);
				QueryResultBufferSink sink = new QueryResultBufferSink();
				match.Run(new SubtractionSource(store, msg), sink);
				if (sink.Bindings.Count > 0) {
					// This MSG can be removed.
					store.RemoveAll(msg);
					if (removed != null) msg.Select(removed);
				}
			}
		}
		
		private static void MakeLeanMSG(Store graph, Store msg, StatementSink removed) {
			// For each of the 2^N subgraphs of store
			// containing all (or not all) of the
			// statements refering to each blank node,
			// check if the store minus that subgraph
			// entails the subgraph.
			
			Entity[] entities = msg.GetEntities();
			ArrayList bnodes = new ArrayList();
			foreach (Entity e in entities)
				if (e.Uri == null)
					bnodes.Add(e);
					
			// Set up an array for the permutations.
			// It would be better to organize this to
			// minimize the amount of change in the
			// variables set and the subgraph store
			// in each iteration.
			bool[] permute = new bool[bnodes.Count+1];
			while (!permute[bnodes.Count]) {
				// Get all of the statements that mention
				// the bnodes marked as 'true' in this
				// permutation.
				
				ResSet variables = new ResSet();
				for (int i = 0; i < bnodes.Count; i++)
					if (permute[i])
						variables.Add((Entity)bnodes[i]);
			
				MemoryStore subgraph = new MemoryStore();
				msg.Select(new Sink(variables, subgraph));
			
				// We only need to consider this permutation if
				// the nodes are connected among themselves.  But
				// we need a really fast way of doing this.				
			
				// Check if subgraph is entailed by (store minus subgraph)
				GraphMatch match = new GraphMatch(subgraph);
				
				// Bnodes that aren't variables in this subgraph
				// (false in this permutation) must be marked
				// as non-variables.
				for (int i = 0; i < bnodes.Count; i++)
					if (!permute[i])
						match.SetNonVariable((Entity)bnodes[i]);

				// Permute before any 'continue' statements.
				permute[0] = !permute[0];
				for (int i = 0; i < bnodes.Count; i++) {
					if (permute[i] == true) break;
					permute[i+1] = !permute[i+1];
				}

				if (subgraph.StatementCount == 0) continue; // things may have been removed, meta entities, etc.

				QueryResultBufferSink qsink = new QueryResultBufferSink();
				
				// The next line is wrong.  'msg' should be 'graph'.
				// But there's a bug that makes that not work.  Also,
				// since msg is in memory already, maybe we can forgo
				// making the graph completely lean and just see if
				// msg, rather than the whole graph, entails this subgraph.
				match.Run(new SubtractionSource(msg, subgraph), qsink);
				
				if (qsink.Bindings.Count > 0) {
					// This subgraph can be removed.
					msg.RemoveAll(subgraph);
					
					// Track which statements were removed.
					if (removed != null) subgraph.Select(removed);
				}
				
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
		// ArrayList objects.)
		public static Graph[] FindMSGs(StatementSource source) {
			FindMSGsSink sink = new FindMSGsSink();
			source.Select(sink);
			ArrayList graphs = new ArrayList();
			foreach (ArrayList a in sink.colors.Keys)
				graphs.Add(new Graph(source, a));
			return (Graph[])graphs.ToArray(typeof(Graph));
		}
		
		public class Graph : StatementSource {
			StatementSource source;
			ResSet entities = new ResSet();
			internal Graph(StatementSource source, ArrayList entities) {
				this.source = source;
				foreach (Entity e in entities)
					this.entities.Add(e);
			}
			public bool Contains(Entity e) {
				return entities.Contains(e);
			}
			public ICollection GetEntites() {
				return entities.Items;
			}
			public void Select(StatementSink s) {
				source.Select(new Sink(this, s));
			}
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

}