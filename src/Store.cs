using System;
using System.Collections;
using System.Data;

namespace SemWeb {
	
	public interface StatementSink {
		bool Add(Statement statement);
	}

	public interface StatementSinkEx : StatementSink {
		Entity CreateAnonymousEntity();
		void Import(RdfParser parser);
	}
	
	internal class StatementCounterSink : StatementSink {
		int counter = 0;
		
		public int StatementCount { get { return counter; } }
		
		public bool Add(Statement statement) {
			counter++;
			return true;
		}
	}

	internal class StatementExistsSink : StatementSink {
		bool exists = false;
		
		public bool Exists { get { return exists; } }
		
		public bool Add(Statement statement) {
			exists = true;
			return false;
		}
	}

	internal class StatementFilterSink : StatementSinkEx {
		StatementSink sink;
		int counter = 0;
		
		public int StatementCount { get { return counter; } }
		
		public StatementFilterSink(StatementSink sink) { this.sink = sink; }
		
		public bool Add(Statement statement) {
			counter++;
			sink.Add(statement);
			return true;
		}
		
		public Entity CreateAnonymousEntity() {
			if (!(sink is StatementSinkEx)) throw new InvalidOperationException();
			return ((StatementSinkEx)sink).CreateAnonymousEntity();
		}
		
		public void Import(RdfParser parser) {
			if (!(sink is StatementSinkEx)) throw new InvalidOperationException();
			((StatementSinkEx)sink).Import(parser);
		}
	}
	
	public abstract class Store : StatementSinkEx {
		
		Entity rdfType;
		
		public static Store CreateForInput(string spec) {
			return (Store)Create(spec, false);
		}		
		
		public static StatementSinkEx CreateForOutput(string spec) {
			return (StatementSinkEx)Create(spec, true);
		}
		
		private static object Create(string spec, bool output) {
			string type = spec;
			
			int c = spec.IndexOf(':');
			if (c != -1) {
				type = spec.Substring(0, c);
				spec = spec.Substring(c+1);
			} else {
				spec = "";
			}
			
			switch (type) {
				case "mem":
					return new MemoryStore();
				case "xml":
					if (spec == "") throw new ArgumentException("Use: xml:filename");
					if (output) {
						return new SemWeb.IO.RdfXmlWriter(spec);
					} else {
						return new MemoryStore(new SemWeb.IO.RdfXmlParser(spec));
					}
				case "n3":
					if (spec == "") throw new ArgumentException("Use: n3:filename");
					if (output) {
						return new SemWeb.IO.N3Writer(spec);
					} else {
						return new MemoryStore(new SemWeb.IO.N3Parser(spec));
					}
				case "sql":
					if (spec == "") throw new ArgumentException("Use: sql:tablename");
					if (output)
						return new SemWeb.IO.SQLWriter(spec);
					else
						throw new InvalidOperationException("sql output does not support input.");
				case "sqlite":
				case "mysql":
					if (spec == "") throw new ArgumentException("Use: sqlite|mysql:table:connection-string");
				
					c = spec.IndexOf(':');
					if (c == -1) throw new ArgumentException("Invalid format for sqlite/mysql spec parameter (table:constring).");
					string table = spec.Substring(0, c);
					spec = spec.Substring(c+1);
					
					string classtype = null;
					if (type == "sqlite")
						classtype = "SemWeb.Stores.SqliteStore, SemWeb.SqliteStore";
					else if (type == "mysql")
						classtype = "SemWeb.Stores.MySQLStore, SemWeb.MySQLStore";
					Type ttype = Type.GetType(classtype);
					if (ttype == null)
						throw new NotSupportedException("The storage type in <" + classtype + "> could not be found.");
					return (Store)Activator.CreateInstance(ttype, new object[] { spec, table });
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		protected Store() {
			rdfType = new Entity("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
		}
		
		public abstract int StatementCount { get; }

		public abstract void Clear();
		
		public abstract Entity CreateAnonymousEntity();

		public Entity[] GetEntitiesOfType(Entity type) {
			ArrayList entities = new ArrayList();
			
			MemoryStore result = Select(new Statement(null, rdfType, type));
			foreach (Statement s in result.Statements) {
				entities.Add(s.Subject);
			}
			
			return (Entity[])entities.ToArray(typeof(Entity));
		}
		
		bool StatementSink.Add(Statement statement) {
			Add(statement);
			return true;
		}
		
		public abstract void Add(Statement statement);
		public abstract void Remove(Statement statement);

		public void Import(Store other) {
			other.Select(new Statement(null,null,null), this);
		}
		
		public virtual void Import(RdfParser parser) {
			parser.Parse(this);
		}
		
		public abstract Entity[] GetAllEntities();
		
		public abstract Entity[] GetAllPredicates();
		
		public virtual bool Contains(Statement statement) {
			StatementExistsSink sink = new StatementExistsSink();
			Select(statement, sink);
			return sink.Exists;
		}
		
		public void Select(Statement template, StatementSink result) {
			Select(template, SelectPartialFilter.All, result);
		}
		
		public void Select(Statement[] templates, StatementSink result) {
			Select(templates, SelectPartialFilter.All, result);
		}
		
		public abstract void Select(Statement template, SelectPartialFilter partialFilter, StatementSink result);
		
		public abstract void Select(Statement[] templates, SelectPartialFilter partialFilter, StatementSink result);
		
		public MemoryStore Select(Statement template) {
			return Select(template, SelectPartialFilter.All);
		}
		
		public MemoryStore Select(Statement template, SelectPartialFilter partialFilter) {
			MemoryStore ms = new MemoryStore();
			Select(template, partialFilter, ms);
			return ms;
		}
		
		public Resource[] SelectObjects(Entity subject, Entity predicate) {
			Hashtable resources = new Hashtable();
			foreach (Statement s in Select(new Statement(subject, predicate, null), new SelectPartialFilter(false, false, true, false)))
				if (!resources.ContainsKey(s.Object))
					resources[s.Object] = s.Object;
			return (Resource[])new ArrayList(resources.Keys).ToArray(typeof(Resource));
		}
		public Entity[] SelectSubjects(Entity predicate, Entity @object) {
			Hashtable resources = new Hashtable();
			foreach (Statement s in Select(new Statement(null, predicate, @object), new SelectPartialFilter(true, false, false, false)))
				if (!resources.ContainsKey(s.Subject))
					resources[s.Subject] = s.Subject;
			return (Entity[])new ArrayList(resources.Keys).ToArray(typeof(Entity));
		}
		
		public abstract void Replace(Entity a, Entity b);
		
		public void Write(RdfWriter writer) {
			Select(new Statement(null,null,null), writer);
		}
		
		public void Write(System.IO.TextWriter writer) {
			using (RdfWriter w = new SemWeb.IO.N3Writer(writer)) {
				Write(w);
			}
		}
		
		protected object GetResourceKey(Resource resource) {
			return resource.GetResourceKey(this);
		}

		protected void SetResourceKey(Resource resource, object value) {
			resource.SetResourceKey(this, value);
		}
		
	}
}

namespace SemWeb.Stores {

