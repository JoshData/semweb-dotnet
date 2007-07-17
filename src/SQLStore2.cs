/**
 * SQLStore2.cs: An abstract implementation of an RDF triple store
 * using an SQL-based backend. This is the second version of SQLStore.
 *
 * The SQLStore creates two tables to store its data, each prefixed
 * with a user-provided name (here "PREFIX").  The tables are
 * organized as follows:
 *   table                columns
 * PREFIX_statements subject, predicate, object, meta: all 28byte binary hashes
 * PREFIX_values     id (28byte binary hash), type (byte), value (binary string),
 *                   language (short binary string), datatype (binary string)
 *
 * Every resource is identified by the hash of its value. Entities (named
 * nodes) are hashed on their URI. Literal values are hashed on their
 * N3 representation, including surrounding quotes, the language, and 
 * datatype (if present), so that the space of URI and literal hashes
 * don't clash. Blank nodes are hashed on a GUID assigned to them.
 * A special hash value is reserved for the global Statement.DefaultMeta
 * value.
 *
 * The statements table stores each triple (or quad) as a row.
 *
 * The values table maps the hash values back to the content of the node.
 * The type column in the values table is 1 for named nodes, 2 for blank
 * nodes, and 3 for literals. For named nodes, the value column contains
 * the node's URI and the other columns are left null. For blank nodes,
 * the value column contains a GUID assigned to the node. For literals,
 * the value, language, and datatype columns are used in the obvious way.
 *
 * Type 0 in the values table is reserved for extra-modal data, such
 * as metadata about the store itself.
 *
 * Some instances of this class will add a UNIQUE contraint over all of
 * the columns in the statements table, ensuring that the triple store
 * is a set of statements, and not a multiset. If this is in place,
 * the Distinct property will be true.
 *
 * A UNIQUE constraint is always enforced over the id column in the
 * values table.
 */

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using SemWeb.Util;

namespace SemWeb.Stores {
	public class SQLStore2 : SelectableSource, StatementSink, IDisposable {
		// Table initialization, etc.
		// --------------------------
	
		// This is a version number representing the current 'schema' implemented
		// by this class in case of future updates.
		int dbformat = 0;
	
		string prefix; // the table prefix, i.e. prefix_statements, prefix_values
		ConnectionManager connection;
		
		// 'guid' is a GUID assigned to this store.  It is created the
		// first time the SQL table structure is made and is saved in
		// the info block of the literal with ID zero.
		string guid;
		
		// this flag tracks the first access to the backend, when it
		// creates tables and indexes if necessary
		bool firstUse = true;

		// Other Flags
		// -----------
		
		// Debugging flags from environment variables.
		static bool Debug = System.Environment.GetEnvironmentVariable("SEMWEB_DEBUG_SQL") != null;
		static bool DebugLogSpeed = System.Environment.GetEnvironmentVariable("SEMWEB_DEBUG_SQL_LOG_SPEED") != null;
		
		// Our SHA1 object which we use to create hashes of literal values.
		SHA1 sha = SHA1.Create();
		
		// Some helpers.
		
		const string rdfs_member = NS.RDFS + "member";
		const string rdf_li = NS.RDF + "_";
		
		const string DEFAULT_META_KEY = "http://razor.occams.info/code/semweb/2007/SQLStore2/DefaultMeta";
		const string MAIN_METADATA_KEY = "AAAAAAAAAAAAAAAAAAAAAAAAAAAA";
		
		public abstract class ConnectionManager {
			public virtual void OpenConnection() { }
			public virtual void CloseConnection() { }
					
			public virtual void CreateTables(String prefix) {
				TryRunCommand("CREATE TABLE " + prefix + "_statements (subject BINARY(28) NOT NULL, predicate BINARY(28) NOT NULL, object BINARY(28) NOT NULL, meta BINARY(28) NOT NULL);");
				
				TryRunCommand("CREATE TABLE " + prefix + "_values (type INT NOT NULL, id BINARY(28) NOT NULL, value BLOB NOT NULL, language TEXT, datatype TEXT);");
			}
		
