using System;
using System.Collections;

namespace SemWeb {
	public class WriterStore : Store {
		RdfWriter writer;
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
			if (subj == null && statement.Subject is MyAnonymousNode)
				subj = ((MyAnonymousNode)statement.Subject).writerURI;
			if (subj == null) return;
			
			string pred = statement.Predicate.Uri;
			if (pred == null && statement.Predicate is MyAnonymousNode)
				pred = ((MyAnonymousNode)statement.Predicate).writerURI;
			if (pred == null) return;

			if (statement.Object is Literal) {
				Literal lit = (Literal)statement.Object;
				writer.WriteStatementLiteral(subj, pred, lit.Value, lit.Language, lit.DataType);
			} else if (statement.Object.Uri != null) {
				writer.WriteStatement(subj, pred, statement.Object.Uri);
			} else if (statement.Predicate is MyAnonymousNode) {
				writer.WriteStatement(subj, pred, ((MyAnonymousNode)statement.Predicate).writerURI);
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
			return new Entity(uri, Model);
		}
		
		public override Entity CreateAnonymousResource() {
			return new MyAnonymousNode(writer.CreateAnonymousNode(), Model);
		}
		
		private class MyAnonymousNode : AnonymousNode {
			public readonly string writerURI;
			public MyAnonymousNode(string uri, KnowledgeModel model) : base(model) { this.writerURI = uri; } 
		}
	}
	
}
