using System;
using System.Collections;
using System.Data;
using System.IO;

namespace SemWeb {
	// TODO: It's not safe to have two concurrent accesses to the same database
	// because the creation of new entities will use the same IDs.
	
	public abstract class SQLStore : Store {
		string table;
		bool firstUse = true;
		
		int scount = 0; // number of statements in the store
		int nextid = 2; // the next ID of a new resource
		
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

			foreach (string column in new string[] { "subject", "predicate", "object", "meta" }) {
				int maxid = RunScalarInt("select max(" + column + ") from " + table, 0);
				if (maxid >= nextid) nextid = maxid + 1;
			}
		}
		
		public override int StatementCount { get { Init(); return RunScalarInt("select count(subject) from " + table, 0); } }
		
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
			string cmd;
			if (statement.Object is Literal) {
				cmd = "INSERT INTO " + table + " VALUES (" + subj + ", " + pred + ", 0, \"" + Escape( ((Literal)statement.Object).Value) + "\", " + meta + ")";
			} else {
				int obj = ID(statement.Object);
				cmd = "INSERT INTO " + table + " VALUES (" + subj + ", " + pred + ", " + obj + ", \"\", " + meta + ")";
			}
			RunCommand(cmd);
		}
		
		public override void Remove(Statement statement) {
			Init();

			System.Text.StringBuilder cmd = new System.Text.StringBuilder("REMOVE FROM ");
			cmd.Append(table);
			WhereClause(statement, cmd);
			RunCommand(cmd.ToString());
		}
		
		public override Entity GetResource(string uri, bool create) {
			if (create)
				return new DBResource(this, 0, uri, false);
			
			int id = GetId(uri, false);
			if (id == 0) return null;
			
			return new DBResource(this, id, uri, false);
		}
		
		public override Entity CreateAnonymousResource() {
			return new DBResource(this, nextid++, null, true);
		}
		
		private void WhereClause(Statement template, System.Text.StringBuilder cmd) {
			if (template.Subject == null && template.Predicate == null && template.Object == null)
				return;
			
			cmd.Append(" WHERE ");
			
			if (template.Subject != null) {
				cmd.Append(" subject = ");
				cmd.Append(ID(template.Subject));
			}
			if (template.Predicate != null) {
				if (template.Subject != null)
					cmd.Append(" and");
				cmd.Append(" predicate = ");
				cmd.Append(ID(template.Predicate));
			}
			if (template.Object != null) {
				if (template.Subject != null || template.Predicate != null)
					cmd.Append(" and");
				if (template.Object is Literal) {
					cmd.Append(" object = 0 and literal = \"");
					cmd.Append(Escape(((Literal)template.Object).Value));
					cmd.Append("\"");
				} else {
					cmd.Append(" object = ");
					cmd.Append(ID(template.Object));
				}
			}
			if (template.Meta != null) {
				cmd.Append(" and");
				cmd.Append(" meta = ");
				cmd.Append(ID(template.Meta));
			}
		}
		
		public override void Select(Statement template, StatementSink result) {
			Init();
			
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT subject, predicate, object, literal, meta FROM ");
			cmd.Append(table);
			WhereClause(template, cmd);
			
			IDataReader reader = RunReader(cmd.ToString());
			while (reader.Read()) {
				int s = int.Parse((string)reader[0]);
				int p = int.Parse((string)reader[1]);
				int o = int.Parse((string)reader[2]);
				int m = int.Parse((string)reader[4]);
				bool ret = result.Add(new Statement(
					new DBResource(this, s, null, false),
					new DBResource(this, p, null, false),
					o == 0 ? (Resource)new Literal((string)reader[3], Model) : new DBResource(this, o, null, false),
					m == 0 ? null : new DBResource(this, m, null, false)
					));
				if (!ret) break;
			}
			reader.Close();
		}

		private int ID(Resource resource) {
			if (resource is DBResource && ((DBResource)resource).store == this) return ((DBResource)resource).GetId();
			if (resource.Uri == null) throw new ArgumentException("An anonymous resource created by another store cannot be used in this store.");
			return GetId(resource.Uri, true);
		}		
		
		private string GetUri(int id) {
			Init();
			
			string ret = (string)RunScalar("SELECT literal FROM " + table + " WHERE subject = " + id + " and predicate = 1");
			if (ret == null) ret = "";
			return ret;
		}
		
		private int GetId(string uri, bool create) {
			Init();
			
			object ret = RunScalar("SELECT subject FROM " + table + " WHERE predicate = 1 and literal = \"" + Escape(uri) + "\"");
			if (ret == null && !create) return 0;
			
			if (ret == null) {				
				RunCommand("INSERT INTO " + table + " VALUES (" + nextid + ", 1, 0, \"" + Escape(uri) + "\", 0)");
				return nextid++;
			} else {
				int id;
				if (ret is int) id = (int)ret; else id = int.Parse(ret.ToString());
				return id;
			}
		}
		
		internal static string Escape(string s) {
			return s;
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
					return uri;
				}
			}
		}

		public override void Import(RdfParser parser) {
			Init();
			
			BeginTransaction();
			base.Import(parser);
			EndTransaction();
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
		
		protected virtual void CreateTable() {
			RunCommand("CREATE TABLE " + table + 
				"(subject int, predicate int, object int, literal blob, meta int)");
		}
		
		protected virtual void CreateIndexes() {
			RunCommand("CREATE INDEX subject_index ON " + table + "(subject)");
			RunCommand("CREATE INDEX predicate_index ON " + table + "(predicate)");
			RunCommand("CREATE INDEX object_index ON " + table + "(object)");
		}
		
		protected virtual void BeginTransaction() { }
		protected virtual void EndTransaction() { }
		
		protected virtual void LockTable() { }
		protected virtual void UnlockTable() { }		
	}
	
	public class SQLWriter : RdfWriter {
		TextWriter writer;
		string table;
		
		int resourcecounter = 0;
		Hashtable resources = new Hashtable();
		
		NamespaceManager m = new NamespaceManager();
		
		public SQLWriter(string spec) : this(GetWriter("-"), spec) { }
		
		public SQLWriter(string file, string tablename) : this(GetWriter(file), tablename) { }

		public SQLWriter(TextWriter writer, string tablename) {
			this.writer = writer;
			this.table = tablename;
		}
		
		public override NamespaceManager Namespaces { get { return m; } }
		
		public override void WriteStatement(string subj, string pred, string obj) {
			writer.WriteLine("INSERT INTO {0} VALUES ({1}, {2}, {3}, DEFAULT);", table, ID(subj), ID(pred), ID(obj)); 
		}
		
		public override void WriteStatementLiteral(string subj, string pred, string literal, string literalType, string literalLanguage) {
			writer.WriteLine("INSERT INTO {0} VALUES ({1}, {2}, 0, \"{3}\");", table, ID(subj), ID(pred), SQLStore.Escape(literal)); 
		}
		
		public override string CreateAnonymousNode() {
			int id = ++resourcecounter;
			string uri = "_anon:" + id;
			resources[uri] = id.ToString();
			return uri;
		}
		
		public override void Dispose() {
			Close();
		}
		
		public override void Close() {
			writer.Close();
		}

		
		private string ID(string uri) {
			if (resources.ContainsKey(uri)) return (string)resources[uri];
			int id = ++resourcecounter;
			resources[uri] = id.ToString();
			writer.WriteLine("INSERT INTO {0} VALUES ({1}, 0, 0, \"{2}\";", table, id, SQLStore.Escape(uri));
			return id.ToString();
		}

	}
}