			public virtual void CreateIndexes(String prefix) {
				RunCommand("CREATE UNIQUE INDEX full_index ON " + prefix + "_statements(subject, predicate, object, meta);");
				RunCommand("CREATE INDEX predicate_index ON " + prefix + "_statements(predicate, object);");
				RunCommand("CREATE INDEX object_index ON " + prefix + "_statements(object);");
				RunCommand("CREATE INDEX meta_index ON " + prefix + "_statements(meta);");
				
				RunCommand("CREATE UNIQUE INDEX hash_index ON " + prefix + "_values(id);");
			}
						
			public abstract void RunCommand(string sql);
			public abstract object RunScalar(string sql);
			public abstract IDataReader RunReader(string sql);
		
			private void TryRunCommand(string sql) {
				try {
					RunCommand(sql);
				} catch (Exception e) {
					if (Debug) Console.Error.WriteLine(e);
				}
			}
			
			public virtual void BeginTransaction() { }
			public virtual void EndTransaction() { }
			
			public virtual bool AreStatementsUnique { get { return true; } }
		
			public virtual String StatementInsertKeywords { get { return ""; } }
			public virtual String ValueInsertKeywords { get { return ""; } }
			public virtual bool SupportsInsertCombined { get { return false; } }
		
			public abstract void CreateNullTest(string column, System.Text.StringBuilder command);
			public abstract void CreatePrefixTest(string column, string prefix, System.Text.StringBuilder command);
		
			public virtual bool CreateEntityPrefixTest(string column, string prefix, String tableprefix, System.Text.StringBuilder command) {
				command.Append('(');
				command.Append(column);
				command.Append(" IN (SELECT id from ");
				command.Append(tableprefix);
				command.Append("_values WHERE type = 1 and ");
				CreatePrefixTest("value", prefix, command);
				command.Append("))");
				return true;
			}
		}
		
		public SQLStore2(string prefix, ConnectionManager connection) {
			this.prefix = prefix;
			this.connection = connection;
		}
		
		protected string TableName { get { return prefix; } }
		
		// If this is the first use, initialize the table and index structures.
		// CreateTable() will create tables if they don't already exist.
		// CreateIndexes() will only be run if this is a new database, so that
		// the user may customize the indexes after the table is first created
		// without SemWeb adding its own indexes the next time again.
		private void Init() {
			if (!firstUse) return;
			firstUse = false;
			
			connection.OpenConnection();
			connection.CreateTables(prefix);
			if (CreateVersion()) // tests if this is a new table
				connection.CreateIndexes(prefix);
		}
		
		// Creates the info block in the literal row with ID zero.  Returns true
		// if it created a new info block (i.e. this is a new database).
		private bool CreateVersion() {	
			string verdatastr = RunScalarString("SELECT value FROM " + prefix + "_values WHERE type = 0 and id = '" + MAIN_METADATA_KEY + "'");
			bool isNew = (verdatastr == null);
			
			NameValueCollection verdata = ParseVersionInfo(verdatastr);
			
			if (verdatastr != null && verdata["ver"] == null)
				throw new InvalidOperationException("The SQLStore adapter in this version of SemWeb cannot read databases created in previous versions.");
			
			verdata["ver"] = dbformat.ToString();
			
			if (verdata["guid"] == null) {
				guid = Guid.NewGuid().ToString("N");
				verdata["guid"] = guid;
			} else {
				guid = verdata["guid"];
			}
			
			string newverdata = SerializeVersionInfo(verdata);
			if (verdatastr == null)
				connection.RunCommand("INSERT INTO " + prefix + "_values (type, id, value) VALUES (0,'" + MAIN_METADATA_KEY + "'," + Escape(newverdata, true) + ")");
			else if (verdatastr != newverdata)
				connection.RunCommand("UPDATE " + prefix + "_values SET value = " + Escape(newverdata, true) + " WHERE type = 0 and id = '" + MAIN_METADATA_KEY + "'");
				
			return isNew;
		}
		
