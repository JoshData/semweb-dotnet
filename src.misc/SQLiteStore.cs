using System;
using System.Collections;

using System.Data;
using Mono.Data.SqliteClient;

namespace SemWeb.Stores {
	
	public class SqliteStore : SQLStore {
		IDbConnection dbcon;
		
		public SqliteStore(string connectionString, string table, KnowledgeModel model)
			: base(table, model) {
			dbcon = new SqliteConnection(connectionString);
			dbcon.Open();
		}
		
		protected override void RunCommand(string sql) {
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			dbcmd.ExecuteNonQuery();
			dbcmd.Dispose();
		}
		
		protected override object RunScalar(string sql) {
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			object ret = dbcmd.ExecuteScalar();
			dbcmd.Dispose();
			return ret;
		}

		protected override IDataReader RunReader(string sql) {
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = sql;
			IDataReader reader = dbcmd.ExecuteReader();
			dbcmd.Dispose();
			return reader;
		}

		protected override void BeginTransaction() {
			RunCommand("BEGIN");
		}
		
		protected override void EndTransaction() {
			RunCommand("END");
		}
		
	}
}
