using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Text;

namespace SemWeb.Stores {
	// TODO: It's not safe to have two concurrent accesses to the same database
	// because the creation of new entities will use the same IDs.
	
	public abstract class SQLStore : Store {
		string table;
		bool firstUse = true;
		
		int scount = 0; // number of statements in the store
		
		bool hasNextId = false;
		int nextid = 2; // the next ID of a new resource
		
		bool tableIsLocked = false;
		
		Hashtable lockedIdCache;
		
		public SQLStore(string table, KnowledgeModel model) : base(model) {
			this.table = table;
		}
		
		protected string TableName { get { return table; } }

		private void Init() {
			if (!firstUse) return;
			firstUse = false;
			
			try {
				CreateTable();
				CreateIndexes();
			} catch (Exception e) {
			}
		}
		
		public override int StatementCount { get { Init(); return RunScalarInt("select count(subject) from " + table, 0); } }
		
		private void GetNextId() {
			if (hasNextId && tableIsLocked) return;

			foreach (string column in new string[] { "subject", "predicate", "object", "meta" }) {
				int maxid = RunScalarInt("select max(" + column + ") from " + table, 0);
				if (maxid >= nextid) nextid = maxid + 1;
			}
			
			if (nextid <= 2) nextid = 2;
			
			if (tableIsLocked) hasNextId = true;
		}
		
		public override void Clear() {
			Init();
			RunCommand("DELETE FROM " + table);
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			Init();
			int subj = ID(statement.Subject);
			int pred = ID(statement.Predicate);
			int meta = statement.Meta == null ? 0 : ID(statement.Meta);
			
			StringBuilder cmd = new StringBuilder();
			cmd.Append("INSERT INTO ");
			cmd.Append(table);
			cmd.Append(" VALUES (");

			cmd.Append(subj);
			cmd.Append(", ");
			cmd.Append(pred);
			cmd.Append(", ");
			
			if (statement.Object is Literal) {
				Literal lit = (Literal)statement.Object;
				cmd.Append("0, ");
				cmd.Append(Escape(lit.ToString()));
			} else {
				int obj = ID(statement.Object);
				cmd.Append(obj);
				cmd.Append(", NULL");
			}
			
			cmd.Append(", ");
			cmd.Append(meta);
			cmd.Append(")");
			
			RunCommand(cmd.ToString());
		}
		
		public override void Remove(Statement statement) {
			Init();

			System.Text.StringBuilder cmd = new System.Text.StringBuilder("REMOVE FROM ");
			cmd.Append(table);
			WhereClause(statement, cmd);
			RunCommand(cmd.ToString());
		}
		
		public override Entity GetResource(string uri, bool create) {
			if (uri == null) throw new ArgumentNullException();
			
			if (create)
				return new DBResource(this, 0, uri, false);
			
			int id = GetId(uri, false);
			if (id == 0) return null;
			
			return new DBResource(this, id, uri, false);
		}
		
		public override Entity CreateAnonymousResource() {
			GetNextId();
			return new DBResource(this, nextid++, null, true);
		}
		
		private void WhereItem(string col, Resource r, System.Text.StringBuilder cmd) {
			if (r is MultiEntity) {
				cmd.Append("(");
					bool first = true;
					foreach (Resource rr in ((MultiEntity)r).items) {
						if (!first)
							cmd.Append(" or ");
						first = false;
						
						if (col != "object" && rr is Literal) continue;
						
						WhereItem(col, rr, cmd);
					}
				cmd.Append(")");
				return;
			} else if (r is Literal) {
				cmd.Append(" (object = 0 and literal = ");
				cmd.Append(Escape(((Literal)r).ToString()));
				cmd.Append(")");
			} else {
				cmd.Append("( ");
				cmd.Append(col);
				cmd.Append(" = ");
				cmd.Append(ID(r));
				cmd.Append(" )");
			}
		}
		