		NameValueCollection ParseVersionInfo(string verdata) {
			NameValueCollection nvc = new NameValueCollection();
			if (verdata == null) return nvc;
			foreach (string s in verdata.Split('\n')) {
				int c = s.IndexOf(':');
				if (c == -1) continue;
				nvc[s.Substring(0, c)] = s.Substring(c+1);
			}
			return nvc;
		}
		string SerializeVersionInfo(NameValueCollection verdata) {
			string ret = "";
			foreach (string k in verdata.Keys)
				ret += k + ":" + verdata[k] + "\n";
			return ret;
		}
		
		// Now we get to the Store implementation.
		
		// Why do we return true here?
		public bool Distinct { get { return connection.AreStatementsUnique; } }
		
		public int StatementCount {
			get {
				Init();
				return RunScalarInt("select count(*) from " + prefix + "_statements", 0);
			}
		}
		
		// Implements Store.Clear() by dropping the tables entirely.
		public void Clear() {
			// Drop the tables, if they exist.
			try { connection.RunCommand("DROP TABLE " + prefix + "_statements;"); } catch (Exception) { }
			try { connection.RunCommand("DROP TABLE " + prefix + "_values;"); } catch (Exception) { }
			firstUse = true;
		
			Init();
		}
		
		// Computes a hash for a resource used as its key in the database.
		private string GetHash(Resource resource) {
			// TODO: Do this with fewer new object creations.
		    string data;
		    if (resource == Statement.DefaultMeta) {
		        data = "X" + DEFAULT_META_KEY;
		    } else if (resource is BNode) {
		    	data = "B" + ((BNode)resource).GetGUID().ToString("N");
		    } else if (resource is Entity) {
		    	data = "U" + resource.Uri.ToString();
		    } else if (resource is Literal) {
		    	data = "L" + resource.ToString();
		    } else {
		    	throw new Exception("Not reachable.");
			}
		    
			byte[] bytedata = System.Text.Encoding.Unicode.GetBytes(data);
			byte[] hash = sha.ComputeHash(bytedata);
			return Convert.ToBase64String(hash);
		}
		
		// Creates the SQL command to add a resource to the _values table.
		private void AddValue(Resource resource, StringBuilder buffer, bool insertCombined, ref bool firstInsert) {
			StringBuilder b = buffer;
		
			if (!insertCombined) {
				b.Append("INSERT ");
				b.Append(connection.ValueInsertKeywords);
				b.Append(" INTO ");
				b.Append(prefix);
				b.Append("_values ");
			} else {
				if (!firstInsert)
					b.Append(',');
				firstInsert = false;
			}
			b.Append('(');
			b.Append('\'');
			b.Append(GetHash(resource));
			b.Append('\'');
			b.Append(',');
			
			if ((object)resource == (object)Statement.DefaultMeta) {
				b.Append('2');
				b.Append(',');
				EscapedAppend(b, DEFAULT_META_KEY);
			} else if (resource is BNode) {
				BNode bnode = (BNode)resource;
				b.Append('2');
				b.Append(',');
				EscapedAppend(b, bnode.GetGUID().ToString("N"));
			} else if (resource is Entity) {
				b.Append('1');
				b.Append(',');
				EscapedAppend(b, resource.Uri);
			} else if (resource is Literal) {
				Literal literal = (Literal)resource;
				b.Append('3');
				b.Append(',');
				EscapedAppend(b, literal.Value);
				b.Append(',');
				if (literal.Language != null)
					EscapedAppend(b, literal.Language);
				else
					b.Append("NULL");
				b.Append(',');
				if (literal.DataType != null)
					EscapedAppend(b, literal.DataType);
				else
					b.Append("NULL");
			}
			
			b.Append(')');
			if (!insertCombined)
				b.Append(';');
		}
		
		// Adds the value immediately to the values table.
		private void AddValue(Resource resource) {
			bool fi = false;
			StringBuilder cmd = new StringBuilder();
			AddValue(resource, cmd, false, ref fi);
			connection.RunCommand(cmd.ToString());
		}

		// Adds a statement to the store.
		bool StatementSink.Add(Statement statement) {
			Add(statement);
			return true;
		}
		public void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			
			Init();
			
			AddValue(statement.Subject);
			AddValue(statement.Predicate);
			AddValue(statement.Object);
			AddValue(statement.Meta);
			
			StringBuilder addBuffer = new StringBuilder();
			
