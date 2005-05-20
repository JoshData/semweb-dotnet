using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Text;

using SemWeb.Util;

namespace SemWeb.Stores {
	// TODO: It's not safe to have two concurrent accesses to the same database
	// because the creation of new entities will use the same IDs.
	
	public abstract class SQLStore : Store, Entity.LazyUriLoader {
		string table;
		
		bool firstUse = true;
		UriMap lockedIdCache = null;
		int cachedNextId = -1;
		
		Hashtable entityMap = new Hashtable();
		int entityMapInsertions = 0;
		
		bool Debug = false;
		
		StringBuilder addBuffer = new StringBuilder();
		StringBuilder cmdBuffer = new StringBuilder();
		Hashtable addLiteralCache = null;
		
		private class ResourceKey {
			public int ResId;
			
			public ResourceKey(int id) { ResId = id; }
			
			public override int GetHashCode() { return ResId; }
			public override bool Equals(object other) { return (other is ResourceKey) && ((ResourceKey)other).ResId == ResId; }
		}
		
		private static readonly string[] fourcols = new string[] { "subject", "predicate", "object", "meta" };
		private static readonly string[] predcol = new string[] { "predicate" };

		public SQLStore(string table) {
			this.table = table;
		}
		
		protected string TableName { get { return table; } }

		private void Init() {
			if (!firstUse) return;
			firstUse = false;
			
			CreateTable();
			CreateIndexes();
		}
		
		public override int StatementCount { get { Init(); RunAddBuffer(); return RunScalarInt("select count(subject) from " + table + "_statements", 0); } }
		
		private int NextId() {
			if (lockedIdCache != null && cachedNextId != -1)
				return ++cachedNextId;
				
			RunAddBuffer();
			
			// the 0 id is reserved for linking resources to their URIs,
			// and for indicating a resource/literal is not present in the tables
			int nextid = 1;
			
			CheckMax("select max(subject) from " + table + "_statements", ref nextid);
			CheckMax("select max(predicate) from " + table + "_statements", ref nextid);
			CheckMax("select max(object) from " + table + "_statements where objecttype=0", ref nextid);
			CheckMax("select max(meta) from " + table + "_statements", ref nextid);
			CheckMax("select max(id) from " + table + "_literals", ref nextid);
			
			cachedNextId = nextid;
			
			return nextid;
		}
		
		private void CheckMax(string command, ref int nextid) {
			int maxid = RunScalarInt(command, 0);
			if (maxid >= nextid) nextid = maxid + 1;
		}
		
		public override void Clear() {
			Init();
			addBuffer.Length = 0;
			RunCommand("DELETE FROM " + table + "_statements");
			RunCommand("DELETE FROM " + table + "_literals");
		}
		
		private int GetLiteralId(string value, string language, string datatype, bool create) {
			// Returns the literal ID associated with the literal.  If a literal
			// doesn't exist and create is true, a new literal is created,
			// otherwise 0 is returned.
			
			if (addLiteralCache != null && addLiteralCache.Count > 0) {
				Literal lit = new Literal(value, language, datatype);
				object ret = addLiteralCache[lit];
				if (ret != null) return (int)ret;
			}

			StringBuilder b = cmdBuffer; cmdBuffer.Length = 0;
			b.Append("SELECT id FROM ");
			b.Append(table);
			b.Append("_literals WHERE value = ");
			b.Append(Escape(value));
			if (language != null) {
				b.Append(" and language = ");
				b.Append(Escape(language));
			}
			if (datatype != null) {
				b.Append(" and datatype = ");
				b.Append(Escape(datatype));
			}
			b.Append(" LIMIT 1");
			
			object id = RunScalar(b.ToString());
			if (id == null) {
				if (create) return AddLiteral(value, language, datatype);
				return 0;
			}
			
			return AsInt(id);
		}
		
		private int AddLiteral(string value, string language, string datatype) {
			int id = NextId();
			
			StringBuilder b = cmdBuffer; cmdBuffer.Length = 0;
			b.Append("INSERT INTO ");
			b.Append(table);
			b.Append("_literals VALUES(");
			b.Append(id);
			b.Append(",");
			b.Append(Escape(value));
			b.Append(",");
			if (language != null)
				b.Append(Escape(language));
			else
				b.Append("NULL");
			b.Append(",");
			if (datatype != null)
				b.Append(Escape(datatype));
			else
				b.Append("NULL");
			b.Append("); ");
			
			if (addLiteralCache != null) {
				addLiteralCache[new Literal(value, language, datatype)] = id;
				addBuffer.Append(b.ToString());
				RunAddBufferIfLarge();
			} else {
				RunCommand(b.ToString());
			}
			
			return id;
		}
		