		private void WhereClause(Statement template, System.Text.StringBuilder cmd) {
			if (template.Subject == null && template.Predicate == null && template.Object == null)
				return;
			
			cmd.Append(" WHERE ");
			
			if (template.Subject != null) {
				WhereItem("subject", template.Subject, cmd);
			}
			
			if (template.Predicate != null) {
				if (template.Subject != null)
					cmd.Append(" and");
				WhereItem("predicate", template.Predicate, cmd);
			} else {
				if (template.Subject != null)
					cmd.Append(" and");
				cmd.Append(" predicate != 1");
			}
			
			if (template.Object != null) {
				cmd.Append(" and");
				WhereItem("object", template.Object, cmd);
			}
			
			if (template.Meta != null) {
				cmd.Append(" and");
				WhereItem("meta", template.Meta, cmd);
			}
		}
		
		private int AsInt(object r) {
			if (r is int) return (int)r;
			if (r is uint) return (int)(uint)r;
			if (r is string) return int.Parse((string)r);
			throw new ArgumentException(r.ToString());
		}
		
		private string AsString(object r) {
			if (r is string)
				return (string)r;
			else if (r is byte[])
				return System.Text.Encoding.UTF8.GetString((byte[])r);
			else
				throw new FormatException("SQL store returned a literal value as " + r);
		}
		
		private struct SPOLM {
			public int S, P, O, M;
			public string L;
		}
		
		public override void Select(Statement[] templates, StatementSink result) {
			if (templates == null) throw new ArgumentNullException();
			if (result == null) throw new ArgumentNullException();
			if (templates.Length == 0) return;
			
			// See how many columns vary.
			Resource s = null, p = null, o = null, m = null;
			bool vs = false, vp = false, vo = false, vm = false;
			bool first = true;
			
			foreach (Statement st in templates) {
				if (first) { s = st.Subject; p = st.Predicate; o = st.Object; m = st.Meta; first = false; }
				
				if ((s == null && st.Subject != null) || (s != null && st.Subject == null) || (s != null && !st.Subject.Equals(s))) vs = true;
				if ((p == null && st.Predicate != null) || (p != null && st.Predicate == null) || (p != null && !st.Predicate.Equals(p))) vp = true;
				if ((o == null && st.Object != null) || (o != null && st.Object == null) || (o != null && !st.Object.Equals(o))) vo = true;
				if ((m == null && st.Meta != null) || (m != null && st.Meta == null) || (m != null && !st.Meta.Equals(m))) vm = true;
			}
			
			// If more than one column varies, do the selection individually.
			int v = (vs ? 1 : 0) + (vp ? 1 : 0) + (vo ? 1 : 0) + (vm ? 1 : 0);
			if (v > 1) { base.Select(templates, result); return; }
			
			if (!(s == null || s is Entity) || !(p == null || p is Entity) || !(m == null || m is Entity)) return;
			
			Statement q = new Statement(
				vs ? new MultiEntity(0, templates) : (Entity)s,
				vp ? new MultiEntity(1, templates) : (Entity)p,
				vo ? new MultiEntity(2, templates) : o,
				vm ? new MultiEntity(3, templates) : (Entity)m);
			
			Select(q, result);
		}
		
		private class MultiEntity : Entity {
			public ArrayList items;			
			public MultiEntity(int c, Statement[] templates) : base(null, null) {
				items = new ArrayList();
				foreach (Statement st in templates) {
					if (c == 0) items.Add(st.Subject);
					if (c == 1) items.Add(st.Predicate);
					if (c == 2) items.Add(st.Object);
					if (c == 3) items.Add(st.Meta);
				}
			}
		}
		
		public override void Select(Statement template, StatementSink result) {
			if (result == null) throw new ArgumentNullException();
	
			Init();
			
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT subject, predicate, object, literal, meta FROM ");
			cmd.Append(table);
			WhereClause(template, cmd);
			
			ArrayList items = new ArrayList();
			
			string cmdstr = cmd.ToString();
			//Console.WriteLine(cmdstr);
			
			IDataReader reader = RunReader(cmdstr);
			try {
				while (reader.Read()) {
					int s = AsInt(reader[0]);
					int p = AsInt(reader[1]);
					int o = AsInt(reader[2]);
					int m = AsInt(reader[4]);
					
					SPOLM d = new SPOLM();
					d.S = s;
					d.P = p;
					d.O = o;
					d.M = m;
					
					string literal = null;
					if (o == 0)
						literal = AsString(reader[3]);
					d.L = literal;
				
					items.Add(d);
				}
			} finally {
				reader.Close();
			}
			
			foreach (SPOLM item in items) {
				bool ret = result.Add(new Statement(
					new DBResource(this, item.S, null, false),
					new DBResource(this, item.P, null, false),
					item.O == 0 ? (Resource)Literal.Parse(item.L, Model, null) : new DBResource(this, item.O, null, false),
					item.M == 0 ? null : new DBResource(this, item.M, null, false)
					));
				if (!ret) break;
			}
		}

