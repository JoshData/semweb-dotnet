// Adapted from:
// Euler proof mechanism -- Jos De Roo
// version = '$Id: euler.js,v 1.33 2006/03/28 19:42:32 josd Exp $'

using System;
using System.Collections;

using SemWeb;
using SemWeb.Util;

namespace SemWeb.Inference {
	
	public class Euler : QueryableSource {

		SelectableSource world;
		Hashtable rules;
		
		public Euler(StatementSource rules) : this(rules, null) {
		}
		
		public Euler(StatementSource rules, SelectableSource world) {
			this.rules = RulesToCases(rules);
			this.world = world;
		}
		
		public bool Distinct { get { return false; } } // not sure...
		
		public void Select(StatementSink sink) {
			Select(Statement.All, sink);
		}

		public bool Contains(Statement template) {
			return Store.DefaultContains(this, template);
		}
		
		public void Select(Statement template, StatementSink sink) {
			if (template.Subject == null) template.Subject = new Variable("subject");
			if (template.Predicate == null) template.Predicate = new Variable("predicate");
			if (template.Object == null) template.Object = new Variable("object");
			
			Hashtable evidence = prove(rules, world, new Statement[] { template }, -1);
			if (evidence == null)
				return; // not provable (in max number of steps, if that were given)
			
			foreach (ArrayList e in evidence.Values)
				foreach (EvidenceItem ei in e)
					sink.Add(ei.head);
		}
		
		public void Select(SelectFilter filter, StatementSink sink) {
			Store.DefaultSelect(this, filter, sink); // TODO!
		}
		
		public Entity[] FindEntities(Statement[] graph) {
			return Store.DefaultFindEntities(this, graph);
		}

		public void Query(Statement[] graph, SemWeb.Query.QueryResultSink sink) {
			Hashtable evidence = prove(rules, world, graph, -1);
			if (evidence == null)
				return; // not provable (in max number of steps, if that were given)
				
			Hashtable vars = new Hashtable();
			foreach (Statement s in graph) {
				if (s.Subject is Variable && !vars.Contains(s.Subject)) vars[s.Subject] = vars.Count;
				if (s.Predicate is Variable && !vars.Contains(s.Predicate)) vars[s.Predicate] = vars.Count;
				if (s.Object is Variable && !vars.Contains(s.Object)) vars[s.Object] = vars.Count;
			}
			
			SemWeb.Query.VariableBinding[] bindings = new SemWeb.Query.VariableBinding[vars.Count];
			foreach (Variable v in vars.Keys)
				bindings[(int)vars[v]] = new SemWeb.Query.VariableBinding(v, null);
			
			foreach (ArrayList e in evidence.Values) {
				foreach (EvidenceItem ei in e) {
					foreach (Ground g in ei.body) {
						foreach (Variable v in vars.Keys)
							if (g.env.ContainsKey(v))
								// vars in the query get bound to vars for rules
								bindings[(int)vars[v]].Target = (Resource)g.env[g.env[v]];
					}
					sink.Add(bindings);
				}
			}
		}
		
		public void Query(SelectFilter[] graph, SemWeb.Query.QueryResultSink sink) {
			throw new NotImplementedException();
		}
		
		// Static methods that wrap the Euler engine
		
		private static Entity entLOGIMPLIES = "http://www.w3.org/2000/10/swap/log#implies";

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
			
			public Rule ToRule() {
				return new Rule(body, new Statement[] { head });
			}
			
			// override to satisfy a warning about overloading ==
			public override bool Equals(object o) {
				return this == (Sequent)o;
			}
			
			// override to satisfy a warning about overloading ==
			public override int GetHashCode() {
				return base.GetHashCode();
			}
			
			public static bool operator ==(Sequent a, Sequent b) {
				if (object.ReferenceEquals(a, null) && object.ReferenceEquals(a, null)) return true;
				if (object.ReferenceEquals(a, null) || object.ReferenceEquals(a, null)) return false;
				if (a.head != b.head) return false;
				if (a.body.Length != b.body.Length) return false;
				for (int i = 0; i < a.body.Length; i++)
					if (a.body[i] != b.body[i]) return false;
				return true;
			}
			public static bool operator !=(Sequent a, Sequent b) {
				return !(a == b);
			}
		}
		
		private class Ground {
			public Sequent src;  // evidence
			public Hashtable env;  // substitution environment: Resource => Resource
			
