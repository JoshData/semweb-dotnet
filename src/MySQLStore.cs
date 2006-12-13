using System;
using System.Collections;

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
		
		static bool Debug = System.Environment.GetEnvironmentVariable("SEMWEB_DEBUG_MYSQL") != null;
		
		public MySQLStore(string connectionString, string table)
			: base(table) {
			this.connectionString = connectionString;
		}

		protected override bool HasUniqueStatementsConstraint { get { return true; } }
		protected override string InsertIgnoreCommand { get { return "IGNORE"; } }
		protected override bool SupportsInsertCombined { get { return true; } }
		protected override bool SupportsSubquery { get { return true; } }
		
		protected override void CreateNullTest(string column, System.Text.StringBuilder command) {
			command.Append("ISNULL(");
			command.Append(column);
			command.Append(')');
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
		}
		
		protected override void RunCommand(string sql) {
			Open();
			if (Debug) Console.Error.WriteLine(sql);
			using (MySqlCommand cmd = new MySqlCommand(sql, connection))
				cmd.ExecuteNonQuery();
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

		protected override void BeginTransaction() {
			//RunCommand("BEGIN");
			//RunCommand("LOCK TABLES " + TableName + "_statements WRITE, " + TableName + "_literals WRITE, " + TableName + "_entities WRITE");
			RunCommand("ALTER TABLE " + TableName + "_statements DISABLE KEYS");
		}
		
		protected override void EndTransaction() {
			//RunCommand("COMMIT");
			//RunCommand("UNLOCK TABLES");
			RunCommand("ALTER TABLE " + TableName + "_statements ENABLE KEYS");
			RunCommand("ANALYZE TABLE " + TableName + "_entities");
			RunCommand("ANALYZE TABLE " + TableName + "_literals");
			RunCommand("ANALYZE TABLE " + TableName + "_statements");
		}
		
	}
}
