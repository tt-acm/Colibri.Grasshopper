using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Colibri.Grasshopper
{
    public class IterateNDArray : IEnumerable<int[]>
    {
        private readonly int[] _lenghts;

        public IterateNDArray(params int[] lenghts)
        {
            _lenghts = lenghts;
        }
        // Must implement GetEnumerator, which returns a new StreamReaderEnumerator.
        public IEnumerator<int[]> GetEnumerator()
        {
            return new IteratorNDArray(_lenghts);
        }

        // Must also implement IEnumerable.GetEnumerator, but implement as a private method.
        private IEnumerator GetEnumerator1()
        {
            return this.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator1();
        }



        class IteratorNDArray : IEnumerator<int[]>
        {
            private readonly int[] _lenghts;
            int[] _current;
            ulong _counter;

            public IteratorNDArray(params int[] lengths)
            {
                _lenghts = lengths;
                _current = Enumerable.Repeat(0, lengths.Length).ToArray();
                _counter = 0;
            }

            public int[] Current
            {

                get
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException();
                    }

                    return _current;
                }
            }

            private object Current1
            {

                get { return this.Current; }
            }

            object IEnumerator.Current
            {
                get { return Current1; }
            }

            public int Dimensions
            {
                get { return _lenghts.Length; }
            }

            // Implement MoveNext and Reset, which are required by IEnumerator.
            public bool MoveNext()
            {
                if (_current == null)
                    return false;

                if (_counter++ == 0)
                    return true;

                bool hasIncreased = false;

                for (int i = 0; i < Dimensions; i++)
                {
                    if (_current[i] == _lenghts[i] - 1)
                    {
                        _current[i] = 0;
                        continue;
                    }
                    else
                    {
                        _current[i]++;
                        hasIncreased = true;
                        break;
                    }
                }

                if (hasIncreased)
                    return true;
                else
                {
                    _current = null;
                    return false;
                }
            }

            public void Reset()
            {
                _current = null;
                _counter = 0;
            }

            // Implement IDisposable, which is also implemented by IEnumerator(T).
            private bool disposedValue = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                    {
                        // Dispose of managed resources.
                    }
                    Console.WriteLine("iterations: " + _counter);
                    Reset();
                }

                this.disposedValue = true;
            }

            ~IteratorNDArray()
            {
                Dispose(false);
            }
        }
    }
}