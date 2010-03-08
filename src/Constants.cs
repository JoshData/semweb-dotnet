namespace SemWeb.Constants
{
	public static class Prefix
	{
        public const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string LOG = "http://www.w3.org/2000/10/swap/log#";
 	}
	
	public static class Identifier
    {
        public static readonly Entity RdfNil = Prefix.RDF + "nil";
    }
	
	public class Predicate
    {
        public static readonly Entity RdfType = Prefix.RDF + "type";
        public static readonly Entity RdfFirst = Prefix.RDF + "first";
        public static readonly Entity RdfRest = Prefix.RDF + "rest";

        public static readonly Entity LogImplies = Prefix.LOG + "implies";
	}
}