using System;
using System.Collections;
using System.IO;
using System.Text;

namespace SemWeb {

	public class KnowledgeModel : Store {
		
		SemWeb.Stores.MultiStore stores;
		Store mainstore;
		
		public KnowledgeModel() : base(null) {
			stores = new SemWeb.Stores.MultiStore(this);
			mainstore = stores;
		}
		
		public KnowledgeModel(RdfParser parser) : this() {
			stores.Add(new SemWeb.Stores.MemoryStore(parser, this));
		}

		public override KnowledgeModel Model { get { return this; } }
		
		public SemWeb.Stores.MultiStore Storage { get { return stores; } }
		
		public void Add(Store storage) {
			Storage.Add(storage);
		}
		
		public void AddReasoning(ReasoningEngine engine) {
			mainstore = new InferenceStore(mainstore, engine);
		}
		
		public Entity this[string uri] {
			get {
				return GetResource(uri);
			}
		}
		
		public override Entity GetResource(string uri, bool create) {
			return stores.GetResource(uri, create);
		}

		public override Entity[] GetAllEntities() { return stores.GetAllEntities(); }
		
		public override Entity[] GetAllPredicates() { return stores.GetAllPredicates(); }
		
		public override void Select(Statement template, StatementSink result) {
			mainstore.Select(template, result);
		}
		
		public override void Select(Statement[] templates, StatementSink result) {
			mainstore.Select(templates, result);
		}

		public override int StatementCount { get { return stores.StatementCount; } }

		public override void Clear() { throw new InvalidOperationException(); }
		public override Entity CreateAnonymousResource() { throw new InvalidOperationException(); }
		public override void Add(Statement statement) { throw new InvalidOperationException(); }
		public override void Remove(Statement statement) { throw new InvalidOperationException(); }
	}

		
}
