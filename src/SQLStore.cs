using System;
using System.Collections;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Text;

using SemWeb.Util;

namespace SemWeb.Stores {
	// TODO: It's not safe to have two concurrent accesses to the same database
	// because the creation of new entities will use the same IDs.
	
	public abstract class SQLStore : Store, SupportsPersistableBNodes {
		string table;
		string guid;
		
		bool firstUse = true;
		IDictionary lockedIdCache = null;
		int cachedNextId = -1;
		
		Hashtable literalCache = new Hashtable();
		int literalCacheSize = 0;

		bool Debug = false;
		
		StringBuilder cmdBuffer = new StringBuilder();
		
		// Buffer statements to process together.
		StatementList addStatementBuffer = null;
		
		string 	INSERT_INTO_LITERALS_VALUES,
				INSERT_INTO_STATEMENTS_VALUES,
				INSERT_INTO_ENTITIES_VALUES;
		char quote;
				
		private class ResourceKey {
			public int ResId;
			
			public ResourceKey(int id) { ResId = id; }
			
			public override int GetHashCode() { return ResId.GetHashCode(); }
			public override bool Equals(object other) { return (other is ResourceKey) && ((ResourceKey)other).ResId == ResId; }
		}
		
		private static readonly string[] fourcols = new string[] { "subject", "predicate", "object", "meta" };
		private static readonly string[] predcol = new string[] { "predicate" };
		private static readonly string[] metacol = new string[] { "meta" };

		protected SQLStore(string table) {
			this.table = table;
			
			INSERT_INTO_LITERALS_VALUES = "INSERT INTO " + table + "_literals VALUES ";
			INSERT_INTO_ENTITIES_VALUES = "INSERT INTO " + table + "_entities VALUES ";
			INSERT_INTO_STATEMENTS_VALUES = "INSERT " + (SupportsInsertIgnore ? "IGNORE " : "") + "INTO " + table + "_statements VALUES ";
			
			quote = GetQuoteChar();
		}
		
		protected string TableName { get { return table; } }
		
		protected abstract bool SupportsNoDuplicates { get; }
		protected abstract bool SupportsInsertIgnore { get; }
		protected abstract bool SupportsInsertCombined { get; }
		protected virtual bool SupportsFastJoin { get { return true; } }
		
		protected abstract string CreateNullTest(string column);

		private void Init() {
			if (!firstUse) return;
			firstUse = false;
			
			CreateTable();
			CreateIndexes();
			CreateVersion();
		}
		
		private void CreateVersion() {	
			string verdatastr = RunScalarString("SELECT value FROM " + table + "_literals WHERE id = 0");
			NameValueCollection verdata = ParseVersionInfo(verdatastr);
			
			if (verdata["guid"] == null) {
				guid = Guid.NewGuid().ToString("N");
				verdata["guid"] = guid;
			} else {
				guid = verdata["guid"];
			}
			
			string newverdata = Escape(SerializeVersionInfo(verdata), true);
			if (verdatastr == null)
				RunCommand("INSERT INTO " + table + "_literals (id, value) VALUES (0, " + newverdata + ")");
			else
				RunCommand("UPDATE " + table + "_literals SET value = " + newverdata + " WHERE id = 0");
		}
		
		NameValueCollection ParseVersionInfo(string verdata) {
			NameValueCollection nvc = new NameValueCollection();
			if (verdata == null) return nvc;
			foreach (string s in verdata.Split('\n')) {
				int c = s.IndexOf(':');
				if (c == -1) continue;
				nvc[s.Substring(0, c)] = s.Substring(c+1);
			}
			return nvc;
		}
		string SerializeVersionInfo(NameValueCollection verdata) {
			string ret = "";
			foreach (string k in verdata.Keys)
				ret += k + ":" + verdata[k] + "\n";
			return ret;
		}
		
		public override bool Distinct { get { return true; } }
		
		public override int StatementCount { get { Init(); RunAddBuffer(); return RunScalarInt("select count(subject) from " + table + "_statements", 0); } }
		
		public string GetStoreGuid() { return guid; }
		
		public string GetNodeId(BNode node) {
			ResourceKey rk = (ResourceKey)GetResourceKey(node);
			if (rk == null) return null;
			return rk.ResId.ToString();
		}
		
		public BNode GetNodeFromId(string persistentId) {
			try {
				int id = int.Parse(persistentId);
				return (BNode)MakeEntity(id, null, null);
			} catch (Exception e) {
				return null;
			}
		}
		
		private int NextId() {
			if (lockedIdCache != null && cachedNextId != -1)
				return ++cachedNextId;
			
			RunAddBuffer();
			
			// The 0 id is not used.
			// The 1 id is reserved for Statement.DefaultMeta.
			int nextid = 2;
			
			CheckMax("select max(subject) from " + table + "_statements", ref nextid);
			CheckMax("select max(predicate) from " + table + "_statements", ref nextid);
			CheckMax("select max(object) from " + table + "_statements", ref nextid);
			CheckMax("select max(meta) from " + table + "_statements", ref nextid);
			CheckMax("select max(id) from " + table + "_literals", ref nextid);
			CheckMax("select max(id) from " + table + "_entities", ref nextid);
			
			cachedNextId = nextid;
			
			return nextid;
		}
		
