using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestFA
{
    public class FAAttributes : Dictionary<string, object>, IEquatable<FAAttributes>
    {
        public bool Equals(FAAttributes? other)
        {
            if (object.ReferenceEquals(this, other)) return true;
            if(object.ReferenceEquals(other,null)) return false;
            if (this.Count != other.Count) return false;
            foreach (var attr in this)
            {
                object? val;
                if (!other.TryGetValue(attr.Key, out val)) return false;
                if (!object.Equals(attr.Value, val)) return false;

            }
            return true;
        }
        public override bool Equals(object? obj)
        {
            if(object.ReferenceEquals(this,obj)) return true;
            if(object.ReferenceEquals(obj,null)) return false;
            return Equals(obj as FAAttributes);
        }
        public override int GetHashCode()
        {
            int result = 0;
            foreach (var attr in this)
            {
                result ^= attr.Key.GetHashCode();
                if (attr.Value != null)
                {
                    result ^= attr.Value.GetHashCode();
                }
            }
            return result;
           
        }
    }
    /// <summary>
    /// Represents a transition in an FSM
    /// </summary>

    struct FATransition : IEquatable<FATransition>
    {
        public readonly FAAttributes Attributes = new FAAttributes();
        /// <summary>
        /// The minimum codepoint of the range
        /// </summary>
        public int Min;
        /// <summary>
        /// The maximum codepoint of the range
        /// </summary>
        public int Max;
        /// <summary>
        /// The destination state
        /// </summary>
        public Dfa To;
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="to">The destination state</param>
        /// <param name="min">The minimum codepoint</param>
        /// <param name="max">The maximum codepoint</param>
        public FATransition(Dfa to, int min = -1, int max = -1)
        {
            Min = min;
            Max = max;
            To = to;
        }
        /// <summary>
        /// Indicates whether or not this is an epsilon transition
        /// </summary>
        /// <remarks>Not used for direct DFA construction, but kept for traditional DFA construction</remarks>
        public bool IsEpsilon { get { return Min == -1 || Max == -1; } }
        /// <summary>
        /// Provides a string representation of the transition
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (IsEpsilon)
            {
                return string.Concat("-> ", To.ToString());
            }
            if (Min == Max)
            {
                return string.Concat("[", char.ConvertFromUtf32(Min), "]-> ", To.ToString());
            }
            return string.Concat("[", char.ConvertFromUtf32(Min), "-", char.ConvertFromUtf32(Max), "]-> ", To.ToString());
        }
        /// <summary>
        /// Value equality
        /// </summary>
        /// <param name="rhs">The transition to compare</param>
        /// <returns></returns>
        public bool Equals(FATransition rhs)
        {
            return To == rhs.To && Min == rhs.Min && Max == rhs.Max && rhs.Attributes.Equals(Attributes);
        }
        /// <summary>
        /// Returns a hashcode for the transition
        /// </summary>
        /// <returns>A hashcode</returns>
        public override int GetHashCode()
        {
            if (To == null)
            {
                return Min.GetHashCode() ^ Max.GetHashCode() ^ Attributes.GetHashCode();
            }
            return Min.GetHashCode() ^ Max.GetHashCode() ^ To.GetHashCode() ^ Attributes.GetHashCode();
        }
        /// <summary>
        /// Value equality
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (!(obj is FATransition)) return false;
            FATransition rhs = (FATransition)obj;
            // ref compare on To so its attributes will always be the same
            return To == rhs.To && Min == rhs.Min && Max == rhs.Max && Attributes.Equals(rhs.Attributes);
        }
    }


    sealed class FAParseException : FAException
    {
        /// <summary>
        /// Indicates the strings that were expected
        /// </summary>
        public string[] Expecting { get; } = null;
        /// <summary>
        /// Indicates the position where the error occurred
        /// </summary>
        public int Position { get; }
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="message">The error</param>
        /// <param name="position">The position</param>
        public FAParseException(string message, int position) : base(message)
        {
            Position = position;
        }
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="message">The error</param>
        /// <param name="position">The position</param>
        /// <param name="innerException">The inner exception</param>
        public FAParseException(string message, int position, Exception innerException) : base(message, innerException)
        {
            Position = position;
        }
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="message">The error</param>
        /// <param name="position">The position</param>
        /// <param name="expecting">The strings that were expected</param>
        public FAParseException(string message, int position, string[] expecting) : base(message)
        {
            Position = position;
            Expecting = expecting;
        }
    }
    /// <summary>
    /// Represents an exception in the FA engine
    /// </summary>

    class FAException : Exception
    {
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="message">The message</param>
        public FAException(string message) : base(message)
        {

        }
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="innerException">The inner exception</param>
        public FAException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    internal class Dfa
    {
        public readonly Dictionary<string, object> Attributes = new Dictionary<string, object>();
        readonly List<FATransition> _transitions = new List<FATransition>(); // TODO: wrap this with IReadOnlyList for a public property, and add AddTransition
        public IReadOnlyList<FATransition> Transitions { get { return _transitions; } }
        
        public void AddTransition(FATransition transition)
        {
            foreach (var trn in _transitions)
            {
                if (trn.Equals(transition))
                    return; // found
            }
            _transitions.Add(transition);
        }
    }
}
