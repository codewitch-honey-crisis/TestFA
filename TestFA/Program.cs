using System;
using System.Collections.Generic;
using System.Linq;

using TestFA;
namespace TestFA
{
    // Add these extensions to your RegexExpression class
    static class RegexExpressionExtensions
    {
        private static Dictionary<RegexExpression, int> _positions = new Dictionary<RegexExpression, int>();
        private static Dictionary<RegexExpression, bool> _nullable = new Dictionary<RegexExpression, bool>();
        private static Dictionary<RegexExpression, HashSet<RegexExpression>> _firstPos = new Dictionary<RegexExpression, HashSet<RegexExpression>>();
        private static Dictionary<RegexExpression, HashSet<RegexExpression>> _lastPos = new Dictionary<RegexExpression, HashSet<RegexExpression>>();

        public static int GetDfaPosition(this RegexExpression expr)
        {
            return _positions.TryGetValue(expr, out int pos) ? pos : -1;
        }

        public static void SetDfaPosition(this RegexExpression expr, int position)
        {
            _positions[expr] = position;
        }

        public static bool GetNullable(this RegexExpression expr)
        {
            return _nullable.TryGetValue(expr, out bool nullable) && nullable;
        }

        public static void SetNullable(this RegexExpression expr, bool nullable)
        {
            _nullable[expr] = nullable;
        }

        public static HashSet<RegexExpression> GetFirstPos(this RegexExpression expr)
        {
            if (!_firstPos.TryGetValue(expr, out HashSet<RegexExpression> set))
            {
                set = new HashSet<RegexExpression>();
                _firstPos[expr] = set;
            }
            return set;
        }

        public static HashSet<RegexExpression> GetLastPos(this RegexExpression expr)
        {
            if (!_lastPos.TryGetValue(expr, out HashSet<RegexExpression> set))
            {
                set = new HashSet<RegexExpression>();
                _lastPos[expr] = set;
            }
            return set;
        }

        public static void ClearDfaProperties()
        {
            _positions.Clear();
            _nullable.Clear();
            _firstPos.Clear();
            _lastPos.Clear();
        }
    }

    // Adapter approach - works with your existing AST
    class DirectDfaBuilder
    {
        private int _positionCounter = 1;
        private RegexExpression _endMarker;
        private Dictionary<int, RegexExpression> _positions = new Dictionary<int, RegexExpression>();
        private Dictionary<RegexExpression, HashSet<RegexExpression>> _followPos =
            new Dictionary<RegexExpression, HashSet<RegexExpression>>();

        public Dfa BuildDfa(RegexExpression regexAst)
        {
            // Clear any previous state
            RegexExpressionExtensions.ClearDfaProperties();
            _positions.Clear();
            _followPos.Clear();
            _positionCounter = 1;

            // Step 1: Augment with end marker
            var augmentedAst = AugmentWithEndMarker(regexAst);

            // Step 2: Assign positions to leaf nodes
            AssignPositions(augmentedAst);

            // Step 3: Compute nullable, firstpos, lastpos
            ComputeNodeProperties(augmentedAst);

            // Step 4: Compute followpos
            ComputeFollowPos(augmentedAst);

            // Step 5: Build DFA
            return ConstructDfa(augmentedAst);
        }

        private RegexConcatExpression AugmentWithEndMarker(RegexExpression root)
        {
            // Create end marker - using RegexLiteralExpression with special marker
            _endMarker = new RegexLiteralExpression { Value = "#END#" };
            _endMarker.SetDfaPosition(_positionCounter++);

            var concat = new RegexConcatExpression(root,_endMarker);
           
            return concat;
        }

        private void AssignPositions(RegexExpression? node)
        {
            if (node == null) return;

            // Assign positions to leaf nodes (literals and character classes)
            if (IsLeafNode(node) && node.GetDfaPosition() == -1)
            {
                node.SetDfaPosition(_positionCounter++);
                _positions[node.GetDfaPosition()] = node;
            }

            // Recursively process child nodes based on your AST structure
            switch (node)
            {
                case RegexConcatExpression concat:
                    AssignPositions(concat.Left);
                    AssignPositions(concat.Right);
                    break;

                case RegexOrExpression or:
                    AssignPositions(or.Left);
                    AssignPositions(or.Right);
                    break;
                case RegexRepeatExpression repeat:
                    AssignPositions(repeat.Expression);
                    break;

            }
        }

        private bool IsLeafNode(RegexExpression node)
        {
            return node is RegexLiteralExpression ||
                   node is RegexCharsetExpression;
        }

