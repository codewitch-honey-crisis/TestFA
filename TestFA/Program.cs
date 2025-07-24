using System;
using System.Collections.Generic;
using System.Linq;

using TestFA;
namespace TestFA
{
    // Adapter approach - works with your existing AST
    class DirectDfaBuilder
    {
        private int _positionCounter = 1;
        private RegexExpression _endMarker;
        private Dictionary<int, RegexExpression> _positions = new Dictionary<int, RegexExpression>();
        private Dictionary<RegexExpression, HashSet<RegexExpression>> _followPos =
            new Dictionary<RegexExpression, HashSet<RegexExpression>>();

        // Add these properties to your existing RegexExpression class:
        // public int Position { get; set; } = -1;  // -1 means not assigned
        // public bool Nullable { get; set; }
        // public HashSet<RegexExpression> FirstPos { get; set; } = new HashSet<RegexExpression>();
        // public HashSet<RegexExpression> LastPos { get; set; } = new HashSet<RegexExpression>();

        public Dfa BuildDfa(RegexExpression regexAst)
        {
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
            _endMarker.Position = _positionCounter++;

            var concat = new RegexConcatExpression();
            concat.Expressions.Add(root);
            concat.Expressions.Add(_endMarker);

            return concat;
        }

        private void AssignPositions(RegexExpression node)
        {
            if (node == null) return;

            // Assign positions to leaf nodes (literals and character classes)
            if (IsLeafNode(node) && node.Position == -1)
            {
                node.Position = _positionCounter++;
                _positions[(int)node.Position] = node;
            }

            // Recursively process child nodes based on your AST structure
            switch (node)
            {
                case RegexConcatExpression concat:
                    foreach (var expr in concat.Expressions)
                        AssignPositions(expr);
                    break;

                case RegexOrExpression or:
                    foreach (var expr in or.Expressions)
                        AssignPositions(expr);
                    break;

                case RegexRepeatExpression repeat:
                    AssignPositions(repeat.Expression);
                    break;

                case RegexMultiExpression multi:
                    foreach (var expr in multi.Expressions)
                        AssignPositions(expr);
                    break;
            }
        }

        private bool IsLeafNode(RegexExpression node)
        {
            return node is RegexLiteralExpression ||
                   node is RegexCharsetExpression ||
                   node is RegexCharsetClassEntry ||
                   node is RegexCharsetCharEntry ||
                   node is RegexCharsetRangeEntry;
        }

