# luthor

An experimental lexer generator that produces simple flat integer arrays that can be used to match text.

Copyright (c) 2025 by honey the codewitch

Lazy matching implemented using Dr. Robert van Engelen's algorithm that he uses in RE/FLEX, and which he was kind enough to help me with via email. All of his respective work is all rights reserved for him.

This lexer relies on direct to DFA conversion as indicated by the Aho-Sethi-Ullman algorithm in the dragon book.

There is no Thompson construction or NFA intermediary using this technique.

Furthermore, this code implements lazy matching using a pure DFA algorithm pioneered by Dr. Robert van Engelen as part of his RE/FLEX project. https://github.com/Genivia/RE-flex

The point of this code is to provide a means to generate flat integer arrays from single regular expressions or entire lexers that can easily be traversed to match unicode text in any language.

Ergo, this code does not contain matching logic, aside from the tests. That is meant for whatever uses the arrays this code can generate.

## Lexer input format

I'm working toward POSIX compliance, which is the eventual goal, at least for the subset that this engine supports. It's a work in progress.
Currently, since it's primarily for lexing, all matches are multiline mode, which means ^ and $ match the beginning and end of a *line* respectively, although the implementor can change that behavior in their own matcher code.

? lazy quantifiers are supported, such as ??, *? and +?

Each expression must be on a single line. Comments are on their own line and must be either `# example` or `#`. `#foo` (no space) will be treated as a regular expression.

For a lexer, rules are each on a line, and accept indexes are assigned in expression order. There is no specifier for a symbolic name, you just have to use the indices/ids.

```
# Test Lexer Example
# C comment
/\*(.|\n)*?\*/
# C identifier
[A-Za-z_][A-Za-z0-9_]*
# more follows... (omitted here)
```

## Lexer array format

Luther arrays come in two flavors: Range arrays and non-range arrays. This is so the smallest possible array and matching code can be used for the situation. Sometimes range arrays end up smaller, sometimes non-range arrays end up smaller, especially for simple expressions without a lot of character sets.

Range arrays are always even in length. Non range arrays are always odd in length, and may be padded with a trailing -1 element to enforce that.

The array is simply a list of states, each of which has lists of transitions to other states. Each value is a 32 bit signed integer.

For each state:

The first integer is the accept id, or -1 if not accepting.

The next integer is the number of unique destination states this state may transition to.

- For each destination state:
    - the next integer is the index of the destination state in the array
    - the next integer is the number of codepoints or two-codepoint range pairs that transition to that state which follows. Whether it's codepoint min/max pairs or single codepoints depends if this is a range array or non-range array
        - The next integer is the codepoint minimum or simple the codepoint for a non-range array
        - the next integer is the maximum codepoint (range array only)

This is repeated for every state.