        private void ComputeNodeProperties(RegexExpression? node)
        {
            if (node == null) return;
            RegexExpression?[] exprs;
            // Post-order traversal - compute children first
            switch (node)
            {
                case RegexConcatExpression concat:
                    // Process all children first
                    ComputeNodeProperties(concat.Left);
                    ComputeNodeProperties(concat.Right);

                    // N-ary concatenation rules
                    exprs = new RegexExpression?[] { concat.Left, concat.Right };
                    node.SetNullable(exprs.All(e => e.GetNullable()));

                    // FirstPos: union of firstpos of expressions until we hit a non-nullable
                    foreach (var expr in exprs)
                    {
                        node.GetFirstPos().UnionWith(expr.GetFirstPos());
                        if (!expr.GetNullable())
                            break;
                    }

                    // LastPos: union of lastpos of expressions from right until we hit a non-nullable
                    for (int i = exprs.Length - 1; i >= 0; i--)
                    {
                        var expr = exprs[i];
                        node.GetLastPos().UnionWith(expr.GetLastPos());
                        if (!expr.GetNullable())
                            break;
                    }
                    break;

                case RegexOrExpression or:
                    // Process all children first
                    exprs = new RegexExpression?[] { or.Left, or.Right };
                    foreach (var expr in exprs)
                        ComputeNodeProperties(expr);

                    // N-ary alternation rules
                    node.SetNullable(exprs.Any(e => e.GetNullable()));

                    // FirstPos and LastPos: union of all children
                    foreach (var expr in exprs)
                    {
                        node.GetFirstPos().UnionWith(expr.GetFirstPos());
                        node.GetLastPos().UnionWith(expr.GetLastPos());
                    }
                    break;

                case RegexRepeatExpression repeat:
                    ComputeNodeProperties(repeat.Expression);

                    // Handle different repeat types
                    if (repeat.MinOccurs <= 0) // *, {0,n}, or ?
                    {
                        node.SetNullable(true);
                        node.GetFirstPos().UnionWith(repeat.Expression.GetFirstPos());
                        node.GetLastPos().UnionWith(repeat.Expression.GetLastPos());
                    }
                    else if (repeat.MinOccurs >= 1) // +, {1,n}, {n}
                    {
                        node.SetNullable(repeat.Expression.GetNullable());
                        node.GetFirstPos().UnionWith(repeat.Expression.GetFirstPos());
                        node.GetLastPos().UnionWith(repeat.Expression.GetLastPos());
                    }
                    break;

                case RegexLiteralExpression literal:
                case RegexCharsetExpression charset:
                    // Leaf nodes
                    node.SetNullable(false);
                    node.GetFirstPos().Add(node);
                    node.GetLastPos().Add(node);
                    break;
            }
        }

        private void ComputeFollowPos(RegexExpression node)
        {
            // Initialize followpos sets
            foreach (var pos in _positions.Values)
            {
                _followPos[pos] = new HashSet<RegexExpression>();
            }

            ComputeFollowPosRecursive(node);
        }

        private void ComputeFollowPosRecursive(RegexExpression node)
        {
            if (node == null) return;

            switch (node)
            {
                case RegexConcatExpression concat:
                    var left = concat.Left;
                    var right = concat.Right;

                    foreach (var pos in left.GetLastPos())
                    {
                        _followPos[pos].UnionWith(right.GetFirstPos());
                    }
                    ComputeFollowPosRecursive(left);
                    ComputeFollowPosRecursive(right);
                        
                    break;

                case RegexOrExpression or:
                    ComputeFollowPosRecursive(or.Left);
                    ComputeFollowPosRecursive(or.Right);
                    break;
                case RegexRepeatExpression repeat:
                    // Rule 2: If n is star/plus-node and i in lastpos(n),
                    // then all positions in firstpos(n) are in followpos(i)
                    if (repeat.MaxOccurs != 1) // *, +, or {n,m} where m > 1
                    {
                        foreach (var pos in node.GetLastPos())
                        {
                            _followPos[pos].UnionWith(node.GetFirstPos());
                        }
                    }
                    ComputeFollowPosRecursive(repeat.Expression);
                    break;
            }
        }

        private Dfa ConstructDfa(RegexExpression root)
        {
            var startState = CreateStateFromPositions(root.GetFirstPos());
            var unmarkedStates = new Queue<Dfa>();
            var allStates = new Dictionary<string, Dfa>();

            string startKey = GetStateKey(root.GetFirstPos());
            allStates[startKey] = startState;
            unmarkedStates.Enqueue(startState);

            while (unmarkedStates.Count > 0)
            {
                var currentState = unmarkedStates.Dequeue();
                var currentPositions = GetPositionsFromState(currentState);

                // Group positions by their symbols/character classes
                var symbolGroups = GroupPositionsBySymbol(currentPositions);

                foreach (var group in symbolGroups)
                {
                    var transitions = group.Value;

                    // Skip end marker
                    if (transitions.Any(t => t.Node == _endMarker))
                        continue;

                    // Compute next state as union of followpos for all positions
                    var nextPositions = new HashSet<RegexExpression>();
                    foreach (var transition in transitions)
                    {
                        nextPositions.UnionWith(_followPos[transition.Node]);
                    }

                    if (nextPositions.Count == 0) continue;

                    string nextKey = GetStateKey(nextPositions);
                    Dfa nextState;

                    if (!allStates.ContainsKey(nextKey))
                    {
                        nextState = CreateStateFromPositions(nextPositions);
                        allStates[nextKey] = nextState;
                        unmarkedStates.Enqueue(nextState);
                    }
                    else
                    {
                        nextState = allStates[nextKey];
                    }

                    // Add transition with appropriate character range
                    currentState.AddTransition(new FATransition(nextState, group.Key.Min, group.Key.Max));
                }
            }

            return startState;
        }

