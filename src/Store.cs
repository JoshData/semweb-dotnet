using System;
using System.Collections;
using System.Data;

namespace SemWeb {
	
	public interface StatementSink {
		bool Add(Statement statement);
	}

	public class StatementCounterSink : StatementSink {
		int counter = 0;
		
		public int StatementCount { get { return counter; } }
		
		public bool Add(Statement statement) {
			counter++;
			return true;
		}
	}

	public class StatementExistsSink : StatementSink {
		bool exists = false;
		
		public bool Exists { get { return exists; } }
		
		public bool Add(Statement statement) {
			exists = true;
			return false;
		}
	}

	internal class StatementFilterSink : StatementSink {
		StatementSink sink;
		
		public StatementFilterSink(StatementSink sink) { this.sink = sink; }
		
		public bool Add(Statement statement) {
			sink.Add(statement);
			return true;
		}
	}
	
	public abstract class Store : StatementSink {
		
		KnowledgeModel model;

		Entity rdfType;
		
		public static Store CreateForInput(string spec, KnowledgeModel model) {
			return Create(spec, model, false);
		}		
		
		public static Store CreateForOutput(string spec, KnowledgeModel model) {
			return Create(spec, model, true);
		}
		
		private static Store Create(string spec, KnowledgeModel model, bool output) {
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
					return new MemoryStore(model);
				case "xml":
					if (spec == "") throw new ArgumentException("Use: xml:filename");
					if (output) {
						return new WriterStore(new RdfXmlWriter(spec), model);
					} else {
						return new MemoryStore(new RdfXmlParser(spec), model);
					}
				case "n3":
					if (spec == "") throw new ArgumentException("Use: n3:filename");
					if (output) {
						return new WriterStore(new N3Writer(spec), model);
					} else {
						return new MemoryStore(new N3Parser(spec), model);
					}
				case "sql":
					if (spec == "") throw new ArgumentException("Use: sql:tablename");
					if (output)
						return new WriterStore(new SQLWriter(spec), model);
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
					return (Store)Activator.CreateInstance(ttype, new object[] { spec, table, model });
				default:
					throw new ArgumentException("Unknown parser type: " + type);
			}
		}
		
		public Store(KnowledgeModel model) {
			this.model = model;
			rdfType = new Entity("http://www.w3.org/1999/02/22-rdf-syntax-ns#type", model);
		}
		
		public virtual KnowledgeModel Model { get { return model; } }
		
		public abstract int StatementCount { get; }

		public abstract void Clear();
		
		public abstract Entity GetResource(string uri, bool create);
		public abstract Entity CreateAnonymousResource();

		public Entity GetResource(string uri) {
			return GetResource(uri, true);
		}
		
		public Entity[] GetEntitiesOfType(string type) {
			return GetEntitiesOfType(new Entity(type, Model));
		}
		
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
		
		public virtual bool Contains(Statement statement) {
			StatementExistsSink sink = new StatementExistsSink();
			Select(statement, sink);
			return sink.Exists;
		}
		
		public abstract void Select(Statement template, StatementSink result);
		
		public virtual void Select(Statement[] templates, StatementSink result) {
			foreach (Statement template in templates)
				Select(template, result);
		}
		
		public MemoryStore Select(Statement template) {
			MemoryStore ms = new MemoryStore(Model);
			Select(template, ms);
			return ms;
		}
		
		public Resource[] SelectObjects(Entity subject, Entity predicate) {
			ArrayList resources = new ArrayList();
			foreach (Statement s in Select(new Statement(subject, predicate, null)).Statements)
				resources.Add(s.Object);
			return (Resource[])resources.ToArray(typeof(Resource));
		}
		public Entity[] SelectSubjects(Entity predicate, Entity @object) {
			ArrayList resources = new ArrayList();
			foreach (Statement s in Select(new Statement(null, predicate, @object)).Statements)
				resources.Add(s.Subject);
			return (Entity[])resources.ToArray(typeof(Entity));
		}
		
		public void Write(RdfWriter writer) {
			Select(new Statement(null,null,null), writer);
		}
		
		public void Write(System.IO.TextWriter writer) {
			Write(writer, new NamespaceManager());
		}
		public void Write(System.IO.TextWriter writer, NamespaceManager ns) {
			using (RdfWriter w = new N3Writer(writer, ns)) {
				Write(w);
			}
		}

	}

	public class MultiStore : Store {
		ArrayList stores = new ArrayList();
		
		public MultiStore(KnowledgeModel model) : base(model) { }
		
		public void Add(Store store) {
			if (store.Model != Model)
				throw new ArgumentException("The store must be in the same KnowledgeModel as this MultiStore.");
				
			stores.Add(store);
		}
		
		public void Add(Store store, RdfParser source) {
			stores.Add(store);
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
		
		public override Entity GetResource(string uri, bool create) {
			foreach (Store s in stores) {
				Entity r = s.GetResource(uri, false);
				if (r != null) return r;
			}
			if (!create) return null;
			return new Entity(uri, Model);
		}
		
		public override Entity CreateAnonymousResource() {
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
		
		public override void Select(Statement template, StatementSink result) {
			foreach (Store s in stores)
				s.Select(template, result);
		}
		
		public override void Select(Statement[] templates, StatementSink result) {
			foreach (Store s in stores)
				s.Select(templates, result);
		}
	}
	
}
