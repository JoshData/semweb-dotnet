using System;
using System.Collections;

using SemWeb;

namespace SemWeb.Util {

	internal class ResSet : ICollection {
		Hashtable items = new Hashtable();
		ICollection keys;
		
		public ResSet() {
		}
		
		public ResSet(Resource[] items) {
			AddRange(items);
		}

		private ResSet(Hashtable items) {
			this.items = items;
		}

		public void Add(Resource res) {
			items[res] = items;
			keys = null;
		}
		
		public void AddRange(Resource[] items) {
			if (items == null) return;
			foreach (Resource r in items)
				Add(r);
		}
			
		public void Remove(Resource res) {
			items.Remove(res);
			keys = null;
		}
		
		public bool Contains(Resource res) {
			return items.ContainsKey(res);
		}
		
		public ICollection Items {
			get {
				if (keys == null)
					keys = items.Keys;
				return keys;
			}
		}
		
		public void AddRange(ResSet set) {
			if (set == null) return;
			foreach (Resource r in set.Items) {
				Add(r);
			}
		}
		
		public void Clear() {
			items.Clear();
			keys = null;
		}
		
		public ResSet Clone() {
			return new ResSet((Hashtable)items.Clone());
		}
		
		public int Count { get { return items.Count; } }
		
		public IEnumerator GetEnumerator() { return items.Keys.GetEnumerator(); }
		
		bool ICollection.IsSynchronized { get { return false; } }
		object ICollection.SyncRoot { get { return null; } }
		
		public void CopyTo(System.Array array, int index) {
			foreach (Resource r in this)
				array.SetValue(r, index++);
		}
		
		public Resource[] ToArray() {
			Resource[] ret = new Resource[Count];
			CopyTo(ret, 0);
			return ret;
		}
		
		public Entity[] ToEntityArray() {
			Entity[] ret = new Entity[Count];
			CopyTo(ret, 0);
			return ret;
		}
		
		public void RetainAll(ResSet set) {
			foreach (Resource r in new ArrayList(this))
				if (!set.Contains(r))
					Remove(r);
		}

		/*Hashtable Intersect(Hashtable x, Hashtable y) {
			Hashtable a, b;
			if (x.Count < y.Count) { a = x; b = y; }
			else { b = x; a = y; }
			Hashtable c = new Hashtable();
			foreach (Resource r in a)
				if (b.ContainsKey(r))
					c[r] = c;
			return c;
		}*/		
	}
	
	public class DistinctStatementsSink : StatementSink {
		StatementSink sink;
		Store store;
		bool resetMeta;
		public DistinctStatementsSink(StatementSink sink, bool resetMeta) {
			this.sink = sink;
			if (sink is Store)
				store = (Store)sink;
			else
				store = new MemoryStore();
			this.resetMeta = resetMeta;
		}
		public bool Add(Statement s) {
			if (resetMeta) s.Meta = Statement.DefaultMeta;
			if (store.Contains(s)) return true;
			if (store != sink) store.Add(s);
			return sink.Add(s);
		}
	}
	

	public class StatementList : ICollection {
		private const int DefaultInitialCapacity = 0x10;
		private int _size;
		private Statement[] _items;

		public StatementList() {
			_items = new Statement[DefaultInitialCapacity];
		}		

		public Statement this[int index] { 
			get { return _items[index]; }
			set { _items[index] = value; } 
		}

		public int Count { get { return _size; } }

		private void EnsureCapacity(int count) { 
			if (count <= _items.Length) return; 
			int newLength;
			Statement[] newData;
			newLength = _items.Length << 1;
			if (newLength == 0)
				newLength = DefaultInitialCapacity;
			while (newLength < count) 
				newLength <<= 1;
			newData = new Statement[newLength];
			Array.Copy(_items, 0, newData, 0, _items.Length);
			_items = newData;
		}
		
