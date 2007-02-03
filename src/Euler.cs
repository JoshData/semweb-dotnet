// Adapted from:
// ---------------------------------------------------------------
// Euler proof mechanism -- Jos De Roo
// version = '$Id: euler.js,v 1.35 2006/12/17 01:25:01 josd Exp $'
// http://eulersharp.cvs.sourceforge.net/eulersharp/2006/02swap/euler.js?view=log
// ---------------------------------------------------------------

using System;
using System.Collections;

using SemWeb;
using SemWeb.Util;

namespace SemWeb.Inference {
	
	public class Euler : QueryableSource {
	
		static bool Debug = false;

		SelectableSource world;
		Hashtable rules;
		
		static Hashtable builtInRelations;
		
		static Euler() {
			RdfRelation[] rs = new RdfRelation[] {
				new SemWeb.Inference.Relations.MathAbsoluteValueRelation(), new SemWeb.Inference.Relations.MathCosRelation(), new SemWeb.Inference.Relations.MathDegreesRelation(), new SemWeb.Inference.Relations.MathEqualToRelation(),
				new SemWeb.Inference.Relations.MathNegationRelation(), new SemWeb.Inference.Relations.MathRoundedRelation(), new SemWeb.Inference.Relations.MathSinRelation(), new SemWeb.Inference.Relations.MathSinhRelation(), new SemWeb.Inference.Relations.MathTanRelation(), new SemWeb.Inference.Relations.MathTanhRelation(),
				new SemWeb.Inference.Relations.MathAtan2Relation(), new SemWeb.Inference.Relations.MathDifferenceRelation(), new SemWeb.Inference.Relations.MathExponentiationRelation(), new SemWeb.Inference.Relations.MathIntegerQuotientRelation(),
				new SemWeb.Inference.Relations.MathQuotientRelation(), new SemWeb.Inference.Relations.MathRemainderRelation(),
				new SemWeb.Inference.Relations.MathSumRelation(), new SemWeb.Inference.Relations.MathProductRelation() 
				};
		
			builtInRelations = new Hashtable();
			foreach (RdfRelation r in rs)
				builtInRelations[r.Uri] = r;
		}
		
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

		public bool Contains(Resource resource) {
			if (world != null)
				return world.Contains(resource);
			return false; // TODO: Also search axioms.
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
			
			foreach (Statement s in filter) { // until we can operate on filter directly
				ArrayList evidence = prove(rules, world, new Statement[] { s }, -1);
				if (evidence == null)
					continue; // not provable (in max number of steps, if that were given)
				
				foreach (EvidenceItem ei in evidence) {
					foreach (Statement h in ei.head) { // better be just one statement
						if (filter.LiteralFilters != null
							&& !LiteralFilter.MatchesFilters(h.Object, filter.LiteralFilters, world))
							continue;
						
						sink.Add(h);
					}
				}
			}
		}
		
		public SemWeb.Query.MetaQueryResult MetaQuery(Statement[] graph, SemWeb.Query.QueryOptions options) {
			SemWeb.Query.MetaQueryResult ret = new SemWeb.Query.MetaQueryResult();
			ret.QuerySupported = true;
			// TODO: Best to check also whether variables in the query are even known to us.
			// Need to implement Contains() completely first.
			return ret;
		}
		
		public void Query(Statement[] graph, SemWeb.Query.QueryOptions options, SemWeb.Query.QueryResultSink sink) {
			// Try to do the inferencing.
			ArrayList evidence = prove(rules, world, graph, -1);
			if (evidence == null)
				return; // not provable (in max number of steps, if that were given)
			
			// Then send the possible bindings to the QueryResultSink.
			
			// Map variables to indexes.
			Hashtable vars = new Hashtable();
			foreach (Statement s in graph) {
				if (s.Subject is Variable && !vars.ContainsKey(s.Subject)) vars[s.Subject] = vars.Count;
				if (s.Predicate is Variable && !vars.ContainsKey(s.Predicate)) vars[s.Predicate] = vars.Count;
				if (s.Object is Variable && !vars.ContainsKey(s.Object)) vars[s.Object] = vars.Count;
			}
			
			// Prepare the bindings array.
			SemWeb.Query.VariableBinding[] bindings = new SemWeb.Query.VariableBinding[vars.Count];
			foreach (Variable v in vars.Keys)
				bindings[(int)vars[v]] = new SemWeb.Query.VariableBinding(v, null);
			
			// Initialize the sink.
			sink.Init(bindings, false, false);
			
			// Send a binding set for each piece of evidence.
			foreach (EvidenceItem ei in evidence) {
				// Write a comment to the results with the actual proof. (nifty actually)
				sink.AddComments(ei.ToProof().ToString());
			
				// Create the binding array and send it on
				foreach (Variable v in vars.Keys)
					if (ei.env.ContainsKey(v))
						bindings[(int)vars[v]].Target = (Resource)ei.env[v];
				sink.Add(bindings);
			}
			
			// Close the sink.
			sink.Finished();
		}
		
