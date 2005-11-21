using System;
using System.Collections;
using System.IO;
using BDB;
using SemWeb.Util;

namespace SemWeb {

	public class BDBStore : Store, IDisposable {
		BDB43.Env env;
		BDB43
			db_info,
			db_entities_id,  db_literals_id,
			db_entities_uri, db_literals_value,
			db_index,
			db_statements;
		
		uint lastid = 0;
		long count = 0;
		
		bool isImporting = false;
		UriMap urimap;
			
		public BDBStore(string directory) {
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
				
			env = new BDB43.Env(directory);
			
			bool create = true;
			db_info = new BDB43("info", create, DBFormat.Hash, false, env);
			db_entities_id = new BDB43("entities_id", create, DBFormat.Btree, false, env);
			db_literals_id = new BDB43("literals_id", create, DBFormat.Btree, false, env);
			db_entities_uri = new BDB43("entities_uri", create, DBFormat.Btree, false, env);
			db_literals_value = new BDB43("literals_value", create, DBFormat.Btree, false, env);
			db_index = new BDB43("index", create, DBFormat.Btree, true, env);
			db_statements = new BDB43("statements", create, DBFormat.Btree, false, env);
			
			db_entities_id.KeyType = BDB.DataType.UInt;
			db_entities_id.ValueType = BDB.DataType.String;
			db_literals_id.KeyType = BDB.DataType.UInt;
			db_literals_id.ValueType = BDB.DataType.String;
			db_entities_uri.KeyType = BDB.DataType.String;
			db_entities_uri.ValueType = BDB.DataType.UInt;
			db_literals_value.KeyType = BDB.DataType.String;
			db_literals_value.ValueType = BDB.DataType.UInt;
			db_index.KeyType = BDB.DataType.UInt;
			db_index.ValueType = BDB.DataType.IntArray;
			db_statements.KeyType = BDB.DataType.IntArray;
			db_statements.ValueType = BDB.DataType.UInt;
			
			object lastidobj = db_info.Get("last_id");
			if (lastidobj == null) {
				lastid = 1;
				count = 0;
			} else {
				lastid = (uint)lastidobj;
				count = (long)db_info.Get("count");
			}
		}
		
		public void Dispose() {
			db_info.Put("last_id", lastid);
			db_info.Put("count", count);
			db_info.Close();
			db_entities_id.Close();
			db_literals_id.Close();
			db_entities_uri.Close();
			db_literals_value.Close();
			db_index.Close();
			db_statements.Close();
			env.Dispose();
		}
		
		struct Quad {
			public int S, P, O, M;
			public int OT;
			public int[] Serialize() {
				return new int[] { S, P, O, M, OT };
			}
			public static Quad Deserialize(int[] data, int index) {
				Quad q = new Quad();
				q.S = data[index+0];
				q.P = data[index+1];
				q.O = data[index+2];
				q.M = data[index+3];
				q.OT = data[index+4];
				return q;
			}
			public uint this[int index] {
				get {
					switch (index) {
					case 0: return (uint)S;
					case 1: return (uint)P;
					case 2: return (uint)O;
					case 3: return (uint)M;
					default:
						throw new ArgumentException();
					}
				}
			}
		}

		Quad QuadFromStatement(Statement s, bool create) {
			Quad q = new Quad();
			q.S = (int)GetResKey(s.Subject, create);
			q.P = (int)GetResKey(s.Predicate, create);
			q.O = (int)GetResKey(s.Object, create);
			if (s.Object != null)
				q.OT = s.Object is Entity ? 0 : 1;
			q.M = (int)GetResKey(s.Meta, create);
			return q;
		}
	