        private void ComputeNodeProperties(RegexExpression node)
        {
            if (node == null) return;

            // Post-order traversal - compute children first
            switch (node)
            {
                case RegexConcatExpression concat:
                    // Process all children first
                    foreach (var expr in concat.Expressions)
                        ComputeNodeProperties(expr);

                    // N-ary concatenation rules
                    node.Nullable = concat.Expressions.All(e => e.Nullable);

                    // FirstPos: union of firstpos of expressions until we hit a non-nullable
                    foreach (var expr in concat.Expressions)
                    {
                        node.FirstPos.UnionWith(expr.FirstPos);
                        if (!expr.Nullable)
                            break;
                    }

                    // LastPos: union of lastpos of expressions from right until we hit a non-nullable
                    for (int i = concat.Expressions.Count - 1; i >= 0; i--)
                    {
                        var expr = concat.Expressions[i];
                        node.LastPos.UnionWith(expr.LastPos);
                        if (!expr.Nullable)
                            break;
                    }
                    break;

                case RegexOrExpression or:
                    // Process all children first
                    foreach (var expr in or.Expressions)
                        ComputeNodeProperties(expr);

                    // N-ary alternation rules
                    node.Nullable = or.Expressions.Any(e => e.Nullable);

                    // FirstPos and LastPos: union of all children
                    foreach (var expr in or.Expressions)
                    {
                        node.FirstPos.UnionWith(expr.FirstPos);
                        node.LastPos.UnionWith(expr.LastPos);
                    }
                    break;

                case RegexRepeatExpression repeat:
                    ComputeNodeProperties(repeat.Expression);

                    // Handle different repeat types
                    if (repeat.MinOccurs <= 0) // * or {0,n}
                    {
                        node.Nullable = true;
                        node.FirstPos.UnionWith(repeat.Expression.FirstPos);
                        node.LastPos.UnionWith(repeat.Expression.LastPos);
                    }
                    else if (repeat.MinOccurs == 1 && repeat.MaxOccurs <= 0) // +
                    {
                        node.Nullable = repeat.Expression.Nullable;
                        node.FirstPos.UnionWith(repeat.Expression.FirstPos);
                        node.LastPos.UnionWith(repeat.Expression.LastPos);
                    }
                    else if (repeat.MinOccurs <= 0 && repeat.MaxOccurs <= 0) // ?
                    {
                        node.Nullable = true;
                        node.FirstPos.UnionWith(repeat.Expression.FirstPos);
                        node.LastPos.UnionWith(repeat.Expression.LastPos);
                    }
                    break;

                case RegexLiteralExpression literal:
                case RegexCharsetExpression charset:
                    // Leaf nodes
                    node.Nullable = false;
                    node.FirstPos.Add(node);
                    node.LastPos.Add(node);
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
                    // N-ary concatenation: for each adjacent pair of expressions,
                    // if i in lastpos(left) then firstpos(right) ⊆ followpos(i)
                    for (int i = 0; i < concat.Expressions.Count - 1; i++)
                    {
                        var left = concat.Expressions[i];
                        var right = concat.Expressions[i + 1];

                        foreach (var pos in left.LastPos)
                        {
                            _followPos[pos].UnionWith(right.FirstPos);
                        }
                    }

                    // Recursively process all children
                    foreach (var expr in concat.Expressions)
                        ComputeFollowPosRecursive(expr);
                    break;

                case RegexOrExpression or:
                    // No followpos rules for alternation itself
                    foreach (var expr in or.Expressions)
                        ComputeFollowPosRecursive(expr);
                    break;

                case RegexRepeatExpression repeat:
                    // Rule 2: If n is star/plus-node and i in lastpos(n),
                    // then all positions in firstpos(n) are in followpos(i)
                    if (repeat.MaxOccurs != 1) // * or + or {n,m} where m > 1
                    {
                        foreach (var pos in node.LastPos)
                        {
                            _followPos[pos].UnionWith(node.FirstPos);
                        }
                    }
                    ComputeFollowPosRecursive(repeat.Expression);
                    break;

            }
        }

        private Dfa ConstructDfa(RegexExpression root)
        {
            var startState = CreateStateFromPositions(root.FirstPos);
            var unmarkedStates = new Queue<Dfa>();
            var allStates = new Dictionary<string, Dfa>();

            string startKey = GetStateKey(root.FirstPos);
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
                    foreach (char c in literal.Value)
                    {
                        ranges.Add(new CharRange { Min = c, Max = c });
                    }
                    break;

                case RegexCharsetExpression charset:
                    // Extract ranges from character set
                    // You'll need to implement this based on your charset structure
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
            var sorted = positions.Where(p => p.Position != -1)
                                .OrderBy(p => p.Position)
                                .Select(p => p.Position.ToString());
            return string.Join(",", sorted);
        }
    }

    // Extensions you'll need to add to your RegexExpression class:
    /*
    public abstract partial class RegexExpression
    {
        public int Position { get; set; } = -1;
        public bool Nullable { get; set; }
        public HashSet<RegexExpression> FirstPos { get; set; } = new HashSet<RegexExpression>();
        public HashSet<RegexExpression> LastPos { get; set; } = new HashSet<RegexExpression>();
    }
    */
    static class Program
    {
        static void Main()
        {
            var ast = RegexExpression.Parse("foo|fubar");
            var ddb = new DirectDfaBuilder();
            var dfa = ddb.BuildDfa(ast);
            return;
        }
    }
}