		// Static methods that wrap the Euler engine
		
		private static readonly Entity entLOGIMPLIES = "http://www.w3.org/2000/10/swap/log#implies";
		private static readonly Entity entRDFFIRST = "http://www.w3.org/1999/02/22-rdf-syntax-ns#first";
		private static readonly Entity entRDFREST = "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest";
		private static readonly Entity entRDFNIL = "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil";

		private class Sequent {
			public Statement head; // consequent
			public Statement[] body; // antecedent
			public Hashtable callArgs; // encapsulates (...) arguments to user predicates
			
			public Sequent(Statement head, Statement[] body, Hashtable callArgs) {
				this.head = head;
				this.body = body;
				this.callArgs = callArgs;
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
			
			public override string ToString() {
				string ret = "";
				foreach (DictionaryEntry kv in env)
					ret += "\t" + kv.Key + "=>" + kv.Value + "\n";
				return ret;
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
			
			public override string ToString() {
				string ret = "";
				foreach (DictionaryEntry kv in env)
					ret += kv.Key + "=>" + kv.Value + "; ";
				ret += rule.ToString() + " @ " + ind + "/" + rule.body.Length;
				return ret;
			}
		}
		
		public static Proof[] Prove(StatementSource rules, SelectableSource world, Statement[] goal) {
			Hashtable cases = RulesToCases(rules);
			
			ArrayList evidence = prove(cases, world, goal, -1);
			if (evidence == null)
				throw new Exception("Could not complete proof within the maximum number of steps allowed.");
			
			ArrayList ret = new ArrayList();
			foreach (EvidenceItem ei in evidence)
				ret.Add(ei.ToProof());

			return (Proof[])ret.ToArray(typeof(Proof));
		}
		
		private static ArrayList prove(Hashtable cases, SelectableSource world, Statement[] goal, int maxNumberOfSteps) {
			// This is the main routine from euler.js.
		
			// cases: Resource (predicate) => IList of Sequent
			
			if (world != null) // cache our queries to the world, if a world is provided
				world = new SemWeb.Stores.CachedSource(world);
		
			// Create the queue and add the first item.
			ArrayList queue = new ArrayList();
			{
				QueueItem start = new QueueItem();
				start.env = new Hashtable();
				start.rule = new Sequent(Statement.All, goal, null);
				start.src = null;
				start.ind = 0;
				start.parent = null;
				start.ground = new ArrayList();
				queue.Add(start);
				if (Debug) Console.Error.WriteLine("Euler: Queue: " + start);
			}
			
			// The evidence array holds the results of this proof.
			ArrayList evidence = new ArrayList();
			
			// Track how many steps we take to not go over the limit.
			int step = 0;
			
			// Process the queue until it's empty or we reach our step limit.
			while (queue.Count > 0) {
				// deal with the QueueItem at the top of the queue
				QueueItem c = (QueueItem)queue[queue.Count-1];
				queue.RemoveAt(queue.Count-1);
				ArrayList g = new ArrayList(c.ground);
				
				// have we done too much?
				step++;
				if (maxNumberOfSteps != -1 && step >= maxNumberOfSteps) {
					if (Debug) Console.Error.WriteLine("Euler: Maximum number of steps exceeded.  Giving up.");
					return null;
				}
				
				// if each statement in the body of the sequent has been proved
				if (c.ind == c.rule.body.Length) {
					// if this is the top-level sequent being proved; we've found a complete evidence for the goal
					if (c.parent == null) {
						EvidenceItem ev = new EvidenceItem();
						ev.head = new Statement[c.rule.body.Length];
						for (int i = 0; i < c.rule.body.Length; i++)
							ev.head[i] = evaluate(c.rule.body[i], c.env);
						ev.body = c.ground;
						ev.env = c.env;
						evidence.Add(ev);
						if (Debug) Console.Error.WriteLine("Euler: Found Evidence: " + ev);
					
					// this is a subproof of something; whatever it is a subgroup for can
					// be incremented
					} else {
						// if the rule had no body, it was just an axiom and we represent that with Ground
						if (c.rule.body.Length != 0) g.Add(new Ground(c.rule, c.env));

						// advance the parent being proved and put the advanced
						// parent into the queue; unify the parent variable assignments
						// with this one's
						QueueItem r = new QueueItem();
						r.rule = c.parent.rule;
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
						if (Debug) Console.Error.WriteLine("Euler: Queue Advancement: " + r);
					}
				
				// this sequent still has parts of the body left to be proved; try to
				// find evidence for the next statement in the body, and if we find
				// evidence, queue up that evidence
				} else {
					// this is the next part of the body that we need to try to prove
					Statement t = c.rule.body[c.ind];
					
					// Try to process this predicate with a user-provided custom
					// function that resolves things like mathematical relations.
					// euler.js provides similar functionality, but the system
					// of user predicates is completely different here.
					RdfRelation b = FindUserPredicate(t.Predicate);
					if (b != null) {
						Resource[] args;
						Variable[] unifyResult;
						Resource value = evaluate(t.Object, c.env);
						
						if (!c.rule.callArgs.ContainsKey(t.Subject)) {
							// The array of arguments to this relation is just the subject of the triple itself
							args = new Resource[] { evaluate(t.Subject, c.env) };
							unifyResult = new Variable[1];
							if (t.Subject is Variable) unifyResult[0] = (Variable)t.Subject;
						
						} else {
							// The array of arguments to this relation comes from a pre-grouped arg list.
							args = (Resource[])c.rule.callArgs[t.Subject];
							unifyResult = new Variable[args.Length];
							
							for (int i = 0; i < args.Length; i++) {
								if (args[i] is Variable) {
									unifyResult[i] = (Variable)args[i];
									args[i] = evaluate(args[i], c.env);
								}
							}
						}
						
						// Run the user relation on the array of arguments (subject) and on the object.
						if (b.Evaluate(args, ref value)) {
							// If it succeeds, we press on.
						
							// The user predicate may update the 'value' variable and the argument
							// list array, and so we want to unify any variables in the subject
							// or object of this user predicate with the values given to us
							// by the user predicate.
							Hashtable newenv = (Hashtable)c.env.Clone();
							if (t.Object is Variable) unify(value, null, t.Object, newenv, true);
							for (int i = 0; i < args.Length; i++)
								if (unifyResult[i] != null)
									unify(args[i], null, unifyResult[i], newenv, true);
						
							g.Add(new Ground(new Sequent(evaluate(t, newenv), new Statement[0], null), new Hashtable()));

							QueueItem r = new QueueItem();
							r.rule = c.rule;
							r.src = c.src;
							r.ind = c.ind;
							r.parent = c.parent;
							r.env = newenv;
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
					object caseKey = t.Predicate;
					if (caseKey is Variable) caseKey = "WILDCARD";
					
					if (cases.ContainsKey(caseKey))
						tcases.AddRange((IList)cases[caseKey]);
						
					// if there is a seprate world, get all of the world
					// statements that witness t
					if (world != null) {
						MemoryStore w = new MemoryStore();
					
						//Console.WriteLine("Q: " + evaluate_filter(t, c.env));
						world.Select(evaluate_filter(t, c.env), w);
						foreach (Statement s in w) {
							//if (Debug) Console.WriteLine("Euler: World Select Response:  " + s);
							Sequent seq = new Sequent(s);
							tcases.Add(seq);
						}
					}
					
					// If there is no evidence or potential evidence (i.e. rules)
					// for t, then we dump this QueueItem.
					if (tcases.Count == 0) continue;
					
					// Otherwise we try each piece of evidence in turn.
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
						 	if (ep == null) {
						 		queue.Insert(0, r);
						 		if (Debug) Console.Error.WriteLine("Euler: Queue from Axiom: " + r);
						 	}
						}
					}
				}
			}
			
			return evidence;
		}
		
		private static bool unify(Resource s, Hashtable senv, Resource d, Hashtable denv, bool f) {
			if (s is Variable) {
				Resource sval = evaluate(s, senv);
				if (sval != null) return unify(sval, senv, d, denv, f);
				else return true;
			} else if (d is Variable) {
				Resource dval = evaluate(d, denv);
				if (dval != null) {
					return unify(s, senv, dval, denv, f);
				} else {
					if (f) denv[d] = evaluate(s, senv);
					return true;
				}
			} else if (s.Equals(d)) {
				return true;
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
		
		private static Resource evaluate(Resource t, Hashtable env) {
			if (t is Variable) {
				Resource val = (Resource)env[t];
				if (val != null)
					return evaluate(val, env);
				else
					return null;
			}
			return t;
		}
		
		private static Resource evaluate2(Resource t, Hashtable env) {
			Resource ret = evaluate(t, env);
			if (ret == null) ret = t;
			return ret;
		}
		
		private static Statement evaluate(Statement t, Hashtable env) {
			return new Statement(
				(Entity)evaluate2(t.Subject, env),
				(Entity)evaluate2(t.Predicate, env),
				(Resource)evaluate2(t.Object, env),
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
		
		// The next few routines convert a set of axioms from a StatementSource
		// into a data structure of use for the algorithm, with Sequents and things.

		private static Hashtable RulesToCases(StatementSource rules) {
			Hashtable cases = new Hashtable();
			MemoryStore rules_store = new MemoryStore(rules);
			foreach (Statement p in rules_store) {
				if (p.Meta == Statement.DefaultMeta) {
					if (p.Predicate == entLOGIMPLIES && p.Object is Entity) {
						Hashtable bnodemap = new Hashtable();
						Statement[] body = BnodesToVariables(new Store(rules_store).Select(new Statement(null, null, null,  (Entity)p.Subject)).ToArray(), bnodemap);
						Statement[] head = BnodesToVariables(new Store(rules_store).Select(new Statement(null, null, null,  (Entity)p.Object)).ToArray(), bnodemap);
						
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
						
						// Replace (...) lists in the body that are tied to the subjects
						// of user predicates with callArgs objects.
						Hashtable callArgs = new Hashtable();
						body = CollectCallArgs(body, callArgs);
						
						// Rules can't have more than one statement in their
						// consequent.  The best we can do is break up
						// the consequent into multiple rules.  (Since all head
						// variables are bound in body, it's equivalent...?)
						foreach (Statement h in head)
							AddSequent(cases, new Sequent(h, body, callArgs));
					} else {
						AddSequent(cases, new Sequent(p, new Statement[0], null));
					}
				}
			}
			
			return cases;
		}

		private static Statement[] BnodesToVariables(Statement[] ss, Hashtable map) {
			for (int i = 0; i < ss.Length; i++) {
				for (int j = 0; j < 3; j++) {
					Resource r = ss[i].GetComponent(j);
					if (r is BNode && !(r is Variable)) {
						if (map.ContainsKey(r)) {
							r = (Resource)map[r];
						} else {
							Variable v = new Variable("bnv" + map.Count);
							map[r] = v;
							r = v;
						}
						ss[i].SetComponent(j, r);
					}
				}
			}
			return ss;
		}
		
		private static Statement[] CollectCallArgs(Statement[] body, Hashtable callArgs) {
			MemoryStore b1 = new MemoryStore(body);
			Store b = new Store(b1);
			foreach (Statement s in new MemoryStore(body)) { // make a copy since we remove statements from b within
				if (FindUserPredicate(s.Predicate) == null) continue;
				
				Entity subj = s.Subject;
				if (!(subj is BNode)) continue; // it would be nice to exclude variables, but we've already converted bnodes to variables
				
				BNode head = (BNode)subj;
				
				ArrayList argList = new ArrayList();
				
				while (true) {
					Resource[] objs = b.SelectObjects(subj, entRDFFIRST);
					if (objs.Length == 0) break; // if this is the first iteration, then we're just not looking at a list
												 // on later iterations, the list must not be well-formed, so we'll just
												 // stop reading it here
					
					argList.Add(objs[0]); // assume a properly formed list
					b.Remove(new Statement(subj, entRDFFIRST, null));
					
					Resource[] rests = b.SelectObjects(subj, entRDFREST);
					if (rests.Length == 0) break; // not well formed
					b.Remove(new Statement(subj, entRDFREST, null));
					if (rests[0] == entRDFNIL) break; // that's the end of the list
					if (!(rests[0] is Entity)) break; // also not well formed
					
					subj = (Entity)rests[0];
				}
				
				if (argList.Count > 0)
					callArgs[head] = argList.ToArray(typeof(Resource));
			}

			return b1.ToArray();
		}
		
		private static void AddSequent(Hashtable cases, Sequent s) {
			object key = s.head.Predicate;
			if (key is Variable) key = "WILDCARD";
			ArrayList list = (ArrayList)cases[key];
			if (list == null) {
				list = new ArrayList();
				cases[key] = list;
			}
			list.Add(s);
		}

		// Lastly, we have special and built-in predicates.

		private static RdfRelation FindUserPredicate(Entity predicate) {
			if (predicate.Uri == null) return null;
			if (builtInRelations.ContainsKey(predicate.Uri))
				return (RdfRelation)builtInRelations[predicate.Uri];
			return null;
		}
	}
}