		uint GetResKey(Resource r, bool create) {
			if (r == null) return 0;
			if ((object)r == (object)Statement.DefaultMeta) return 1;

			object keyobj = GetResourceKey(r);
			if (keyobj != null) return (uint)keyobj;
			
			if (r.Uri != null && isImporting) {
				keyobj = urimap[r.Uri];
				if (keyobj != null) return (uint)keyobj;
			}
			
			uint key = 0;
			
			string stringval = null;
			BDB43 db = null, db2 = null;
			/*uint hashcode = 0;
			uint[] ids = null;*/
			
			if (r is Literal) {
				stringval = r.ToString();
				db = db_literals_value;
				db2 = db_literals_id;
			} else {
				if (r.Uri != null)
					stringval = r.Uri;
				db = db_entities_uri;
				db2 = db_entities_id;
			}
			
			if (stringval != null) {
				/*hashcode = (uint)(uint.MinValue + stringval.GetHashCode());
				ids = (uint[])db.Get(hashcode);
				if (ids != null) {
					foreach (uint id in ids) {
						string val = (string)db_resource_ids.Get(id);
						if (val == stringval) {
							key = id;
							break;
						}
					}
				}*/
				keyobj = db.Get(stringval);
				if (keyobj != null) key = (uint)keyobj;
			}
			
			if (key == 0) {
				if (!create) return 0;
			
				lastid++;
				key = lastid;
				SetResourceKey(r, key);
				
				if (stringval != null)
					db.Put(stringval, key);
					
				// Anonymous entities: stringval == null
				db2.Put(key, stringval);
			}
			
			if (r.Uri != null && isImporting) urimap[r.Uri] = key;
			
			return key;
		}
		Statement QuadToStatement(Quad q, Hashtable createdResources) {
			return new Statement(
				(Entity)GetRes((uint)q.S, 0, createdResources),
				(Entity)GetRes((uint)q.P, 0, createdResources),
				GetRes((uint)q.O, q.OT, createdResources),
				(Entity)GetRes((uint)q.M, 0, createdResources)
				);
		}
		Resource GetRes(uint key, int type, Hashtable createdResources) {
			if (key == 1) return Statement.DefaultMeta;
			if (createdResources.ContainsKey(key)) return (Resource)createdResources[key];
			Resource ret;
			if (type == 0)
				ret = new Entity((string)db_entities_id.Get(key));
			else
				ret = Literal.Parse((string)db_literals_id.Get(key), null);
			SetResourceKey(ret, key);
			createdResources[key] = ret;
			return ret;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			Quad q = QuadFromStatement(statement, true);
			int[] qs = q.Serialize();
			if (!db_statements.PutNew(qs, (uint)0)) return;
			Index(qs, (uint)q.S);
			Index(qs, (uint)q.P);
			Index(qs, (uint)q.O);
			Index(qs, (uint)q.M);
			count++;
		}
		
		Hashtable pendingIndexes;
		
		void Index(int[] qs, uint key) {
			if (!isImporting) {
				db_index.Put(key, qs);
			} else {
				ArrayList idx = (ArrayList)pendingIndexes[key];
				if (idx == null) {
					idx = new ArrayList();
					pendingIndexes[key] = idx;
				}
				idx.AddRange(qs);
				if (pendingIndexes.Count > 5000) {
					WritePendingIndexes();
				} else if (idx.Count > 50000) {
					db_index.Put(key, idx.ToArray(typeof(int)));
					idx.Clear();
				}
			}
		}
		
		void WritePendingIndexes() {
			foreach (DictionaryEntry keyval in pendingIndexes) {
				ArrayList idx = (ArrayList)keyval.Value;
				if (idx.Count == 0) continue;
				db_index.Put(keyval.Key, idx.ToArray(typeof(int)));
				idx.Clear();
			}
			pendingIndexes.Clear();
		}
		
		public override void Import(StatementSource source) {
			if (isImporting) throw new InvalidOperationException();
			try {
				urimap = new UriMap();
				pendingIndexes = new Hashtable();
				isImporting = true;
				
				source.Select(this);
				WritePendingIndexes();
				
			} finally {
				isImporting = true;
				urimap = null;
				pendingIndexes = null;
			}
		}
		
		public override void Select(Statement template, StatementSink sink) {
			if (template == Statement.All)
				SelectAll(sink);
			else
				Select2(template, sink);
		}
		
		private void SelectAll(StatementSink sink) {
			Hashtable createdResources = new Hashtable();
			using (BDB43.Cursor cursor = db_statements.NewCursor()) {
				object k, v;
				while (cursor.Get(BDB43.Cursor.Seek.Next, out k, out v)) {
					Quad q = Quad.Deserialize( (int[])k , 0 );
					Statement s = QuadToStatement(q, createdResources);
					if (!sink.Add(s))
						break;
				}
			}
		}
		
