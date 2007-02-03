using System;

using SemWeb;

namespace SemWeb.Inference {
	public abstract class RdfRelation : SemWeb.Query.RdfFunction {
		public sealed override Resource Evaluate (Resource[] args) {
			Resource r = null;
			if (Evaluate(args, ref r))
				return r;
			return null;
		}

		public abstract bool Evaluate(Resource[] args, ref Resource @object);
	}
	
	namespace Relations {
		public abstract class MathUnaryRelation : RdfRelation {
			protected abstract Decimal EvaluateForward(Decimal left);
			protected abstract Decimal EvaluateReverse(Decimal right);
		
			public override bool Evaluate(Resource[] args, ref Resource @object) {
				if (args.Length != 1) return false;
				if (args[0] == null && @object == null) return false;
				if ((args[0] != null && !(args[0] is Literal)) || (@object != null && !(@object is Literal))) return false;
				
				if (args[0] == null) {
					Decimal right = (Decimal)Convert.ChangeType( ((Literal)@object).ParseValue() , typeof(Decimal) );
					Decimal left = EvaluateReverse(right);
					if (left == Decimal.MinValue) return false;
					args[0] = Literal.FromValue(left);
					return true;
				} else {
					Decimal left = (Decimal)Convert.ChangeType( ((Literal)args[0]).ParseValue() , typeof(Decimal) );
					Decimal right  = EvaluateForward(left);
					if (@object == null) {
						@object = Literal.FromValue(right);
						return true;
					} else {
						Decimal right2 = (Decimal)Convert.ChangeType( ((Literal)@object).ParseValue() , typeof(Decimal) );
						return right == right2;
					}
				}
			}
		}
	
		public class MathAbsoluteValueRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#absoluteValue"; } }
			protected override Decimal EvaluateForward(Decimal left) { return left >= 0 ? left : -left; }
			protected override Decimal EvaluateReverse(Decimal right) { return Decimal.MinValue; }
		}
		public class MathCosRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#cos"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)Math.Cos((double)left); }
			protected override Decimal EvaluateReverse(Decimal right) { return (Decimal)Math.Acos((double)right); }
		}
		public class MathDegreesRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#degrees"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)((double)left * Math.PI / 180.0); }
			protected override Decimal EvaluateReverse(Decimal right) { return (Decimal)((double)right * 180.0 / Math.PI); }
		}
		public class MathEqualToRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#equalTo"; } }
			protected override Decimal EvaluateForward(Decimal left) { return left; }
			protected override Decimal EvaluateReverse(Decimal right) { return right; }
		}
		public class MathNegationRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#negation"; } }
			protected override Decimal EvaluateForward(Decimal left) { return -left; }
			protected override Decimal EvaluateReverse(Decimal right) { return -right; }
		}
		public class MathRoundedRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#rounded"; } }
			protected override Decimal EvaluateForward(Decimal left) { return Decimal.Floor(left); }
			protected override Decimal EvaluateReverse(Decimal right) { return Decimal.MinValue; }
		}
		public class MathSinRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#sin"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)Math.Sin((double)left); }
			protected override Decimal EvaluateReverse(Decimal right) { return (Decimal)Math.Asin((double)right); }
		}
		public class MathSinhRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#sinh"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)Math.Sinh((double)left); }
			protected override Decimal EvaluateReverse(Decimal right) { return Decimal.MinValue; }
		}
		public class MathTanRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#tan"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)Math.Tan((double)left); }
			protected override Decimal EvaluateReverse(Decimal right) { return (Decimal)Math.Atan((double)right); }
		}
		public class MathTanhRelation : MathUnaryRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#tanh"; } }
			protected override Decimal EvaluateForward(Decimal left) { return (Decimal)Math.Tanh((double)left); }
			protected override Decimal EvaluateReverse(Decimal right) { return Decimal.MinValue; }
		}

		public abstract class MathPairRelation : RdfRelation {
			protected abstract Decimal Evaluate(Decimal left, Decimal right);
		
			public override bool Evaluate(Resource[] args, ref Resource @object) {
				if (args.Length != 2) return false;
				if (args[0] == null || !(args[0] is Literal)) return false;
				if (args[1] == null || !(args[1] is Literal)) return false;
				Decimal left = (Decimal)Convert.ChangeType( ((Literal)args[0]).ParseValue() , typeof(Decimal) );
				Decimal right = (Decimal)Convert.ChangeType( ((Literal)args[1]).ParseValue() , typeof(Decimal) );
				Resource newvalue = Literal.FromValue(Evaluate(left, right));
				if (@object == null) {
					@object = newvalue;
					return true;
				} else {
					return @object.Equals(newvalue);
				}
			}
		}

		public class MathAtan2Relation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#atan2"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return (Decimal)Math.Atan2((double)left, (double)right); }
		}
		public class MathDifferenceRelation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#difference"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return left - right; }
		}
		public class MathExponentiationRelation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#exponentiation"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return (Decimal)Math.Pow((double)left, (double)right); }
		}
		public class MathIntegerQuotientRelation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#integerQuotient"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return Decimal.Floor((left / right)); }
		}
		public class MathQuotientRelation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#quotient"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return left / right; }
		}
		public class MathRemainderRelation : MathPairRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#remainder"; } }
			protected override Decimal Evaluate(Decimal left, Decimal right) { return left % right; }
		}

		public abstract class MathListRelation : RdfRelation {
			protected abstract Decimal InitialValue { get; }
			protected abstract Decimal Combine(Decimal left, Decimal right);
		
			public override bool Evaluate(Resource[] args, ref Resource @object) {
				Decimal sum = InitialValue;
				foreach (Resource r in args) {
					if (r == null) return false;
					if (!(r is Literal)) return false;
					Decimal v = (Decimal)Convert.ChangeType( ((Literal)r).ParseValue() , typeof(Decimal) );
					sum = Combine(sum, v);
				}
				Resource newvalue = Literal.FromValue(sum);
				if (@object == null) {
					@object = newvalue;
					return true;
				} else {
					return @object.Equals(newvalue);
				}
			}
		}
	
		public class MathSumRelation : MathListRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#sum"; } }
			protected override Decimal InitialValue { get { return Decimal.Zero; } }
			protected override Decimal Combine(Decimal left, Decimal right) { return left + right; }
		}
		public class MathProductRelation : MathListRelation {
			public override string Uri { get { return "http://www.w3.org/2000/10/swap/math#product"; } }
			protected override Decimal InitialValue { get { return Decimal.One; } }
			protected override Decimal Combine(Decimal left, Decimal right) { return left * right; }
		}
		
	}
	
}
