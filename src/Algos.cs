using System;

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

	// This class makes a graph lean by removing all
	// MSGs entailed by the rest of the graph.
	public static class Lean {
	
		public static void MakeLean(Store store) {
			Entity[] entities = store.GetEntities();
			foreach (Entity e in entities) {
				if (e.Uri != null) continue;
				// We can also skip entities that were
				// part of MSGs we've already processed.
				
				MemoryStore msg = MSG.FindMSG(store, e);
				if (msg.StatementCount == 0) continue; // entities used only as meta nodes
				
				GraphMatch match = new GraphMatch(msg);
				QueryResultBufferSink sink = new QueryResultBufferSink();
				match.Run(new SubtractionSource(store, msg), sink);
				if (sink.Bindings.Count > 0) {
					// This MSG can be removed.
					store.RemoveAll(msg);
				}
			}
		}
	
	}

	// This class finds all minimal self-describing graphs
	// in a graph.
	public static class MSG {

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
	}

}