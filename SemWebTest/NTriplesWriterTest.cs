using System.IO;
using NUnit.Framework;
using SemWeb.Constants;

namespace SemWeb
{
    [TestFixture]
    public class NTriplesWriterTest
    {
        StringWriter writer;

        [SetUp]
        public void SetUp()
        {
            writer = new StringWriter();
        }

        [Test]
        public void TestWriteSimpleStatements()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"X"));
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"Y"));
            instance.Add(new Statement("S", "o", (Entity)"Z"));
            instance.Add(new Statement("P", "o", (Literal)"s"));
            instance.Close();

            string expected = "<S> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <X>.\n" +
                              "<S> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <Y>.\n" +
                              "<S> <o> <Z>.\n" +
                              "<P> <o> \"s\".\n";

            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteStatementWithAnonymousBNode()
        {
            BNode bNode = new BNode();
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement(bNode, "b", (Entity)"C"));
            instance.Close();

            string expected = bNode + " <b> <C>.\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteStatementWithLiteral()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement("A", "b", new Literal("C")));
            instance.Close();

            string expected = "<A> <b> \"C\".\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteStatementWithLiteralHavingLanguage()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement("A", "b", new Literal("C", "en", "http://www.w3.org/2001/XMLSchema#string")));
            instance.Close();

            string expected = "<A> <b> \"C\"@en.\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteStatementWithLiteralHavingDataType()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Add(new Statement("A", "b", Literal.FromValue("C")));
            instance.Close();

            string expected = "<A> <b> \"C\"^^<http://www.w3.org/2001/XMLSchema#string>.\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteEscapesAllResources()
        {
            // note: should test for unicode characters >= \U0001000,
            //       but Mono on Linux does not support this. (works in Windows .Net)
            string subject = "http://\u00E9xampl\u00E8.org/\t\n\u000B\u000C\r\u000E\u0012\u001F";

            NTriplesWriter instance = new NTriplesWriter(writer);
            instance.Namespaces.AddNamespace("http://\u00E9xampl\u00E8.org/", "ex");
            instance.Add(new Statement(subject, "\uABCD", (Literal)"\u0020\u0021\u000A"));
            instance.Close();

            string expected = @"<http://\u00E9xampl\u00E8.org/\t\n\u000B\u000C\r\u000E\u0012\u001F> <\uABCD> " + "\" !\\n\".\n";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestWriteFormula()
        {
            NTriplesWriter instance = new NTriplesWriter(writer);

            BNode statementId = new BNode("s");
            instance.Add(new Statement("A", "b", (Entity)"C", statementId));
            instance.Close();

            string expected = "_:s <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement>.\n" +
                              "_:s <http://www.w3.org/1999/02/22-rdf-syntax-ns#subject> <A>.\n" +
                              "_:s <http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate> <b>.\n" +
                              "_:s <http://www.w3.org/1999/02/22-rdf-syntax-ns#object> <C>.\n";
            Assert.AreEqual(expected, writer.ToString());
        }
    }
}