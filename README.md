# Build Your Own Lisp
C# implementation of Daniel Holden's "Build Your Own Lisp" with a *twist*

# Structural Changes
My idea of a finished Lisp is to get the Y-Combinator working on it. First I faithfully re-created the system as per the book, with all chapters' tests passing. But the Y-Combinator gave binding errors (same happens in the original C version). Consider this:
```lisp
(def {add} 
     (\ {n m} 
        {+ m n}))
((add 2) 3)
; returns 5

(def {add_} 
     (\ {n} 
        {\ {m} 
           {+ m n}}))
((add_ 2) 3)
; unbound symbol n!

; Haskell Curry dictates these should be the same.
```
The add function returns 5, but the add_ function says that there is an unbound variable, n. This is because (add 2) returns a LVAL_FUN object, which contains a local environment (n, 2). When this function object is called with 3, we evaluate the function body {\ {m} {+ m n}} relative to the its local environment (n, 2). But the function's body is a new lambda constructor - which creates a new local environment... and the outer environment, (n, 2), that was passed to it, is lost! This can be seen in the original C code in builtin_lambda(lenv* e, lval* a) because e is never used there. 

To remedy this, in the lambda constructor, instead of giving it a new environment, give it a new environment that points to whatever was given to us. Then its formals get bound, yet it still has access to anything 'above' it.
```C#
        static private lval builtin_lambda(lenv e, lval a)
        {
            // pop first two arguments and pass them to lval_lambda
            lval formals = a.pop(0);
            lval body = a.pop(0);

            // ADD THESE TWO LINES
            lenv new_env = new lenv();
            new_env.par = e;

            return new lval(new_env, formals, body);
        }
```
But there is a conflict, because in the call() function, this lambda itself gets evaluated, and its parent is replaced with a link to the global environment. So I got rid of this line, because the global environment should already be at the very end of the link above.
```C#
            // if all formals have been bound, eval
            if (formals.count == 0)
            {
                // set env parent to eval env 
                env.par = e; // <- bad line!
                
                return builtin_eval(env, new lval(LVAL.SEXPR).add(body.copy())); 
            }
```
With this small addition, we can now use everyone's favorite straightforward looping mechanism:
```lisp
(def {Y} 
    (\ {Ie}
       {(\ {f} {f f})
        (\ {f}
           {Ie (\ {x} 
                  {(f f) x})})}))


(def {facfac} 
    (\ {f} 
       {\ {n} 
          {if (== 1 n) 
              {1} 
              {* n (f (- n 1))}}}))


((Y facfac) 5)

; prints 120
```
# Yay! Now what?
Other changes include:
1. Added a 'type' function which also prints the environment chain.
2. not using an S-Expression to wrap the prompt input, so you have to write things as they are. 
3. Immutable: the environment is a simple dictionary, and if you want to change something that's already in it, it throws an error. 
4. there is some weird parsing bug where the last line of a script can't be a comment.
5. The solution I made broke the Fibonacci function that is written in the standard library. When I rewrote the Fibonacci bare-bones,
```lisp
(def {fib}
     (\ {n} 
        {if (== n 0) 
            {0} 
            {if (== n 1)
                {1}
                {+ (fib (- n 1)) (fib (- n 2))}}}))
```
It worked fine. To preserve the density of my hair I shall not debug these things further, being content with the current state of the implementation. 

# Going Furrrther
- JIT compiler feature, since the expression is already an AST
- enough library functions to implement language processors, such as those from EOPL or Beautiful Racket
- implement the Pie language from Little Typer in here. 

# Update 1
- Added Hindley Milner type inference, the entire expanded interpreter is in the file "Hindley_Milner.cs". Now it can type things like:
```lisp
(\ {g x} 
   {g (g x)})

; prints t8 -> t8 -> t8 -> t8

(\ {g} 
   {\ {x} 
      {g (g x)}})

; prints t10 -> t10 -> t10 -> t10

(\ {g} 
   {\ {x} 
      {g (g (+ x 1))}})

; prints int -> int -> int -> int
```
This will be useful for implementing the Pie language later. I adopted the algorithm from Sestoft's Programming Language Concepts book, so had to remix the type rules for Lispy, basically S-Expressions became ML-style function calls, and lambdas became quasi-letfun expressions. 