		private int GetEntityId(string uri, bool create) {
			// Returns the resource ID associated with the URI.  If a resource
			// doesn't exist and create is true, a new resource is created,
			// otherwise 0 is returned.	
			
			if (lockedIdCache != null) {
				object idobj = lockedIdCache[uri];
				if (idobj == null && !create) return 0;
				if (idobj != null) return (int)idobj;
			} else {
				StringBuilder cmd = cmdBuffer; cmdBuffer.Length = 0;
				cmd.Append("SELECT subject FROM ");
				cmd.Append(table);
				cmd.Append("_statements, ");
				cmd.Append(table);
				cmd.Append("_literals WHERE predicate = 0 and objecttype = 1 and object = id and value =");
				cmd.Append(Escape(uri));
				cmd.Append(" LIMIT 1");
				int id = RunScalarInt(cmd.ToString(), 0);
				if (id != 0 || !create) return id;
			}
			
			// If we got here, no such resource exists and
			// create is true.  Get the literal ID and then
			// add the resource.
			int literalId = GetLiteralId(uri, null, null, create);
			if (literalId == 0) return 0; // only happens if !create
			
			return AddEntity(literalId, uri);
		}
		
		private int GetResourceId(Resource resource, bool create) {
			if (resource == null) return 0;
			
			if (resource is Literal) {
				Literal lit = (Literal)resource;
				return GetLiteralId(lit.Value, lit.Language, lit.DataType, create);
			}
				
			ResourceKey key = (ResourceKey)GetResourceKey(resource);
			if (key != null) return key.ResId;
			
			if (resource.Uri == null) throw new ArgumentException("An anonymous resource created by another store cannot be used in this store.");

			int id = GetEntityId(resource.Uri, create);
			if (id != 0)
				SetResourceKey(resource, new ResourceKey(id));
			return id;
		}
		
		private int AddEntity(int uriId, string uri) {
			int id = NextId();
			
			StringBuilder b = cmdBuffer; cmdBuffer.Length = 0;
			b.Append("INSERT INTO ");
			b.Append(table);
			b.Append("_statements VALUES(");
			b.Append(id);
			b.Append(",0,1,");
			b.Append(uriId);
			b.Append(",0); ");
			
			if (lockedIdCache != null && uri != null) {
				lockedIdCache[uri] = id;
				addBuffer.Append(b.ToString());
				RunAddBufferIfLarge();
			} else {
				RunCommand(b.ToString());
			}
			
			return id;
		}
		
		private int ObjectType(Resource r) {
			if (r is Literal) return 1;
			return 0;
		}
		
		private Literal GetLiteral(int literalId, Hashtable cache) {
			// This is only called from Select, which means
			// we've just run the add buffer.
		
			if (cache != null && cache.ContainsKey(literalId))
				return (Literal)cache[literalId];
			
			IDataReader reader = RunReader("SELECT value, language, datatype FROM " + table + "_literals WHERE id = " + literalId + " LIMIT 1");
			try {
				while (reader.Read()) {
					string value = AsString(reader[0]);
					string lang = AsString(reader[1]);
					string datatype = AsString(reader[2]);
					Literal lit = new Literal(value, lang, datatype);
					if (cache != null)
						cache[literalId] = lit;
					return lit;
				}
			} finally {
				reader.Close();
			}
			return null;
		}		
		
		private string GetEntityUri(int resourceId) {
			RunAddBuffer();
			StringBuilder cmd = cmdBuffer; cmdBuffer.Length = 0;
			cmd.Append("SELECT value FROM ");
			cmd.Append(table);
			cmd.Append("_statements, ");
			cmd.Append(table);
			cmd.Append("_literals WHERE predicate = 0 and objecttype = 1 and object = id and subject =");
			cmd.Append(resourceId);
			cmd.Append(" LIMIT 1");
			return RunScalarString(cmd.ToString());
		}