		private void CheckMax(string command, ref int nextid) {
			int maxid = RunScalarInt(command, 0);
			if (maxid >= nextid) nextid = maxid + 1;
		}
		
		public override void Clear() {
			// Drop the tables, if they exist.
			try { RunCommand("DROP TABLE " + table + "_statements;"); } catch (Exception e) { }
			try { RunCommand("DROP TABLE " + table + "_literals;"); } catch (Exception e) { }
			try { RunCommand("DROP TABLE " + table + "_entities;"); } catch (Exception e) { }
			firstUse = true;
		
			Init();
			if (addStatementBuffer != null) addStatementBuffer.Clear();

			//RunCommand("DELETE FROM " + table + "_statements;");
			//RunCommand("DELETE FROM " + table + "_literals;");
			//RunCommand("DELETE FROM " + table + "_entities;");
		}
		
		private int GetLiteralId(Literal literal, bool create, bool cacheIsComplete, StringBuilder buffer, bool insertCombined) {
			// Returns the literal ID associated with the literal.  If a literal
			// doesn't exist and create is true, a new literal is created,
			// otherwise 0 is returned.
			
			if (literalCache.Count > 0) {
				object ret = literalCache[literal];
				if (ret != null) return (int)ret;
			}

			if (!cacheIsComplete) { 
				StringBuilder b = cmdBuffer; cmdBuffer.Length = 0;
				b.Append("SELECT id FROM ");
				b.Append(table);
				b.Append("_literals WHERE ");
				WhereLiteral(b, literal);
				b.Append(" LIMIT 1;");
				
				object id = RunScalar(b.ToString());
				if (id != null) return AsInt(id);
			}
				
			if (create) {
				int id = AddLiteral(literal.Value, literal.Language, literal.DataType, buffer, insertCombined);
				if (literal.Value.Length < 50) {
					literalCache[literal] = id;
					literalCacheSize += literal.Value.Length;
					CheckLiteralCacheSize();
				}
				return id;
			}
			
			return 0;
		}
		
		void CheckLiteralCacheSize() {
			if (literalCacheSize + 32*8*literalCache.Count > 10000000) {
				literalCacheSize = 0;
				literalCache.Clear();
			}
		}
		
		private void WhereLiteral(StringBuilder b, Literal literal) {
			b.Append("value = ");
			EscapedAppend(b, literal.Value);
			b.Append(" AND ");
			if (literal.Language != null) {
				b.Append("language = ");
				EscapedAppend(b, literal.Language);
			} else {
				b.Append(CreateNullTest("language"));
			}
			b.Append(" AND ");
			if (literal.DataType != null) {
				b.Append("datatype = ");
				EscapedAppend(b, literal.DataType);
			} else {
				b.Append(CreateNullTest("datatype"));
			}
		}
		
		private int AddLiteral(string value, string language, string datatype, StringBuilder buffer, bool insertCombined) {
			int id = NextId();
			
			StringBuilder b;
			if (buffer != null) {
				b = buffer;
			} else {
				b = cmdBuffer; cmdBuffer.Length = 0;
			}
			
			if (!insertCombined) {
				b.Append(INSERT_INTO_LITERALS_VALUES);
			} else {
				if (b.Length > 0)
					b.Append(",");
			}
			b.Append("(");
			b.Append(id);
			b.Append(",");
			EscapedAppend(b, value);
			b.Append(",");
			if (language != null)
				EscapedAppend(b, language);
			else
				b.Append("NULL");
			b.Append(",");
			if (datatype != null)
				EscapedAppend(b, datatype);
			else
				b.Append("NULL");
			b.Append(")");
			if (!insertCombined)
				b.Append(";");
			
			if (buffer == null)
				RunCommand(b.ToString());
			
			return id;
		}
		
		private int GetEntityId(string uri, bool create, StringBuilder entityInsertBuffer, bool insertCombined) {
			// Returns the resource ID associated with the URI.  If a resource
			// doesn't exist and create is true, a new resource is created,
			// otherwise 0 is returned.
			
			int id;	
			
			if (lockedIdCache != null) {
				object idobj = lockedIdCache[uri];
				if (idobj == null && !create) return 0;
				if (idobj != null) return (int)idobj;
			} else {
				StringBuilder cmd = cmdBuffer; cmdBuffer.Length = 0;
				cmd.Append("SELECT id FROM ");
				cmd.Append(table);
				cmd.Append("_entities WHERE value =");
				EscapedAppend(cmd, uri);
				cmd.Append(" LIMIT 1;");
				id = RunScalarInt(cmd.ToString(), 0);
				if (id != 0 || !create) return id;
			}
			
			// If we got here, no such resource exists and create is true.
			
			if (uri.Length > 255)
				throw new NotSupportedException("URIs must be a maximum of 255 characters for this store due to indexing constraints (before MySQL 4.1.2).");

			id = NextId();
			
			StringBuilder b;
			if (entityInsertBuffer != null) {
				b = entityInsertBuffer;
			} else {
				b = cmdBuffer; cmdBuffer.Length = 0;
			}
			
			if (!insertCombined) {
				b.Append(INSERT_INTO_ENTITIES_VALUES);
			} else {
				if (b.Length > 0)
					b.Append(",");
			}
			b.Append("(");
			b.Append(id);
			b.Append(",");
			EscapedAppend(b, uri);
			b.Append(")");
			if (!insertCombined)
				b.Append(";");
			
			if (entityInsertBuffer == null)
				RunCommand(b.ToString());
				
			// Add it to the URI map
					
			if (lockedIdCache != null)
				lockedIdCache[uri] = id;
			
			return id;
		}
		
