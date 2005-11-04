using System;
using BDB;

namespace SemWeb {

	public class BDBStore {
		BDB43
			db_info, // hashtable of info
			db_entities_id, db_entities_uri, // entity tables: id as key, uri as key
			db_literals_id, db_literals_value, // literal tables: id as key, literal.ToString() as key
			db_entity_index, db_literal_index, // entity/literal id key, IndexEntry[] value;
			db_statements; // quad as key, null value
	
		public BDBStore(string file) {
			bool create = !System.IO.File.Exists(file);
			db_info = new BDB43(file, "info", create, DBFormat.Hash);
			db_entities_id = new BDB43(file, "entities_id", create, DBFormat.Hash);
			db_entities_uri = new BDB43(file, "entities_uri", create, DBFormat.Hash);
			db_literals_id = new BDB43(file, "literals_id", create, DBFormat.Hash);
			db_literals_value = new BDB43(file, "literals_value", create, DBFormat.Hash);
			db_entity_index = new BDB43(file, "entity_index", create, DBFormat.Hash);
			db_literal_index = new BDB43(file, "literal_index", create, DBFormat.Hash);
			db_statements = new BDB43(file, "statements", create, DBFormat.Hash);
		}
		
		[Serializable]
		struct Quad {
			public int S, P, O, M;
			public int OT;
		}
		
		[Serializable]
		struct IndexEntry {
			public int SPOM;
			public Quad Quad;
		}
		
		Quad QuadFromStatement(Statement s) {
			Quad q = new Quad();
			q.S = GetResKey(s.Subject);
			q.P = GetResKey(s.Predicate);
			q.O = GetResKey(s.Object);
			q.OT = s.Object is Entity ? 0 : 1;
			q.M = GetResKey(s.Meta);
			return q;
		}
		int GetResKey(Resource r) {
			if (r == null) return -1;
			object key = r.GetResourceKey(this);
			if (key != null) return (int)key;
			if (r is Literal) {
				key = db_literals_value.Get(r.ToString());
			} else if (r.Uri != null) {
				key = db_entities_uri.Get(r.Uri);
			}
			if (key == null) {
				object lastidobj = db_info.Get("last_id");
				int lastid;
				if (lastidobj == null)
					lastid = 0;
				else
					lastid = (int)lastidobj;
				lastid++;
				db_info.Put("last_id", lastid);
				key = lastid;
				r.SetResourceKey(this, key);
				
				if (r is Literal) {
					db_literals_value.Put(r.ToString(), key);
					db_literals_id.Put(key, r.ToString());
				} else {
					if (r.Uri != null) {
						db_entities_uri.Put(r.Uri, key);
						db_entities_id.Put(key, r.Uri);
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
				ret = new Entity((string)db_entities_id.Get(key));
			else
				ret = Literal.Parse((string)db_literals_id.Get(key), null);
			ret.SetResourceKey(this, key);
			return ret;
		}
		
		public void Add(Statement statement) {
			Quad q = QuadFromStatement(statement);
			Index(q, 0, q.S, 0);
			Index(q, 1, q.P, 0);
			Index(q, 2, q.O, q.OT);
			Index(q, 3, q.M, 0);
			db_statements.Put(q, null);
		}
		
		void Index(Quad q, int spom, object key, int ot) {
			BDB43 db = (ot == 0 ? db_entity_index : db_literal_index);
			IndexEntry[] curindex = (IndexEntry[])db.Get(key);
			IndexEntry[] newindex;
			if (curindex == null) {
				newindex = new IndexEntry[1];
			} else {
				newindex = new IndexEntry[curindex.Length + 1];
				curindex.CopyTo(newindex, 0);
			}
			newindex[newindex.Length-1].SPOM = spom;
			newindex[newindex.Length-1].Quad = q;
			db.Put(key, newindex);
		}
		
		public void Select(Statement template, StatementSink sink) {
			if (template == Statement.All) {
				throw new NotImplementedException();
			}
			
			for (int spom = 0; spom < 4; spom++) {
				Resource r = null;
				if (spom == 0) r = template.Subject;
				if (spom == 1) r = template.Predicate;
				if (spom == 2) r = template.Object;
				if (spom == 3) r = template.Meta;
				if (r == null) continue;
				
				BDB43 db = (r is Entity ? db_entity_index : db_literal_index);
				object rkey = GetResKey(r);
				IndexEntry[] index = (IndexEntry[])db.Get(rkey);
				
				if (index == null || index.Length == 0) return;
				
				foreach (IndexEntry entry in index) {
					if (entry.SPOM != spom) continue;
					Statement s = QuadToStatement(entry.Quad);
					
					if (!template.Matches(s)) continue;
					if (!sink.Add(s)) return;
				}
				
				break;
			}
		}
	}

}