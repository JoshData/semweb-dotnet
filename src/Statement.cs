using System;

namespace SemWeb {
	public struct Statement {
		private Entity s;
		private Entity p;
		private Resource o;
		private Entity m;
		
		public Statement(Entity subject, Entity predicate, Resource @object)
		: this(subject, predicate, @object, null) {
		}
		
		public Statement(Entity subject, Entity predicate, Resource @object, Entity meta) {
		  s = subject;
		  p = predicate;
		  o = @object;
		  m = meta;
		}
		
		public Entity Subject { get { return s; } }
		public Entity Predicate { get { return p; } }
		public Resource Object { get { return o; } }
		
		public Entity Meta { get { return m; } }
		
		internal bool AnyNull {
			get {
				return Subject == null || Predicate == null || Object == null;
			}
		}
		
		public override string ToString() {
			string ret = "";
			if (Subject != null) ret += "<" + Subject + "> "; else ret += "? ";
			if (Predicate != null) ret += "<" + Predicate + "> "; else ret += "? ";
			if (Object != null) ret += "<" + Object + ">"; else ret += "?";
			return ret + ".";
		}
		
		public override bool Equals(object other) {
			return (Statement)other == this;
		}
		
		public override int GetHashCode() {
			int ret = 0;
			if (s != null) ret = unchecked(ret + s.GetHashCode());
			if (p != null) ret = unchecked(ret + p.GetHashCode());
			if (o != null) ret = unchecked(ret + o.GetHashCode());
			return ret;
		}
		
		public static bool operator ==(Statement a, Statement b) {
			if ((a.Subject == null) != (b.Subject == null)) return false;
			if ((a.Predicate == null) != (b.Predicate == null)) return false;
			if ((a.Object == null) != (b.Object == null)) return false;
			if (a.Subject != null && !a.Subject.Equals(b.Subject)) return false;
			if (a.Predicate != null && !a.Predicate.Equals(b.Predicate)) return false;
			if (a.Object != null && !a.Object.Equals(b.Object)) return false;
			return true;
		}
		public static bool operator !=(Statement a, Statement b) {
			return !(a == b);
		}
	}
}
