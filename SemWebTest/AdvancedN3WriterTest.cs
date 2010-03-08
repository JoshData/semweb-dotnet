#if DOTNET2
using System.IO;
using NUnit.Framework;
using SemWeb.Constants;

namespace SemWeb
{
    [TestFixture]
    public class AdvancedN3WriterTest
    {
        StringWriter writer;

        [SetUp]
        public void SetUp()
        {
            writer = new StringWriter();
        }

        [Test]
        public void TestSimpleStatements()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"X"));
            instance.Add(new Statement("S", Predicate.RdfType, (Entity)"Y"));
            instance.Add(new Statement("S", "o", (Entity)"Z"));
            instance.Add(new Statement("P", "o", (Literal)"s"));
            instance.Close();

            string expected = "<P> <o> \"s\".\n" +
                           "<S> a <X>,\n" +
                           "        <Y>;\n" +
                           "    <o> <Z>.";

            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestStatementsWithNamedBNode()
        {
            BNode bNode = new BNode("node");
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);
            instance.Add(new Statement(bNode, Predicate.RdfType, (Entity)"X"));
            instance.Close();

            string expected = bNode + " a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestStatementWithAnonymousBNode()
        {
            BNode bNode = new BNode();
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);
            instance.Add(new Statement(bNode, Predicate.RdfType, (Entity)"X"));
            instance.Close();

            string expected = bNode + " a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestStatementsWithVariable()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);
            instance.Add(new Statement(new Variable("var"), Predicate.RdfType, (Entity)"X"));
            instance.Close();

            string expected = "?var a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestEscaping()
        {
			// note: should test for unicode characters >= \U0001000,
			//       but Mono on Linux does not support this. (works in Windows .Net)
            string subject = "http://\u00E9xampl\u00E8.org/\t\n\u000B\u000C\r\u000E\u0012\u001F";

            AdvancedN3Writer instance = new AdvancedN3Writer(writer);
            instance.Namespaces.AddNamespace("http://\u00E9xampl\u00E8.org/", "ex");
            instance.Add(new Statement(subject, "\uABCD", (Literal)"\u0020\u0021\u000A"));
            instance.Close();

            string expected = @"@prefix ex: <http://\u00E9xampl\u00E8.org/>." + '\n'
                            + @"ex:\t\n\u000B\u000C\r\u000E\u0012\u001F <\uABCD> " + "\" !\\n\".";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestReifiedStatementsSingleOccurence()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, (Entity)statementYId));
            instance.Close();

            string expected = "{<S> <p> <X>.} => {<S> <p> <Y>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestReifiedStatementsMultipleOccurences()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode statementId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"O", statementId));
            instance.Add(new Statement("MyTheory", "contains", statementId));
            instance.Add(new Statement(statementId, "is", (Entity)"True"));
            instance.Close();

            string expected = "<MyTheory> <contains> {<S> <p> <O>.}.\n{<S> <p> <O>.} <is> <True>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestReifiedReifiedStatements()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            BNode statementImplId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, (Entity)statementYId, statementImplId));
            instance.Add(new Statement(statementImplId, Predicate.LogImplies, (Entity)"Truth"));
            instance.Close();

            string expected = "{\n{<S> <p> <X>.}\n=>\n{<S> <p> <Y>.}.\n} => <Truth>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestReifiedAndNonReifiedStatements()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            instance.Add(new Statement("S", "b", (Entity)"B"));
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, (Entity)statementYId));
            instance.Close();

            string expected = "<S> <b> <B>.\n{<S> <p> <X>.} => {<S> <p> <Y>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestNamespaces()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            instance.Add(new Statement("http://example.org/A", Predicate.RdfType, (Entity)"X"));
            instance.Namespaces.AddNamespace("http://example.org/", "ex");
            instance.Close();

            string expected = "@prefix ex: <http://example.org/>.\nex:A a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestListWithZeroElements()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode listId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfType, (Entity)"List"));
            instance.Add(new Statement(listId, Predicate.RdfFirst, Identifier.RdfNil));
            instance.Add(new Statement(listId, Predicate.RdfRest, Identifier.RdfNil));
            instance.Close();

            string expected = "() a <List>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestListWithOneElement()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode listId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfType, (Entity)"List"));
            instance.Add(new Statement(listId, Predicate.RdfFirst, (Entity)"A"));
            instance.Add(new Statement(listId, Predicate.RdfRest, Identifier.RdfNil));
            instance.Close();

            string expected = "(<A>) a <List>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestListWithThreeElements()
        {
            AdvancedN3Writer instance = new AdvancedN3Writer(writer);

            BNode listId = new BNode();
            BNode listTwoId = new BNode();
            BNode listThreeId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfType, (Entity)"List"));
            instance.Add(new Statement(listId, Predicate.RdfFirst, (Entity)"A"));
            instance.Add(new Statement(listId, Predicate.RdfRest, listTwoId));
            instance.Add(new Statement(listTwoId, Predicate.RdfFirst, (Entity)"B"));
            instance.Add(new Statement(listTwoId, Predicate.RdfRest, listThreeId));
            instance.Add(new Statement(listThreeId, Predicate.RdfFirst, (Entity)"C"));
            instance.Add(new Statement(listThreeId, Predicate.RdfRest, Identifier.RdfNil));
            instance.Close();

            string expected = "(<A> <B> <C>) a <List>.";
            Assert.AreEqual(expected, writer.ToString());
        }
    }
}
#endif