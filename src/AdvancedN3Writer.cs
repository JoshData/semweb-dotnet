#if DOTNET2
using System;
using System.Collections.Generic;
using System.IO;
using SemWeb.Constants;

namespace SemWeb
{
    /// <summary>
    /// N3 writer that handles formulas (reficiations) and lists.
    /// </summary>
    public class AdvancedN3Writer : RdfWriter
    {
        /// <summary>Dictionary of common predicate abbreviations.</summary>
        private static readonly Dictionary<Entity, string> PredicateAbbreviations;

        /// <summary>Whether the writer is empty.</summary>
        private bool m_Empty = true;
        /// <summary>Output writer.</summary>
        private TextWriter m_Writer;
        /// <summary>Namespace manager.</summary>
        private NamespaceManager m_NamespaceManager = new NamespaceManager();

        /// <summary>All non-reified, non-list statements, keyed by subject.</summary>
        private Lookup<Entity, Statement> m_StatementsBySubject = new Lookup<Entity, Statement>();
        /// <summary>All reified statements, keyed by statement ID.</summary>
        private Lookup<Entity, Statement> m_ReifiedStatementsById = new Lookup<Entity, Statement>();
        /// <summary>All list values, keyed by list ID.</summary>
        private Dictionary<Entity, Resource> m_ValuesByListId = new Dictionary<Entity, Resource>();
        /// <summary>All list tail IDs, keyed by list ID.</summary>
        private Dictionary<Entity, Entity> m_TailsByListId = new Dictionary<Entity, Entity>();

        /// <summary>
        /// Initializes the <see cref="AdvancedN3Writer"/> class.
        /// </summary>
        static AdvancedN3Writer()
        {
            PredicateAbbreviations = new Dictionary<Entity, string>();
            PredicateAbbreviations.Add(Predicate.RdfType, "a");
            PredicateAbbreviations.Add(Predicate.LogImplies, "=>");
        }

        /// <summary>
        /// Initializes a new <see cref="AdvancedN3Writer"/> instance.
        /// </summary>
        /// <param name="writer">The writer.</param>
        public AdvancedN3Writer(TextWriter writer)
        {
            if(writer == null)
                throw new ArgumentNullException("writer");
            m_Writer = writer;
        }

        /// <summary>
        /// Gets the namespace manager.
        /// </summary>
        /// <value>The namespace manager.</value>
        public override NamespaceManager Namespaces
        {
            get { return m_NamespaceManager; }
        }

        /// <summary>
        /// Adds the specified statement.
        /// </summary>
        /// <param name="statement">The statement.</param>
        public override void Add(Statement statement)
        {
            if (statement.Meta == Statement.DefaultMeta)
            {
                Entity predicate = statement.Predicate;
                if(predicate == Predicate.RdfFirst)
                    m_ValuesByListId.Add(statement.Subject, statement.Object);
                else if (predicate == Predicate.RdfRest)
                    m_TailsByListId.Add(statement.Subject, (Entity)statement.Object);
                else
                    m_StatementsBySubject.Add(statement.Subject, statement);
            }
            else
            {
                m_ReifiedStatementsById.Add(statement.Meta, statement);
            }
        }

        /// <summary>
        /// Removes the specified statement, marking it as written.
        /// </summary>
        /// <param name="statement">The statement.</param>
        protected void Remove(Statement statement)
        {
            m_StatementsBySubject.Remove(statement.Subject, statement);
            m_ReifiedStatementsById.Remove(statement.Meta, statement);
        }

        /// <summary>
        /// Closes N3 output.
        /// </summary>
        public override void Close()
        {
            WriteNamespaces();
            WriteStatements();
            m_Writer.Close();
        }

        /// <summary>
        /// Writes the namespace declarations.
        /// </summary>
        protected virtual void WriteNamespaces()
        {
            foreach (string prefix in m_NamespaceManager.GetPrefixes())
            {
                Write("@prefix ");
                WriteEscaped(prefix);
                Write(": <");
                WriteEscaped(m_NamespaceManager.GetNamespace(prefix));
                Write(">.\n");
            }
        }

