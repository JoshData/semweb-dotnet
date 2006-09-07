using System;
using System.Collections;

using System.Data;
using Npgsql;

namespace SemWeb.Stores {
	
	public class PostgreSQLStore : SQLStore, IDisposable {
		NpgsqlConnection connection;
		string connectionString;
		
		bool Debug = false;
		
		string[] CreateTableCommands;
		string[] CreateIndexCommands;

		public PostgreSQLStore(string connectionString, string table)
			: base(table) {

			this.CreateTableCommands = new string[] {
				"CREATE TABLE " + table + "_statements" +
				"(subject INTEGER NOT NULL, predicate INTEGER NOT NULL, objecttype INTEGER NOT NULL, object INTEGER NOT NULL, meta INTEGER NOT NULL);",
				
				"CREATE TABLE " + table + "_literals" +
				"(id INTEGER NOT NULL, value TEXT NOT NULL, language TEXT, datatype TEXT, PRIMARY KEY(id));",
				
				"CREATE TABLE " + table + "_entities" +
				"(id INTEGER NOT NULL, value TEXT NOT NULL, PRIMARY KEY(id));"
				};

			this.CreateIndexCommands = new string[] {
				"CREATE INDEX subject_index ON " + table + "_statements(subject,predicate,object,meta);",
				"CREATE INDEX predicate_index ON " + table + "_statements(predicate);",
				"CREATE INDEX object_index ON " + table + "_statements(object);",
				"CREATE INDEX meta_index ON " + table + "_statements(meta);",
			
				"CREATE UNIQUE INDEX literal_index ON " + table + "_literals(hash);",
				"CREATE UNIQUE INDEX entity_index ON " + table + "_entities(value);"
				};
			this.connectionString = connectionString;
			RefreshConnection();
		}

		protected override bool HasUniqueStatementsConstraint { get { return false; } }
		protected override string InsertIgnoreCommand { get { return null; } }
		protected override bool SupportsInsertCombined { get { return false; } }
		protected override bool SupportsSubquery { get { return false; } }
		
		protected override void CreateNullTest(string column, System.Text.StringBuilder command) {
			command.Append(column);
			command.Append(" IS NULL");
		}

		public override void Close() {
			connection.Close();
		}
		
		private void RefreshConnection() {
			if (connection != null)
				connection.Close();
			connection = new NpgsqlConnection(connectionString);
			connection.Open();		
		}
		
		protected override void RunCommand(string sql) {
			if (Debug) Console.Error.WriteLine(sql);
			using (NpgsqlCommand cmd = new NpgsqlCommand(sql, connection))
				cmd.ExecuteNonQuery();
		}
		
		protected override object RunScalar(string sql) {
			using (NpgsqlCommand cmd = new NpgsqlCommand(sql, connection)) {
			object ret = cmd.ExecuteScalar();
			if (Debug) Console.Error.WriteLine(sql + " => " + ret);
			return ret;
			}
		}

		protected override IDataReader RunReader(string sql) {
			if (Debug) Console.Error.WriteLine(sql);
			using (NpgsqlCommand cmd = new NpgsqlCommand(sql, connection)) {
			IDataReader reader = cmd.ExecuteReader();
			return reader;
			}
		}

		protected override void BeginTransaction() {
			RunCommand("BEGIN");
		}
		protected override void EndTransaction() {
			RunCommand("END");
		}

		protected override void CreateTable() {
			foreach (string cmd in CreateTableCommands) {
				try {
					RunCommand(cmd);
				} catch (Exception e) {
					if (Debug) Console.Error.WriteLine(e);
				}
			}
		}
		protected override void CreateIndexes() {
			foreach (string cmd in CreateIndexCommands) {
				try {
					RunCommand(cmd);
				} catch (Exception e) {
					if (Debug) Console.Error.WriteLine(e);
				}
			}
		}
		protected override char GetQuoteChar() {
			return '\'';
		}
	}
}

