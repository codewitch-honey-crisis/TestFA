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
                var ccommentLazy = @"\/\*(.|\n)*?\*\/";
                var test1 = "(?<baz>foo|fubar)+";
                var test2 = "(a|b)*?bb";
                var test3 = "hello world!";
                var ast = RegexExpression.Parse($"({ccommentLazy})|({test2})|({test3})");
                Console.WriteLine(ast);
                ast.Visit((parent, expr, index, level) =>
                {
                    var indent = new string(' ', level * 4);
                    Console.WriteLine($"{indent}{expr.GetType().Name}\t -> {expr.ToString()}");
                    return true;
                });
                //return;
                var dfa = ast.ToDfa();
                var array = dfa.ToArray();
                PrintArray(array);
                dfa = Dfa.FromArray(array);
                dfa.RenderToFile(@"..\..\..\dfa.dot");
                dfa.RenderToFile(@"..\..\..\dfa.jpg");
                Console.WriteLine("DFA construction successful!");
                Console.WriteLine($"Start state created with {dfa.Transitions.Count} transitions. State machine has {dfa.FillClosure().Count} states");

                // Test the DFA with some strings
                TestDfa(dfa, "aaabababb");
                TestDfa(dfa, "/* foo */");
                TestDfa(dfa, "fubar");
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

            Console.WriteLine($"\n=== Testing '{input}' ===");

     
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
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
                    Console.WriteLine($"REJECTED: No transition for '{c}' at position {i}");
                    return;
                }

             }


            bool isAccepted = currentState.IsAccept;

            if (isAccepted)
            {
                Console.WriteLine($"ACCEPTED: {currentState.AcceptSymbol}");
               
            }
            else
            {
                Console.WriteLine($"REJECTED: Not in accept state");
            }
        }

 
    }
}