		bool GetCursor(int[] keys, ref BDB43.Cursor cursor, ref int index) {
			// Find the cursor over one of the resources in qtemplate
			// that has the fewest number of index blocks.
			bool found = false;
			for (int i = 0; i < keys.Length; i++) {
				uint rkey = (uint)keys[i];
				if (rkey == 0) continue;
				
				BDB43.Cursor c = db_index.NewCursor();
				
				if (c.MoveTo(rkey) == null) {
					c.Dispose();
					if (found) cursor.Dispose();
					return false;
				}
				
				// Note that cursor.Count() is the number of
				// blocks of index entries, so it's only
				// a stand-in for a count of the number of
				// statements.
				
				if (!found || c.Count() < cursor.Count()) {
					if (found)
						cursor.Dispose();
					cursor = c;
					index = i;
					found = true;
				} else {
					c.Dispose();
				}
			}
			return found;
		}
		
		void RunCursor(BDB43.Cursor cursor, Quad qtemplate, Hashtable createdResources, QuadSink sink, Hashtable[] filters) {
			object key, value;
			BDB43.Cursor.Seek seek = BDB43.Cursor.Seek.Current;
			while (cursor.Get(seek, out key, out value)) {
				seek = BDB43.Cursor.Seek.NextDup;
				
				int[] statements = (int[])value;
				for (int i = 0; i < statements.Length; i+=5) {
					Quad q = Quad.Deserialize(statements, i);
					
					bool matched = true;
					for (int spom = 0; spom < 4; spom++) {
						if (qtemplate[spom] != 0 && qtemplate[spom] != q[spom]) {
							matched = false;
							break;
						}
						if (filters != null && filters[spom] != null && !filters[spom].ContainsKey(q[spom])) {
							matched = false;
							break;
						}
					}
					if (!matched) continue;
					
					if (!sink.Add(q)) return;
				}
			}
		}
		
		abstract class QuadSink {
			protected BDBStore store;
			protected Hashtable createdResources;
			public QuadSink(BDBStore store, Hashtable createdResources) {
				this.store = store;
				this.createdResources = createdResources;
			}
			public abstract bool Add(Quad q);
		}
		class QuadSink2 : QuadSink {
			StatementSink sink;
			public QuadSink2(StatementSink sink, BDBStore store, Hashtable createdResources) : base(store, createdResources) {
				this.sink = sink;
			}
			public override bool Add(Quad q) {
				Statement s = store.QuadToStatement(q, createdResources);
				return sink.Add(s);
			}
		}
		
		private void Select2(Statement template, StatementSink sink) {
			Quad qtemplate = QuadFromStatement(template, false);
			if (template.Subject != null && qtemplate.S == 0) return;
			if (template.Predicate != null && qtemplate.P == 0) return;
			if (template.Object != null && qtemplate.O == 0) return;
			if (template.Meta != null && qtemplate.M == 0) return;
			
			if (!template.AnyNull) {
				object v = db_statements.Get(qtemplate.Serialize());
				if (v != null)
					sink.Add(template);
				return;
			}
		
			BDB43.Cursor cursor = new BDB43.Cursor();
			int cursor_index = 0;
			if (!GetCursor(qtemplate.Serialize(), ref cursor, ref cursor_index))
				return;
			try {
				Hashtable createdResources = new Hashtable();
				if (template.Subject != null) createdResources[qtemplate.S] = template.Subject;
				if (template.Predicate != null) createdResources[qtemplate.P] = template.Predicate;
				if (template.Object != null) createdResources[qtemplate.O] = template.Object;
				if (template.Meta != null) createdResources[qtemplate.M] = template.Meta;
				QuadSink2 sink2 = new QuadSink2(sink, this, createdResources);
				
				RunCursor(cursor, qtemplate, createdResources, sink2, null);
				
			} finally {
				cursor.Dispose();
			}
		}
		
		public override void Select(Statement[] templates, StatementSink result) {
			foreach (Statement s in templates)
				Select(s, result);
		}
		
