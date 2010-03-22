#if DOTNET2
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SemWeb.Constants;

namespace SemWeb
{
    [TestFixture]
    public class TurtleWriterTest
    {
        protected StringWriter writer;

        [SetUp]
        public virtual void SetUp()
        {
            writer = new StringWriter();
        }

        public virtual TurtleWriter CreateInstance()
        {
            return new TurtleWriter(writer);
        }

        [Test]
        public virtual void TestSimpleStatements()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"X"));
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"Y"));
            instance.Add(new Statement("S", "o", (Entity)"Z"));
            instance.Add(new Statement("P", "o", (Literal)"s"));
            instance.Close();

            string expected = "<S> a <X>,\n" +
                              "\t\t<Y>;\n" +
                              "\t<o> <Z>.\n" +
                              "<P> <o> \"s\".";

            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestStatementsWithNamedBNode()
        {
            BNode bNode = new BNode("node");
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement(bNode, "b", (Entity)"C"));
            instance.Close();

            string expected = bNode + " <b> <C>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestStatementWithAnonymousBNode()
        {
            BNode bNode = new BNode();
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement(bNode, "b", (Entity)"C"));
            instance.Close();

            string expected = bNode + " <b> <C>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestStatementsWithNamedVariable()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement(new Variable("myvar"), "b", (Entity)"C"));
            instance.Close();

            string expected = "?myvar <b> <C>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestStatementsWithAnonymousVariable()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement(new Variable("var"), "b", (Entity)"C"));
            instance.Close();

            string expected = "?var <b> <C>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteral()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement("A", "b", new Literal("C")));
            instance.Close();

            string expected = "<A> <b> \"C\".\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralHavingLanguage()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", new Literal("C", "en", "http://www.w3.org/2001/XMLSchema#string")));
            instance.Close();

            string expected = "<A> <b> \"C\"@en.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralHavingDataType()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue("C")));
            instance.Close();

            string expected = "<A> <b> \"C\"^^<http://www.w3.org/2001/XMLSchema#string>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralHavingDataTypeUsingNamespace()
        {
            TurtleWriter instance = CreateInstance();
            instance.Namespaces.AddNamespace("http://www.w3.org/2001/XMLSchema#", "xsd");
            instance.Add(new Statement("A", "b", Literal.FromValue("C")));
            instance.Close();

            string expected = "@prefix xsd: <http://www.w3.org/2001/XMLSchema#>.\n<A> <b> \"C\"^^xsd:string.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralOfTypeInteger()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue(1)));
            instance.Close();

            string expected = "<A> <b> 1.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralOfTypeDouble()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue(1.2)));
            instance.Close();

            string expected = "<A> <b> 1.2.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralOfTypeDecimal()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue(1.2d)));
            instance.Close();

            string expected = "<A> <b> 1.2.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralOfTypeFloat()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue(1.2f)));
            instance.Close();

            string expected = "<A> <b> 1.2.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteStatementWithLiteralOfTypeBoolean()
        {
            TurtleWriter instance = CreateInstance();
            instance.Add(new Statement("A", "b", Literal.FromValue(true)));
            instance.Close();

            string expected = "<A> <b> true.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteEscapesAllResources()
        {
			// note: should test for unicode characters >= \U0001000,
			//       but Mono on Linux does not support this. (works in Windows .Net)
            string subject = "http://\u00E9xampl\u00E8.org/\t\n\u000B\u000C\r\u000E\u0012\u001F";

            TurtleWriter instance = CreateInstance();
            instance.Namespaces.AddNamespace("http://\u00E9xampl\u00E8.org/", "ex");
            instance.Add(new Statement(subject, "\uABCD", (Literal)"\u0020\u0021\u000A"));
            instance.Close();

            string expected = @"@prefix ex: <http://\u00E9xampl\u00E8.org/>." + '\n'
                            + @"ex:\t\n\u000B\u000C\r\u000E\u0012\u001F <\uABCD> " + "\" !\\n\".";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteWithNamespaces()
        {
            TurtleWriter instance = CreateInstance();
            instance.Namespaces.AddNamespace("http://example.org/", "ex");
            instance.Add(new Statement("http://example.org/A", Predicate.RdfType, (Entity)"X"));
            instance.Close();

            string expected = "@prefix ex: <http://example.org/>.\nex:A a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public virtual void TestWriteFormulaWithOneStatement()
        {
            TurtleWriter instance = CreateInstance();

            BNode statementId = new BNode("s");
            instance.Add(new Statement("A", "b", (Entity)"C", statementId));
            instance.Close();

            string expected = "_:s a <http://www.w3.org/2000/10/swap/log#Formula>;\n" +
                              "\t<http://www.w3.org/2000/10/swap/log#includes> _:bnode.\n" +
                              "_:bnode a <http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#subject> <A>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate> <b>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#object> <C>.";
            Assert.AreEqual(expected, Regex.Replace(writer.ToString(), @"_:bnode\d+", "_:bnode"));
        }

        [Test]
        public void TestWriteFormulaWithTwoStatements()
        {
            NTriplesWriter instance = CreateInstance();

            BNode statementId = new BNode("s");
            instance.Add(new Statement("A", "b", (Entity)"C", statementId));
            instance.Add(new Statement("D", "e", (Entity)"F", statementId));
            instance.Close();

            // should not repeat "_:s a log:Formula"
            string expected = "_:s a <http://www.w3.org/2000/10/swap/log#Formula>;\n" +
                              "\t<http://www.w3.org/2000/10/swap/log#includes> _:bnode.\n" +
                              "_:bnode a <http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#subject> <A>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate> <b>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#object> <C>.\n" +
                              "_:s <http://www.w3.org/2000/10/swap/log#includes> _:bnode.\n" +
                              "_:bnode a <http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#subject> <D>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate> <e>;\n" +
                              "\t<http://www.w3.org/1999/02/22-rdf-syntax-ns#object> <F>.";
            Assert.AreEqual(expected, Regex.Replace(writer.ToString(), @"_:bnode\d+", "_:bnode"));
        }
    }
}
#endif