# Build Your Own Lisp
C# implementation of Daniel Holden's "Build Your Own Lisp" with a *twist*

# Structural Changes
My idea of a finished Lisp is to get the Y-Combinator working on it. First I faithfully re-created the system as per the book, with all chapters' tests passing. But the Y-Combinator gave binding errors (same happens in the original C version). Turns out there was one important thing that needed to be changed:

```C#
            // if all formals have been bound, eval
            if (formals.count == 0)
            {                
                env.par = e; // set env parent to eval env 
                
                return builtin_eval(env, new lval(LVAL.SEXPR).add(body.copy())); 
            }
```
when a lambda, aka a Q-Expression with a local environment, fills up its formals, a new environment is created for it *then*. But if you have something like:
```lisp
(def {add} (\ {n m} {+ m n}))
((add 2) 3)

(def {add_} (\ {n} {\ {m} {+ m n}}))
((add_ 2) 3)
```
Then the add function returns 5, but the add_ function says that there is an unbound variable, n. This is because (add 2) returns a LVAL_FUN object, which contains a local environment (n, 2). When this function object is called with 3, a new environment is created, so the previous environment with (n, 2) becomes lost. 

To remedy this, upon creation of the function object (when the lambda expression is going through eval), we can give it a new environment whose parent is whatever we are dealing with currently. Then its formals get bound, yet it still has access to anything before it.
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
```
# Yay! Now what?
Other changes include:
1. not using an S-Expression to wrap the prompt input, so you have to write things as they are. 
2. Immutable: the environment is a simple dictionary, and if you want to change something that's already in it, it throws an error. But making it immutable makes fibonacci fail.
3. there is some weird parsing bug where the last line of a script can't be a comment.
4. My solution actually broke fibonacci (I will need to go through it another day) but here is a solution that fixes both: 

keep the line 
```C#
env.par = e; 
```
change 
```C#
return new lval(new_env, formals, body);
```
to 
```C#
return new lval(e, formals, body);
```
in builtin_lambda. Then both Y and fibonacci work. 