		private int ID(Resource resource) {
			if (resource is DBResource && ((DBResource)resource).store == this) return ((DBResource)resource).GetId();
			if (resource.Uri == null) throw new ArgumentException("An anonymous resource created by another store cannot be used in this store.");
			return GetId(resource.Uri, true);
		}		
		
		private string GetUri(int id) {
			Init();
			
			return RunScalarString("SELECT literal FROM " + table + " WHERE subject = " + id + " and predicate = 1");
		}
		
		private int GetId(string uri, bool create) {
			if (lockedIdCache != null && lockedIdCache.ContainsKey(uri))
				return (int)lockedIdCache[uri];
			
			Init();
			
			object ret = null;
			
			if (lockedIdCache == null) {
				ret = RunScalar("SELECT subject FROM " + table + " WHERE predicate = 1 and literal = " + Escape(uri) + "");
				if (ret == null && !create) return 0;
			}
			
			int id;
			
			if (ret == null) {				
				GetNextId();
				RunCommand("INSERT INTO " + table + " VALUES (" + nextid + ", 1, 0, " + Escape(uri) + ", 0)");
				id = nextid++;
			} else {
				if (ret is int) id = (int)ret; else id = int.Parse(ret.ToString());
			}
			
			if (lockedIdCache != null)
				lockedIdCache[uri] = id;
			
			return id;
		}
		
		internal static string Escape(string str) {
			System.Text.StringBuilder b = new System.Text.StringBuilder(str);
			b.Replace("\\", "\\\\");
			b.Replace("\"", "\\\"");
			b.Replace("\n", "\\n");
			return "\"" + b.ToString() + "\"";
		}

		class DBResource : Entity {
			public readonly SQLStore store;
			
			int id;
			bool anon;
			
			public DBResource(SQLStore store, int id, string uri, bool anon) : base(uri, store.Model) {
				this.store = store; this.id = id; this.anon = anon;
				if (uri == null && id == 0)
					throw new ArgumentException("URI-less resources must have an ID.");
			}
			
			public int GetId() {
				if (id == 0) id = store.GetId(Uri, true);
				return id;
			}
			
			public override string Uri {
				get {
					if (anon) return null;
					if (uri == null) uri = store.GetUri(id);
					if (uri == null) anon = true;
					return uri;
				}
			}
			
			public override int GetHashCode() {
				return GetId().GetHashCode();
			}
			
			public override bool Equals(object o) {
				if (o is Literal) return false;
				if (!(o is DBResource)) return base.Equals(o);
				DBResource r = (DBResource)o;
				return GetId() == r.GetId();
			}
		}

		public override void Import(RdfParser parser) {
			if (parser == null) throw new ArgumentNullException();
			
			Init();
			
			bool oldLockState = tableIsLocked;
			
			if (lockedIdCache == null) {
				lockedIdCache = new Hashtable();
				IDataReader reader = RunReader("SELECT subject, literal FROM " + table + " where predicate = 1 and object = 0");
				try {
					while (reader.Read()) {
						int id = AsInt(reader[0]);
						string uri = AsString(reader[1]);
						lockedIdCache[uri] = id;
					}
				} finally {
					reader.Close();
				}
			}

			BeginTransaction();
			
			try {
				tableIsLocked = true;
				base.Import(parser);
			} finally {
				tableIsLocked = oldLockState;
				if (!tableIsLocked) lockedIdCache = null;
				EndTransaction();
			}
		}
		
		protected abstract void RunCommand(string sql);
		protected abstract object RunScalar(string sql);
		protected abstract IDataReader RunReader(string sql);
		
		protected int RunScalarInt(string sql, int def) {
			object ret = RunScalar(sql);
			if (ret == null) throw new InvalidOperationException("The SQL command did not return a value.");
			if (ret is int) return (int)ret;
			try {
				return int.Parse(ret.ToString());
			} catch (FormatException e) {
				return def;
			}
		}
		
