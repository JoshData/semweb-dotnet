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
			Select(new SelectFilter(template), sink);
		}
		
		public void Select(SelectFilter filter, StatementSink sink) {
			if (filter.Subjects == null) filter.Subjects = new Entity[] { new Variable("subject") };
			if (filter.Predicates == null) filter.Predicates = new Entity[] { new Variable("predicate") };
			if (filter.Objects == null) filter.Objects = new Entity[] { new Variable("object") };
			
			ArrayList evidence = prove(rules, world, new SelectFilter[] { filter }, -1);
			if (evidence == null)
				return; // not provable (in max number of steps, if that were given)
			
			foreach (EvidenceItem ei in evidence) {
				foreach (Statement h in ei.head) { // better be just one statement
					if (filter.LiteralFilters != null
						&& !LiteralFilter.MatchesFilters(h.Object, filter.LiteralFilters, world))
						continue;
					
					sink.Add(h);
				}
			}
		}
		
		public void Query(Statement[] graph, SemWeb.Query.QueryResultSink sink) {
			ArrayList evidence = prove(rules, world, SelectFilter.FromGraph(graph), -1);
			if (evidence == null)
				return; // not provable (in max number of steps, if that were given)
				
			Hashtable vars = new Hashtable();
			foreach (Statement s in graph) {
				if (s.Subject is Variable && !vars.ContainsKey(s.Subject)) vars[s.Subject] = vars.Count;
				if (s.Predicate is Variable && !vars.ContainsKey(s.Predicate)) vars[s.Predicate] = vars.Count;
				if (s.Object is Variable && !vars.ContainsKey(s.Object)) vars[s.Object] = vars.Count;
			}
			
			SemWeb.Query.VariableBinding[] bindings = new SemWeb.Query.VariableBinding[vars.Count];
			foreach (Variable v in vars.Keys)
				bindings[(int)vars[v]] = new SemWeb.Query.VariableBinding(v, null);
			
			sink.Init(bindings, false, false);
			
			foreach (EvidenceItem ei in evidence) {
				foreach (Variable v in vars.Keys)
					if (ei.env.ContainsKey(v))
						bindings[(int)vars[v]].Target = (Resource)ei.env[v];
				sink.Add(bindings);
			}
			
			sink.Finished();
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
			
			public override string ToString() {
				string ret = "{ ";
				foreach (Statement b in body) {
					if (ret != "{ ") ret += ", ";
					ret += b.ToString();
				}
				ret += " } => " + head;
				return ret;
			}
			
			public static bool operator ==(Sequent a, Sequent b) {
				if (object.ReferenceEquals(a, b)) return true;
				if (object.ReferenceEquals(a, null) && object.ReferenceEquals(b, null)) return true;
				if (object.ReferenceEquals(a, null) || object.ReferenceEquals(b, null)) return false;
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
			public Statement[] head;
			public ArrayList body; // of Ground
			public Hashtable env; // substitutions of goal-level variables
			
			public Proof ToProof() {
				ProofStep[] steps = new ProofStep[body.Count];
				for (int i = 0; i < body.Count; i++)
					steps[i] = ((Ground)body[i]).ToProofStep();
				return new Proof(head, steps);
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
			return Prove(rules, world, SelectFilter.FromGraph(goal));
		}
		
		public static Proof[] Prove(StatementSource rules, SelectableSource world, SelectFilter[] goal) {
			Hashtable cases = RulesToCases(rules);
			
			ArrayList evidence = prove(cases, world, goal, -1);
			if (evidence == null)
				throw new Exception("Could not complete proof within the maximum number of steps allowed.");
			
			ArrayList ret = new ArrayList();
			foreach (EvidenceItem ei in evidence)
				ret.Add(ei.ToProof());

			return (Proof[])ret.ToArray(typeof(Proof));
		}
		
		private static ArrayList prove(Hashtable cases, SelectableSource world, SelectFilter[] goal, int maxNumberOfSteps) {
			// cases: Resource (predicate) => IList of Sequent
			world = new SemWeb.Stores.CachedSource(world);
		
			ArrayList queue = new ArrayList();

			{
				QueueItem start = new QueueItem();
				start.env = new Hashtable();

				Statement[] gst = new Statement[goal.Length];
				for (int i = 0; i < goal.Length; i++) {
					gst[i] = new Statement(
						goal[i].Subjects.Length == 1 ? goal[i].Subjects[0] : new Variable("multisubject"),
						goal[i].Predicates.Length == 1 ? goal[i].Predicates[0] : new Variable("multipredicate"),
						goal[i].Objects.Length == 1 ? goal[i].Objects[0] : new Variable("multiobject")
						);
					if (goal[i].Subjects.Length > 1) start.env[gst[i].Subject] = goal[i].Subjects;
					if (goal[i].Predicates.Length > 1) start.env[gst[i].Predicate] = goal[i].Predicates;
					if (goal[i].Objects.Length > 1) start.env[gst[i].Object] = goal[i].Objects;
				}
				
				start.rule = new Sequent(Statement.All, gst);
				start.src = null;
				start.ind = 0;
				start.parent = null;
				start.ground = new ArrayList();
				queue.Add(start);
			}
			
			ArrayList evidence = new ArrayList();
			ArrayList proved = new ArrayList();
			
			int step = 0;
			
			while (queue.Count > 0) {
				// deal with the QueueItem at the top of the queue
				QueueItem c = (QueueItem)queue[queue.Count-1];
				queue.RemoveAt(queue.Count-1);
				ArrayList g = new ArrayList(c.ground);
				
				// have we done too much?
				step++;
				if (maxNumberOfSteps != -1 && step >= maxNumberOfSteps) return null;
				
				// if each statement in the body of the sequent has been proved
				if (c.ind >= c.rule.body.Length) {
					// if this is the top-level sequent being proved
					if (c.parent == null) {
						EvidenceItem ev = new EvidenceItem();
						ev.head = new Statement[c.rule.body.Length];
						for (int i = 0; i < c.rule.body.Length; i++)
							ev.head[i] = evaluate(c.rule.body[i], c.env);
						ev.body = c.ground;
						ev.env = c.env;
						evidence.Add(ev);
					
					// this is a subproof
					} else {
						// note that the rule was used in a proof of this part
						if (c.rule.body.Length != 0) g.Add(new Ground(c.rule, c.env));

						// advance the parent being proved and put the advanced
						// parent into the queue; unify the parent variable assignments
						// with this one's
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
					}
				
				// this sequent still has parts of the body left to be proved
				} else {
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
					
					// t can be proved either by the use of a rule
					// or if t literally exists in the world

					ArrayList tcases = new ArrayList();
					
					// get all of the rules that apply to the predicate in question
					if (cases.ContainsKey(t.Predicate))
						tcases.AddRange((IList)cases[t.Predicate]);
						
					// if there is a seprate world, get all of the world
					// statements that witness t
					if (world != null) {
						MemoryStore w = new MemoryStore();
					
						//Console.WriteLine("Q: " + evaluate_filter(t, c.env));
						world.Select(evaluate_filter(t, c.env), w);
						foreach (Statement s in w) {
							//Console.WriteLine("  " + s);
							Sequent seq = new Sequent(s);
							tcases.Add(seq);
						}
					}
					if (tcases.Count == 0) continue;
					
					foreach (Sequent rl in tcases) {
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
			}
			
			return evidence;
		}
		
		private static bool unify(object s, Hashtable senv, object d, Hashtable denv, bool f) {
			if (s is Variable) {
				object sval = evaluate(s, senv);
				if (sval != null) return unify(sval, senv, d, denv, f);
				else return true;
			} else if (d is Variable) {
				object dval = evaluate(d, denv);
				if (dval != null) {
					if (dval is Resource[]) {
						if (Array.IndexOf( (Resource[])dval , s) != -1) {
							if (f) denv[d] = s;
							return true;
						}
						return false;
					} else {
						return unify(s, senv, dval, denv, f);
					}
				} else {
					object sval = evaluate(s, senv);
					if (f && sval != null) denv[d] = sval;
					return true;
				}
			} else if (s.Equals(d)) {
				return true;
			} else if (s is Resource[]) {
				return Array.IndexOf( (Resource[])s , d) != -1;
			} else {
				return false;
			}
		}

		private static bool unify(Statement s, Hashtable senv, Statement d, Hashtable denv, bool f) {
			if (s == Statement.All) return false;
			if (!unify(s.Subject, senv, d.Subject, denv, f)) return false;
			if (!unify(s.Predicate, senv, d.Predicate, denv, f)) return false;
			if (!unify(s.Object, senv, d.Object, denv, f)) return false;
			return true;
		}
		
		private static object evaluate(object t, Hashtable env) {
			if (t is Variable) {
				object val = env[t];
				if (val is Resource) {
					return evaluate((Resource)val, env);
				} else {
					return val;
				}
			}
			return t;
		}
		
		private static Statement evaluate(Statement t, Hashtable env) {
			return new Statement(
				(Entity)evaluate(t.Subject, env),
				(Entity)evaluate(t.Predicate, env),
				(Resource)evaluate(t.Object, env),
				t.Meta);
		}
		
		private static Resource[] evaluate_array(Resource r, Hashtable env) {
			object v = evaluate(r, env);
			if (v == null) return null;
			if (v is Entity) return new Entity[] { (Entity)v };
			if (v is Resource) return new Resource[] { (Resource)v };
			if (v is Resource[]) return (Resource[])v;
			throw new InvalidOperationException();
		}
		
		private static SelectFilter evaluate_filter(Statement t, Hashtable env) {
			return new SelectFilter(
				(Entity[])evaluate_array(t.Subject, env),
				(Entity[])evaluate_array(t.Predicate, env),
				(Resource[])evaluate_array(t.Object, env),
				new Entity[] { t.Meta });
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
