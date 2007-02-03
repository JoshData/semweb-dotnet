using System;
using System.Collections;
using SemWeb;

namespace SemWeb.Inference {
	
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
		public readonly IDictionary Substitutions;
		
		public ProofStep(Rule rule, IDictionary substitutions) {
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
				foreach (Variable v in step.Substitutions.Keys) {
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
