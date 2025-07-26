using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TestFA;
namespace TestFA
{

    static class Program
    {
        static void PrintArray(int[] arr)
        {
            Console.Write("[");
            Console.Write(arr[0]);
            for (int i = 1; i < arr.Length; i++)
            {
                Console.Write($", {arr[i]}");
            }
            Console.WriteLine("]");
        }
        static void Main()
        {
            try
            {
                var ccomment = @"\/\*([^\*]|\*+[^\/])*\*\/";
                var ccommentLazy = "# C comment\n"+@"\/\*(.|\n)*?\*\/";
                var test1 = "(?<baz>foo|fubar)+";
                var test2 = "(a|b)*?(b{2})";
                var test3 = "^hello world!$";
                var lexer = $"# Test Lexer\n{test1}\n{ccommentLazy}\n{test2}\n{test3}";
                var ast = RegexExpression.Parse(lexer);
                //var ast = RegexExpression.Parse(test2x);
                Console.WriteLine(ast);
                ast.Visit((parent, expr, index, level) =>
                {
                    var indent = new string(' ', level * 4);
                    Console.WriteLine($"{indent}{expr.GetType().Name}\t -> {expr.ToString()}");
                    return true;
                });
      
               
                var dfa = ast.ToDfa();
                var array = dfa.ToArray();
                if (Dfa.IsRangeArray(array))
                {
                    Console.WriteLine("Using range array");
                } else
                {
                    Console.WriteLine("Using non range array");
                }
                PrintArray(array);
                //dfa = Dfa.FromArray(array);
                dfa.RenderToFile(@"..\..\..\dfa.dot");
                dfa.RenderToFile(@"..\..\..\dfa.jpg");
                Console.WriteLine("DFA construction successful!");
                Console.WriteLine($"Start state created with {dfa.Transitions.Count} transitions. State machine has {dfa.FillClosure().Count} states. Array length is {array.Length}");

                // Test the DFA with some strings
                TestDfa(dfa, "aaabababb");
                TestDfa(dfa, "/* foo */");
                TestDfa(dfa, "fubar");
                TestDfa(dfa, "foobaz");
                TestDfa(dfa, "/* broke *");
                TestDfa(dfa, "hello world!");
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
            int position = 0;
            bool atLineStart = true;

            Console.WriteLine($"\n=== Testing '{input}' ===");

            while (position <= input.Length)
            {
                bool found = false;

                foreach (var transition in currentState.Transitions)
                {
                    // Check anchor transitions first
                    if (transition.Min == -2 && transition.Max == -2)  // START_ANCHOR ^
                    {
                        if (atLineStart)
                        {
                            currentState = transition.To;
                            atLineStart = false;
                            found = true;
                            break;  // Exit foreach, don't check other transitions
                        }
                    }
                    else if (transition.Min == -3 && transition.Max == -3)  // END_ANCHOR $
                    {
                        if (position == input.Length)
                        {
                            currentState = transition.To;
                            found = true;
                            break;  // Exit foreach, don't check other transitions
                        }
                    }
                    // Check character transitions only if not an anchor
                    else if (transition.Min >= 0 && position < input.Length)
                    {
                        char c = input[position];
                        if (c >= transition.Min && c <= transition.Max)
                        {
                            currentState = transition.To;
                            position++;
                            atLineStart = (c == '\n');
                            found = true;
                            break;  // Exit foreach, don't check other transitions
                        }
                    }
                }

                if (!found)
                {
                    if (position < input.Length)
                        Console.WriteLine($"REJECTED: No transition for '{input[position]}' at position {position}");
                    else
                        Console.WriteLine($"REJECTED: No valid end transition");
                    return;
                }

                // Check for acceptance
                if (currentState.IsAccept)
                {
                    if (position < input.Length - 1)
                    {
                        Console.WriteLine($"Rejected: Input remaining");
                    }
                    else
                    {
                        Console.WriteLine($"ACCEPTED: {currentState.AcceptSymbol}");
                    }
                    return;
                }
            }
            
            Console.WriteLine($"REJECTED: Not in accept state");
        }

 
    }
}