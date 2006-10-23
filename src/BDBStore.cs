using System;
using System.Collections;
using System.IO;
using BDB;
using SemWeb.Util;

namespace SemWeb.Stores {

	public class BDBStore : Store, IDisposable {
		BDB43.Env env;
		BDB43 db_id_to_value;
		BDB43 db_value_to_id;
		BDB43 db_statements_index;
		
		uint lastid = 0;
		long count = 0;
		
		Hashtable importIndexCache = new Hashtable();
		
		public BDBStore(string directory) {
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
				
			env = new BDB43.Env(directory);
			
			bool create = true;
			db_id_to_value = new BDB43("id_to_value", create, DBFormat.Hash, false, env);
			db_value_to_id = new BDB43("value_to_id", create, DBFormat.Hash, false, env);
			db_statements_index = new BDB43("statements_index", create, DBFormat.Hash, false, env);
			
			db_id_to_value.KeyType = BDB.DataType.UInt;
			db_id_to_value.ValueType = BDB.DataType.String;
			db_value_to_id.KeyType = BDB.DataType.String;
			db_value_to_id.ValueType = BDB.DataType.UInt;
			db_statements_index.KeyType = BDB.DataType.UInt;
			db_statements_index.ValueType = BDB.DataType.IntArray;
		}
		
		public void Dispose() {
			StoreImportIndexCache();
		
			db_id_to_value.Close();
			db_value_to_id.Close();
			db_statements_index.Close();
			env.Dispose();
		}
		
		public override bool Distinct { get { return false; } }
		
		public override int StatementCount {
			get {
				return (int)count;
			}
		}
		
		public override void Clear() {
			db_id_to_value.Truncate();
			db_value_to_id.Truncate();
			db_statements_index.Truncate();
		}
		
		struct Quad {
			public int S, P, O, M;
			public static Quad Deserialize(int[] data, int index) {
				Quad q = new Quad();
				q.S = data[index+0];
				q.P = data[index+1];
				q.O = data[index+2];
				q.M = data[index+3];
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
			q.M = (int)GetResKey(s.Meta, create);
			return q;
		}
	
		uint GetResKey(Resource r, bool create) {
			if (r == null) return 0;
			if ((object)r == (object)Statement.DefaultMeta) return 1;

			object keyobj = GetResourceKey(r);
			if (keyobj != null) return (uint)keyobj;
			
			uint key = 0;
			
			string stringval = null;
			if (r is Literal) {
				stringval = "L" + r.ToString();
			} else if (r.Uri != null) {
				stringval = "U" + r.Uri;
			}
			
			if (stringval != null) {
				keyobj = db_value_to_id.Get(stringval);
				if (keyobj != null) key = (uint)keyobj;
			}
			
			if (key == 0) {
				if (!create) return 0;
			
				lastid++;
				key = lastid;
				SetResourceKey(r, key);
				
				if (stringval != null) {
					db_id_to_value.Put(key, stringval);
					db_value_to_id.Put(stringval, key);
				}
			}
			
			return key;
		}
		
		Statement QuadToStatement(Quad q, Hashtable createdResources) {
			return new Statement(
				(Entity)GetRes((uint)q.S, createdResources),
				(Entity)GetRes((uint)q.P, createdResources),
				GetRes((uint)q.O, createdResources),
				(Entity)GetRes((uint)q.M, createdResources)
				);
		}
		
		Resource GetRes(uint key, Hashtable createdResources) {
			if (key == 1) return Statement.DefaultMeta;
			if (createdResources.ContainsKey(key)) return (Resource)createdResources[key];
			
			Resource ret;
			
			string stringval = (string)db_id_to_value.Get(key);
			if (stringval == null) // must be a bnode
				ret = new BNode();
			else if (stringval[0] == 'L')
				ret = Literal.Parse(stringval.Substring(1), null);
			else if (stringval[0] == 'U')
				ret = new Entity(stringval.Substring(1));
			else
				throw new InvalidOperationException();
			
			SetResourceKey(ret, key);
			createdResources[key] = ret;
			
			return ret;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			
			Quad q = QuadFromStatement(statement, true);
			for (int i = 0; i < 4; i++)
				Add(q, i);
		}
		
		void Add(Quad q, int index) {
			uint resid = q[index];
			uint indexid = resid<<2 + index;
			
			int[] stmts = null;
			
			stmts = (int[])db_statements_index.Get(indexid);
				
			if (stmts == null) {
				stmts = new int[1 + 10*4];
				stmts[0] = 0;
			}
			
			if ((stmts[0]+1) * 4 + 1 > stmts.Length) {
				int[] nstmts = new int[stmts.Length + 4 +  stmts.Length/2];
				stmts.CopyTo(nstmts, 0);
				stmts = nstmts;
			}
			
			int idx = 1 + stmts[0] * 4;
			stmts[0]++;
			
			stmts[idx+0] = q.S;
			stmts[idx+1] = q.P;
			stmts[idx+2] = q.O;
			stmts[idx+3] = q.M;
			
			db_statements_index.Put(indexid, stmts);

			count++;
		}
		
		void StoreImportIndexCache() {
			foreach (DictionaryEntry ent in importIndexCache)
				db_statements_index.Put(ent.Key, ent.Value);
			importIndexCache.Clear();
		}
		
		public override void Select(SelectFilter filter, StatementSink sink) {
			throw new NotImplementedException();
		}
		
		public override void Select(Statement template, StatementSink sink) {
			if (template == Statement.All)
				throw new NotImplementedException();
			else
				SelectSome(template, sink);
		}
		
		void SelectSome(Statement template, StatementSink sink) {
			// Get a cursor over the first non-null component of template.
			int[] stmts;
			if (template.Subject != null) stmts = GetStatements(template.Subject, 0);
			else if (template.Predicate != null) stmts = GetStatements(template.Predicate, 1);
			else if (template.Object != null) stmts = GetStatements(template.Object, 2);
			else if (template.Meta != null) stmts = GetStatements(template.Meta, 3);
			else throw new InvalidOperationException();
			
			if (stmts == null) return;
			
			Hashtable createdResources = new Hashtable();
			
			for (int i = 0; i < stmts[0]; i++) {
				Quad q = Quad.Deserialize(stmts, 1 + i*4);
				Statement s = QuadToStatement(q, createdResources);
				if (template.Matches(s)) {
					if (!sink.Add(s))
						return;
				}
			}
		}
		
		int[] GetStatements(Resource res, int index) {
			uint resid = GetResKey(res, false);
			if (resid == 0) return null;
			
			uint indexid = resid<<2 + index;
			return (int[])db_statements_index.Get(indexid);
		}
		
		public override void Remove(Statement statement) {
			if (statement == Statement.All) { Clear(); return; }
			throw new NotImplementedException();
		}
		
		public override Entity[] GetEntities() {
			throw new NotImplementedException();
		}
		
		public override Entity[] GetPredicates() {
			throw new NotImplementedException();
		}
		
		public override Entity[] GetMetas() {
			throw new NotImplementedException();
		}
	}

}