        private class TransitionInfo
        {
            public RegexExpression Node { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
        }

        private class CharRange
        {
            public int Min { get; set; }
            public int Max { get; set; }

            public override bool Equals(object obj) =>
                obj is CharRange r && r.Min == Min && r.Max == Max;
            public override int GetHashCode() => Min.GetHashCode() ^ Max.GetHashCode();
        }

        private Dictionary<CharRange, List<TransitionInfo>> GroupPositionsBySymbol(HashSet<RegexExpression> positions)
        {
            var groups = new Dictionary<CharRange, List<TransitionInfo>>();

            foreach (var pos in positions.Where(p => p != _endMarker))
            {
                var ranges = GetCharacterRanges(pos);

                foreach (var range in ranges)
                {
                    var key = new CharRange { Min = range.Min, Max = range.Max };

                    if (!groups.ContainsKey(key))
                        groups[key] = new List<TransitionInfo>();

                    groups[key].Add(new TransitionInfo { Node = pos, Min = range.Min, Max = range.Max });
                }
            }

            return groups;
        }

        private List<CharRange> GetCharacterRanges(RegexExpression node)
        {
            var ranges = new List<CharRange>();

            switch (node)
            {
                case RegexLiteralExpression literal:
                    // Convert string to character ranges
                    if (literal.Codepoint != -1)
                    {
                        ranges.Add(new CharRange { Min = literal.Codepoint, Max = literal.Codepoint });
                        
                    }
                    break;

                case RegexCharsetExpression charset:
                    // Extract ranges from character set
                    ranges.AddRange(ExtractCharsetRanges(charset));
                    break;
            }

            return ranges;
        }

        private List<CharRange> ExtractCharsetRanges(RegexCharsetExpression charset)
        {
            var ranges = new List<CharRange>();

            foreach (var faRange in charset.GetRanges())
            {
                ranges.Add(new CharRange { Min = faRange.Min, Max = faRange.Max });
            }

            return ranges;
        }

        private Dfa CreateStateFromPositions(HashSet<RegexExpression> positions)
        {
            var state = new Dfa();

            // Mark as accept state if it contains the end marker
            if (positions.Contains(_endMarker))
            {
                state.Attributes["IsAccept"] = true;
            }

            // Store positions for reference
            state.Attributes["Positions"] = positions.ToList();

            return state;
        }

        private HashSet<RegexExpression> GetPositionsFromState(Dfa state)
        {
            var positions = (List<RegexExpression>)state.Attributes["Positions"];
            return new HashSet<RegexExpression>(positions);
        }

        private string GetStateKey(HashSet<RegexExpression> positions)
        {
            var sorted = positions.Where(p => p.GetDfaPosition() != -1)
                                .OrderBy(p => p.GetDfaPosition())
                                .Select(p => p.GetDfaPosition().ToString());
            return string.Join(",", sorted);
        }
    }

    static class Program
    {
        static void Main()
        {
            try
            {
                var ast = RegexExpression.Parse("(foo|fubar)+");
                var ddb = new DirectDfaBuilder();
                var dfa = ddb.BuildDfa(ast);
                dfa.RenderToFile(@"..\..\..\dfa.jpg");
                Console.WriteLine("DFA construction successful!");
                Console.WriteLine($"Start state created with {dfa.Transitions.Count} transitions");

                // Test the DFA with some strings
                TestDfa(dfa, "foo");
                TestDfa(dfa, "fubar");
                TestDfa(dfa, "foobar");
                TestDfa(dfa, "fu");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void TestDfa(Dfa startState, string input)
        {
            var currentState = startState;

            foreach (char c in input)
            {
                bool found = false;
                foreach (var transition in currentState.Transitions)
                {
                    if (c >= transition.Min && c <= transition.Max)
                    {
                        currentState = transition.To;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Console.WriteLine($"String '{input}': REJECTED (no transition for '{c}')");
                    return;
                }
            }

            bool isAccepted = currentState.Attributes.ContainsKey("IsAccept") &&
                             (bool)currentState.Attributes["IsAccept"];
            Console.WriteLine($"String '{input}': {(isAccepted ? "ACCEPTED" : "REJECTED")}");
        }
    }
}