			addBuffer.Append("INSERT ");
			addBuffer.Append(connection.StatementInsertKeywords);
			addBuffer.Append(" INTO ");
			addBuffer.Append(prefix);
			addBuffer.Append("_statements ");

			addBuffer.Append('(');
			addBuffer.Append('\'');
			addBuffer.Append(GetHash(statement.Subject));
			addBuffer.Append('\'');
			addBuffer.Append(',');
			addBuffer.Append('\'');
			addBuffer.Append(GetHash(statement.Predicate));
			addBuffer.Append('\'');
			addBuffer.Append(',');
			addBuffer.Append('\'');
			addBuffer.Append(GetHash(statement.Object));
			addBuffer.Append('\'');
			addBuffer.Append(',');
			addBuffer.Append('\'');
			addBuffer.Append(GetHash(statement.Meta));
			addBuffer.Append('\'');
			addBuffer.Append("); ");
			
			connection.RunCommand(addBuffer.ToString());
		}
		
		class Importer : StatementSink {
			readonly SQLStore2 sql;
		
			// This is a buffer of statements waiting to be processed.
			readonly StatementList addStatementBuffer = new StatementList();
			
			// These track the performance of our buffer so we can adjust its size
			// on the fly to maximize performance.
			int importAddBufferSize = 200, importAddBufferRotation = 0;
			TimeSpan importAddBufferTime = TimeSpan.MinValue;

			public Importer(SQLStore2 parent) { sql = parent; }
			
			public bool Add(Statement statement) {
				addStatementBuffer.Add(statement);
				RunAddBufferDynamic();
				return true;
			}
		
			private void RunAddBufferDynamic() {
				// This complicated code here adjusts the size of the add
				// buffer dynamically to maximize performance.
				int thresh = importAddBufferSize;
				if (importAddBufferRotation == 1) thresh += 100; // experiment with changing
				if (importAddBufferRotation == 2) thresh -= 100; // the buffer size
				
				if (addStatementBuffer.Count >= thresh) {
					DateTime start = DateTime.Now;
					RunAddBuffer();
					TimeSpan duration = DateTime.Now - start;
					
					if (DebugLogSpeed)
						Console.Error.WriteLine(thresh + "\t" + thresh/duration.TotalSeconds);
					
					// If there was an improvement in speed, per statement, on an 
					// experimental change in buffer size, keep the change.
					if (importAddBufferRotation != 0
						&& duration.TotalSeconds/thresh < importAddBufferTime.TotalSeconds/importAddBufferSize
						&& thresh >= 200 && thresh <= 10000)
						importAddBufferSize = thresh;
					importAddBufferTime = duration;
					importAddBufferRotation++;
					if (importAddBufferRotation == 3) importAddBufferRotation = 0;
				}
			}
			
			public void RunAddBuffer() {
				if (addStatementBuffer.Count == 0) return;
				
				// TODO: We compute the hash of each resource 2 times.
				
				bool insertCombined = sql.connection.SupportsInsertCombined;
				
				StringBuilder valueInsertions = new StringBuilder();
				StringBuilder statementInsertions = new StringBuilder();
				if (insertCombined) valueInsertions.Append("INSERT " + sql.connection.ValueInsertKeywords + " INTO " + sql.prefix + "_values ");
				if (insertCombined) statementInsertions.Append("INSERT " + sql.connection.StatementInsertKeywords + " INTO " + sql.prefix + "_statements ");
				bool firstValueInsert = true; // only used if insertCombined is true
				
				StatementList statements = addStatementBuffer;
				for (int i = 0; i < statements.Count; i++) {
					Statement statement = (Statement)statements[i];
					
					sql.AddValue(statement.Subject, valueInsertions, insertCombined, ref firstValueInsert);
					sql.AddValue(statement.Predicate, valueInsertions, insertCombined, ref firstValueInsert);
					sql.AddValue(statement.Object, valueInsertions, insertCombined, ref firstValueInsert);
					sql.AddValue(statement.Meta, valueInsertions, insertCombined, ref firstValueInsert);
				
					if (!insertCombined)
						statementInsertions.Append("INSERT " + sql.connection.StatementInsertKeywords + " INTO " + sql.prefix + "_statements ");
					
					if (i > 0)
						statementInsertions.Append('(');
					statementInsertions.Append('(');
					statementInsertions.Append('\'');
					statementInsertions.Append(sql.GetHash(statement.Subject));
					statementInsertions.Append('\'');
					statementInsertions.Append(',');
					statementInsertions.Append('\'');
					statementInsertions.Append(sql.GetHash(statement.Predicate));
					statementInsertions.Append('\'');
					statementInsertions.Append(',');
					statementInsertions.Append('\'');
					statementInsertions.Append(sql.GetHash(statement.Object));
					statementInsertions.Append('\'');
					statementInsertions.Append(',');
					statementInsertions.Append('\'');
					statementInsertions.Append(sql.GetHash(statement.Meta));
					statementInsertions.Append('\'');
					if (i == statements.Count-1 || !insertCombined)
						statementInsertions.Append(");");
					else
						statementInsertions.Append(")");
				}
				
				addStatementBuffer.Clear();
				
				if (insertCombined)
					valueInsertions.Append(';');
				if (Debug) Console.Error.WriteLine(valueInsertions.ToString());
				sql.connection.RunCommand(valueInsertions.ToString());
				
				if (Debug) Console.Error.WriteLine(statementInsertions.ToString());
				sql.connection.RunCommand(statementInsertions.ToString());
			}
		}
		