		private Entity GetEntity(int resourceId, string uri, bool anon, Hashtable cache) {
			if (resourceId == 0)
				return null;
			
			ResourceKey rk = new ResourceKey(resourceId);
			
			if (cache != null && cache.ContainsKey(rk))
				return (Entity)cache[rk];
			
			if (entityMap.ContainsKey(rk)) {
				WeakReference wr = (WeakReference)entityMap[rk];
				if (wr.Target != null)
					return (Entity)wr.Target;
			}				
			
			Entity ent;
			if (anon)
				ent = new Entity(null);
			else if (uri == null)
				ent = Entity.LazyResolve(this);
			else
				ent = new Entity(uri);
			SetResourceKey(ent, rk);
			
			if (cache != null)
				cache[rk] = ent;
			
			entityMap[rk] = new WeakReference(ent);
			entityMapInsertions++;
			if (entityMapInsertions == 10000) {
				// Clear out the map of old references.
				Hashtable newMap = new Hashtable();
				foreach (DictionaryEntry d in entityMap) {
					if (((WeakReference)d.Value).Target != null)
						newMap[d.Key] = d.Value;
				}
				entityMap = newMap;
				entityMapInsertions = 0;
			}
			
			return ent;
		}
		
		string Entity.LazyUriLoader.LazyLoadUri(Entity entity) {
			ResourceKey id = (ResourceKey)GetResourceKey(entity);
			return GetEntityUri(id.ResId);
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			Init();
			
			int subj = GetResourceId(statement.Subject, true);
			int pred = GetResourceId(statement.Predicate, true);
			int objtype = ObjectType(statement.Object);
			int obj = GetResourceId(statement.Object, true);
			int meta = GetResourceId(statement.Meta, true);
			
			addBuffer.Append("INSERT INTO ");
			addBuffer.Append(table);
			addBuffer.Append("_statements VALUES (");

			addBuffer.Append(subj);
			addBuffer.Append(", ");
			addBuffer.Append(pred);
			addBuffer.Append(", ");
			addBuffer.Append(objtype);
			addBuffer.Append(", ");
			addBuffer.Append(obj);
			addBuffer.Append(", ");
			addBuffer.Append(meta);
			addBuffer.Append("); ");
			
			if (lockedIdCache == null) {
				RunCommand(addBuffer.ToString());
				addBuffer.Length = 0;
			} else {				
				RunAddBufferIfLarge();
			}
		}
		
		private void RunAddBufferIfLarge() {
			if (addBuffer.Length > 50000)
				RunAddBuffer();
		}
		
		private void RunAddBuffer() {
			if (addBuffer.Length == 0) return;
			RunCommand(addBuffer.ToString());
			addBuffer.Length = 0;
			addLiteralCache.Clear();
		}
		
		public override void Remove(Statement template) {
			Init();
			RunAddBuffer();

			System.Text.StringBuilder cmd = new System.Text.StringBuilder("DELETE FROM ");
			cmd.Append(table);
			cmd.Append("_statements ");
			if (!WhereClause(template, cmd)) return;
			
			RunCommand(cmd.ToString());
		}
		
		public override Entity[] GetAllEntities() {
			return GetAllEntities(fourcols);
		}
			
		public override Entity[] GetAllPredicates() {
			return GetAllEntities(predcol);
		}
		
		private Entity[] GetAllEntities(string[] cols) {
			RunAddBuffer();
			Hashtable h = new Hashtable();
			foreach (string col in cols) {
				ArrayList ids = new ArrayList();
				IDataReader reader = RunReader("SELECT DISTINCT " + col + " FROM " + table + (col == "object" ? " WHERE objecttype=0" : ""));
				try {
					while (reader.Read()) {
						ids.Add( AsInt(reader[0]) );
					}
				} finally {
					reader.Close();
				}
				foreach (int id in ids) {
					if (id != 0 && !h.ContainsKey(id))
						h[id] = GetEntity(id, null, false, null);
				}
			}
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}
		
		public override Entity CreateAnonymousEntity() {
			int resId;
			if (lockedIdCache != null) {
				// Can just increment the counter.
				resId = NextId();
			} else {
				// Reserve a slot in the database for this resource so its
				// id is not reused by another call to CreateAnonymousResource.
				resId = AddEntity(0, null);
			}
			return GetEntity(resId, null, true, null);
		}
		