			public Ground(Sequent src, Hashtable env) {
				this.src = src;
				this.env = env;
			}
			
			public ProofStep ToProofStep() {
				return new ProofStep(src.ToRule(), env);
			}
		}
		
		private class EvidenceItem {
			public Statement head;
			public ArrayList body; // of Ground
			
			public Proof ToProof() {
				ProofStep[] steps = new ProofStep[body.Count];
				for (int i = 0; i < body.Count; i++)
					steps[i] = ((Ground)body[i]).ToProofStep();
				return new Proof(new Statement[] { head }, steps);
			}
		}
		
		private class QueueItem {
			public Sequent rule;
			public Sequent src;
			public int ind;
			public QueueItem parent;
			public Hashtable env; // substitution environment: Resource => Resource
			public ArrayList ground;
			
			public QueueItem Clone() {
				return (QueueItem)MemberwiseClone();
			}
		}
		
		private abstract class UserPredicate {
			public abstract bool Evaluate(Resource subject, Resource obj);
		}
		
		public static Proof[] Prove(StatementSource rules, SelectableSource world, Statement[] goal) {
			return Prove(rules, world, goal, true);
		}
		
		public static Proof[] Prove(StatementSource rules, SelectableSource world, Statement[] goal, bool bnodesAsVariables) {
			if (bnodesAsVariables)
				goal = BnodesToVariables(new MemoryStore(goal)).ToArray();
			
			Hashtable cases = RulesToCases(rules);
			
			Hashtable evidence = prove(cases, world, goal, -1);
			if (evidence == null)
				throw new Exception("Could not complete proof within the maximum number of steps allowed.");
			
			ArrayList ret = new ArrayList();
			foreach (ArrayList e in evidence.Values) {
				foreach (EvidenceItem ei in e) {
					ret.Add(ei.ToProof());
				}
			}

			return (Proof[])ret.ToArray(typeof(Proof));
		}
		