	public class MultiStore : Store {
		ArrayList stores = new ArrayList();
		
		public MultiStore() { }
		
		public void Add(Store store) {
			stores.Add(store);
		}
		
		public void Add(Store store, RdfParser source) {
			Add(store);
			store.Import(source);
		}
		
		public void Remove(Store store) {
			stores.Remove(store);
		}
		
		public override int StatementCount {
			get {
				int ret = 0;
				foreach (Store s in stores)
					ret += s.StatementCount;
				return ret;
			}
		}

		public override void Clear() {
			throw new InvalidOperationException("Clear is not a valid operation on a MultiStore.");
		}
		
		public override Entity[] GetAllEntities() {
			Hashtable h = new Hashtable();
			foreach (Store s in stores)
				foreach (Resource r in s.GetAllEntities())
					h[r] = h;
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}
		
		public override Entity[] GetAllPredicates() {
			Hashtable h = new Hashtable();
			foreach (Store s in stores)
				foreach (Resource r in s.GetAllPredicates())
					h[r] = h;
			return (Entity[])new ArrayList(h.Keys).ToArray(typeof(Entity));
		}

		public override Entity CreateAnonymousEntity() {
			throw new InvalidOperationException("CreateAnonymousResource is not a valid operation on a MultiStore.");
		}

		public override void Add(Statement statement) { throw new InvalidOperationException("Add is not a valid operation on a MultiStore."); }
		
		public override bool Contains(Statement statement) {
			foreach (Store s in stores)
				if (s.Contains(statement))
					return true;
			return false;
		}
			
		public override void Remove(Statement statement) { throw new InvalidOperationException("Add is not a valid operation on a MultiStore."); }
		
		public override void Select(Statement template, SelectPartialFilter partialFilter, StatementSink result) {
			foreach (Store s in stores)
				s.Select(template, partialFilter, result);
		}
		
		public override void Select(Statement[] templates, SelectPartialFilter partialFilter, StatementSink result) {
			foreach (Store s in stores)
				s.Select(templates, partialFilter, result);
		}

		public override void Replace(Entity a, Entity b) {
			foreach (Store s in stores)
				s.Replace(a, b);
		}		
	}
	
}