		private void WhereItem(string col, Resource[] r, System.Text.StringBuilder cmd, bool and) {
			if (and) cmd.Append(" and ");
			
			cmd.Append('(');
			cmd.Append(col);
			cmd.Append(" IN (");
			for (int i = 0; i < r.Length; i++) {
				String hash = GetHash(r[i]);
				if (i > 0) cmd.Append(',');
				cmd.Append('\'');
				cmd.Append(hash);
				cmd.Append('\'');
			}
			cmd.Append(" ))");
			
			// TODO: Special handinlg for rdfs_member
				/*if (r.Uri != null && r.Uri == rdfs_member) {
					if (CreateEntityPrefixTest(col, rdf_li, cmd)) return true;
				}*/
		}
		
		private bool WhereClause(SelectFilter filter, System.Text.StringBuilder cmd) {
			if (filter.Subjects == null && filter.Predicates == null && filter.Objects == null && filter.Metas == null)
				return false;
				
			cmd.Append(" WHERE ");
			
			if (filter.Subjects != null)
				WhereItem("subject", filter.Subjects, cmd, false);
			
			if (filter.Predicates != null)
				WhereItem("predicate", filter.Predicates, cmd, filter.Subjects != null);
			
			if (filter.Objects != null)
				WhereItem("object", filter.Objects, cmd, filter.Subjects != null || filter.Predicates != null);
			
			if (filter.Metas != null)
				WhereItem("meta", filter.Metas, cmd, filter.Subjects != null || filter.Predicates != null || filter.Objects != null);
			
			return true;
		}
		
		// Some helpers for converting the return of a database query into various C# types
		
		private int AsInt(object r) {
			if (r is int) return (int)r;
			if (r is uint) return (int)(uint)r;
			if (r is long) return (int)(long)r;
			if (r is string) return int.Parse((string)r);
			throw new ArgumentException(r.ToString());
		}
		
		private string AsString(object r) {
			if (r == null)
				return null;
			else if (r is System.DBNull)
				return null;
			else if (r is string)
				return (string)r;
			else if (r is byte[])
				return System.Text.Encoding.UTF8.GetString((byte[])r);
			else
				throw new FormatException("SQL store returned a literal value as " + r.GetType());
		}
		
		private static void AppendComma(StringBuilder builder, string text, bool comma) {
			if (comma)
				builder.Append(',');
			builder.Append(text);
		}
		
		///////////////////////////
		// QUERYING THE DATABASE //
		///////////////////////////
		
		public bool Contains(Resource resource) {
			String hash = GetHash(resource);
			object ret = connection.RunScalar("SELECT type FROM " + prefix + "_values WHERE hash = '" + hash + "' and refcount > 0");
			return ret != null;
		}
		
		public bool Contains(Statement template) {
			return Store.DefaultContains(this, template);
		}

