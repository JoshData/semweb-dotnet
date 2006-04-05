// Adapted from:
// Euler proof mechanism -- Jos De Roo
// version = '$Id: euler.js,v 1.33 2006/03/28 19:42:32 josd Exp $'

using System;
using System.Collections;

using SemWeb;
using SemWeb.Util;

namespace SemWeb.Inference {
	
	public class Euler {
		
		Entity entLOGIMPLIES = "http://www.w3.org/2000/10/swap/log#implies";

		private class Sequent {
			public Statement head; // consequent
			public Statement[] body; // antecedent
			
			public Sequent(Statement head, Statement[] body) {
				this.head = head;
				this.body = body;
			}
			public Sequent(Statement head) {
				this.head = head;
				this.body = new Statement[0];
			}
			
			public override string ToString() {
				string ret = "";
				foreach (Statement b in body) {
					if (ret != "") ret += " , ";
					ret += b.ToString();
				}
				ret += " => ";
				ret += head.ToString();
				return ret;
			}
		}
		
		private class Ground {
			public Sequent src;  // evidence
			public Hashtable env;  // substitution environment: string => Resource
			public Ground(Sequent src, Hashtable env) {
				this.src = src;
				this.env = env;
			}
		}
		
		private class EvidenceItem {
			public Statement head;
			public ArrayList body;
		}
		
		private class QueueItem {
			public Sequent rule;
			public int src;
			public int ind;
			public QueueItem parent;
			public Hashtable env;
			public ArrayList ground;
			
			public QueueItem Clone() {
				return (QueueItem)MemberwiseClone();
			}
		}
		
		private abstract class UserPredicate {
			public abstract bool Evaluate(Resource subject, Resource obj);
		}

		public void Prove(MemoryStore premises, MemoryStore goal, ICollection variables) {
			ArrayList axioms = new ArrayList();
			
			foreach (Statement p in premises) {
				if (p.Meta == Statement.DefaultMeta) {
					if (p.Predicate == entLOGIMPLIES && p.Object is Entity) {
						Statement[] body = premises.Select(new Statement(null, null, null,  (Entity)p.Subject)).ToArray();
						Statement[] head = premises.Select(new Statement(null, null, null,  (Entity)p.Object)).ToArray();
						if (head.Length != 1)
							continue;
						for (int i = 0; i < body.Length; i++) { body[i].Meta = Statement.DefaultMeta; }
						head[0].Meta = Statement.DefaultMeta;
						axioms.Add(new Sequent(head[0], body));
					} else {
						axioms.Add(new Sequent(p, new Statement[0]));
					}
				}
			}
			
			Sequent[] axioms2 = (Sequent[])axioms.ToArray(typeof(Sequent));
			
			ArrayList result = prove(axioms2, goal.ToArray(), -1, variables);
			if (result == null)
				throw new Exception("Could not complete proof within the maximum number of steps allowed.");
			
			foreach (EvidenceItem e in result) {
				Console.WriteLine("! " + e.head);
			}
		}

		private ArrayList prove(Sequent[] axioms, Statement[] goal, int maxNumberOfSteps, ICollection variables) {
			Hashtable cases = new Hashtable();
			foreach (Sequent axiom in axioms) {
				ArrayList list = (ArrayList)cases[axiom.head.Predicate];
				if (list == null) {
					list = new ArrayList();
					cases[axiom.head.Predicate] = list;
				}
				list.Add(axiom);
			}
			
			Hashtable evidence = prove(cases, goal, maxNumberOfSteps, new ResSet(variables));
			if (evidence == null) return null;
			
			ArrayList ret = new ArrayList();
			foreach (ArrayList e in evidence.Values)
				ret.AddRange(e);

			return ret;
		}
		