		private bool WhereItem(string col, Resource r, System.Text.StringBuilder cmd, bool and) {
			if (and) cmd.Append(" and ");
			
			if (col == "object") {
				if (r is Literal) {
					Literal lit = (Literal)r;
					int id = GetLiteralId(lit.Value, lit.Language, lit.DataType, false);
					if (id == 0) return false;
					cmd.Append(" (objecttype = 1 and object = ");
					cmd.Append(id);
					cmd.Append(")");
				} else {
					int id = GetResourceId(r, false);
					if (id == 0) return false;
					cmd.Append(" (objecttype = 0 and object = ");
					cmd.Append(id);
					cmd.Append(")");
				}
				
			} else {
				int id = GetResourceId(r, false);
				if (id == 0) return false;
				
				cmd.Append("( ");
				cmd.Append(col);
				cmd.Append(" = ");
				cmd.Append(id);
				cmd.Append(" )");
			}
			
			return true;
		}
		
		private bool WhereClause(Statement template, System.Text.StringBuilder cmd) {
			if (template.Subject == null && template.Predicate == null && template.Object == null && template.Meta == null)
				return true;
			
			cmd.Append(" WHERE ");
			
			if (template.Subject != null)
				if (!WhereItem("subject", template.Subject, cmd, false)) return false;
			
			if (template.Subject != null)
				cmd.Append(" and");

			if (template.Predicate != null) {
				if (!WhereItem("predicate", template.Predicate, cmd, false)) return false;
			} else {
				cmd.Append(" predicate != 0");
			}
			
			if (template.Object != null)
				if (!WhereItem("object", template.Object, cmd, true)) return false;
			
			if (template.Meta != null)
				if (!WhereItem("meta", template.Meta, cmd, true)) return false;
			
			return true;
		}
		
		private int AsInt(object r) {
			if (r is int) return (int)r;
			if (r is uint) return (int)(uint)r;
			if (r is string) return int.Parse((string)r);
			throw new ArgumentException(r.ToString());
		}
		
		private string AsString(object r) {
			if (r is System.DBNull)
				return null;
			else if (r is string)
				return (string)r;
			else if (r is byte[])
				return System.Text.Encoding.UTF8.GetString((byte[])r);
			else
				throw new FormatException("SQL store returned a literal value as " + r.GetType());
		}
		
		private struct SPOLM {
			public int S, P, OT, OID, M;
		}
		
		private static void AppendComma(StringBuilder builder, string text, bool comma) {
			if (comma)
				builder.Append(", ");
			builder.Append(text);
		}
		
		private static void SelectFilter(SelectPartialFilter partialFilter, StringBuilder cmd) {
			bool f = true;
			
			if (partialFilter.Subject) { cmd.Append("subject"); f = false; }
			if (partialFilter.Predicate) { AppendComma(cmd, "predicate", !f); f = false; }
			if (partialFilter.Object) { AppendComma(cmd, "objecttype, object", !f); f = false; }
			if (partialFilter.Meta) { AppendComma(cmd, "meta", !f); f = false; }
		}
		