		private struct SelectColumnFilter {
			public bool Subject, Predicate, Object, Meta;
		}
	
		public void Select(StatementSink result) {
			Select(Statement.All, result);
		}

		public void Select(Statement template, StatementSink result) {
			if (result == null) throw new ArgumentNullException();

			Init();
			Select2(new SelectFilter(template), result);
		}

		public void Select(SelectFilter filter, StatementSink result) {
			// We don't want to select on more than say 1000 resources
			// at a time, so this breaks down the selection into
			// a union of selections that each select on no more than
			// 1000 resources.
			if (result == null) throw new ArgumentNullException();
			
			Init();
			foreach (Entity[] s in SplitArray(filter.Subjects))
			foreach (Entity[] p in SplitArray(filter.Predicates))
			foreach (Resource[] o in SplitArray(filter.Objects))
			foreach (Entity[] m in SplitArray(filter.Metas))
			{
				SelectFilter f = new SelectFilter(s, p, o, m);
				f.LiteralFilters = filter.LiteralFilters;
				f.Limit = filter.Limit; // TODO: Do the limit better since it should shrink on each iteration.
				Select2(f, result);
			}
		}
		
		Resource[][] SplitArray(Resource[] e) {
			int lim = 1000;
			if (e == null || e.Length <= lim) {
				if (e is Entity[])
					return new Entity[][] { (Entity[])e };
				else
					return new Resource[][] { e };
			}
			int overflow = e.Length % lim;
			int n = (e.Length / lim) + ((overflow != 0) ? 1 : 0);
			Resource[][] ret;
			if (e is Entity[]) ret = new Entity[n][]; else ret = new Resource[n][];
			for (int i = 0; i < n; i++) {
				int c = lim;
				if (i == n-1 && overflow != 0) c = overflow;
				if (e is Entity[]) ret[i] = new Entity[c]; else ret[i] = new Resource[c];
				Array.Copy(e, i*lim, ret[i], 0, c);
			}
			return ret;
		}
		