		protected string RunScalarString(string sql) {
			object ret = RunScalar(sql);
			if (ret == null) return null;
			if (ret is string) return (string)ret;
			if (ret is byte[]) return System.Text.Encoding.UTF8.GetString((byte[])ret);
			throw new FormatException("SQL store returned a literal value as " + ret);
		}

		protected virtual void CreateTable() {
			RunCommand("CREATE TABLE " + table + 
				"(subject int UNSIGNED NOT NULL, predicate int UNSIGNED NOT NULL, object int UNSIGNED NOT NULL, literal blob, meta int UNSIGNED NOT NULL)");
		}
		
		protected virtual void CreateIndexes() {
			RunCommand("CREATE INDEX subject_index ON " + table + "(subject)");
			RunCommand("CREATE INDEX predicate_index ON " + table + "(predicate)");
			RunCommand("CREATE INDEX object_index ON " + table + "(object)");
			RunCommand("CREATE INDEX literal_index ON " + table + "(literal(128))");
		}
		
		protected virtual void BeginTransaction() { }
		protected virtual void EndTransaction() { }
	}
	
}

namespace SemWeb.IO {
	using SemWeb;
	using SemWeb.Stores;
	
	public class SQLWriter : RdfWriter {
		TextWriter writer;
		string table;
		
		int resourcecounter = 0;
		Hashtable resources = new Hashtable();
		
		NamespaceManager m = new NamespaceManager();
		
		string[,] fastmap = new string[3,2];
		
		public SQLWriter(string spec) : this(GetWriter("-"), spec) { }
		
		public SQLWriter(string file, string tablename) : this(GetWriter(file), tablename) { }

		public SQLWriter(TextWriter writer, string tablename) {
			this.writer = writer;
			this.table = tablename;
			
			writer.WriteLine("CREATE TABLE `" + table + "` (`subject` int UNSIGNED NOT NULL, `predicate` int UNSIGNED NOT NULL, `object` int UNSIGNED NOT NULL, `literal` blob, `meta` int UNSIGNED NOT NULL);");
		}
		
		public override NamespaceManager Namespaces { get { return m; } }
		
		public override void WriteStatement(string subj, string pred, string obj) {
			writer.WriteLine("INSERT INTO {0} VALUES ({1}, {2}, {3}, NULL, 0);", table, ID(subj, 0), ID(pred, 1), ID(obj, 2)); 
		}
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			writer.WriteLine("INSERT INTO {0} VALUES ({1}, {2}, 0, {3}, 0);", table, ID(subj, 0), ID(pred, 1), SQLStore.Escape(literal.ToString())); 
		}
		
		public override string CreateAnonymousNode() {
			int id = ++resourcecounter;
			string uri = "_anon:" + id;
			return uri;
		}
		
		public override void Dispose() {
			writer.WriteLine("CREATE INDEX subject_index ON " + table + "(subject);");
			writer.WriteLine("CREATE INDEX predicate_index ON " + table + "(predicate);");
			writer.WriteLine("CREATE INDEX object_index ON " + table + "(object);");
			writer.WriteLine("CREATE INDEX literal_index ON " + table + "(literal(128));");
			Close();
		}
		
		public override void Close() {
			writer.Close();
		}

		
		private string ID(string uri, int x) {
			if (uri.StartsWith("_anon:")) return uri.Substring(6);
			
			// Make this faster when a subject, predicate, or object is repeated.
			if (fastmap[0,0] != null && uri == fastmap[0, 0]) return fastmap[0, 1];
			if (fastmap[1,0] != null && uri == fastmap[1, 0]) return fastmap[1, 1];
			if (fastmap[2,0] != null && uri == fastmap[2, 0]) return fastmap[2, 1];
			
			string id;
			
			if (resources.ContainsKey(uri)) {
				id = (string)resources[uri];
			} else {
				id = (++resourcecounter).ToString();
				resources[uri] = id;
				writer.WriteLine("INSERT INTO {0} VALUES ({1}, 1, 0, {2}, 0);", table, id, SQLStore.Escape(uri));
			}
			
			fastmap[x, 0] = uri;
			fastmap[x, 1] = id;
			
			return id;
		}

	}
}