        /// <summary>
        /// Writes all statements to the output writer, grouped by subject.
        /// </summary>
        protected virtual void WriteStatements()
        {
            var sortedSubjects = new List<Entity>(m_StatementsBySubject.Keys);
            sortedSubjects.Sort();

            while (sortedSubjects.Count > 0)
            {
                var subject = sortedSubjects[0];
                if (m_StatementsBySubject.Keys.Contains(subject))
                {
                    ICollection<Statement> statements = m_StatementsBySubject[subject];
                    WriteStatements(statements, subject);
                }
                else
                {
                    sortedSubjects.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Writes the specified statements (sharing a subject) to the output writer.
        /// </summary>
        /// <param name="statements">The statements.</param>
        /// <param name="subject">The subject of the statements.</param>
        protected virtual void WriteStatements(ICollection<Statement> statements, Entity subject)
        {
            WriteLine();
            WriteEntity(subject);

            List<Statement> sortedStatements = new List<Statement>(statements);
            sortedStatements.Sort(
                CompareStatementBodies);
            Write(' ');
            WriteStatementBodies(sortedStatements);
            Write(".");
        }

        /// <summary>
        /// Compares the statement bodies for sorting.
        /// </summary>
        /// <param name="x">The first statement.</param>
        /// <param name="y">The second statement.</param>
        /// <returns>A number indicating the order of <paramref name="x"/> and <paramref name="y"/></returns>
        protected virtual int CompareStatementBodies(Statement x, Statement y)
        {
            int predicateComp = x.Predicate.CompareTo(y.Predicate);
            return predicateComp != 0 ? predicateComp : x.Object.CompareTo(y.Object);
        }

        /// <summary>
        /// Writes the bodies of the specified statements (sharing a subject) to the output writer.
        /// </summary>
        /// <param name="statements">The statements.</param>
        protected virtual void WriteStatementBodies(List<Statement> statements)
        {
            Entity previousPredicate = null;
            foreach (Statement statement in statements)
            {
                Remove(statement);

                if(statement.Predicate != previousPredicate)
                {
                    if(previousPredicate != null)
                    {
                        Write(";\n    ");
                    }
                    WritePredicate(statement.Predicate);
                    Write(' ');
                }
                else
                {
                    Write(",\n        ");
                }
                WriteResource(statement.Object);

                previousPredicate = statement.Predicate;
            }
        }

        /// <summary>
        /// Writes the specified predicate to the output writer.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        protected virtual void WritePredicate(Entity predicate)
        {
            string abbreviation;
            if (PredicateAbbreviations.TryGetValue(predicate, out abbreviation))
                Write(abbreviation);
            else
                WriteEntity(predicate);
        }

        /// <summary>
        /// Writes the specified resource to the output writer.
        /// </summary>
        /// <param name="resource">The resource.</param>
        protected virtual void WriteResource(Resource resource)
        {
            if(resource is Entity)
                WriteEntity((Entity)resource);
            else
                WriteLiteral((Literal)resource);
        }

        /// <summary>
        /// Writes the literal to the output writer.
        /// </summary>
        /// <param name="literal">The literal.</param>
        protected virtual void WriteLiteral(Literal literal)
        {
            Write('"');
            WriteEscaped(literal.Value);
            Write('"');
        }

        /// <summary>
        /// Writes the specified entity to the output writer.
        /// </summary>
        /// <param name="entity">The entity.</param>
        protected virtual void WriteEntity(Entity entity)
        {
            if (IsFormulaId(entity))
                WriteFormula(entity);
            else if(IsListId(entity))
                WriteList(entity);
            else
                WriteEntityByUri(entity);
        }

        /// <summary>
        /// Writes the URI of the specified entity to the output writer.
        /// </summary>
        /// <param name="entity">The entity.</param>
        protected virtual void WriteEntityByUri(Entity entity)
        {
            if (entity is BNode)
            {
                Write(entity is Variable ? "?" : "_:");
                WriteEscaped(((BNode)entity).LocalName ?? ("bnode" + Math.Abs(entity.GetHashCode())));
            }
            else
            {
                string prefix, localname;
                if (m_NamespaceManager.Normalize(entity.Uri, out prefix, out localname))
                {
                    WriteEscaped(prefix);
                    Write(':');
                    WriteEscaped(localname);
                }
                else
                {
                    Write('<');
                    WriteEscaped(entity.Uri);
                    Write('>');
                }
            }
        }

        /// <summary>
        /// Writes the formula identified by the specified ID to the output writer.
        /// </summary>
        /// <param name="formulaId">The formula ID.</param>
        protected virtual void WriteFormula(Entity formulaId)
        {
            Statement formula = FirstOrDefault(m_ReifiedStatementsById[formulaId]);

            Write(IsFormulaId(formula.Subject) ? "{\n" : "{");
            WriteEntity(formula.Subject);
            Write(IsFormulaId(formula.Subject) ? '\n' : ' ');

            WritePredicate(formula.Predicate);

            Write(IsFormulaId(formula.Object) ? '\n' : ' ');
            WriteResource(formula.Object);
            Write(IsFormulaId(formula.Object) ? ".\n}" : ".}");
        }

        /// <summary>
        /// Writes the specified list to the output writer.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        protected virtual void WriteList(Entity listId)
        {
            Write('(');
            while (listId != Identifier.RdfNil)
            {
                Resource item;
                if(m_ValuesByListId.TryGetValue(listId, out item) && item != Identifier.RdfNil)
                {
                    WriteResource(item);
                }

                Entity tail;
                if(m_TailsByListId.TryGetValue(listId, out tail))
                {
                    listId = m_TailsByListId[listId];
                    Write(listId != Identifier.RdfNil ? ' ' : ')');
                }
                else
                {
                    Write(' ');
                    WriteEntity(new BNode());
                    Write(')');
                }
            }
        }

        /// <summary>
        /// Writes the specified character to the output writer.
        /// </summary>
        /// <param name="c">The character.</param>
        protected void Write(char c)
        {
            m_Writer.Write(c);
        }

        /// <summary>
        /// Writes the specified string to the output writer.
        /// </summary>
        /// <param name="s">The string.</param>
        protected void Write(string s)
        {
            m_Writer.Write(s);
        }

        /// <summary>
        /// Writes the specified string to the output writer, escaping special characters.
        /// </summary>
        /// <param name="s">The string.</param>
        protected void WriteEscaped(string s)
        {
            int length = s.Length;
            for (int i = 0; i < length; i++)
            {
                char c = s[i];
                int charCode = (int)c;
                switch (charCode)
                {
                    case 0x09: Write(@"\t"); break;
                    case 0x0A: Write(@"\n"); break;
                    case 0x0D: Write(@"\r"); break;
                    case 0x22: Write(@"\" + '"'); break;
                    case 0x5C: Write(@"\\"); break;
                    default:
                        if (charCode >= 0x20 && charCode <= 0x7E)
                        {
                            Write(c);
                        }
                        else
                        {
                            if (!Char.IsSurrogate(c) || i == length - 1)
                            {
                                Write(@"\u");
                                Write(charCode.ToString("X4"));
                            }
                            else
                            {
                                Write(@"\U");
                                Write(Char.ConvertToUtf32(c, s[++i]).ToString("X8"));
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Starts a new line in the output writer.
        /// </summary>
        protected virtual void WriteLine()
        {
            if (!m_Empty)
                Write('\n');
            else
                m_Empty = false;
        }

        /// <summary>
        /// Determines whether the specified resource is the ID of a formula.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns><c>true</c> if the resource is a formula ID; otherwise, <c>false</c>.</returns>
        protected bool IsFormulaId(Resource node)
        {
            return node is Entity && m_ReifiedStatementsById.Contains((Entity)node);
        }

        /// <summary>
        /// Determines whether the specified resource is the ID of a list.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns><c>true</c> if the resource is a list ID; otherwise, <c>false</c>.</returns>
        protected bool IsListId(Resource node)
        {
            return node is Entity
                && (m_ValuesByListId.ContainsKey((Entity)node)
                 || m_TailsByListId.ContainsKey((Entity)node));
        }

        /// <summary>
        /// Returns the first element in the collection or the default value if the collection is empty.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="items">The items.</param>
        /// <returns>The first element or the default value.</returns>
        protected T FirstOrDefault<T>(IEnumerable<T> items)
        {
            var enumerator = items.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : default(T);
        }

        /// <summary>
        /// Implements a multi-valued dictionary.
        /// </summary>
        /// <typeparam name="K">Key type.</typeparam>
        /// <typeparam name="V">Value type.</typeparam>
        protected sealed class Lookup<K, V>
        {
            /// <summary>Dictionary of items.</summary>
            private Dictionary<K, Dictionary<V, V>> m_Items;

            /// <summary>
            /// Gets the items with the specified key.
            /// </summary>
            /// <value>The items, or an empty collection if no item with the specified key exists.</value>
            public ICollection<V> this[K key] { get { return GetAll(key); } }

            /// <summary>
            /// Gets the keys of the dictionary.
            /// </summary>
            /// <value>The keys.</value>
            public ICollection<K> Keys { get { return m_Items.Keys; } }

            /// <summary>
            /// Gets a value indicating whether this collection is empty.
            /// </summary>
            /// <value><c>true</c> if this collection is empty; otherwise, <c>false</c>.</value>
            public bool IsEmpty { get { return m_Items.Count == 0; } }

            /// <summary>
            /// Initializes a new <see cref="Lookup&lt;K, V&gt;"/> instance.
            /// </summary>
            public Lookup()
            {
                m_Items = new Dictionary<K, Dictionary<V, V>>();
            }

            /// <summary>
            /// Determines whether the collection contains items with the specified key.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <returns><c>true</c> if [contains] [the specified key]; otherwise, <c>false</c>.</returns>
            public bool Contains(K key)
            {
                return m_Items.ContainsKey(key);
            }

            /// <summary>
            /// Adds the specified value with the specified key.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="statement">The statement.</param>
            public void Add(K key, V statement)
            {
                Dictionary<V, V> keyStatements;
                if(!m_Items.TryGetValue(key, out keyStatements))
                    m_Items[key] = keyStatements = new Dictionary<V, V>();
                if(!keyStatements.ContainsKey(statement))
                    keyStatements.Add(statement, statement);
            }

            /// <summary>
            /// Removes the item with the specified key.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="statement">The statement.</param>
            public void Remove(K key, V statement)
            {
                Dictionary<V, V> keyStatements;
                if(m_Items.TryGetValue(key, out keyStatements))
                {
                    keyStatements.Remove(statement);
                    if (keyStatements.Count == 0)
                        m_Items.Remove(key);
                }
            }

            /// <summary>
            /// Gets all items with the specified key.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <returns></returns>
            public ICollection<V> GetAll(K key)
            {
                Dictionary<V, V> keyStatements;
                return m_Items.TryGetValue(key, out keyStatements) ? keyStatements.Keys : (ICollection<V>)new V[0];
            }
        }
    }
}
#endif