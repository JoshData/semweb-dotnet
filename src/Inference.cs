using System;

#if !DOTNET2
using System.Collections;
#else
using System.Collections.Generic;
#endif

using SemWeb;
using SemWeb.Query;

namespace SemWeb.Inference {

	public abstract class Reasoner {
		public abstract bool Distinct { get; } // assume targetModel.Distinct is true, *then* would Select be distinct?

		public void Select(Statement template, SelectableSource targetModel, StatementSink sink) {
			Select(new SelectFilter(template), targetModel, sink);
		}
		
		public abstract void Select(SelectFilter filter, SelectableSource targetModel, StatementSink sink);
		
		public abstract MetaQueryResult MetaQuery(Statement[] graph, QueryOptions options, SelectableSource targetModel);
		public abstract void Query(Statement[] graph, QueryOptions options, SelectableSource targetModel, QueryResultSink result);
	}
	
	public class SimpleEntailment : Reasoner {
		public override bool Distinct { get { return true; } }
		
		public override void Select(SelectFilter filter, SelectableSource targetModel, StatementSink sink) {
			targetModel.Select(filter, sink);
		}
		
		public override MetaQueryResult MetaQuery(Statement[] graph, SemWeb.Query.QueryOptions options, SelectableSource source) {
			SemWeb.Query.MetaQueryResult ret = new SemWeb.Query.MetaQueryResult();
			
			ret.QuerySupported = true;
			
			ret.NoData = new bool[graph.Length];
			for (int i = 0; i < graph.Length; i++) {
				for (int j = 0; j < 4; j++) {
					Resource r = graph[i].GetComponent(j);
					
					if (r != null && !(r is Variable) && !source.Contains(r))
						ret.NoData[i] = true;
					
					if (r != null && r is Variable && options.VariableKnownValues != null && options.VariableKnownValues[(Variable)r] != null) {
						bool found = false;
						#if !DOTNET2
						foreach (Resource s in (ICollection)options.VariableKnownValues[(Variable)r]) {
						#else
						foreach (Resource s in (ICollection<Resource>)options.VariableKnownValues[(Variable)r]) {
						#endif
							if (source.Contains(s)) {
								found = true;
								break;
							}
						}
						if (!found) {
							ret.NoData[i] = true;
						}
					}
				}
			}
			
			return ret;
		}
		
		public override void Query(Statement[] graph, SemWeb.Query.QueryOptions options, SelectableSource targetModel, QueryResultSink result) {
			SemWeb.Query.GraphMatch q = new SemWeb.Query.GraphMatch();
			foreach (Statement s in graph)
				q.AddGraphStatement(s);
				
			q.ReturnLimit = options.Limit;
			
			if (options.VariableKnownValues != null) {
			#if !DOTNET2
			foreach (DictionaryEntry ent in options.VariableKnownValues)
				q.SetVariableRange((Variable)ent.Key, (ICollection)ent.Value);
			#else
			foreach (KeyValuePair<Variable,ICollection<Resource>> ent in options.VariableKnownValues)
				q.SetVariableRange(ent.Key, ent.Value);
			#endif
			}

			if (options.VariableLiteralFilters != null) {			
			#if !DOTNET2
			foreach (DictionaryEntry ent in options.VariableLiteralFilters)
				foreach (LiteralFilter filter in (ICollection)ent.Value)
					q.AddLiteralFilter((Variable)ent.Key, filter);
			#else
			foreach (KeyValuePair<Variable,ICollection<LiteralFilter>> ent in options.VariableLiteralFilters)
				foreach (LiteralFilter filter in ent.Value)
					q.AddLiteralFilter(ent.Key, filter);
			#endif
			}

			q.Run(targetModel, result);
		}
	}
	
	public class Rule {
		public readonly Statement[] Antecedent;
		public readonly Statement[] Consequent;
	
		public Rule(Statement[] antecedent, Statement[] consequent) {
			Antecedent = antecedent;
			Consequent = consequent;
		}
		
		public override string ToString() {
			string ret = "";
			if (Antecedent.Length == 0) {
				ret += "(axiom) ";
			} else {
				if (Antecedent.Length > 1) ret += "{";
				foreach (Statement s in Antecedent)
					ret += " " + s.ToString();
				if (Antecedent.Length > 1) ret += " }";
				ret += " => ";
			}
			if (Consequent.Length > 1) ret += "{";
			foreach (Statement s in Consequent)
				ret += " " + s.ToString();
			if (Consequent.Length > 1) ret += " }";
			return ret;
		}
	}
	
	public class ProofStep {
		public readonly Rule Rule;
		public readonly System.Collections.IDictionary Substitutions;
		
		public ProofStep(Rule rule, System.Collections.IDictionary substitutions) {
			Rule = rule;
			Substitutions = substitutions;
		}
	}
	
	public class Proof {
		public readonly Statement[] Proved;
		public readonly ProofStep[] Steps;
		
		public Proof(Statement[] proved, ProofStep[] steps) {
			Proved = proved;
			Steps = steps;
		}
		
		public override string ToString () {
			System.Text.StringBuilder ret = new System.Text.StringBuilder();

			ret.Append("Proved: ");
			foreach (Statement s in Proved)
				ret.Append(s.ToString());
			ret.Append("\n");

			foreach (ProofStep step in Steps) {
				ret.Append("\t");
				ret.Append(step.Rule.ToString());
				ret.Append("\n");
				ArrayList vars = new ArrayList(step.Substitutions.Keys);
				vars.Sort();
				foreach (Variable v in vars) {
					ret.Append("\t\t");
					ret.Append(v);
					ret.Append(" => ");
					ret.Append(step.Substitutions[v]);
					ret.Append("\n");
				}
			}
			
			return ret.ToString();
		}

	}
}