		private void Select2(SelectFilter filter, StatementSink result) {
			// Don't select on columns that we already know from the template.
			SelectColumnFilter columns = new SelectColumnFilter();
			columns.Subject = (filter.Subjects == null) || (filter.Subjects.Length > 1);
			columns.Predicate = (filter.Predicates == null) || (filter.Predicates.Length > 1);
			columns.Object = (filter.Objects == null) || (filter.Objects.Length > 1);
			columns.Meta = (filter.Metas == null) || (filter.Metas.Length > 1);;
			
			if (filter.Predicates != null | Array.IndexOf(filter.Predicates, rdfs_member) != 1)
				columns.Predicate = true;
			
			// Have to select something
			if (!columns.Subject && !columns.Predicate && !columns.Object && !columns.Meta)
				columns.Subject = true;
				
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT ");
			if (!connection.AreStatementsUnique)
				cmd.Append("DISTINCT ");
			
			ArrayList cols = new ArrayList();
			if (columns.Subject) { cols.Add("sinfo.type"); cols.Add("sinfo.value"); }
			if (columns.Predicate) { cols.Add("pinfo.type"); cols.Add("pinfo.value"); }
			if (columns.Object) { cols.Add("oinfo.type"); cols.Add("oinfo.value"); cols.Add("oinfo.language"); cols.Add("oinfo.datatype"); }
			if (columns.Meta) { cols.Add("minfo.type"); cols.Add("minfo.value"); }
			cmd.Append(String.Join(", ", (String[])cols.ToArray(typeof(String))));
				
			cmd.Append(" FROM ");
			cmd.Append(prefix);
			cmd.Append("_statements AS q");

			if (columns.Subject) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(prefix);
				cmd.Append("_values AS sinfo ON q.subject = sinfo.id");
			}
			if (columns.Predicate) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(prefix);
				cmd.Append("_values AS pinfo ON q.predicate = pinfo.id");
			}
			if (columns.Object) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(prefix);
				cmd.Append("_values AS oinfo ON q.object = oinfo.id");
			}
			if (columns.Meta) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(prefix);
				cmd.Append("_values AS minfo ON q.meta = minfo.id");
			}
		
			cmd.Append(' ');
			
			bool wroteWhere = WhereClause(filter, cmd);
			
			// Transform literal filters into SQL.
			if (filter.LiteralFilters != null) {
				foreach (LiteralFilter f in filter.LiteralFilters) {
					string s = FilterToSQL(f, "oinfo.value");
					if (s != null) {
						if (!wroteWhere) { cmd.Append(" WHERE "); wroteWhere = true; }
						else { cmd.Append(" AND "); }
						cmd.Append(' ');
						cmd.Append(s);
					}
				}
			}
			
			if (filter.Limit >= 1) {
				cmd.Append(" LIMIT ");
				cmd.Append(filter.Limit);
			}

			cmd.Append(';');
			
			if (Debug) {
				string cmd2 = cmd.ToString();
				//if (cmd2.Length > 80) cmd2 = cmd2.Substring(0, 80);
				Console.Error.WriteLine(cmd2);
			}
			
			using (IDataReader reader = connection.RunReader(cmd.ToString())) {
				while (reader.Read()) {
					Entity s = columns.Subject ? null : filter.Subjects[0];
					Entity p = columns.Predicate ? null : filter.Predicates[0];
					Resource o = columns.Object ? null : filter.Objects[0];
					Entity m = columns.Meta ? null : filter.Metas[0];
					
					int col = 0;
					if (columns.Subject) { s = SelectEntity(reader.GetInt32(col++), reader.GetString(col++)); }
					if (columns.Predicate) { p = SelectEntity(reader.GetInt32(col++), reader.GetString(col++)); }
					if (columns.Object) { o = SelectResource(reader.GetInt32(col++), reader.GetString(col++), reader.GetString(col++), reader.GetString(col++)); }
					if (columns.Meta) { m = SelectEntity(reader.GetInt32(col++), reader.GetString(col++)); }
					
					if (filter.LiteralFilters != null && !LiteralFilter.MatchesFilters(o, filter.LiteralFilters, this))
						continue;
						
					bool ret = result.Add(new Statement(s, p, o, m));
					if (!ret) break;
				}
			}
		}
		
		private Entity SelectEntity(int type, string value) {
			switch (type) {
			case 1: return new Entity(value);
			case 2:
				if (value == DEFAULT_META_KEY) return Statement.DefaultMeta;
				BNode b = new BNode();
				b.SetGUID(new Guid(value));
				return b;
			}
			throw new Exception(); //unreachable
		}
		
		private Resource SelectResource(int type, string value, string language, string datatype) {
			switch (type) {
			case 1:
			case 2:
				return SelectEntity(type,value);
			case 3:
				return new Literal(value, language, datatype);
			}
			throw new Exception(); //unreachable
		}
		
		private string CreatePrefixTest(string column, string match) {
			StringBuilder s = new StringBuilder();
			connection.CreatePrefixTest(column, match, s);
			return s.ToString();
		}

		private string FilterToSQL(LiteralFilter filter, string col) {
			if (filter is SemWeb.Filters.StringCompareFilter) {
				SemWeb.Filters.StringCompareFilter f = (SemWeb.Filters.StringCompareFilter)filter;
				return col + FilterOpToSQL(f.Type) + Escape(f.Pattern, true);
			}
			/*if (filter is SemWeb.Filters.StringContainsFilter) {
				SemWeb.Filters.StringContainsFilter f = (SemWeb.Filters.StringContainsFilter)filter;
				return CreateLikeTest(col, f.Pattern, 1); // 1=contains
			}*/
			if (filter is SemWeb.Filters.StringStartsWithFilter) {
				SemWeb.Filters.StringStartsWithFilter f = (SemWeb.Filters.StringStartsWithFilter)filter;
				return CreatePrefixTest(col, f.Pattern); // 0=starts-with
			}
			if (filter is SemWeb.Filters.NumericCompareFilter) {
				SemWeb.Filters.NumericCompareFilter f = (SemWeb.Filters.NumericCompareFilter)filter;
				return col + FilterOpToSQL(f.Type) + f.Number;
			}
			return null;
		}
		
		private string FilterOpToSQL(LiteralFilter.CompType op) {
			switch (op) {
			case LiteralFilter.CompType.LT: return " < ";
			case LiteralFilter.CompType.LE: return " <= ";
			case LiteralFilter.CompType.NE: return " <> ";
			case LiteralFilter.CompType.EQ: return " = ";
			case LiteralFilter.CompType.GT: return " > ";
			case LiteralFilter.CompType.GE: return " >= ";
			default: throw new ArgumentException(op.ToString());
			}			
		}
		
		private string Escape(string str, bool quotes) {
			if (str == null) return "NULL";
			StringBuilder b = new StringBuilder();
			EscapedAppend(b, str, quotes, false);
			return b.ToString();
		}
		
		protected void EscapedAppend(StringBuilder b, string str) {
			EscapedAppend(b, str, true, false);
		}

		protected virtual void EscapedAppend(StringBuilder b, string str, bool quotes, bool forLike) {
			if (quotes) b.Append('\'');
			for (int i = 0; i < str.Length; i++) {
				char c = str[i];
				switch (c) {
					case '\n': b.Append("\\n"); break;
					case '\\':
					case '\"':
					case '*':
						b.Append('\\');
						b.Append(c);
						break;
					case '%':
					case '_':
						if (forLike)
							b.Append('\\');
						b.Append(c);
						break;
					default:
						b.Append(c);
						break;
				}
			}
			if (quotes) b.Append('\'');
		}
		
		public void Import(StatementSource source) {
			if (source == null) throw new ArgumentNullException();
			
			Init();
			connection.BeginTransaction();
			
			Importer imp = new Importer(this);
		
			try {
				source.Select(imp);
			} finally {
				imp.RunAddBuffer();
				connection.EndTransaction();
			}
		}

		public void Replace(Entity a, Entity b) {
			Init();
			
			AddValue(b);
			
			foreach (string col in new string[] { "subject", "predicate", "object", "meta" }) {
				StringBuilder cmd = new StringBuilder();
				cmd.Append("UPDATE ");
				cmd.Append(prefix);
				cmd.Append("_statements SET ");
				cmd.Append(col);
				cmd.Append('=');
				cmd.Append('\'');
				cmd.Append(GetHash(b));
				cmd.Append('\'');
				WhereItem(col, new Resource[] { a }, cmd, false);
				cmd.Append(';');
				connection.RunCommand(cmd.ToString());
			}
		}
		
		public void Replace(Statement find, Statement replacement) {
			if (find.AnyNull) throw new ArgumentNullException("find");
			if (replacement.AnyNull) throw new ArgumentNullException("replacement");
			if (find == replacement) return;
			
			Init();

			AddValue(replacement.Subject);
			AddValue(replacement.Predicate);
			AddValue(replacement.Object);
			AddValue(replacement.Meta);
			
			StringBuilder cmd = new StringBuilder();
			
			cmd.Append("UPDATE ");
			cmd.Append(prefix);
			cmd.Append("_statements SET subject='");
			cmd.Append(GetHash(replacement.Subject));
			cmd.Append("', predicate='");
			cmd.Append(GetHash(replacement.Predicate));
			cmd.Append("', object='");
			cmd.Append(GetHash(replacement.Object));
			cmd.Append("', meta='");
			cmd.Append(GetHash(replacement.Meta));
			cmd.Append("' ");
			
			WhereClause(new SelectFilter(find), cmd);
			
			connection.RunCommand(cmd.ToString());
		}
		
		private int RunScalarInt(string sql, int def) {
			object ret = connection.RunScalar(sql);
			if (ret == null) return def;
			if (ret is int) return (int)ret;
			try {
				return int.Parse(ret.ToString());
			} catch (FormatException) {
				return def;
			}
		}
		
		private string RunScalarString(string sql) {
			object ret = connection.RunScalar(sql);
			if (ret == null) return null;
			if (ret is string) return (string)ret;
			if (ret is byte[]) return System.Text.Encoding.UTF8.GetString((byte[])ret);
			throw new FormatException("SQL store returned a literal value as " + ret);
		}
		
		void IDisposable.Dispose() {
			Close();
		}
		
		public void Close() {
			connection.CloseConnection();
		}

	}
	
}