		/*public override void Select(Statement[] templates, StatementSink result) {
			if (templates == null) throw new ArgumentNullException();
			if (result == null) throw new ArgumentNullException();
			if (templates.Length == 0) return;
	
			// This is very similar in SQL store...
			// Determine for each SPOM, if any are constant in all templates.
			bool first = true;
			Resource sv = null, pv = null, ov = null, mv = null;
			bool sm = false, pm = false, om = false, mm = false;
			ArrayList sl = new ArrayList(), pl = new ArrayList(), ol = new ArrayList(), ml = new ArrayList();
			foreach (Statement template in templates) {
				if (first) {
					first = false;
					sv = template.Subject;
					pv = template.Predicate;
					ov = template.Object;
					mv = template.Meta;
				} else {
					if (sv != template.Subject) sm = true;
					if (pv != template.Predicate) pm = true;
					if (ov != template.Object) om = true;
					if (mv != template.Meta) mm = true;
				}
				if (template.Subject != null) sl.Add(template.Subject);
				if (template.Predicate != null) pl.Add(template.Predicate);
				if (template.Object != null) ol.Add(template.Object);
				if (template.Meta != null) ml.Add(template.Meta);
			}
			
			// No SPOMs are changed, thus all templates are the same.
			if (!sm && !pm && !om && !mm) {
				Select(templates[0], result);
			
			// Just one SPOM changes in the templates.
			} else if (sm && !pm && !om && !mm) {
				Select3(sl, pl, ol, ml, result);
			} else if (!sm && pm && !om && !mm) {
				Select3(sl, pl, ol, ml, result);
			} else if (!sm && !pm && om && !mm) {
				Select3(sl, pl, ol, ml, result);
			} else if (!sm && !pm && !om && mm) {
				Select3(sl, pl, ol, ml, result);
			
			// Otherwise do individual selects.
			} else {
				foreach (Statement template in templates)
					Select(template, result);
			}
		}
		
		void Select3(ArrayList sl, ArrayList pl, ArrayList ol, ArrayList ml, StatementSink sink) {
			// Just one of the above lists has more than one value.
			
			// For that list, build a hashtable of values so we
			// can do a ContainsKey quickly later.
			Hashtable[] filters = new Hashtable[4];
			Hashtable values = new Hashtable();
			ArrayList vl = null;
			if (sl.Count > 1) { vl = sl; filters[0] = values; }
			if (pl.Count > 1) { vl = pl; filters[1] = values; }
			if (ol.Count > 1) { vl = ol; filters[2] = values; }
			if (ml.Count > 1) { vl = ml; filters[3] = values; }
			foreach (Resource r in vl) {
				uint rkey = GetResKey(r, false);
				if (rkey != 0) values[rkey] = values;
			}
			if (values.Count == 0) return;
			
			Quad qtemplate = new Quad();
			if (sl.Count == 1) { qtemplate.S = (int)GetResKey((Resource)sl[0], false); if (qtemplate.S == 0) return; }
			if (pl.Count == 1) { qtemplate.P = (int)GetResKey((Resource)pl[0], false); if (qtemplate.P == 0) return; }
			if (ol.Count == 1) { qtemplate.O = (int)GetResKey((Resource)ol[0], false); if (qtemplate.O == 0) return; }
			if (ml.Count == 1) { qtemplate.M = (int)GetResKey((Resource)ml[0], false); if (qtemplate.M == 0) return; }			
			
			
			// Then do a select like normal, except don't use
			// an index for the list with multiple value.
			BDB43.Cursor cursor = new BDB43.Cursor();
			int cspom = -1;
			try {
				if (!GetCursor(qtemplate, ref cursor, ref cspom))
					return;
				
				Hashtable createdResources = new Hashtable();
				
				RunCursor(cursor, qtemplate, createdResources, sink, filters);
			} finally {
				if (cspom != -1)
					cursor.Dispose();
			}
		}*/
		