		private void Shift(int index, int count) { 
			if (count > 0) { 
				if (_size + count > _items.Length) { 
					int newLength;
					Statement[] newData;
					newLength = (_items.Length > 0) ? _items.Length << 1 : 1;
					while (newLength < _size + count) 
						newLength <<= 1;
					newData = new Statement[newLength];
					Array.Copy(_items, 0, newData, 0, index);
					Array.Copy(_items, index, newData, index + count, _size - index);
					_items = newData;
				} else {
					Array.Copy(_items, index, _items, index + count, _size - index);
				}
			} else if (count < 0) {
				int x = index - count ;
				Array.Copy(_items, x, _items, index, _size - x);
			}
		}

		public int Add(Statement value) { 
			if (_items.Length <= _size /* same as _items.Length < _size + 1) */) 
				EnsureCapacity(_size + 1);
			_items[_size] = value;
			return _size++;
		}
		
		public void Remove(Statement s) {
			if (_size == 0) return;
			int index = Array.IndexOf(_items, s, 0, _size);
			if (index < 0) return;
			RemoveAt(index);
		}

		public virtual void Clear() { 
			Array.Clear(_items, 0, _size);
			_size = 0;
		}

		public virtual void RemoveAt(int index) { 
			if (index < 0 || index >= _size) 
				throw new ArgumentOutOfRangeException("index", index,
					"Less than 0 or more than list count.");
			Shift(index, -1);
			_size--;
		}

		public void Reverse() {
			for (int i = 0; i <= Count / 2; i++) {
				Statement t = this[i];
				this[i] = this[Count-i-1];
				this[Count-i-1] = t;
			}				
		}
		
		public Statement[] ToArray() {
			Statement[] ret = new Statement[_size];
			Array.Copy(_items, ret, _size);
			return ret;
		}
		
		internal Statement[] ToArray(Type t) {
			return ToArray();
		}
		
		public static implicit operator Statement[](StatementList list) {
			return list.ToArray();
		}
		
		public IEnumerator GetEnumerator() { return new Enumer(this); }
		
		public void CopyTo(Array dest, int start) {
			_items.CopyTo(dest, start);
		}
		
		public object SyncRoot { get { return null; } }
		
		public bool IsSynchronized { get { return false; } }
		
		class Enumer : IEnumerator {
			StatementList list;
			int index;
			public Enumer(StatementList list) { this.list = list; index = -1; }
			public void Reset() { index = -1; }
			public bool MoveNext() {
				if (index == list.Count - 1)
					return false;
				index++;
				return true;
			}
			public object Current { get { return list[index]; } }
		}
	}

	internal class MultiMap {
		Hashtable items = new Hashtable();
		
		public MultiMap() {
		}
		
		public void Put(object key, object value) {
			object entry = items[key];
			if (entry == null) {
				items[key] = value;
			} else if (entry is ArrayList) {
				((ArrayList)entry).Add(value);
			} else {
				ArrayList list = new ArrayList();
				list.Add(entry);
				list.Add(value);
				items[key] = list;
			}
		}

		public void Clear() {
			items.Clear();
		}
		
		public IList Get(object key) {
			object ret = items[key];
			if (ret == null) return null;
			if (ret is ArrayList) return (ArrayList)ret;
			ArrayList list = new ArrayList();
			list.Add(ret);
			return list;
		}
		
		public IEnumerable Keys {
			get {
				return items.Keys;
			}
		}
	}
	
	internal class Permutation {
		int[] state;
		int[] max;
		public Permutation(int n) : this(n, 2) {
		}
		public Permutation(int n, int e) {
			state = new int[n];
			max = new int[n];
			for (int i = 0; i < n; i++)
				max[i] = e;
		}
		public Permutation(int[] choices) {
			state = new int[choices.Length];
			max = choices;
		}
		public int[] Next() {
			if (state == null) return null;
		
			int[] ret = (int[])state.Clone();
			
			state[0]++;
			for (int i = 0; i < max.Length; i++) { // use max.Length because state becomes null
				if (state[i] < max[i]) break;
				state[i] = 0;
				if (i == state.Length-1) // done the next time around
					state = null;
				else // carry
					state[i+1]++;
			}

			return ret;
		}
	}
}
