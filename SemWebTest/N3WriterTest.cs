#if DOTNET2
using NUnit.Framework;
using SemWeb.Constants;
using NUnit.Framework.SyntaxHelpers;

namespace SemWeb
{
    [TestFixture]
    public class N3WriterTest : TurtleWriterTest
    {
        public virtual TurtleWriter CreateInstance()
        {
            return new N3Writer(writer);
        }

        [Test]
        public void TestFormulaSingleOccurence()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, statementYId));
            instance.Close();

            string expected = "{<S> <p> <X>.} => {<S> <p> <Y>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestFormulaWithMultipleStatementsSingleOccurence()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode formulaId = new BNode();
            BNode statementZId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", formulaId));
            instance.Add(new Statement("S", "p", (Entity)"Y", formulaId));
            instance.Add(new Statement("S", "p", (Entity)"Z", statementZId));
            instance.Add(new Statement(formulaId, Predicate.LogImplies, statementZId));
            instance.Close();

            string expected = "{\n <S> <p> <X>.\n <S> <p> <Y>.\n} => {<S> <p> <Z>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestFormulaMultipleOccurences()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode statementId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"O", statementId));
            instance.Add(new Statement("MyTheory", "contains", statementId));
            instance.Add(new Statement(statementId, "is", (Entity)"True"));
            instance.Close();

            string expected = "<MyTheory> <contains> {<S> <p> <O>.}.\n{<S> <p> <O>.} <is> <True>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestFormulaWithinFormula()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            BNode statementImplId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, statementYId, statementImplId));
            instance.Add(new Statement(statementImplId, Predicate.LogImplies, (Entity)"Truth"));
            instance.Close();

            string expected = "{\n{<S> <p> <X>.}\n=>\n{<S> <p> <Y>.}.\n} => <Truth>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestFormulaMixedWithRegularStatements()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            instance.Add(new Statement("S", "b", (Entity)"B"));
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, statementYId));
            instance.Close();

            string expected = "<S> <b> <B>.\n{<S> <p> <X>.} => {<S> <p> <Y>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestNamespaces()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            instance.Add(new Statement("http://example.org/A", Predicate.RdfType, (Entity)"X"));
            instance.Namespaces.AddNamespace("http://example.org/", "ex");
            instance.Close();

            string expected = "@prefix ex: <http://example.org/>.\nex:A a <X>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestListWithZeroElements()
        {
            N3Writer instance = (N3Writer)CreateInstance();

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
            N3Writer instance = (N3Writer)CreateInstance();

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
            N3Writer instance = (N3Writer)CreateInstance();

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

        [Test]
        public void TestListWithThreeElementsWithLastUnbound()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode listId = new BNode();
            BNode listTwoId = new BNode();
            BNode listThreeId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfType, (Entity)"List"));
            instance.Add(new Statement(listId, Predicate.RdfFirst, (Entity)"A"));
            instance.Add(new Statement(listId, Predicate.RdfRest, listTwoId));
            instance.Add(new Statement(listTwoId, Predicate.RdfFirst, (Entity)"B"));
            instance.Add(new Statement(listTwoId, Predicate.RdfRest, listThreeId));
            instance.Add(new Statement(listThreeId, Predicate.RdfFirst, (Entity)"C"));
            instance.Close();

            string expected = @"\(<A> <B> <C> _:bnode\d+\) a <List>.";
            Assert.That(writer.ToString(), Text.Matches(expected));
        }

        [Test]
        public void TestListWithFormulaElements()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            BNode statementZId = new BNode();
            instance.Add(new Statement("S", "p", (Entity)"X", statementXId));
            instance.Add(new Statement("S", "p", (Entity)"Y", statementYId));
            instance.Add(new Statement("S", "p", (Entity)"Z", statementZId));

            BNode listId = new BNode();
            BNode listTwoId = new BNode();
            BNode listThreeId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfType, (Entity)"List"));
            instance.Add(new Statement(listId, Predicate.RdfFirst, statementXId));
            instance.Add(new Statement(listId, Predicate.RdfRest, listTwoId));
            instance.Add(new Statement(listTwoId, Predicate.RdfFirst, statementYId));
            instance.Add(new Statement(listTwoId, Predicate.RdfRest, listThreeId));
            instance.Add(new Statement(listThreeId, Predicate.RdfFirst, statementZId));
            instance.Add(new Statement(listThreeId, Predicate.RdfRest, Identifier.RdfNil));
            instance.Close();

            string expected = "({<S> <p> <X>.}\n {<S> <p> <Y>.}\n {<S> <p> <Z>.}) a <List>.";
            Assert.AreEqual(expected, writer.ToString());
        }

        [Test]
        public void TestFormulaWithList()
        {
            N3Writer instance = (N3Writer)CreateInstance();

            BNode listId = new BNode();
            BNode listTwoId = new BNode();
            BNode listThreeId = new BNode();
            instance.Add(new Statement(listId, Predicate.RdfFirst, (Entity)"A"));
            instance.Add(new Statement(listId, Predicate.RdfRest, listTwoId));
            instance.Add(new Statement(listTwoId, Predicate.RdfFirst, (Entity)"B"));
            instance.Add(new Statement(listTwoId, Predicate.RdfRest, listThreeId));
            instance.Add(new Statement(listThreeId, Predicate.RdfFirst, (Entity)"C"));
            instance.Add(new Statement(listThreeId, Predicate.RdfRest, Identifier.RdfNil));

            BNode statementXId = new BNode();
            BNode statementYId = new BNode();
            instance.Add(new Statement(listId, "p", (Entity)"X", statementXId));
            instance.Add(new Statement(listId, "p", (Entity)"Y", statementYId));
            instance.Add(new Statement(statementXId, Predicate.LogImplies, statementYId));
            instance.Close();

            string expected = "{(<A> <B> <C>) <p> <X>.} => {(<A> <B> <C>) <p> <Y>.}.";
            Assert.AreEqual(expected, writer.ToString());
        }
    }
}
#endif