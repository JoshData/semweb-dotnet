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
			db_resource_ids,
			db_entities_uri, db_literals_value,
			db_index,
			db_statements;
		
		uint lastid = 0;	
			
		public BDBStore(string directory) {
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
				
			env = new BDB43.Env(directory);
			
			bool create = true;
			db_info = new BDB43("info", create, DBFormat.Hash, false, env);
			db_resource_ids = new BDB43("resource_ids", create, DBFormat.Btree, false, env);
			db_entities_uri = new BDB43("entities_uri", create, DBFormat.Btree, false, env);
			db_literals_value = new BDB43("literals_value", create, DBFormat.Btree, false, env);
			db_index = new BDB43("index", create, DBFormat.Btree, true, env);
			db_statements = new BDB43("statements", create, DBFormat.Btree, false, env);
			
			db_resource_ids.KeyType = BDB.DataType.UInt;
			db_resource_ids.ValueType = BDB.DataType.String;
			db_entities_uri.KeyType = BDB.DataType.String;
			db_entities_uri.ValueType = BDB.DataType.UInt;
			db_literals_value.KeyType = BDB.DataType.String;
			db_literals_value.ValueType = BDB.DataType.UInt;
			db_index.KeyType = BDB.DataType.UInt;
			db_index.ValueType = BDB.DataType.IntArray;
			db_statements.KeyType = BDB.DataType.IntArray;
			db_statements.ValueType = BDB.DataType.UInt;
			
			object lastidobj = db_info.Get("last_id");
			if (lastidobj == null)
				lastid = 1;
			else
				lastid = (uint)lastidobj;
		}
		
		public void Dispose() {
			db_info.Put("last_id", lastid);
			db_info.Close();
			db_resource_ids.Close();
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
			public static Quad Deserialize(int[] data) {
				Quad q = new Quad();
				q.S = data[0];
				q.P = data[1];
				q.O = data[2];
				q.M = data[3];
				q.OT = data[4];
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
			
			uint key = 0;
			
			string stringval = null;
			BDB43 db = null;
			/*uint hashcode = 0;
			uint[] ids = null;*/
			
			if (r is Literal) {
				stringval = r.ToString();
				db = db_literals_value;
			} else if (r.Uri != null) {
				stringval = r.Uri;
				db = db_entities_uri;
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
				
				if (r is Literal || r.Uri != null) {
					/*if (ids == null) {
						db.Put(hashcode, new uint[] { (uint)key } );
					} else {
						uint[] ids2 = new uint[ids.Length+1];
						ids.CopyTo(ids2, 0);
						ids2[ids2.Length-1] = (uint)key;
						db.Put(hashcode, ids2);
					}*/
					db.Put(stringval, key);
					db_resource_ids.Put(key, stringval);
				}
			}
			
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
				ret = new Entity((string)db_resource_ids.Get(key));
			else
				ret = Literal.Parse((string)db_resource_ids.Get(key), null);
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
		}
		
		Hashtable pendingIndexes = new Hashtable();
		
		void Index(int[] qs, uint key) {
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
				pendingIndexes.Remove(key);
			}
		}
		
		void WritePendingIndexes() {
			foreach (DictionaryEntry keyval in pendingIndexes) {
				ArrayList idx = (ArrayList)keyval.Value;
				db_index.Put(keyval.Key, idx.ToArray(typeof(int)));
			}
			pendingIndexes.Clear();
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
					Quad q = Quad.Deserialize( (int[])k );
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
					Statement s = QuadToStatement(Quad.Deserialize( (int[])value ));
					if (!template.Matches(s)) continue;
					if (!sink.Add(s)) return;
				}
			} finally {
				if (cspom != -1)
					cursor.Dispose();
			}
		}
		
		public override int StatementCount {
			get {
				return 0;
			}
		}
		
		public override void Clear() {
			db_info.Truncate();
			db_resource_ids.Truncate();
			db_entities_uri.Truncate();
			db_literals_value.Truncate();
			db_index.Truncate();
			db_statements.Truncate();
		}
		
		public override void Remove(Statement statement) {
			throw new NotImplementedException();
		}
		
		public override Entity[] GetAllEntities() {
			throw new NotImplementedException();
		}
		
		public override Entity[] GetAllPredicates() {
			throw new NotImplementedException();
		}
		
		public override Entity[] GetAllMetas() {
			throw new NotImplementedException();
		}

		public override void Select(Statement[] templates, StatementSink result) {
			foreach (Statement s in templates)
				Select(s, result);
		}
		
		public override void Replace(Entity find, Entity replacement) {
			throw new NotImplementedException();
		}
	}

}