		private int GetResourceId(Resource resource, bool create) {
			return GetResourceIdBuffer(resource, create, false, null, null, false);
		}
		
		private int GetResourceIdBuffer(Resource resource, bool create, bool literalCacheComplete, StringBuilder literalInsertBuffer, StringBuilder entityInsertBuffer, bool insertCombined) {
			if (resource == null) return 0;
			
			if (resource is Literal) {
				Literal lit = (Literal)resource;
				return GetLiteralId(lit, create, literalCacheComplete, literalInsertBuffer, insertCombined);
			}
			
			if (object.ReferenceEquals(resource, Statement.DefaultMeta))
				return 1;
			
			ResourceKey key = (ResourceKey)GetResourceKey(resource);
			if (key != null) return key.ResId;
			
			int id;
			
			if (resource.Uri != null) {
				id = GetEntityId(resource.Uri, create, entityInsertBuffer, insertCombined);
			} else {
				// This anonymous node didn't come from the database
				// since it didn't have a resource key.  If !create,
				// then just return 0 to signal the resource doesn't exist.
				if (!create) return 0;

				if (lockedIdCache != null) {
					// Can just increment the counter.
					id = NextId();
				} else {
					// We need to reserve an id for this resource so that
					// this function returns other ids for other anonymous
					// resources.  Don't know how to do this yet, so
					// just throw an exception.
					throw new NotImplementedException("Anonymous nodes cannot be added to this store outside of an Import operation.");
				}
			}
				
			if (id != 0)
				SetResourceKey(resource, new ResourceKey(id));
			return id;
		}

		private int ObjectType(Resource r) {
			if (r is Literal) return 1;
			return 0;
		}
		
		private Entity MakeEntity(int resourceId, string uri, Hashtable cache) {
			if (resourceId == 0)
				return null;
			if (resourceId == 1)
				return Statement.DefaultMeta;
			
			ResourceKey rk = new ResourceKey(resourceId);
			
			if (cache != null && cache.ContainsKey(rk))
				return (Entity)cache[rk];
			
			Entity ent;
			if (uri != null) {
				ent = new Entity(uri);
			} else {
				ent = new BNode();
			}
			
			SetResourceKey(ent, rk);
			
			if (cache != null)
				cache[rk] = ent;
				
			return ent;
		}
		
		public override void Add(Statement statement) {
			if (statement.AnyNull) throw new ArgumentNullException();
			
			if (addStatementBuffer != null) {
				addStatementBuffer.Add(statement);
				if (addStatementBuffer.Count >= 400)
					RunAddBuffer();
				return;
			}
			
			Init();
			
			int subj = GetResourceId(statement.Subject, true);
			int pred = GetResourceId(statement.Predicate, true);
			int objtype = ObjectType(statement.Object);
			int obj = GetResourceId(statement.Object, true);
			int meta = GetResourceId(statement.Meta, true);
			
			StringBuilder addBuffer = cmdBuffer; addBuffer.Length = 0;
			
			addBuffer.Append(INSERT_INTO_STATEMENTS_VALUES);
			addBuffer.Append("(");

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
			
			RunCommand(addBuffer.ToString());
		}
		
