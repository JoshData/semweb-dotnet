using System;
using System.Collections;
using System.IO;
using BDB;
using SemWeb.Util;

namespace SemWeb {

	public class BDBStore : Store, IDisposable {
		BDB43
			db_info,
			db_resource_ids,
			db_entities_uri, db_literals_value,
			db_index,
			db_statements;
			
		int lastid = 0;	
			
		public BDBStore(string directory) {
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
		
			bool create = true;
			db_info = new BDB43(directory + "/info", create, DBFormat.Btree, false);
			db_resource_ids = new BDB43(directory + "/resource_ids", create, DBFormat.Btree, false);
			db_entities_uri = new BDB43(directory + "/entities_uri", create, DBFormat.Btree, false);
			db_literals_value = new BDB43(directory + "/literals_value", create, DBFormat.Btree, false);
			db_index = new BDB43(directory + "/index", create, DBFormat.Btree, true);
			db_statements = new BDB43(directory + "/statements", create, DBFormat.Btree, false);
			
			object lastidobj = db_info.Get("last_id");
			if (lastidobj == null)
				lastid = 0;
			else
				lastid = (int)lastidobj;
		}
		
		public void Dispose() {
			db_info.Put("last_id", lastid);
			db_info.Close();
			db_resource_ids.Close();
			db_entities_uri.Close();
			db_literals_value.Close();
			db_index.Close();
			db_statements.Close();
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
			q.S = GetResKey(s.Subject, true);
			q.P = GetResKey(s.Predicate, true);
			q.O = GetResKey(s.Object, true);
			q.OT = s.Object is Entity ? 0 : 1;
			q.M = GetResKey(s.Meta, true);
			return q;
		}
	
		int GetResKey(Resource r, bool create) {
			if (r == null) return -1;
			object key = GetResourceKey(r);
			if (key != null) return (int)key;
			
			string literaltostring = null;
			if (r is Literal) {
				literaltostring = r.ToString();
				key = db_literals_value.Get(literaltostring);
			} else if (r.Uri != null) {
				if (key == null)
					key = db_entities_uri.Get(r.Uri);
			}
			
			if (key == null) {
				if (!create) return -1;
			
				lastid++;
				key = lastid;
				SetResourceKey(r, key);
				
				if (r is Literal) {
					db_literals_value.Put(literaltostring, key);
					db_resource_ids.Put(key, literaltostring);
				} else {
					if (r.Uri != null) {
						db_entities_uri.Put(r.Uri, key);
						db_resource_ids.Put(key, r.Uri);
					}
				}
			}
			return (int)key;
		}
		Statement QuadToStatement(Quad q) {
			return new Statement(
				(Entity)GetRes(q.S, 0),
				(Entity)GetRes(q.P, 0),
				GetRes(q.O, q.OT),
				(Entity)GetRes(q.M, 0)
				);
		}
		Resource GetRes(int key, int type) {
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
			Index(qs, 0, q.S, 0);
			Index(qs, 1, q.P, 0);
			Index(qs, 2, q.O, q.OT);
			Index(qs, 3, q.M, 0);
			db_statements.Put(qs, 0);
		}
		
		void Index(int[] qs, int spom, object key, int ot) {
			using (BDB43.Cursor cursor = db_index.NewCursor()) {
				cursor.Append(key, qs);
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
					
					int rkey = GetResKey(r, false);
					if (rkey == -1) return;
					
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