		private static Hashtable prove(Hashtable cases, SelectableSource world, Statement[] goal, int maxNumberOfSteps) {
			// cases: Resource (predicate) => IList of Sequent
			world = new SemWeb.Stores.CachedSource(world);
		
			ArrayList queue = new ArrayList();

			{
				QueueItem start = new QueueItem();
				start.rule = new Sequent(Statement.All, goal);
				start.src = null;
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
							Statement t2 = evaluate(c.rule.body[i], c.env);
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
					unify(c.rule.head, c.env, r.rule.body[r.ind], r.env, true);
					r.ind++;
					queue.Add(r);
					continue;
				}
				
				Statement t = c.rule.body[c.ind];
				
				UserPredicate b = FindUserPredicate(t.Predicate);
				if (b != null) {
					if (b.Evaluate(t.Subject, t.Object)) {
						g.Add(new Ground(new Sequent(evaluate(t, c.env), new Statement[0]), new Hashtable()));

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


				ArrayList tcases = new ArrayList();
				if (cases.ContainsKey(t.Predicate))
					tcases.AddRange((IList)cases[t.Predicate]);
				if (world != null) {
					MemoryStore w = new MemoryStore();
					//Console.WriteLine(evaluate(t, c.env));
					world.Select(evaluate(t, c.env), w);
					foreach (Statement s in w) {
						Sequent seq = new Sequent(s);
						tcases.Add(seq);
					}
				}
				if (tcases.Count == 0) continue;
				
				for (int k = 0; k < tcases.Count; k++) {
					Sequent rl = (Sequent)tcases[k];
					ArrayList g2 = (ArrayList)c.ground.Clone();
					if (rl.body.Length == 0) g2.Add(new Ground(rl, new Hashtable()));
					
					QueueItem r = new QueueItem();
					r.rule = rl;
					r.src = rl;
					r.ind = 0;
					r.parent = c;
					r.env = new Hashtable();
					r.ground = g2;
					
					if (unify(t, c.env, rl.head, r.env, true)) {
						QueueItem ep = c;  // euler path
					 	while ((ep = ep.parent) != null) 
					  		if (ep.src == c.src && unify(ep.rule.head, ep.env, c.rule.head, c.env, false))
					  			break;
					 	if (ep == null)
					 		queue.Insert(0, r);
					}
				}
			}
			
			return evidence;
		}
		
		private static bool unify(Resource s, Hashtable senv, Resource d, Hashtable denv, bool f) {
			if (s is Variable) {
				Resource sval = evaluate(s, senv);
				if (sval != null) return unify(sval, senv, d, denv, f);
				else {
					// next line inserted to ensure variables in
					// the goal get bindings to variables in the
					// rules, which Euler doesn't
					// care about, but we want to track
					if (f) denv[s] = d;
					return true;
				}
			} else if (d is Variable) {
				Resource dval = evaluate(d, denv);
				if (dval != null) {
					return unify(s, senv, dval, denv, f);
				} else {
					if (f) denv[d] = evaluate(s, senv);
					return true;
				}
			} else if (s == d) {
				return true;
			} else {
				return false;
			}
		}

		private static bool unify(Statement s, Hashtable senv, Statement d, Hashtable denv, bool f) {
			if (!unify(s.Subject, senv, d.Subject, denv, f)) return false;
			if (!unify(s.Predicate, senv, d.Predicate, denv, f)) return false;
			if (!unify(s.Object, senv, d.Object, denv, f)) return false;
			return true;
		}
		
		private static Resource evaluate(Resource t, Hashtable env) {
			if (t is Variable) {
				if (env.ContainsKey(t)) {
					Resource a = (Resource)env[t];
					return evaluate(a, env);
				} else {
					return null;
				}
			}
			return t;
		}

		private static Statement evaluate(Statement t, Hashtable env) {
			Statement ret = new Statement(
				(Entity)evaluate(t.Subject, env),
				(Entity)evaluate(t.Predicate, env),
				evaluate(t.Object, env)
				);
			return ret;
		}
		
		private static UserPredicate FindUserPredicate(Entity predicate) {
			return null;
		}
		
		private static Hashtable RulesToCases(StatementSource rules) {
			Hashtable cases = new Hashtable();
			MemoryStore rules_store = BnodesToVariables(rules);
			foreach (Statement p in rules_store) {
				if (p.Meta == Statement.DefaultMeta) {
					if (p.Predicate == entLOGIMPLIES && p.Object is Entity) {
						Statement[] body = rules_store.Select(new Statement(null, null, null,  (Entity)p.Subject)).ToArray();
						Statement[] head = rules_store.Select(new Statement(null, null, null,  (Entity)p.Object)).ToArray();
						
						// Set the meta of these statements to DefaultMeta
						for (int i = 0; i < body.Length; i++)
							body[i].Meta = Statement.DefaultMeta;
						for (int i = 0; i < head.Length; i++)
							head[i].Meta = Statement.DefaultMeta;
						
						// Make sure all variables in the head are bound
						// in the body.
						ResSet bodyvars = new ResSet();
						foreach (Statement b in body) {
							if (b.Subject is Variable) bodyvars.Add(b.Subject);
							if (b.Predicate is Variable) bodyvars.Add(b.Predicate);
							if (b.Object is Variable) bodyvars.Add(b.Object);
						}
						foreach (Statement h in head) {
							if (h.Subject is Variable && !bodyvars.Contains(h.Subject)) throw new ArgumentException("The variable " + h.Subject + " is not bound in the body of a rule.");
							if (h.Predicate is Variable && !bodyvars.Contains(h.Predicate)) throw new ArgumentException("The variable " + h.Predicate + " is not bound in the body of a rule.");
							if (h.Object is Variable && !bodyvars.Contains(h.Object)) throw new ArgumentException("The variable " + h.Object + " is not bound in the body of a rule.");
						}
						
						// Rules can't have more than one statement in their
						// consequence.  The best we can do is break up
						// the consequence into multiple rules.  (Since all head
						// variables are bound in body, it's equivalent.)
						foreach (Statement h in head)
							AddSequent(cases, new Sequent(h, body));
					} else {
						AddSequent(cases, new Sequent(p, new Statement[0]));
					}
				}
			}
			
			return cases;
		}

		private static MemoryStore BnodesToVariables(StatementSource s) {
			MemoryStore m = new MemoryStore(s);
			foreach (Entity e in m.GetEntities())
				if (e is BNode && !(e is Variable))
					m.Replace(e, new Variable(((BNode)e).LocalName));
			return m;
		}

		private static void AddSequent(Hashtable cases, Sequent s) {
			ArrayList list = (ArrayList)cases[s.head.Predicate];
			if (list == null) {
				list = new ArrayList();
				cases[s.head.Predicate] = list;
			}
			list.Add(s);
		}

	}
}