		private Hashtable prove(Hashtable cases, Statement[] goal, int maxNumberOfSteps, ResSet variables) {
			// cases: Resource (predicate) => IList of Sequent
		
			ArrayList queue = new ArrayList();

			{
				QueueItem start = new QueueItem();
				start.rule = new Sequent(Statement.All, goal);
				start.src = 0;
				start.ind = 0;
				start.parent = null;
				start.env = new Hashtable();
				start.ground = new ArrayList();
				queue.Add(start);
			}
			
			// predicate => ArrayList of EvidenceItem
			Hashtable evidence = new Hashtable();
			
			int step = 0;
			
			while (queue.Count > 0) {
				QueueItem c = (QueueItem)queue[queue.Count-1];
				queue.RemoveAt(queue.Count-1);
				ArrayList g = new ArrayList(c.ground);
				
				step++;
				if (maxNumberOfSteps != -1 && step >= maxNumberOfSteps) return null;
				
				if (c.ind >= c.rule.body.Length) {
					if (c.parent == null) {
						for (int i = 0; i < c.rule.body.Length; i++) {
							Statement t2 = evaluate(c.rule.body[i], c.env, variables);
							if (evidence[t2.Predicate] == null) evidence[t2.Predicate] = new ArrayList();
							EvidenceItem ev = new EvidenceItem();
							ev.head = t2;
							ev.body = c.ground;
							((ArrayList)evidence[t2.Predicate]).Add(ev);
						}
						continue;
					}
					if (c.rule.body.Length != 0) g.Add(new Ground(c.rule, c.env));

					QueueItem r = new QueueItem();
					r.rule = new Sequent(c.parent.rule.head, c.parent.rule.body);
					r.src = c.parent.src;
					r.ind = c.parent.ind;
					r.parent = c.parent.parent != null
						? c.parent.parent.Clone()
						: null;
					r.env = (Hashtable)c.parent.env.Clone();
					r.ground = g;
					unify(c.rule.head, c.env, r.rule.body[r.ind], r.env, true, variables);
					r.ind++;
					queue.Add(r);
					continue;
				}
				
				Statement t = c.rule.body[c.ind];
				
				UserPredicate b = FindUserPredicate(t.Predicate);
				if (b != null) {
					if (b.Evaluate(t.Subject, t.Object)) {
						g.Add(new Ground(new Sequent(evaluate(t, c.env, variables), new Statement[0]), new Hashtable()));

						QueueItem r = new QueueItem();
						r.rule = new Sequent(c.rule.head, c.rule.body);
						r.src = c.src;
						r.ind = c.ind;
						r.parent = c.parent;
						r.env = c.env;
						r.ground = g;
						r.ind++;
						queue.Add(r);
					}
					continue;
				}


				IList tcases = (IList)cases[t.Predicate];
				if (tcases == null) continue;
				
				int src = 0;
				for (int k = 0; k < tcases.Count; k++) {
					Sequent rl = (Sequent)tcases[k];
					src++;
					ArrayList g2 = (ArrayList)c.ground.Clone();
					if (rl.body.Length == 0) g2.Add(new Ground(rl, new Hashtable()));
					
					QueueItem r = new QueueItem();
					r.rule = rl;
					r.src = src;
					r.ind = 0;
					r.parent = c;
					r.env = new Hashtable();
					r.ground = g2;
					
					if (unify(t, c.env, rl.head, r.env, true, variables)) {
						QueueItem ep = c;  // euler path
					 	while ((ep = ep.parent) != null) 
					  		if (ep.src == c.src && unify(ep.rule.head, ep.env, c.rule.head, c.env, false, variables))
					  			break;
					 	if (ep == null)
					 		queue.Insert(0, r);
					}
				}
			}
			
			return evidence;
		}
		
		private bool unify(Resource s, Hashtable senv, Resource d, Hashtable denv, bool f, ResSet variables) {
			if (isVar(s, variables)) {
				Resource sval = evaluate(s, senv, variables);
				if (sval != null) return unify(sval, senv, d, denv, f, variables);
				else return true;
			} else if (isVar(d, variables)) {
				Resource dval = evaluate(d, denv, variables);
				if (dval != null) {
					return unify(s, senv, dval, denv, f, variables);
				} else {
					if (f) denv[d] = evaluate(s, senv, variables);
					return true;
				}
			} else if (s == d) {
				return true;
			} else {
				return false;
			}
		}

		private bool unify(Statement s, Hashtable senv, Statement d, Hashtable denv, bool f, ResSet variables) {
			if (!unify(s.Subject, senv, d.Subject, denv, f, variables)) return false;
			if (!unify(s.Predicate, senv, d.Predicate, denv, f, variables)) return false;
			if (!unify(s.Object, senv, d.Object, denv, f, variables)) return false;
			return true;
		}
		
		private Resource evaluate(Resource t, Hashtable env, ResSet variables) {
			if (isVar(t, variables)) {
				if (env.ContainsKey(t)) {
					Resource a = (Resource)env[t];
					return evaluate(a, env, variables);
				} else {
					return null;
				}
			}
			return t;
		}

		private Statement evaluate(Statement t, Hashtable env, ResSet variables) {
			Statement ret = new Statement(
				(Entity)evaluate(t.Subject, env, variables),
				(Entity)evaluate(t.Predicate, env, variables),
				evaluate(t.Object, env, variables)
				);
			if (ret.AnyNull) throw new Exception();
			return ret;
		}

		private bool isVar(Resource res, ResSet vars) {
			return vars.Contains(res);
		}
		
		private UserPredicate FindUserPredicate(Entity predicate) {
			return null;
		}
	

		
	}
}
