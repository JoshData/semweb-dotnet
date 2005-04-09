using System;
using System.Collections;

using System.Data;
using ByteFX.Data.MySqlClient;

namespace SemWeb.Stores {
	
	public class MySQLStore : SQLStore, IDisposable {
		MySqlConnection connection;
		string connectionString;
		
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
			if (Debug) Console.Error.WriteLine(sql);
			MySqlCommand cmd = new MySqlCommand(sql, connection);
			cmd.ExecuteNonQuery();
			cmd.Dispose();
		}
		
		protected override object RunScalar(string sql) {
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
			if (Debug) Console.Error.WriteLine(sql);
			MySqlCommand cmd = new MySqlCommand(sql, connection);
			IDataReader reader = cmd.ExecuteReader();
			cmd.Dispose();
			return reader;
		}

		protected override void BeginTransaction() {
			RefreshConnection();
			
			try {
				RunCommand("DROP INDEX subject_index on " + TableName);
				RunCommand("DROP INDEX predicate_index on " + TableName);
				RunCommand("DROP INDEX object_index on " + TableName);
				RunCommand("DROP INDEX subject_predicate_index on " + TableName);
				RunCommand("DROP INDEX predicate_object_index on " + TableName);
			} catch (Exception e) {
			}
			
			RunCommand("BEGIN");
			//RunCommand("LOCK TABLES " + TableName + " WRITE");
		}
		
		protected override void EndTransaction() {
			RunCommand("COMMIT");
			//RunCommand("UNLOCK TABLES");
			
			CreateIndexes();
		}
	}
}
