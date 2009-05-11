using System;
using System.Collections;
using System.Text;

using System.Data;
using Mono.Data.SqliteClient;

namespace SemWeb.Stores {
	
	public class SqliteStore : SQLStore {
		string connectionString;
		SqliteConnection dbcon;
		
		bool debug = false;
		
		public SqliteStore(string connectionString, string table)
			: base(table) {
			this.connectionString = connectionString;
			dbcon = new SqliteConnection(connectionString);
			dbcon.Open();
		}
		
		protected override bool HasUniqueStatementsConstraint { get { return dbcon.Version == 3; } }
		protected override string InsertIgnoreCommand { get { return "OR IGNORE"; } }
		protected override bool SupportsInsertCombined { get { return false; } }
		protected override bool SupportsSubquery { get { return false; } }
		
		protected override void CreateNullTest(string column, System.Text.StringBuilder command) {
			command.Append(column);
			command.Append(" ISNULL");
		}
		
		protected override void CreateLikeTest(string column, string match, int method, System.Text.StringBuilder command) {
			command.Append(column);
			command.Append(" LIKE '");
			if (method == 1 || method == 2) command.Append("%"); // contains or ends-with
			EscapedAppend(command, match, true);
			if (method != 2) command.Append("%"); // contains or starts-with
			command.Append("' ESCAPE '\\'");
		}
		
		protected override void EscapedAppend(StringBuilder b, string str) {
			EscapedAppend(b, str, false);
		}
		
		private void EscapedAppend(StringBuilder b, string str, bool forLike) {
			if (!forLike) b.Append('\'');
			for (int i = 0; i < str.Length; i++) {
				char c = str[i];
				switch (c) {
					case '\'':
						b.Append(c);
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
			if (!forLike) b.Append('\'');
		}
		
		public override void Close() {
			dbcon.Close();
		}
		
		protected override void RunCommand(string sql) {
			if (debug) Console.Error.WriteLine(sql);
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			dbcmd.ExecuteNonQuery();
			dbcmd.Dispose();
		}
		
		protected override object RunScalar(string sql) {
			if (debug) Console.Error.Write(sql);
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			object ret = dbcmd.ExecuteScalar();
			dbcmd.Dispose();
			if (debug) Console.Error.WriteLine(" => " + ret);
			return ret;
		}

		protected override IDataReader RunReader(string sql) {
			if (debug) Console.Error.WriteLine(sql);
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			IDataReader reader = dbcmd.ExecuteReader();
			dbcmd.Dispose();
			return reader;
		}

		protected override void BeginTransaction() {
			RunCommand("BEGIN");
			RunCommand("DROP INDEX subject_index");
			RunCommand("DROP INDEX predicate_index");
			RunCommand("DROP INDEX object_index");
			RunCommand("DROP INDEX meta_index");
		}
		
		protected override void EndTransaction() {
			RunCommand("END");
			CreateIndexes();
		}
		
		protected override void CreateIndexes() {
			foreach (string cmd in GetCreateIndexCommands(TableName, dbcon.Version)) {
				try {
					RunCommand(cmd);
				} catch (Exception) {
					// creating an index with the same name as an existing one, even with IF NOT EXISTS,
					// causes the data adapter to throw an exception.
				}
			}
		}
		static ArrayList GetCreateIndexCommands(string table, int ver) {
			ArrayList ret = new ArrayList();
			string ine = "";
			if (ver == 3)
				ine = "IF NOT EXISTS";
			ret.AddRange(new string[] {
				"CREATE INDEX " + ine + " subject_index ON " + table + "_statements(subject);",
				"CREATE INDEX " + ine + " predicate_index ON " + table + "_statements(predicate);",
				"CREATE INDEX " + ine + " object_index ON " + table + "_statements(object);",
				"CREATE INDEX " + ine + " meta_index ON " + table + "_statements(meta);",
			
				"CREATE UNIQUE INDEX " + ine + " literal_index ON " + table + "_literals(hash);",
				"CREATE UNIQUE INDEX " + ine + " entity_index ON " + table + "_entities(value);"
					});
			if (ver == 3)
				ret.Add("CREATE UNIQUE INDEX IF NOT EXISTS full_index ON " + table + "_statements(subject,predicate,object,meta);");
			return ret;
		}
	}
}