		private void RunAddBuffer() {
			if (addStatementBuffer == null || addStatementBuffer.Count == 0) return;
			
			bool insertCombined = SupportsInsertCombined;
			
			Init();
			
			// Prevent recursion through NextId=>StatementCount
			StatementList statements = addStatementBuffer;
			addStatementBuffer = null;
			
			// Prefetch the IDs of all literals that aren't
			// in the literal map.
			StringBuilder cmd = new StringBuilder();
			cmd.Append("SELECT id, value, language, datatype FROM ");
			cmd.Append(table);
			cmd.Append("_literals WHERE ");
			bool hasLiterals = false;
			Hashtable litseen = new Hashtable();
			foreach (Statement s in statements) {
				Literal lit = s.Object as Literal;
				if (lit == null) continue;
				if (litseen.ContainsKey(lit)) continue;
				if (literalCache.ContainsKey(lit)) continue;
				
				if (hasLiterals)
					cmd.Append(" or ");
				cmd.Append("(");
				WhereLiteral(cmd, lit);
				cmd.Append(")");
				hasLiterals = true;
				litseen[lit] = litseen;
			}
			if (hasLiterals) {
				cmd.Append(";");
				using (IDataReader reader = RunReader(cmd.ToString())) {
					while (reader.Read()) {
						//int literalid = AsInt(reader[0]);	
						int literalid = reader.GetInt32(0);
						string val = AsString(reader[1]);
						string lang = AsString(reader[2]);
						string dt = AsString(reader[3]);

						Literal lit = new Literal(val, lang, dt);
						
						literalCache[lit] = literalid;
						literalCacheSize += val.Length;
					}
				}
			}
			
			StringBuilder entityInsertions = new StringBuilder();
			StringBuilder literalInsertions = new StringBuilder();
			
			cmd = new StringBuilder();
			if (insertCombined)
				cmd.Append(INSERT_INTO_STATEMENTS_VALUES);

			for (int i = 0; i < statements.Count; i++) {
				Statement statement = (Statement)statements[i];
			
				int subj = GetResourceIdBuffer(statement.Subject, true, true, literalInsertions, entityInsertions, insertCombined);
				int pred = GetResourceIdBuffer(statement.Predicate, true,  true, literalInsertions, entityInsertions, insertCombined);
				int objtype = ObjectType(statement.Object);
				int obj = GetResourceIdBuffer(statement.Object, true, true, literalInsertions, entityInsertions, insertCombined);
				int meta = GetResourceIdBuffer(statement.Meta, true, true, literalInsertions, entityInsertions, insertCombined);
				
				if (!insertCombined)
					cmd.Append(INSERT_INTO_STATEMENTS_VALUES);
				
				cmd.Append("(");
				cmd.Append(subj);
				cmd.Append(", ");
				cmd.Append(pred);
				cmd.Append(", ");
				cmd.Append(objtype);
				cmd.Append(", ");
				cmd.Append(obj);
				cmd.Append(", ");
				cmd.Append(meta);
				if (i == statements.Count-1 || !insertCombined)
					cmd.Append(");");
				else
					cmd.Append("),");
			}
			
			if (literalInsertions.Length > 0) {
				if (insertCombined) {
					literalInsertions.Insert(0, INSERT_INTO_LITERALS_VALUES);
					literalInsertions.Append(';');
				}
				RunCommand(literalInsertions.ToString());
			}
			
			if (entityInsertions.Length > 0) {
				if (insertCombined) {
					entityInsertions.Insert(0, INSERT_INTO_ENTITIES_VALUES);
					entityInsertions.Append(';');
				}
				RunCommand(entityInsertions.ToString());
			}
			
			RunCommand(cmd.ToString());
			
			// Clear the array and reuse it.
			statements.Clear();
			addStatementBuffer = statements;
			CheckLiteralCacheSize();
		}
		
		public override void Remove(Statement template) {
			Init();
			RunAddBuffer();

			System.Text.StringBuilder cmd = new System.Text.StringBuilder("DELETE FROM ");
			cmd.Append(table);
			cmd.Append("_statements ");
			if (!WhereClause(template, cmd)) return;
			cmd.Append(";");
			
			RunCommand(cmd.ToString());
		}
		
		public override Entity[] GetEntities() {
			return GetAllEntities(fourcols);
		}
			
		public override Entity[] GetPredicates() {
			return GetAllEntities(predcol);
		}
		
		public override Entity[] GetMetas() {
			return GetAllEntities(metacol);
		}

		private Entity[] GetAllEntities(string[] cols) {
			RunAddBuffer();
			ArrayList ret = new ArrayList();
			Hashtable seen = new Hashtable();
			foreach (string col in cols) {
				using (IDataReader reader = RunReader("SELECT " + col + ", value FROM " + table + "_statements LEFT JOIN " + table + "_entities ON " + col + "=id " + (col == "object" ? " WHERE objecttype=0" : "") + " GROUP BY " + col + ";")) {
					while (reader.Read()) {
						int id = reader.GetInt32(0);
						if (id == 1 && col == "meta") continue; // don't return DefaultMeta in meta column.
						
						if (seen.ContainsKey(id)) continue;
						seen[id] = seen;
						
						string uri = AsString(reader[1]);
						ret.Add(MakeEntity(id, uri, null));
					}
				}
			}
			return (Entity[])ret.ToArray(typeof(Entity));;
		}
		
