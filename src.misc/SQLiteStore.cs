using System;
using System.Collections;

using System.Data;
using Mono.Data.SqliteClient;

namespace SemWeb.Stores {
	
	public class SqliteStore : SQLStore {
		IDbConnection dbcon;
		
		bool debug = false;
		
		public SqliteStore(string connectionString, string table, KnowledgeModel model)
			: base(table, model) {
			dbcon = new SqliteConnection(connectionString);
			dbcon.Open();
		}
		
		protected override void RunCommand(string sql) {
			if (debug) Console.Error.WriteLine(sql);
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
			if (debug) Console.Error.WriteLine(sql + " => " + ret);
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
			try {
				RunCommand("DROP INDEX subject_index on " + TableName);
				RunCommand("DROP INDEX predicate_index on " + TableName);
				RunCommand("DROP INDEX object_index on " + TableName);
				RunCommand("DROP INDEX subject_predicate_index on " + TableName);
				RunCommand("DROP INDEX predicate_object_index on " + TableName);
			} catch (Exception e) {
			}

			RunCommand("BEGIN");
		}
		
		protected override void EndTransaction() {
			RunCommand("END");
			CreateIndexes();
		}
		
	}
}