		public override void Select(Statement[] templates, SelectPartialFilter partialFilter, StatementSink result) {
			if (templates == null) throw new ArgumentNullException();
			if (result == null) throw new ArgumentNullException();
			if (templates.Length == 0) return;
	
			Init();
			RunAddBuffer();
			
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT ");
			SelectFilter(partialFilter, cmd);
			cmd.Append(" FROM ");
			cmd.Append(table);
			cmd.Append("_statements WHERE (");
			
			bool first = true;
			Resource sv = null, pv = null, ov = null, mv = null;
			bool sm = false, pm = false, om = false, mm = false;
			foreach (Statement template in templates) {
				if (first) {
					first = false;
					sv = template.Subject;
					pv = template.Predicate;
					ov = template.Object;
					mv = template.Meta;
				} else {
					if (sv != template.Subject) sm = true;
					if (pv != template.Predicate) pm = true;
					if (ov != template.Object) om = true;
					if (mv != template.Meta) mm = true;
				}
			}
			
			if (!sm && !pm && !om && !mm) {
				Select(templates[0], result);
				return;
			} else {
				if (!sm && sv != null)
					if (!WhereItem("subject", sv, cmd, false)) return;
				if (!pm && pv != null)
					if (!WhereItem("predicate", pv, cmd, (!sm && sv != null))) return;
				if (!om && ov != null)
					if (!WhereItem("object", ov, cmd, (!sm && sv != null) || (!pm && pv != null))) return;
				if (!mm && mv != null)
					if (!WhereItem("meta", mv, cmd, (!sm && sv != null) || (!pm && pv != null) || (!om && ov != null))) return;
				
				if (!((!sm && sv != null) || (!pm && pv != null) || (!om && ov != null) || (!mm && mv != null))) {
					cmd.Append("1");
				}
			}
			
			cmd.Append(") and (");
			
			first = true;
			foreach (Statement template in templates) {
				if (template.Subject == null && template.Predicate == null && template.Object == null) {
					Select(Statement.Empty, result);
					return;
				}
				
				if (!first)
					cmd.Append(" OR ");
				first = false;
				
				cmd.Append("(");

				if (sm && template.Subject != null)
					if (!WhereItem("subject", template.Subject, cmd, false)) return;

				if (pm && template.Predicate != null)
					if (!WhereItem("predicate", template.Predicate, cmd, (sm && template.Subject != null))) return;
					
				if (om && template.Object != null)
					if (!WhereItem("object", template.Object, cmd, (sm && template.Subject != null) || (pm && template.Predicate != null))) return;

				if (mm && template.Meta != null)
					if (!WhereItem("meta", template.Meta, cmd, (sm && template.Subject != null) || (pm && template.Predicate != null) || (om && template.Object != null))) return;

				cmd.Append(")");
			}
			
			cmd.Append(")");
			
			Select2(cmd.ToString(), Statement.Empty, partialFilter, partialFilter.SelectFirst, result);
		}
		
		public override void Select(Statement template, SelectPartialFilter partialFilter, StatementSink result) {
			if (result == null) throw new ArgumentNullException();
	
			Init();
			RunAddBuffer();
			
			bool limitOne = partialFilter.SelectFirst;
			
			// Don't select on columns that we already know from the template
			partialFilter = new SelectPartialFilter(
				partialFilter.Subject && template.Subject == null,
				partialFilter.Predicate && template.Predicate == null,
				partialFilter.Object && template.Object == null,
				partialFilter.Meta && template.Meta == null
				);
			
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT ");
			SelectFilter(partialFilter, cmd);
			cmd.Append(" FROM ");
			cmd.Append(table);
			cmd.Append("_statements");
			if (!WhereClause(template, cmd)) return;
			
			Select2(cmd.ToString(), template, partialFilter, limitOne, result);
		}
		
		private void Select2(string cmd, Statement template, SelectPartialFilter partialFilter, bool limitOne, StatementSink result) {
			// Run the query
			
			if (limitOne)
				cmd += " LIMIT 1";
			
			if (Debug) {
				string cmd2 = cmd;
				if (cmd2.Length > 80) cmd2 = cmd2.Substring(0, 80);
				Console.Error.WriteLine(cmd2);
			}
			
			ArrayList items = new ArrayList();
			IDataReader reader = RunReader(cmd);
			ArrayList entities = new ArrayList();
			try {
				while (reader.Read()) {
					int col = 0;
					int s = -1, p = -1, ot = 0, oid = -1, m = -1;
					
					if (partialFilter.Subject) { s = AsInt(reader[col++]); entities.Add(s); }
					if (partialFilter.Predicate) { p = AsInt(reader[col++]); entities.Add(p); }
					if (partialFilter.Object) { ot = AsInt(reader[col++]); oid = AsInt(reader[col++]); if (ot == 0) entities.Add(oid); }
					if (partialFilter.Meta) { m = AsInt(reader[col++]); if (m > 0) entities.Add(m); }
					
					SPOLM d = new SPOLM();
					d.S = s;
					d.P = p;
					d.OT = ot;
					d.OID = oid;
					d.M = m;
					
					items.Add(d);
				}
			} finally {
				reader.Close();
			}
			
			/*
			Hashtable uris = new Hashtable();		
			if (entities.Count > 0) {
				// Get all of the URIs of the resources
				StringBuilder uricmd = new StringBuilder();
				uricmd.Append("SELECT subject, value FROM ");
				uricmd.Append(table);
				uricmd.Append("_statements, ");
				uricmd.Append(table);
				uricmd.Append("_literals WHERE predicate = 0 and objecttype = 1 and object = id and (0");
				foreach (int ent in entities) {
					uricmd.Append(" or subject = ");
					uricmd.Append(ent);
				}
				uricmd.Append(")");
				IDataReader urireader = RunReader(uricmd.ToString());
				try {
					while (urireader.Read()) {
						int ent = AsInt(urireader[0]);
						string uri = AsString(urireader[1]);
						uris[ent] = uri;
					}
				} finally {
					urireader.Close();
				}
			}
			*/
			
			Hashtable entMap = new Hashtable();
			Hashtable litMap = new Hashtable();
			
			foreach (SPOLM item in items) {
				bool ret = result.Add(new Statement(
					template.Subject != null ? template.Subject : GetEntity(item.S, null, false, entMap),
					template.Predicate != null ? template.Predicate : GetEntity(item.P, null, false, entMap),
					template.Object != null ? template.Object : 
						(item.OT == 0 ? (Resource)GetEntity(item.OID, null, false, entMap) : (Resource)GetLiteral(item.OID, litMap)),
					(template.Meta != null || item.M <= 0) ? template.Meta : GetEntity(item.M, null, false, entMap)
					));
				if (!ret) break;
			}
		}
		
