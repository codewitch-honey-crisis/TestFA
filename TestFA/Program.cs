using System;
using System.Collections.Generic;
using System.Linq;

using TestFA;
namespace TestFA
{
   
    static class Program
    {
        static void Main()
        {
            try
            {
                var ccomment = @"\/\*([^\*]|\*+[^\/])*\*\/";
                var test1 = "(foo|fubar)+";
                var ast = RegexExpression.Parse(test1);
                Console.WriteLine(ast);
                var dfa = new DirectDfaBuilder().BuildDfa(ast);
                
                dfa.RenderToFile(@"..\..\..\dfa.jpg");
                Console.WriteLine("DFA construction successful!");
                Console.WriteLine($"Start state created with {dfa.Transitions.Count} transitions");

                // Test the DFA with some strings
                TestDfa(dfa, "fo");
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