		public override Entity[] FindEntities(Statement[] filters) {
			// Convert each filter to a Quad.
			Quad[] qfilters = new Quad[filters.Length];
			for (int i = 0; i < filters.Length; i++) {
				qfilters[i] = QuadFromStatement(filters[i], false);
				if (filters[i].Subject != null && qfilters[i].S == 0) return new Entity[0];
				if (filters[i].Predicate != null && qfilters[i].P == 0) return new Entity[0];
				if (filters[i].Object != null && qfilters[i].O == 0) return new Entity[0];
				if (filters[i].Meta != null && qfilters[i].M == 0) return new Entity[0];
			}
			
			// Assemble a list of ids.
			int[] ids = new int[filters.Length*4];
			int idx = 0;
			for (int i = 0; i < filters.Length; i++) {
				ids[idx++] = qfilters[i].S;
				ids[idx++] = qfilters[i].P;
				ids[idx++] = qfilters[i].O;
				ids[idx++] = qfilters[i].M;
			}
			
			// Find the best cursor for all of the resources.
			BDB43.Cursor cursor = new BDB43.Cursor();
			int id_index = 0;
			if (!GetCursor(ids, ref cursor, ref id_index))
				return new Entity[0];
			
			int filter_index = id_index / 4;
			int spom2 = 0;
			for (int i = 0; i < 4; i++) {
				if (qfilters[filter_index][i] == 0) {
					spom2 = i;
					break;
				}
			}
			
			// For each matching resource for that filter, test
			// if the resource passes the rest of the filters.
			Hashtable createdResources = new Hashtable();
			TestContains sink2 = new TestContains(qfilters, filter_index, spom2, this, createdResources);
			try {
				RunCursor(cursor, qfilters[filter_index], createdResources, sink2, null);
			} finally {
				cursor.Dispose();
			}
			
			return (Entity[])sink2.ents.ToArray(typeof(Entity));
		}
		
		class TestContains : QuadSink {
			Quad[] qfilters;
			int filter_index, spom;
			public ArrayList ents = new ArrayList();
			public TestContains(Quad[] qfilters, int filter_index, int spom, BDBStore store, Hashtable createdResources) : base(store, createdResources) {
				this.qfilters = qfilters;
				this.filter_index = filter_index;
				this.spom = spom;
			}
			public override bool Add(Quad q) {
				uint res = q[spom];
				int restype = (spom == 2 ? q.OT : 0);
						
				for (int i = 0; i < qfilters.Length; i++) {
					if (i == filter_index) continue;
					Quad q2 = qfilters[i];
					if (q2.S == 0) q2.S = (int)res;
					if (q2.P == 0) q2.P = (int)res;
					if (q2.O == 0) { q2.O = (int)res; q2.OT = restype; }
					if (q2.M == 0) q2.M = (int)res;
					
					object v = store.db_statements.Get(q2.Serialize());
					if (v == null)
						return false;
				}
				
				Resource r = store.GetRes(res, restype, createdResources);
				ents.Add(r);
				
				return true;
			}
		}

		public override int StatementCount {
			get {
				return (int)count;
			}
		}
		
		public override void Clear() {
			db_info.Truncate();
			db_entities_id.Truncate();
			db_literals_id.Truncate();
			db_entities_uri.Truncate();
			db_literals_value.Truncate();
			db_index.Truncate();
			db_statements.Truncate();
		}
		
		public override void Remove(Statement statement) {
			if (statement == Statement.All) { Clear(); return; }
			throw new NotImplementedException();
		}
		
		Entity[] GetEntities(int filter) {
			ArrayList ents = new ArrayList();
			using (BDB43.Cursor cursor = db_entities_id.NewCursor()) {
				object k, v;
				while (cursor.Get(BDB43.Cursor.Seek.Next, out k, out v)) {
					uint id = (uint)k;
					string uri = (string)v;
					Entity e = new Entity(uri);
					if (filter == 1 && !Contains(new Statement(null, e, null, null)))
						continue;
					if (filter == 2 && !Contains(new Statement(null, null, null, e)))
						continue;
					SetResourceKey(e, id);
					ents.Add(e);
				}
			}
			return (Entity[])ents.ToArray(typeof(Entity));
		}
		
		public override Entity[] GetEntities() {
			return GetEntities(0);
		}
		
		public override Entity[] GetPredicates() {
			return GetEntities(1);
		}
		
		public override Entity[] GetMetas() {
			return GetEntities(2);
		}
	}

}