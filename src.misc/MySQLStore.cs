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
			MySqlCommand cmd = new MySqlCommand(sql, connection);
			cmd.ExecuteNonQuery();
			cmd.Dispose();
		}
		
		protected override object RunScalar(string sql) {
			Yield();
			MySqlCommand cmd = new MySqlCommand(sql, connection);
			IDataReader reader = cmd.ExecuteReader();
			object ret = null;
			if (reader.Read()) {
				ret = reader[0];
			}
			reader.Close();
			cmd.Dispose();
			if (Debug) Console.Error.WriteLine(sql + " => " + ret);
			return ret;
		}

		protected override IDataReader RunReader(string sql) {
			Yield();
			if (Debug) Console.Error.WriteLine(sql);
			MySqlCommand cmd = new MySqlCommand(sql, connection);
			IDataReader reader = cmd.ExecuteReader();
			cmd.Dispose();
			return reader;
		}

		protected override void BeginTransaction() {
			//RunCommand("BEGIN");
			RunCommand("LOCK TABLES " + TableName + "_statements WRITE, " + TableName + "_literals WRITE");
			locked = true;
		}
		
		protected override void EndTransaction() {
			//RunCommand("COMMIT");
			RunCommand("UNLOCK TABLES");
			locked = false;
		}
		
		private void Yield() {
			if (!locked) return;
			if (locker++ == 50000) {
				locker = 0;
				RunCommand("UNLOCK TABLES; LOCK TABLES " + TableName + "_statements WRITE, " + TableName + "_literals WRITE;");
			}
		}
	}
}
