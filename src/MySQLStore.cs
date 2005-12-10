using System;
using System.Collections;

using System.Data;
using ByteFX.Data.MySqlClient;

namespace SemWeb.Stores {
	
	public class MySQLStore : SQLStore, IDisposable {
		MySqlConnection connection;
		string connectionString;
		
		bool locked = false;
		int locker = 0;
		
		bool Debug = false;
		
		public MySQLStore(string connectionString, string table)
			: base(table) {
			this.connectionString = connectionString;
			RefreshConnection();
		}

		protected override bool SupportsNoDuplicates { get { return true; } }
		protected override bool SupportsInsertIgnore { get { return true; } }
		protected override bool SupportsInsertCombined { get { return true; } }
		protected override bool SupportsUseIndex { get { return true; } }
		
		protected override string CreateNullTest(string column) {
			return "ISNULL(" + column + ")";
		}

		public void Dispose() {
			connection.Close();
		}
		
		private void RefreshConnection() {
			if (connection != null)
				connection.Close();
			connection = new MySqlConnection(connectionString);
			connection.Open();		
		}
		
		protected override void RunCommand(string sql) {
			Yield();
			if (Debug) Console.Error.WriteLine(sql);
			using (MySqlCommand cmd = new MySqlCommand(sql, connection))
				cmd.ExecuteNonQuery();
		}
		
		protected override object RunScalar(string sql) {
			Yield();
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
			Yield();
			if (Debug) Console.Error.WriteLine(sql);
			using (MySqlCommand cmd = new MySqlCommand(sql, connection)) {
				return cmd.ExecuteReader();
			}
		}

		protected override void BeginTransaction() {
			//RunCommand("BEGIN");
			RunCommand("LOCK TABLES " + TableName + "_statements WRITE, " + TableName + "_literals WRITE, " + TableName + "_entities WRITE");
			locked = true;
		}
		
		protected override void EndTransaction() {
			//RunCommand("COMMIT");
			RunCommand("UNLOCK TABLES");
			locked = false;
		}
		
		private void Yield() {
			if (!locked) return;
			if (locker++ == 100) {
				locker = 0;
				RunCommand("UNLOCK TABLES;");
				BeginTransaction();
			}
		}
	}
}