		internal static string Escape(string str) {
			if (str == null) return "NULL";
			System.Text.StringBuilder b = new System.Text.StringBuilder(str);
			b.Replace("\\", "\\\\");
			b.Replace("\"", "\\\"");
			b.Replace("\n", "\\n");
			return "\"" + b.ToString() + "\"";
		}

		public override void Import(RdfReader parser) {
			if (parser == null) throw new ArgumentNullException();
			
			Init();
			RunAddBuffer();
			
			UriMap oldLockedIdCache = lockedIdCache;
			Hashtable oldAddLiteralCache = addLiteralCache;
			
			if (lockedIdCache == null) {
				cachedNextId = -1;
				lockedIdCache = new UriMap();
				addLiteralCache = new Hashtable();
				IDataReader reader = RunReader("SELECT subject, value FROM " + table + "_statements, " + table + "_literals WHERE predicate = 0 AND id = object");
				try {
					while (reader.Read()) {
						int resourceid = AsInt(reader[0]);
						string uri = AsString(reader[1]);
						lockedIdCache[uri] = resourceid;
					}
				} finally {
					reader.Close();
				}
			}

			BeginTransaction();
			
			try {
				base.Import(parser);
			} finally {
				RunAddBuffer();
				
				lockedIdCache = oldLockedIdCache;
				addLiteralCache = oldAddLiteralCache;
				EndTransaction();
			}
		}

		public override void Replace(Entity a, Entity b) {
			Init();
			RunAddBuffer();
			int id = GetResourceId(b, true);
			
			foreach (string col in fourcols) {
				StringBuilder cmd = new StringBuilder();
				cmd.Append("UPDATE ");
				cmd.Append(table);
				cmd.Append("_statements SET ");
				cmd.Append(col);
				cmd.Append("=");
				cmd.Append(id);
				if (!WhereItem(col, a, cmd, false)) return;
				RunCommand(cmd.ToString());
			}			
		}
		
		protected abstract void RunCommand(string sql);
		protected abstract object RunScalar(string sql);
		protected abstract IDataReader RunReader(string sql);
		
		protected int RunScalarInt(string sql, int def) {
			object ret = RunScalar(sql);
			if (ret == null) return def;
			if (ret is int) return (int)ret;
			try {
				return int.Parse(ret.ToString());
			} catch (FormatException e) {
				return def;
			}
		}
		
		protected string RunScalarString(string sql) {
			object ret = RunScalar(sql);
			if (ret == null) return null;
			if (ret is string) return (string)ret;
			if (ret is byte[]) return System.Text.Encoding.UTF8.GetString((byte[])ret);
			throw new FormatException("SQL store returned a literal value as " + ret);
		}

		protected virtual void CreateTable() {
			foreach (string cmd in GetCreateTableCommands(table)) {
				try {
					RunCommand(cmd);
				} catch (Exception e) {
				}
			}
		}
		
		protected virtual void CreateIndexes() {
			foreach (string cmd in GetCreateIndexCommands(table)) {
				try {
					RunCommand(cmd);
				} catch (Exception e) {
				}
			}
		}
		
