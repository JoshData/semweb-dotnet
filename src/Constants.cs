namespace SemWeb.Constants
{
    /// <summary>
    /// Common RDF namespaces.
    /// </summary>
    public static class Namespace
    {
        public const string RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string LOG = "http://www.w3.org/2000/10/swap/log#";
    }

    /// <summary>
    /// Common RDF identifiers.
    /// </summary>
    public static class Identifier
    {
        public static readonly Entity RdfNil = Namespace.RDF + "nil";
    }

    /// <summary>
    /// Common RDF predicates.
    /// </summary>
    public class Predicate
    {
        public static readonly Entity RdfType = Namespace.RDF + "type";
        public static readonly Entity RdfFirst = Namespace.RDF + "first";
        public static readonly Entity RdfRest = Namespace.RDF + "rest";

        public static readonly Entity LogImplies = Namespace.LOG + "implies";
    }
}