using System;
using System.Collections;

namespace SemWeb {
	public class WriterStore : Store {
		RdfWriter writer;
		Hashtable uriToResource = new Hashtable();
		Hashtable anons = new Hashtable();
		long anonId = 0;
		
		int ctr = 0;
		
		public WriterStore(RdfWriter writer, KnowledgeModel model) : base(model) { this.writer = writer; }
		
		public override int StatementCount { get { return ctr; } }
		
		public override void Clear() {
			throw new InvalidOperationException();
		}
		
		public override void Import(RdfParser parser) {
			base.Import(parser);
			writer.Close();
		}
		
		public override void Add(Statement statement) {
			ctr++;
			
			string subj = statement.Subject.Uri;
			if (subj == null) {
				subj = (string)anons[statement.Subject];
				if (subj == null) {
					subj = writer.CreateAnonymousNode();
					anons[statement.Subject] = subj;
				}
			}				
			
			string pred = statement.Predicate.Uri;
			if (pred == null) {
				pred = (string)anons[statement.Predicate];
				if (pred == null) {
					pred = writer.CreateAnonymousNode();
					anons[statement.Predicate] = pred;
				}
			}				

			if (statement.Object is Literal) {
				Literal lit = (Literal)statement.Object;
				writer.WriteStatementLiteral(subj, pred, lit.Value, lit.Language, lit.DataType);
			} else if (statement.Object.Uri != null) {
				string obj = statement.Object.Uri;
				writer.WriteStatement(subj, pred, obj);
			} else {
				string obj = (string)anons[statement.Object];
				if (obj == null) {
					obj = writer.CreateAnonymousNode();
					anons[statement.Object] = obj;
				}
				writer.WriteStatement(subj, pred, obj);
			}
		}
		
		public override bool Contains(Statement statement) {
			throw new InvalidOperationException();
		}
		
		public override void Remove(Statement statement) {
			throw new InvalidOperationException();
		}
		
		public override void Select(Statement template, StatementSink result) {
			throw new InvalidOperationException();
		}
		
		public override Entity GetResource(string uri, bool create) {
			Entity ret = (Entity)uriToResource[uri];
			if (ret == null && create) {
				ret = new Entity(uri, Model);
				uriToResource[uri] = ret;
			}
			return ret;
		}
		
		public override Entity CreateAnonymousResource() {
			return new AnonymousNode(Model);
		}
	}
	
}