		protected virtual void BeginTransaction() { }
		protected virtual void EndTransaction() { }
		
		internal static string[] GetCreateTableCommands(string table) {
			return new string[] {
				"CREATE TABLE " + table + "_statements" +
				"(subject int UNSIGNED NOT NULL, predicate int UNSIGNED NOT NULL, objecttype int NOT NULL, object int UNSIGNED NOT NULL, meta int UNSIGNED NOT NULL);",
				
				"CREATE TABLE " + table + "_literals" +
				"(id INT NOT NULL, value BLOB NOT NULL, language TEXT, datatype TEXT, PRIMARY KEY(id));"
				};
		}
		
		internal static string[] GetCreateIndexCommands(string table) {
			return new string[] {
				"CREATE INDEX subject_index ON " + table + "_statements(subject);",
				"CREATE INDEX predicate_index ON " + table + "_statements(predicate);",
				"CREATE INDEX object_index ON " + table + "_statements(objecttype, object);",
			
				"CREATE INDEX literal_index ON " + table + "_literals(value(128));"
				};
		}
	}
	
}

namespace SemWeb.IO {
	using SemWeb;
	using SemWeb.Stores;
	
	public class SQLWriter : RdfWriter {
		TextWriter writer;
		string table;
		
		int resourcecounter = 0;
		Hashtable resources = new Hashtable();
		
		NamespaceManager m = new NamespaceManager();
		
		string[,] fastmap = new string[3,2];
		
		public SQLWriter(string spec) : this(GetWriter("-"), spec) { }
		
		public SQLWriter(string file, string tablename) : this(GetWriter(file), tablename) { }

		public SQLWriter(TextWriter writer, string tablename) {
			this.writer = writer;
			this.table = tablename;
			
			foreach (string cmd in SQLStore.GetCreateTableCommands(table))
				writer.WriteLine(cmd);
		}
		
		public override NamespaceManager Namespaces { get { return m; } }
		
		public override void WriteStatement(string subj, string pred, string obj) {
			writer.WriteLine("INSERT INTO {0}_statements VALUES ({1}, {2}, 0, {3}, 0);", table, ID(subj, 0), ID(pred, 1), ID(obj, 2)); 
		}
		
		public override void WriteStatement(string subj, string pred, Literal literal) {
			writer.WriteLine("INSERT INTO {0}_statements VALUES ({1}, {2}, 1, {3}, 0);", table, ID(subj, 0), ID(pred, 1), ID(literal)); 
		}
		
		public override string CreateAnonymousEntity() {
			int id = ++resourcecounter;
			string uri = "_anon:" + id;
			return uri;
		}
		
		public override void Close() {
			base.Close();
			foreach (string cmd in SQLStore.GetCreateIndexCommands(table))
				writer.WriteLine(cmd);
			writer.Close();
		}

		private string ID(Literal literal) {
			string id = (string)resources[literal];
			if (id == null) {
				id = (++resourcecounter).ToString();
				resources[literal] = id;
				writer.WriteLine("INSERT INTO {0}_literals VALUES ({1}, {2}, {3}, {4});", table, id, SQLStore.Escape(literal.Value), SQLStore.Escape(literal.Language), SQLStore.Escape(literal.DataType));
			}
			return id;
		}
		
		private string ID(string uri, int x) {
			if (uri.StartsWith("_anon:")) return uri.Substring(6);
			
			// Make this faster when a subject, predicate, or object is repeated.
			if (fastmap[0,0] != null && uri == fastmap[0, 0]) return fastmap[0, 1];
			if (fastmap[1,0] != null && uri == fastmap[1, 0]) return fastmap[1, 1];
			if (fastmap[2,0] != null && uri == fastmap[2, 0]) return fastmap[2, 1];
			
			string id;
			
			if (resources.ContainsKey(uri)) {
				id = (string)resources[uri];
			} else {
				id = (++resourcecounter).ToString();
				resources[uri] = id;
				
				string literalid = ID(new Literal(uri));
				writer.WriteLine("INSERT INTO {0}_statements VALUES ({1}, 0, 1, {2}, 0);", table, id, literalid);
			}
			
			fastmap[x, 0] = uri;
			fastmap[x, 1] = id;
			
			return id;
		}

	}
	
}