		private bool WhereItem(string col, Resource r, System.Text.StringBuilder cmd, bool and) {
			if (and) cmd.Append(" and ");
			
			if (col.EndsWith("object")) {
				if (r is MultiRes) {
					// Assumption that ID space of literals and entities are the same.
					cmd.Append("(");
					cmd.Append(col);
					cmd.Append(" IN (");
					if (!AppendMultiRes((MultiRes)r, cmd)) return false;
					cmd.Append(" ))");
				} else if (r is Literal) {
					Literal lit = (Literal)r;
					int id = GetResourceId(lit, false);
					if (id == 0) return false;
					cmd.Append(" (");
					cmd.Append(col);
					cmd.Append(" = ");
					cmd.Append(id);
					cmd.Append(")");
				} else {
					int id = GetResourceId(r, false);
					if (id == 0) return false;
					cmd.Append(" (");
					cmd.Append(col);
					cmd.Append(" = ");
					cmd.Append(id);
					cmd.Append(")");
				}
			
			} else if (r is MultiRes) {
				cmd.Append("( ");
				cmd.Append(col);
				cmd.Append(" IN (");
				if (!AppendMultiRes((MultiRes)r, cmd)) return false;
				cmd.Append(" ))");
				
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
		
		private bool AppendMultiRes(MultiRes r, StringBuilder cmd) {
			for (int i = 0; i < r.items.Length; i++) {
				if (i != 0) cmd.Append(",");
				int id = GetResourceId(r.items[i], false);
				if (id == 0) return false;
				cmd.Append(id);
			}
			return true;
		}
		
		private bool WhereClause(Statement template, System.Text.StringBuilder cmd) {
			return WhereClause(template.Subject, template.Predicate, template.Object, template.Meta, cmd);
		}

		private bool WhereClause(Resource templateSubject, Resource templatePredicate, Resource templateObject, Resource templateMeta, System.Text.StringBuilder cmd) {
			if (templateSubject == null && templatePredicate == null && templateObject == null && templateMeta == null)
				return true;
			
			cmd.Append(" WHERE ");
			
			if (templateSubject != null)
				if (!WhereItem("subject", templateSubject, cmd, false)) return false;
			
			if (templatePredicate != null)
				if (!WhereItem("predicate", templatePredicate, cmd, templateSubject != null)) return false;
			
			if (templateObject != null)
				if (!WhereItem("object", templateObject, cmd, templateSubject != null || templatePredicate != null)) return false;
			
			if (templateMeta != null)
				if (!WhereItem("meta", templateMeta, cmd, templateSubject != null || templatePredicate != null || templateObject != null)) return false;
			
			return true;
		}
		
		private int AsInt(object r) {
			if (r is int) return (int)r;
			if (r is uint) return (int)(uint)r;
			if (r is long) return (int)(long)r;
			if (r is string) return int.Parse((string)r);
			throw new ArgumentException(r.ToString());
		}
		
		private string AsString(object r) {
			if (r == null)
				return null;
			else if (r is System.DBNull)
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
			
			if (partialFilter.Subject) { cmd.Append("q.subject, suri.value"); f = false; }
			if (partialFilter.Predicate) { AppendComma(cmd, "q.predicate, puri.value", !f); f = false; }
			if (partialFilter.Object) { AppendComma(cmd, "q.objecttype, q.object, ouri.value", !f); f = false; }
			if (partialFilter.Meta) { AppendComma(cmd, "q.meta, muri.value", !f); f = false; }
		}
		
		public override void Select(SelectFilter filter, StatementSink result) {
			if (result == null) throw new ArgumentNullException();
			foreach (Entity[] s in SplitArray(filter.Subjects))
			foreach (Entity[] p in SplitArray(filter.Predicates))
			foreach (Resource[] o in SplitArray(filter.Objects))
			foreach (Entity[] m in SplitArray(filter.Metas))
			{
				Select(
					ToMultiRes(s),
					ToMultiRes(p),
					ToMultiRes(o),
					ToMultiRes(m),
					result,
					filter.Limit); // hmm, repeated
			}
		}
		
		Resource[][] SplitArray(Resource[] e) {
			int lim = 500;
			if (e == null || e.Length <= lim) {
				if (e is Entity[]) return new Entity[][] { (Entity[])e }; else return new Resource[][] { e };
			}
			int overflow = e.Length % lim;
			int n = (e.Length / lim) + ((overflow != 0) ? 1 : 0);
			Resource[][] ret;
			if (e is Entity[]) ret = new Entity[n][]; else ret = new Resource[n][];
			for (int i = 0; i < n; i++) {
				int c = lim;
				if (i == n-1 && overflow != 0) c = overflow;
				if (e is Entity[]) ret[i] = new Entity[c]; else ret[i] = new Resource[c];
				Array.Copy(e, i*lim, ret[i], 0, c);
			}
			return ret;
		}
		
		Resource ToMultiRes(Resource[] r) {
			if (r == null || r.Length == 0) return null;
			if (r.Length == 1) return r[0];
			return new MultiRes(r);
		}
		
		private class MultiRes : Resource {
			public MultiRes(Resource[] a) { items = a; }
			public Resource[] items;
			public override string Uri { get { return null; } }
		}
		
		public override void Select(Statement template, StatementSink result) {
			if (result == null) throw new ArgumentNullException();
			Select(template.Subject, template.Predicate, template.Object, template.Meta, result, 0);
		}

		private void Select(Resource templateSubject, Resource templatePredicate, Resource templateObject, Resource templateMeta, StatementSink result, int limit) {
			if (result == null) throw new ArgumentNullException();
	
			Init();
			RunAddBuffer();
			
			// Don't select on columns that we already know from the template
			SelectPartialFilter partialFilter = new SelectPartialFilter(
				(templateSubject == null) || templateSubject is MultiRes,
				(templatePredicate == null) || templatePredicate is MultiRes,
				(templateObject == null) || templateObject is MultiRes,
				(templateMeta == null) || templateMeta is MultiRes
				);
			
			if (partialFilter.SelectNone) // have to select something
				partialFilter = new SelectPartialFilter(true, false, false, false);
				
			// SQLite has a problem with LEFT JOIN: When a condition is made on the
			// first table in the ON clause (q.objecttype=0/1), when it fails,
			// it excludes the row from the first table, whereas it should only
			// exclude the results of the join.
						
			System.Text.StringBuilder cmd = new System.Text.StringBuilder("SELECT ");
			if (!SupportsNoDuplicates)
				cmd.Append("DISTINCT ");
			SelectFilter(partialFilter, cmd);
			if (partialFilter.Object)
				cmd.Append(", lit.value, lit.language, lit.datatype");
			cmd.Append(" FROM ");
			cmd.Append(table);
			cmd.Append("_statements AS q");
			
			if (partialFilter.Object) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(table);
				cmd.Append("_literals AS lit ON q.object=lit.id");
			}
			if (partialFilter.Subject) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(table);
				cmd.Append("_entities AS suri ON q.subject = suri.id");
			}
			if (partialFilter.Predicate) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(table);
				cmd.Append("_entities AS puri ON q.predicate = puri.id");
			}
			if (partialFilter.Object) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(table);
				cmd.Append("_entities AS ouri ON q.object = ouri.id");
			}
			if (partialFilter.Meta) {
				cmd.Append(" LEFT JOIN ");
				cmd.Append(table);
				cmd.Append("_entities AS muri ON q.meta = muri.id");
			}
			cmd.Append(' ');
			if (!WhereClause(templateSubject, templatePredicate, templateObject, templateMeta, cmd)) return;
			cmd.Append(";");
			
