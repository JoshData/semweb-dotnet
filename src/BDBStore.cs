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
		}

		Quad QuadFromStatement(Statement s) {
			Quad q = new Quad();
			q.S = (int)GetResKey(s.Subject, true);
			q.P = (int)GetResKey(s.Predicate, true);
			q.O = (int)GetResKey(s.Object, true);
			q.OT = s.Object is Entity ? 0 : 1;
			q.M = (int)GetResKey(s.Meta, true);
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
		Statement QuadToStatement(Quad q) {
			return new Statement(
				(Entity)GetRes((uint)q.S, 0),
				(Entity)GetRes((uint)q.P, 0),
				GetRes((uint)q.O, q.OT),
				(Entity)GetRes((uint)q.M, 0)
				);
		}
		Resource GetRes(uint key, int type) {
			if (key == 1) return Statement.DefaultMeta;
			Resource ret;
			if (type == 0)
				ret = new Entity((string)db_entities_id.Get(key));
			else
				ret = Literal.Parse((string)db_literals_id.Get(key), null);
			SetResourceKey(ret, key);
			return ret;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			Quad q = QuadFromStatement(statement);
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
			using (BDB43.Cursor cursor = db_statements.NewCursor()) {
				object k, v;
				while (cursor.Get(BDB43.Cursor.Seek.Next, out k, out v)) {
					Quad q = Quad.Deserialize( (int[])k , 0 );
					Statement s = QuadToStatement(q);
					if (!sink.Add(s))
						break;
				}
			}
		}
		
		private void Select2(Statement template, StatementSink sink) {
			BDB43.Cursor cursor = new BDB43.Cursor();
			int cspom = -1;
			try {
				for (int spom = 0; spom < 4; spom++) {
					Resource r = null;
					if (spom == 0) r = template.Subject;
					if (spom == 1) r = template.Predicate;
					if (spom == 2) r = template.Object;
					if (spom == 3) r = template.Meta;
					if (r == null) continue;
					
					uint rkey = GetResKey(r, false);
					if (rkey == 0) return;
					
					BDB43.Cursor c = db_index.NewCursor();
					
					if (c.MoveTo(rkey) == null)
						return;
					
					// Note that cursor.Count() is the number of
					// blocks of index entries, so it's only
					// a stand-in for a count of the number of
					// statements.
					
					if (cspom == -1 || c.Count() < cursor.Count()) {
						if (cspom != -1)
							cursor.Dispose();
						cursor = c;
						cspom = spom;
					}
				}
				
				object key, value;
				BDB43.Cursor.Seek seek = BDB43.Cursor.Seek.Current;
				while (cursor.Get(seek, out key, out value)) {
					seek = BDB43.Cursor.Seek.NextDup;
					
					int[] statements = (int[])value;
					for (int i = 0; i < statements.Length/5; i+=5) {
						Statement s = QuadToStatement(Quad.Deserialize(statements, i));
						if (!template.Matches(s)) continue;
						if (!sink.Add(s)) return;
					}
				}
			} finally {
				if (cspom != -1)
					cursor.Dispose();
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
		
		public override Entity[] GetAllEntities() {
			return GetEntities(0);
		}
		
		public override Entity[] GetAllPredicates() {
			return GetEntities(1);
		}
		
		public override Entity[] GetAllMetas() {
			return GetEntities(2);
		}

		public override void Select(Statement[] templates, StatementSink result) {
			foreach (Statement s in templates)
				Select(s, result);
		}
	}

}