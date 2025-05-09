# Eleu

A  script language which is used to teach programming. The language is based on famous book [Crafting interpreters](http://craftinginterpreters.com/).

See it live at: https://eleu.app

## Features/ changes

* `print` is not a statement but there is a `print` function.
* `repeat`-statement

```eleu
repeat(5) { print("Hello Eleu"); }
```

* Better error messages (in German) to help pupils identify bugs
* a lot of basic native functions for math and string manipulation
* Puzzles a fun way to learn programming


## Contents of the repository

```
|- CSharp  (C# Code part)
|  |- DomCask: utility for HTML Dom handling
|  |- Eleu: the interpreter
|  |- eleu-cli: a command line interface for the interpreter
|  |- EleuTester: a tool to run the tests
|  |- EleuWeb: Code for the Website eleu.app
|  |- GenerateAst: a utility program to generate the code for the abstract syntax tree
|- Eleu (Eleu code part)  
|- VscExtension: the extension for Visual Studio Code to handle .eleu files
```