			if (limit >= 1) {
				cmd.Append(" LIMIT ");
				cmd.Append(limit);
			}
			
			if (Debug || false) {
				string cmd2 = cmd.ToString();
				//if (cmd2.Length > 80) cmd2 = cmd2.Substring(0, 80);
				Console.Error.WriteLine(cmd2);
			}
			
			Hashtable entMap = new Hashtable();
			
			using (IDataReader reader = RunReader(cmd.ToString())) {
				while (reader.Read()) {
					int col = 0;
					int sid = -1, pid = -1, ot = -1, oid = -1, mid = -1;
					string suri = null, puri = null, ouri = null, muri = null;
					
					if (partialFilter.Subject) { sid = reader.GetInt32(col++); suri = AsString(reader[col++]); }
					if (partialFilter.Predicate) { pid = reader.GetInt32(col++); puri = AsString(reader[col++]); }
					if (partialFilter.Object) { ot = reader.GetInt32(col++); oid = reader.GetInt32(col++); ouri = AsString(reader[col++]); }
					if (partialFilter.Meta) { mid = reader.GetInt32(col++); muri = AsString(reader[col++]); }
					
					string lv = null, ll = null, ld = null;
					if (ot == 1 && partialFilter.Object) {
						lv = AsString(reader[col++]);
						ll = AsString(reader[col++]);
						ld = AsString(reader[col++]);
					}
					
					bool ret = result.Add(new Statement(
						!partialFilter.Subject ? (Entity)templateSubject : MakeEntity(sid, suri, entMap),
						!partialFilter.Predicate ? (Entity)templatePredicate : MakeEntity(pid, puri, entMap),
						!partialFilter.Object ? templateObject : 
							(ot == 0 ? (Resource)MakeEntity(oid, ouri, entMap)
								     : (Resource)new Literal(lv, ll, ld)),
						(!partialFilter.Meta || mid == 0) ? (Entity)templateMeta : MakeEntity(mid, muri, entMap)
						));
					if (!ret) break;

				}
			}
		}
		
		private string Escape(string str, bool quotes) {
			if (str == null) return "NULL";
			StringBuilder b = new StringBuilder();
			EscapedAppend(b, str, quotes);
			return b.ToString();
		}
		
		protected void EscapedAppend(StringBuilder b, string str) {
			EscapedAppend(b, str, true);
		}

		protected virtual char GetQuoteChar() {
			return '\"';
		}
		protected virtual void EscapedAppend(StringBuilder b, string str, bool quotes) {
			if (quotes) b.Append(quote);
			for (int i = 0; i < str.Length; i++) {
				char c = str[i];
				switch (c) {
					case '\n': b.Append("\\n"); break;
					case '\\':
					case '\"':
					case '*':
						b.Append('\\');
						b.Append(c);
						break;
					default:
						b.Append(c);
						break;
				}
			}
			if (quotes) b.Append(quote);
		}
		
		internal static void Escape(StringBuilder b) {
			b.Replace("\\", "\\\\");
			b.Replace("\"", "\\\"");
			b.Replace("\n", "\\n");
			b.Replace("%", "\\%");
			b.Replace("*", "\\*");
		}

		public override void Import(StatementSource source) {
			if (source == null) throw new ArgumentNullException();
			if (lockedIdCache != null) throw new InvalidOperationException("Store is already importing.");
			
			Init();
			RunAddBuffer();
			
			cachedNextId = -1;
			lockedIdCache = new UriMap();
			addStatementBuffer = new StatementList();
			
			using (IDataReader reader = RunReader("SELECT id, value from " + table + "_entities;")) {
				while (reader.Read())
					lockedIdCache[AsString(reader[1])] = reader.GetInt32(0);
			}
			
			BeginTransaction();
			
			try {
				base.Import(source);
			} finally {
				RunAddBuffer();
				EndTransaction();
				
				lockedIdCache = null;
				addStatementBuffer = null;
				
				literalCache.Clear();
				literalCacheSize = 0;			
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
				cmd.Append(";");
				RunCommand(cmd.ToString());
			}			
		}
		
		public override void Replace(Statement find, Statement replacement) {
			if (find.AnyNull) throw new ArgumentNullException("find");
			if (replacement.AnyNull) throw new ArgumentNullException("replacement");
			if (find == replacement) return;
			
			Init();
			RunAddBuffer();

			int subj = GetResourceId(replacement.Subject, true);
			int pred = GetResourceId(replacement.Predicate, true);
			int objtype = ObjectType(replacement.Object);
			int obj = GetResourceId(replacement.Object, true);
			int meta = GetResourceId(replacement.Meta, true);

			StringBuilder cmd = cmdBuffer; cmd.Length = 0;
			
			cmd.Append("UPDATE ");
			cmd.Append(table);
			cmd.Append("_statements SET subject=");
			cmd.Append(subj);
			cmd.Append(", predicate=");
			cmd.Append(pred);
			cmd.Append(", objecttype=");
			cmd.Append(objtype);
			cmd.Append(", object=");
			cmd.Append(obj);
			cmd.Append(", meta=");
			cmd.Append(meta);
			cmd.Append(" ");
			
			if (!WhereClause(find, cmd))
				return;
			
			RunCommand(cmd.ToString());
		}
		
		public override Entity[] FindEntities(Statement[] filters) {
			if (filters.Length == 0) return new Entity[0];
		
			if (!SupportsFastJoin)
				return base.FindEntities(filters);
		
			Init();
			
			string f1pos = is_spom(filters[0]);
			if (f1pos == null) throw new ArgumentException("Null must appear in every statement.");
			
			StringBuilder cmd = new StringBuilder();
			cmd.Append("SELECT s.");
			cmd.Append(f1pos);
			cmd.Append(", uri.value FROM ");
			cmd.Append(table);
			cmd.Append("_statements AS s LEFT JOIN ");
			cmd.Append(table);
			cmd.Append("_entities AS uri ON uri.id=s.");
			cmd.Append(f1pos);
			
			if (isliteralmatch(filters[0].Object))
				appendLiteralMatch(cmd, "l0", "s", ((Literal)filters[0].Object).Value);
			
			for (int i = 1; i < filters.Length; i++) {
				cmd.Append(" INNER JOIN ");
				cmd.Append(table);
				cmd.Append("_statements AS f");
				cmd.Append(i);
				cmd.Append(" ON s.");
				cmd.Append(f1pos);
				cmd.Append("=f");
				cmd.Append(i);
				cmd.Append(".");
				string fipos = is_spom(filters[i]);
				if (fipos == null) throw new ArgumentException("Null must appear in every statement.");
				cmd.Append(fipos);
				
				if (filters[i].Subject != null)
					if (!WhereItem("f" + i + ".subject", filters[i].Subject, cmd, true)) return new Entity[0];
				if (filters[i].Predicate != null)
					if (!WhereItem("f" + i + ".predicate", filters[i].Predicate, cmd, true)) return new Entity[0];
				if (filters[i].Object != null && !isliteralmatch(filters[i].Object))
					if (!WhereItem("f" + i + ".object", filters[i].Object, cmd, true)) return new Entity[0];
				if (filters[i].Meta != null)
					if (!WhereItem("f" + i + ".meta", filters[i].Meta, cmd, true)) return new Entity[0];
				
				if (filters[i].Object == null)
					cmd.Append("AND f" + i + ".objecttype=0 ");
					
				if (isliteralmatch(filters[i].Object)) {
					cmd.Append("AND f" + i + ".objecttype=1 ");
					appendLiteralMatch(cmd, "l" + i, "f" + i, ((Literal)filters[i].Object).Value);
				}
			}
			
			cmd.Append(" WHERE 1 ");
			
			if (filters[0].Subject != null)
				if (!WhereItem("s.subject", filters[0].Subject, cmd, true)) return new Entity[0];
			if (filters[0].Predicate != null)
				if (!WhereItem("s.predicate", filters[0].Predicate, cmd, true)) return new Entity[0];
			if (filters[0].Object != null && !isliteralmatch(filters[0].Object))
				if (!WhereItem("s.object", filters[0].Object, cmd, true)) return new Entity[0];
			if (isliteralmatch(filters[0].Object))
				cmd.Append("AND s.objecttype=1 ");
			if (filters[0].Meta != null)
				if (!WhereItem("s.meta", filters[0].Meta, cmd, true)) return new Entity[0];
			
			if (filters[0].Object == null)
				cmd.Append(" AND s.objecttype=0");
				
			cmd.Append(";");
			
			//Console.Error.WriteLine(cmd.ToString());
			
			ArrayList entities = new ArrayList();
			Hashtable seen = new Hashtable();

			using (IDataReader reader = RunReader(cmd.ToString())) {
				while (reader.Read()) {
					int id = reader.GetInt32(0);
					string uri = AsString(reader[1]);
					if (seen.ContainsKey(id)) continue;
					seen[id] = seen;
 					entities.Add(MakeEntity(id, uri, null));
 				}
			}
			
			return (Entity[])entities.ToArray(typeof(Entity));
		}
		
		private string is_spom(Statement s) {
			if (s.Subject == null) return "subject";
			if (s.Predicate == null) return "predicate";
			if (s.Object == null) return "object";
			if (s.Meta == null) return "meta";
			return null;
		}
		
		private bool isliteralmatch(Resource r) {
			if (r == null || !(r is Literal)) return false;
			return ((Literal)r).DataType == "SEMWEB::LITERAL::CONTAINS";
		}
		
		private void appendLiteralMatch(StringBuilder cmd, string joinalias, string lefttable, string pattern) {
			cmd.Append(" INNER JOIN ");
			cmd.Append(table);
			cmd.Append("_literals AS ");
			cmd.Append(joinalias);
			cmd.Append(" ON ");
			cmd.Append(joinalias);
			cmd.Append(".id=");
			cmd.Append(lefttable);
			cmd.Append(".object");
			cmd.Append(" AND ");
			cmd.Append(joinalias);
			cmd.Append(".value LIKE \"%");
			cmd.Append(Escape(pattern.Replace("%", "\\%"), false));
			cmd.Append("%\" ");
		}
		
		protected abstract void RunCommand(string sql);
		protected abstract object RunScalar(string sql);
		protected abstract IDataReader RunReader(string sql);
		
		private int RunScalarInt(string sql, int def) {
			object ret = RunScalar(sql);
			if (ret == null) return def;
			if (ret is int) return (int)ret;
			try {
				return int.Parse(ret.ToString());
			} catch (FormatException e) {
				return def;
			}
		}
		
		private string RunScalarString(string sql) {
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
					if (Debug) Console.Error.WriteLine(e);
				}
			}
		}
		
		protected virtual void CreateIndexes() {
			foreach (string cmd in GetCreateIndexCommands(table)) {
				try {
					RunCommand(cmd);
				} catch (Exception e) {
					if (Debug) Console.Error.WriteLine(e);
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
				"(id INT NOT NULL, value BLOB NOT NULL, language TEXT, datatype TEXT, PRIMARY KEY(id));",
				
				"CREATE TABLE " + table + "_entities" +
				"(id INT NOT NULL, value BLOB NOT NULL, PRIMARY KEY(id));"
				};
		}
		
		internal static string[] GetCreateIndexCommands(string table) {
			return new string[] {
				"CREATE UNIQUE INDEX subject_full_index ON " + table + "_statements(subject, predicate, object, meta);",
				"CREATE INDEX predicate_index ON " + table + "_statements(predicate);",
				"CREATE INDEX object_index ON " + table + "_statements(object);",
				"CREATE INDEX meta_index ON " + table + "_statements(meta);",
			
				"CREATE INDEX literal_index ON " + table + "_literals(value(30));",
				"CREATE UNIQUE INDEX entity_index ON " + table + "_entities(value(255));"
				};
		}
	}
	
}

namespace SemWeb.IO {
	using SemWeb;
	using SemWeb.Stores;
	
	// NEEDS TO BE UPDATED
	/*class SQLWriter : RdfWriter {
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
				writer.WriteLine("INSERT INTO {0}_literals VALUES ({1}, {2}, {3}, {4});", table, id, Escape(literal.Value), Escape(literal.Language), Escape(literal.DataType));
			}
			return id;
		}
		
		private string Escape(string str) {
			if (str == null) return "NULL";
			return "\"" + EscapeUnquoted(str) + "\"";
		}
		
		StringBuilder EscapeUnquotedBuffer = new StringBuilder();
		private string EscapeUnquoted(string str) {
			StringBuilder b = EscapeUnquotedBuffer;
			b.Length = 0;
			b.Append(str);
			SQLStore.Escape(b);
			return b.ToString();
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

	}*/
	
}
