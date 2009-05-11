//#define CATCHEXCEPTIONS

using System;
using System.Collections;
using System.Text;

using System.Data;

#if BYTEFX
using ByteFX.Data.MySqlClient;
#elif CONNECTOR
using MySql.Data.MySqlClient;
#endif

namespace SemWeb.Stores {
	
	public class MySQLStore : SQLStore {
		MySqlConnection connection;
		string connectionString;
		Version version;
		
		static bool Debug = System.Environment.GetEnvironmentVariable("SEMWEB_DEBUG_MYSQL") != null;
		static string ImportMode;
		static bool DoAnalyze;
		
		static MySQLStore() {
			string mode = System.Environment.GetEnvironmentVariable("SEMWEB_MYSQL_IMPORT_MODE");
			if (mode != null) {
				string[] modeinfo = mode.Split(',');
				ImportMode = modeinfo[0];
				if (modeinfo.Length == 2)
					DoAnalyze = !(modeinfo[1] == "NOANALYZE");
			}
			if (ImportMode == null)
				ImportMode = "TRANSACTION";
		}
		
		public MySQLStore(string connectionString, string table)
			: base(table) {
			this.connectionString = connectionString;
		}
		
		public override string ToString() {
			return "mysql:" + TableName + ":" + connectionString;
		}

		protected override bool HasUniqueStatementsConstraint { get { return true; } }
		protected override string InsertIgnoreCommand { get { return "IGNORE"; } }
		protected override bool SupportsInsertCombined { get { return true; } }
		protected override bool SupportsSubquery { get { return true; } }
		protected override bool SupportsViews { get { Open(); return version >= new Version(5,0,1,0); } }
		protected override int MaximumUriLength { get { Open(); return version >= new Version(4,1,2) ? -1 : 255; } }
		
		protected override void CreateNullTest(string column, System.Text.StringBuilder command) {
			command.Append("ISNULL(");
			command.Append(column);
			command.Append(')');
		}
		
		protected override void CreateLikeTest(string column, string match, int method, System.Text.StringBuilder command) {
			command.Append(column);
			command.Append(" LIKE \"");
			if (method == 1 || method == 2) command.Append("%"); // contains or ends-with
			EscapedAppend(command, match, true);
			if (method != 2) command.Append("%"); // contains or starts with
			command.Append("\"");
		}

		protected override void EscapedAppend(StringBuilder b, string str) {
			EscapedAppend(b, str, false);
		}
		
		private void EscapedAppend(StringBuilder b, string str, bool forLike) {
			if (!forLike) b.Append('\"');
			for (int i = 0; i < str.Length; i++) {
				char c = str[i];
				switch (c) {
					case '\n': b.Append("\\n"); break;
					case '\\':
					case '\"':
					case '*':
					case '\'':
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
			if (!forLike) b.Append('\"');
		}
		
		public override void Close() {
			base.Close();
			if (connection != null)
				connection.Close();
		}
		
		private void Open() {
			if (connection != null)
				return;
			MySqlConnection c = new MySqlConnection(connectionString);
			c.Open();
			connection = c; // only set field if open was successful
			
			using (IDataReader reader = RunReader("show variables like \"version\"")) {
				reader.Read();
				
				// I get 5.0.51a-3ubuntu5.1.
				string v = reader.GetString(1);
				if (v.IndexOf('-') != -1)
					v = v.Substring(0, v.IndexOf('-'));
				while (char.IsLetter(v[v.Length-1]))
					v = v.Substring(0, v.Length-1);
				
				version = new Version(v);
			}
		}
		
		#if !CATCHEXCEPTIONS
		
		protected override void RunCommand(string sql) {
			Open();
			if (Debug) Console.Error.WriteLine(sql);
			using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
				cmd.CommandTimeout = 0; // things like Clear can take a while
				cmd.ExecuteNonQuery();
			}
		}
		
		protected override object RunScalar(string sql) {
			Open();
			using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
				object ret = null;
				using (IDataReader reader = cmd.ExecuteReader()) {
					if (reader.Read()) {
						ret = reader[0];
					}
				}
				if (Debug) Console.Error.WriteLine(sql + " => " + ret);
				return ret;
			}
		}

		protected override IDataReader RunReader(string sql) {
			Open();
			if (Debug) Console.Error.WriteLine(sql);
			using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
				return cmd.ExecuteReader();
			}
		}
		
		#else

		protected override void RunCommand(string sql) {
			Open();
			try {
				if (Debug) Console.Error.WriteLine(sql);
				using (MySqlCommand cmd = new MySqlCommand(sql, connection))
					cmd.ExecuteNonQuery();
			} catch (Exception e) {
				Console.WriteLine(sql);
				throw e;
			}
		}
		
		protected override object RunScalar(string sql) {
			Open();
			try {
				using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
					object ret = null;
					using (IDataReader reader = cmd.ExecuteReader()) {
						if (reader.Read()) {
							ret = reader[0];
						}
					}
					if (Debug) Console.Error.WriteLine(sql + " => " + ret);
					return ret;
				}
			} catch (Exception e) {
				Console.WriteLine(sql);
				throw e;
			}
		}

		protected override IDataReader RunReader(string sql) {
			Open();
			try {
				if (Debug) Console.Error.WriteLine(sql);
				using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
					return cmd.ExecuteReader();
				}
			} catch (Exception e) {
				Console.WriteLine(sql);
				throw e;
			}
		}
		
		#endif

		protected override void BeginTransaction() {
			if (ImportMode == "DISABLEKEYS")
				RunCommand("ALTER TABLE " + TableName + "_statements DISABLE KEYS");
			else if (ImportMode == "TRANSACTION")
				RunCommand("BEGIN");
			else if (ImportMode == "LOCK")
				RunCommand("LOCK TABLES " + TableName + "_statements WRITE, " + TableName + "_literals WRITE, " + TableName + "_entities WRITE");
				
			//RunCommand("ALTER TABLE " + TableName + "_entities DELAY_KEY_WRITE=1");
			//RunCommand("ALTER TABLE " + TableName + "_literals DELAY_KEY_WRITE=1");
		}
		
		protected override void EndTransaction() {
			//RunCommand("ALTER TABLE " + TableName + "_entities DELAY_KEY_WRITE=0");
			//RunCommand("ALTER TABLE " + TableName + "_literals DELAY_KEY_WRITE=0");
			
			if (ImportMode == "DISABLEKEYS")
				RunCommand("ALTER TABLE " + TableName + "_statements ENABLE KEYS");
			else if (ImportMode == "TRANSACTION")
				RunCommand("COMMIT");
			else if (ImportMode == "LOCK")
				RunCommand("UNLOCK TABLES");
				
			RunCommand("ANALYZE TABLE " + TableName + "_entities");
			RunCommand("ANALYZE TABLE " + TableName + "_literals");
			RunCommand("ANALYZE TABLE " + TableName + "_statements");
		}
		
	}
}
