using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

/*     p r e s s   ctrl m o / ctrl m l   t o   m i n i m i z e   /   e x p a n d
IDEAS
- MAKE A PYTHON SCRIPT that basically does the C preprocessor's job, replacing LASSERT with a statement. 
- add some library fns, see bonus challenges
- META: script variables for a SHADER, movement of a cube, or in a game engine.
- check out my previous JIT projects and the Creel video on fn ptrs
- META: once we have an AST, we can compile it (Nystrom) then create a JIT environment (C++) 
    and the code can now be run in that JIT area (assuming it's a simple stack, doesnt take more than 4kb)
TYPE INFERENCE
- the basic type inference can check ahead of time if the types of the operators and operands coexist.
- it will decide whether an expression is pure or not. if it's pure, we can send it to the JIT. if not, 
    we can send it to the existing interpreter which does run time type checks. 
- chane 'type' to 'pre_type' as its the type assigned by the parser 


HINDLER-MILNER TYPE INF
TODO: 
- add types for builtins. 
- add support for user-defined fns
- check library fns, and how they interact w env
IDEAS: 
- a huge bug was in the TypeScheme constructor that creates a new type variable. it adds the variable to its own list.
    - APPARENTLY THATS BAD? once i removed it, everything works. Not sure why its not supposed to be there???
    - well his book literally said the opposite. the other constructor may be buggy as well.
- Features: NO COPYING ANYTHING. ALL REFERENCES. 

*/


namespace Lispy
{
    class Lispy
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Lispy Version: 0.1.5\nPress Ctrl+C to Exit\n~~~~~~~~~~~~~~~~~~~~");


            lenv global = new lenv(); // value env 
            lval.lenv_add_builtins(global);
            //lenv tenv = new lenv(); // type env 
            //lval.tenv_add_builtins(tenv);
            Env<TypeScheme> tenv = new Env<TypeScheme>();
            Type.tenv_add_builtins(tenv);

            //   L O A D   S C R I P T
            //Tools.load_script(global, "go"); // "stdlib"
            // get full file name & read it
            string script_name = "unif";
            string base_dir = @"C:\Users\pwnag\source\repos\Lispy\Lispy\";
            string file_name = System.IO.Path.Join(base_dir, script_name + ".lispy");
            var sb = new StringBuilder(System.IO.File.ReadAllText(file_name, Encoding.ASCII));

            // run script
            lval x = new lval(LVAL.SEXPR); // x is wrapper for all exprs in the file
            Tools.lval_read_expr(x, sb.Append('\0').ToString(), 0, '\0');
            foreach (lval expr in x.cell)
            {
                Console.Write("Original expression: ");
                expr.print("\n");           // print original exp

                // unification algo 
                Type t = Type.typeCheck(0, tenv, expr);
                Console.WriteLine("Final Type: " + t);


                // My shitty type checker 
                //var t = expr.typeCheck(0, tenv);
                //Console.WriteLine("Final type: " + t);
                //tenv.print();
                
                
                //expr.eval(global).print("\n");   // print evaluated exp
            }

            while (true) // interactive prompt
            {
                // user input
                Console.Write("lispy> ");
                StringBuilder input = new StringBuilder(Console.ReadLine());

                // parse & eval
                x = new lval(LVAL.SEXPR);
                Tools.lval_read_expr(x, input.Append('\0').ToString(), 0, '\0');
                x.cell[0].eval(global).print("\n"); // doing this now makes me require outermost parentheses (as it should)
            }
        }
    }
    public partial class lval
    {
        //   D A T A 
        public LVAL pre_type; // must be mutable to change from Q to S expr
        //public TypeScheme type_scheme;
        // Basic
        readonly public long num;
        readonly public string sym_err; // Error and Symbol types have some string data 
        // Function
        readonly lbuiltin builtin; // null = user-defined, else builtin
        readonly public lenv env;
        readonly lval formals;
        readonly lval body;
        // Expression - Count and Pointer to a list of "lval*"; 
        public int count => cell.Count;
        readonly public List<lval> cell;
        //   C O N S T R U C T O R S
        public lval(LVAL type) // constructor for SEXPR, QEXPR
        {
            Tools.LASSERT(null, type == LVAL.SEXPR || type == LVAL.QEXPR, "Sev_Error: Type must be S or Q expr!"); 
            this.pre_type = type;
            cell = new List<lval>();
        }
        public lval(string sym_err, LVAL type) // Symbol, String
        {
            Tools.LASSERT(null, ((type == LVAL.SYM) || (type == LVAL.STR) || (type == LVAL.ERR)), "Sev_Error: Sym or Str Initialized with wrong Type!");
            this.pre_type = type;
            this.sym_err = sym_err;
        }
        public lval(long num) // number
        {
            pre_type = LVAL.INT;
            this.num = num;
        }
        public lval(lbuiltin builtin) // biultin function
        {
            pre_type = LVAL.FUN;
            this.builtin = builtin;
        }
        public lval(lenv env, lval formals, lval body) // user defined function
        {
            pre_type = LVAL.FUN;
            builtin = null; // not builtin fn
            this.env = env;
            this.formals = formals;
            this.body = body;
        }



        //   I N S T A N C E   M E T H O D S   - E V A L U A T I O N
        public lval eval(lenv e)
        {
            switch (pre_type)
            {
                case LVAL.SYM: return e.get(this); // syms get looked up
                case LVAL.SEXPR: return eval_sexpr(e); // evaluate S-expressions 
                default: return this; // all other types remain the same 
            }
        }

        private lval eval_sexpr(lenv e)
        {
            // evaluate children & error checking
            for (int i = 0; i < count; i++)
            {
                cell[i] = cell[i].eval(e);
                if (cell[i].pre_type == LVAL.ERR) return pop(i);
            }

            switch (count)
            {
                case 0: return this;   // empty S-expr, nothing to evaluate
                case 1: return pop(0); // one thing "wrapped" in an S-expr
                default:
                    lval f = pop(0);   // take first elem, which is the function
                    if (f.pre_type != LVAL.FUN) return new lval("First element is not a function!", LVAL.ERR); // nicer than LASSERT lol
                    return f.call(e, this); // if so call function to get result. 'this' now contains only args!!!
            }
        }

        private lval call(lenv e, lval a)
        {
            // if builtin then simply apply that 
            if (builtin != null) return builtin(e, a);

            // U S E R - D E F I N E D   B E L O W

            // while arguments still remain to be processed
            while (a.count > 0)
            {
                // if we ran out of formal arguments to bind
                if (formals.count == 0) return new lval("Function passed too many args", LVAL.ERR);

                // pop the first symbol from formals
                lval sym = formals.pop(0);

                // special case to deal with '&'
                if (sym.sym_err == "&")
                {
                    // ensure & is followed by another symbol
                    if (formals.count != 1) return new lval("Symbol '&' not followed by single symbol.", LVAL.ERR);

                    // next formal should be boun dto remaining args
                    lval nsym = formals.pop(0);
                    env.put(nsym, builtin_list(e, a));
                    break;
                }

                // pop the next argument from the list
                lval val = a.pop(0);

                // bind a copy into the function's env
                env.put(sym, val);
            }

            // If '&' remains in formal list bind to empty list 
            if (formals.count > 0 && formals.cell[0].sym_err == "&")
            {
                // Check to ensure that & is not passed invalidly. 
                if (formals.count != 2) return new lval("Function format invalid. Symbol '&' not followed by single symbol.", LVAL.ERR);

                // Pop and delete '&' symbol 
                formals.pop(0);

                // Pop next symbol and create empty list 
                lval sym = formals.pop(0);
                lval val = new lval(LVAL.QEXPR);

                // Bind to environment and delete 
                env.put(sym, val);
            }

            // if all formals have been bound, eval
            if (formals.count == 0)
            {
                // set env parent to eval env 
                //env.par = e; 
                
                // my method sets the previous env's parent to the one above. this funciton would delete that upon fn invocation.
                // in order for both to work, I need to set the previous function's current environment as the one above, and this sets its parent to teh global one.

                // since i have set the lambda's env to any previous env, any first lambda has its parent the global env. 
                // then any nested lambda will get the outer lambda's scope, which is itself linked to the global env. 

                // eval and return 
                // the new sexpr just puts it in a format compatible with builtin_eval, 
                // whose argument is an s-expr that contains one q-expr (because in the original call, the function name is popped,
                // and the result is just a list with only arguments remaining. and for the env we use the lambda's. 
                return builtin_eval(env, new lval(LVAL.SEXPR).add(body.copy()));
            }
            else
            {
                // otherwise return partially evaluated fn
                return this.copy();
            }
        }



        //   I N S T A N C E   M E T H O D S   -   I N T E R N A L
        public lval copy()
        {
            lval x;
            switch (pre_type)
            {
                case LVAL.FUN:
                    if (builtin != null) x = new lval(builtin);
                    else
                    {
                        x = new lval(env.copy(), formals.copy(), body.copy());
                    }
                    break;
                case LVAL.INT: x = new lval(num); break;
                case LVAL.ERR: x = new lval(sym_err, LVAL.ERR); break;
                case LVAL.SYM: x = new lval(sym_err, LVAL.SYM); break;
                case LVAL.STR: x = new lval(sym_err, LVAL.STR); break;
                case LVAL.SEXPR:
                    x = new lval(LVAL.SEXPR);
                    for (int i = 0; i < count; i++) x.cell.Add(cell[i].copy()); break;
                case LVAL.QEXPR:
                    x = new lval(LVAL.QEXPR);
                    for (int i = 0; i < count; i++) x.cell.Add(cell[i].copy()); break;
                default:
                    Tools.LASSERT(null, false, "deep copy failed?");
                    x = new lval("soy", LVAL.ERR);
                    break;
            }

            //// NEW: deep copy the type_scheme
            //if (this.type_scheme != null)
            //{
            //    x.type_scheme = TypeScheme.makeTypeSchemeFromExistingType(this.type_scheme.type);
            //}

                
            return x;
        }
        public lval add(lval x) // add an element to an S-expression 
        {
            cell.Add(x);
            return this;
        }
        private lval pop(int i)
        {
            // ok so the original function is quite interesting. it basically separates 
            // the pointer to x from the list, removes it from list, and returns the pointer.
            // so in memory, nothing is changed except the list forgot about x. 
            // so there is still only one reference to x. 
            // in C#, creating x adds a reference to that item. then i pop it from the list, 
            // which deletes one, so that item's references are still 1. 
            lval x = this.cell[i];

            cell.RemoveAt(i);

            return x;
        }
        public override string ToString()
        {
            this.print();
            return "";
        }
        public void print(string last_char = "")
        {
            switch (pre_type)
            {
                case LVAL.INT: Console.Write(num); break;
                case LVAL.ERR: Console.Write($"Error: {sym_err}"); break;
                case LVAL.SYM: Console.Write(sym_err); break;
                case LVAL.SEXPR: print_expr("(", ")"); break;
                case LVAL.QEXPR: print_expr("{", "}"); break;
                case LVAL.STR:
                    Console.Write('"');
                    // Loop over the characters in the string 
                    for (int i = 0; i < sym_err.Length; i++)
                    {
                        if (Tools.lval_str_escapable.Contains(sym_err[i]))
                        {
                            // If the character is escapable then escape it 
                            Console.Write(Tools.lval_str_escape(sym_err[i]));
                        }
                        else
                        {
                            // Otherwise print character as it is 
                            Console.Write(sym_err[i]);
                        }
                    }
                    Console.Write('"');

                    break;

                case LVAL.FUN:
                    if (this.builtin != null) Console.WriteLine("<builtin>");
                    else
                    {
                        Console.Write("(\\");
                        formals.print();
                        Console.Write(" ");
                        body.print();
                        Console.Write(")");
                    }
                    break;
            }
            Console.Write(last_char);
        }
        private void print_expr(string open, string close)
        {
            Console.Write(open);
            for (int i = 0; i < count; i++)
            {
                // pritn value contained within
                cell[i].print();

                // dont print trailing white space if last element
                if (i != count - 1) Console.Write(' ');
            }
            Console.Write(close);
        }
        //   B U I L T I N   F U N C T I O N S   -   S T A T I C   M E T H O D S 
        static public void lenv_add_builtins(lenv e)
        {
            // list functions
            lenv_add_builtin(e, "list", builtin_list);
            lenv_add_builtin(e, "head", builtin_head);
            lenv_add_builtin(e, "tail", builtin_tail);
            lenv_add_builtin(e, "eval", builtin_eval);
            lenv_add_builtin(e, "join", builtin_join);
            lenv_add_builtin(e, "def", builtin_def);
            lenv_add_builtin(e, "=", builtin_put);
            lenv_add_builtin(e, "\\", builtin_lambda);

            // math functions
            lenv_add_builtin(e, "+", builtin_add);
            lenv_add_builtin(e, "-", builtin_sub);
            lenv_add_builtin(e, "*", builtin_mul);
            lenv_add_builtin(e, "/", builtin_div);

            // comparison functions
            lenv_add_builtin(e, "if", builtin_if);
            lenv_add_builtin(e, "==", builtin_eq);
            lenv_add_builtin(e, "!=", builtin_ne);
            lenv_add_builtin(e, ">", builtin_gt);
            lenv_add_builtin(e, "<", builtin_lt);
            lenv_add_builtin(e, ">=", builtin_ge);
            lenv_add_builtin(e, "<=", builtin_le);

            // misc functions
            lenv_add_builtin(e, "print", builtin_print);
            lenv_add_builtin(e, "error", builtin_error);
            lenv_add_builtin(e, "load", builtin_load);
            lenv_add_builtin(e, "type", builtin_type);
        }
        static private void lenv_add_builtin(lenv e, string name, lbuiltin func) => e.put(new lval(name, LVAL.SYM), new lval(func));
        // arithmetic
        static private lval builtin_add(lenv e, lval a) => builtin_op(e, a, "+");
        static private lval builtin_sub(lenv e, lval a) => builtin_op(e, a, "-");
        static private lval builtin_mul(lenv e, lval a) => builtin_op(e, a, "*");
        static private lval builtin_div(lenv e, lval a) => builtin_op(e, a, "/");
        static private lval builtin_op(lenv e, lval a, string op)
        {
            // ensure all args are numbers
            for (int i = 0; i < a.count; i++)
            {
                if (a.cell[i].pre_type != LVAL.INT) return new lval("Cannot operate on non-number!", LVAL.ERR);
            } // cell.Select(x => x.type == LVAL.NUM).Aggregate((x,y) => x && y)


            // pop first element
            long x = a.pop(0).num;

            // if no arguments and sub then perform unary negation
            if ((op == "-") && a.count == 0) x = -x;

            // while there are still elements remaining
            while (a.count > 0)
            {
                // pop next element
                lval y = a.pop(0);

                switch (op)
                {
                    //case "+": x.num += y.num; break;
                    case "+": x += y.num; break;
                    //case "-": x.num -= y.num; break;
                    case "-": x -= y.num; break;
                    //case "*": x.num *= y.num; break;
                    case "*": x *= y.num; break;
                    case "/":
                        if (y.num == 0)
                            return new lval("Division by zero!", LVAL.ERR);
                        //x.num /= y.num; break;
                        x /= y.num; break;
                }
            }
            return new lval(x);
        }
        // logic 
        static private lval builtin_gt(lenv e, lval a) => builtin_ord(e, a, ">");
        static private lval builtin_lt(lenv e, lval a) => builtin_ord(e, a, "<");
        static private lval builtin_ge(lenv e, lval a) => builtin_ord(e, a, ">=");
        static private lval builtin_le(lenv e, lval a) => builtin_ord(e, a, "<=");
        static private lval builtin_eq(lenv e, lval a) => builtin_cmp(e, a, "==");
        static private lval builtin_ne(lenv e, lval a) => builtin_cmp(e, a, "!=");
        static private lval builtin_ord(lenv e, lval a, string op)
        {
            Tools.LASSERT_NUM(op, a, 2);
            Tools.LASSERT_TYPE(op, a, 0, LVAL.INT);
            Tools.LASSERT_TYPE(op, a, 1, LVAL.INT);

            bool r;
            switch (op)
            {
                case ">": r = (a.cell[0].num > a.cell[1].num); break;
                case "<": r = (a.cell[0].num < a.cell[1].num); break;
                case ">=": r = (a.cell[0].num >= a.cell[1].num); break;
                case "<=": r = (a.cell[0].num <= a.cell[1].num); break;
                default:
                    r = false;
                    Tools.LASSERT(null, false, "Bruh moment"); break;
            }
            return r ? new lval(1) : new lval((long)0);
        }
        static private lval builtin_cmp(lenv e, lval a, string op)
        {
            Tools.LASSERT_NUM(op, a, 2);
            bool r;
            switch (op)
            {
                case "==": r = eq(a.cell[0], a.cell[1]); break;
                case "!=": r = !eq(a.cell[0], a.cell[1]); break;
                default:
                    r = false;
                    Tools.LASSERT(null, false, "Bruh Moment");
                    break;
            }
            return r ? new lval(1) : new lval((long)0);
        }
        static private lval builtin_if(lenv e, lval a)
        {
            Tools.LASSERT_NUM("if", a, 3);
            Tools.LASSERT_TYPE("if", a, 0, LVAL.INT);
            Tools.LASSERT_TYPE("if", a, 1, LVAL.QEXPR);
            Tools.LASSERT_TYPE("if", a, 2, LVAL.QEXPR);

            // mark both expressions as evaluable
            lval x;
            a.cell[1].pre_type = LVAL.SEXPR;
            a.cell[2].pre_type = LVAL.SEXPR;

            if (a.cell[0].num > 0)
            {
                // if condition is true, eval first expr
                x = a.pop(1).eval(e);
            }
            else
            {
                // otherwise evaluate second expr
                x = a.pop(2).eval(e);
            }

            return x;
        }
        static private bool eq(lval x, lval y)
        {
            // diff types are always unequal
            if (x.pre_type != y.pre_type) return false;

            // compare based on type
            switch (x.pre_type)
            {
                // compare nums
                case LVAL.INT: return (x.num == y.num);

                // compare string values 
                case LVAL.ERR: return (x.sym_err == y.sym_err);
                case LVAL.SYM: return (x.sym_err == y.sym_err);
                case LVAL.STR: return (x.sym_err == y.sym_err);

                // if builtin compare, otherwise compare formals and body
                case LVAL.FUN:
                    if (x.builtin != null || y.builtin != null) return x.builtin == y.builtin;
                    else return eq(x.formals, y.formals) && eq(x.body, y.body);

                // if list compare every individual element
                case LVAL.QEXPR:
                case LVAL.SEXPR:
                    if (x.count != y.count) return false;
                    for (int i = 0; i < x.count; i++)
                    {
                        // if any element not equal then whole list not equal
                        if (!eq(x.cell[i], y.cell[i])) return false;
                    }
                    // otherwise must be equal
                    return true;
            }
            return false;
        }
        // list aka Q-expr functions
        static private lval builtin_head(lenv e, lval a)
        {
            Tools.LASSERT(a, a.count == 1, "Function 'head' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].pre_type == LVAL.QEXPR, $"Function 'head' passed incorrect type! Got {Tools.ltype_name(a.cell[0].pre_type)}, Expected {Tools.ltype_name(LVAL.QEXPR)}");
            Tools.LASSERT(a, a.cell[0].count != 0, "Function 'head' passed {}!");
                
            lval v = a.pop(0);//  a.take(0);
            while (v.count > 1) v.pop(1);
            return v;
        }
        static private lval builtin_tail(lenv e, lval a)
        {
            Tools.LASSERT(a, a.count == 1, "Function 'tail' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].pre_type == LVAL.QEXPR, "Function 'tail' passed incorrect type!");
            Tools.LASSERT(a, a.cell[0].count != 0, "Function 'tail' passed {}!");

            lval v = a.pop(0);// a.take(0);
            v.pop(0);
            return v;
        }
        static private lval builtin_list(lenv e, lval a)
        {
            a.pre_type = LVAL.QEXPR;
            return a;
        }
        static private lval builtin_eval(lenv e, lval a)
        {
            // takes in a Q-expr, changes type to S-expr and calls eval() on itself. 
            Tools.LASSERT(a, a.count == 1, "Function 'eval' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].pre_type == LVAL.QEXPR, "Function 'eval' passed incorrect type!");

            lval x = a.pop(0);
            x.pre_type = LVAL.SEXPR;
            return x.eval(e);
        }
        static private lval builtin_join(lenv e, lval a)
        {
            for (int i = 0; i < a.count; i++)
            {
                Tools.LASSERT(a, a.cell[i].pre_type == LVAL.QEXPR, "Function 'join' passed incorrect type!");
            }

            lval x = a.pop(0);

            while (a.count > 0)
            {
                x.cell.AddRange(a.pop(0).cell); // used to be x.join(a.pop(0));
            }

            return x;
        }
        // variable & fn creation
        static private lval builtin_def(lenv e, lval a) => builtin_var(e, a, "def");
        static private lval builtin_put(lenv e, lval a) => builtin_var(e, a, "=");
        static private lval builtin_var(lenv e, lval a, string func)
        {
            Tools.LASSERT_TYPE(func, a, 0, LVAL.QEXPR);

            // first argument is symbol list 
            lval syms = a.cell[0];

            // ensure all elements of first lsit are symbols
            for (int i = 0; i < syms.count; i++)
            {
                Tools.LASSERT(a, syms.cell[i].pre_type == LVAL.SYM, "Function 'def' canot define non-symbol!");
            }

            // check correct number of symbols and values 
            Tools.LASSERT(a, syms.count == a.count - 1, "Function 'def' cannot define incorrect number of values to symbols");

            // assign copies of values to symbols
            for (int i = 0; i < syms.count; i++)
            {
                // if 'def' define globally. if 'put' define locally
                if (func == "def") e.def(syms.cell[i], a.cell[i + 1]);

                if (func == "=") e.put(syms.cell[i], a.cell[i + 1]);
            }

            return new lval(LVAL.SEXPR);
        }
        static private lval builtin_lambda(lenv e, lval a)
        {
            // check two arguments, each of which are Q-expr -> a is (Q, Q) from the original (\ Q Q)
            Tools.LASSERT_NUM("\\", a, 2); // checks only 2 arguments in 'a'
            Tools.LASSERT_TYPE("\\", a, 0, LVAL.QEXPR); // first arg in 'a' is QEXPR
            Tools.LASSERT_TYPE("\\", a, 1, LVAL.QEXPR); // same for second

            // check first Q-expr contains only symbols (function's formals)
            for (int i = 0; i < a.cell[0].count; i++)
            {
                Tools.LASSERT(a, (a.cell[0].cell[i].pre_type == LVAL.SYM), $"Cannot define non-symbol, Got {Tools.ltype_name(a.cell[0].cell[i].pre_type)}, Expected {Tools.ltype_name(LVAL.SYM)}.");
            }

            // pop first two arguments and pass them to lval_lambda
            lval formals = a.pop(0);
            lval body = a.pop(0);

            lenv new_env = new lenv();
            new_env.par = e;
            return new lval(new_env, formals, body); 
        }
        // external interfacing and misc
        static private lval builtin_print(lenv e, lval a)
        {
            // print each argument followed by a space 
            for (int i = 0; i < a.count; i++)
            {
                a.cell[i].print();
                Console.Write(' ');
            }

            // print a new line
            Console.WriteLine();

            return new lval(LVAL.SEXPR);
        }
        static private lval builtin_error(lenv e, lval a)
        {
            Tools.LASSERT_NUM("error", a, 1);
            Tools.LASSERT_TYPE("error", a, 0, LVAL.STR);

            // construct error from first argument
            lval err = new lval(a.cell[0].sym_err, LVAL.STR);
            return err;
        }
        static private lval builtin_load(lenv e, lval a)
        {
            Tools.LASSERT_NUM("load", a, 1);
            Tools.LASSERT_TYPE("load", a, 0, LVAL.STR); // cant be SYM bc it gets evaluated first -.- 

            Tools.load_script(e, a.cell[0].sym_err); // need to unwrap external S-expr

            return new lval(LVAL.SEXPR);
        }
        static private lval builtin_type(lenv e, lval a)
        {
            // a IS AN S-EXPR WRAPPER
            // the argument a we get is an s-expr containing all the arguments
            // so if the original expression was (+ 2 2) then a = (2 2), an s-expr of args
            // so we need to do a loop. Or I can just assert that it takes 1 argument.
            if (a.count > 1) return new lval("function <type> passed in more than 1 argument! Sad!", LVAL.ERR);

            lval x = a.cell[0];
            Console.WriteLine("type := " + x.pre_type);

            if (x.pre_type == LVAL.FUN)
            {
                Console.WriteLine("Function Environment:");
                x.env.print();
            }
            return new lval(LVAL.SEXPR);
        }
    }
    // Lval Types
    public enum LVAL { ERR, INT, STR, FUN, SEXPR, QEXPR, SYM, TYPE_CARRIER, }
    public delegate lval lbuiltin(lenv x, lval a);
    public class lenv : Dictionary<string, lval>
    {
        public lenv par = null; // parent env
        public lenv() : base() { }
        public lval get(lval k)
        {
            if (this.ContainsKey(k.sym_err)) return this[k.sym_err].copy();

            // if no symbol check in parent otherwise error
            if (par != null) return par.get(k);
            else return new lval($"Unbound Symbol! {k.sym_err}", LVAL.ERR);
        }
        public void put(lval k, lval v) // for local env. I M M U T A B L E 
        {
            // this makes it immutable, BUT ONLY IN ONE SCOPE! (think one cactus leg)
            if (ContainsKey(k.sym_err)) 
                Tools.LASSERT(null, false, $"Tried to mutate already defined variable: {k.sym_err}");
            this[k.sym_err] = v.copy();
        }
        public void def(lval k, lval v)
        {
            // iterate till e has no parent
            lenv e = this;
            while (e.par != null) e = e.par;
            // put value in e
            e.put(k, v);
        }
        public lenv copy() // this function makes the class kinda integrated with LVAL so i dont wanna genericalize it
        {
            lenv n = new lenv(); 
            n.par = par;
            foreach (var key in Keys) n[key] = this[key].copy();
            return n;
        }
        public void print(string offset = "")
        {
            if (Keys.Count == 0) Console.WriteLine($"{offset}<empty>");
            foreach (var k in Keys) Console.WriteLine($"{offset}{k}\t->\t{this[k].pre_type}");
            if (par != null)
            {
                if (this.par.par != null) Console.WriteLine($"{offset}  ||_p_a_r_e_n_t_|| :=");
                else Console.WriteLine($"{offset}  ||_G_L_O_B_A_L_|| :=");

                par.print(new String(' ', 4) + offset);
            }
        }
    }
    public static class Tools
    {
        private static bool USE_SQUARE_BRACKETS = false; // only for typing in and reading scripts. 
        private static char L_PAREN = USE_SQUARE_BRACKETS ? (char)91 : (char)40;
        private static char R_PAREN = USE_SQUARE_BRACKETS ? (char)93 : (char)41;
        // load a script
        public static void load_script(lenv e, string script_name, string base_dir = @"C:\Users\pwnag\source\repos\Lispy\Lispy\")
        {
            // get full file name & read it
            string file_name = System.IO.Path.Join(base_dir, script_name + ".lispy");
            var sb = new StringBuilder(System.IO.File.ReadAllText(file_name, Encoding.ASCII));
            
            // run script
            lval x = new lval(LVAL.SEXPR); // x is wrapper for all exprs in the file
            Tools.lval_read_expr(x, sb.Append('\0').ToString(), 0, '\0');
            foreach (lval expr in x.cell)
            {
                expr.print("\n");           // print original exp
                expr.eval(e).print("\n");   // print evaluated exp
            }
        }
        // A S S E R T S 
        public static void LASSERT(object args, bool cond, string err, params object[] list)
        {
            if (!cond)
            {
                //return new lval(err, list);
                throw new Exception(err);
            }
        }
        public static void LASSERT_TYPE(string func, lval args, int index, LVAL expect)
        {
            LASSERT(args, args.cell[index].pre_type == expect,
                $"Function {func} passed incorrect type for argument {index}. Got {ltype_name(args.cell[index].pre_type)}, Expected {ltype_name(expect)}.");
        }
        public static void LASSERT_NUM(string func, lval args, int num)
        {
            LASSERT(args, args.count == num,
                $"Function {func} passed incorrect number of arguments. Got {args.count}, Expected {num}.");
        }
        public static void LASSERT_NOT_EMPTY(lval func, lval args, int index)
        {
            LASSERT(args, args.cell[index].count != 0,
                $"Function {func} passed {{}} for argument {index}.");
        }
        public static string ltype_name(LVAL t)
        {
            switch (t)
            {
                case LVAL.FUN: return "Function";
                case LVAL.INT: return "Number";
                case LVAL.ERR: return "Error";
                case LVAL.SYM: return "Symbol";
                case LVAL.STR: return "String";
                case LVAL.SEXPR: return "S-Expression";
                case LVAL.QEXPR: return "Q-Expression";
                default: return "Unknown";
            }
        }
        // Parsing 
        public static int lval_read_expr(lval v, string s, int i, char end)
        {
            // every expression that is read gets added to the cells in the input lval

            while (s[i] != end)
            {
                // If we reach end of input then there is some syntax error 
                if (s[i] == '\0')
                {
                    v.add(new lval($"Missing {end} at end of input", LVAL.ERR));
                    return s.Length + 1;
                }

                // Skip all whitespace 
                if (" \t\v\r\n".Contains(s[i]))
                {
                    i++;
                    continue;
                }

                // If next char is ; then read comment 
                if (s[i] == ';')
                {
                    while (s[i] != '\n' && s[i] != '\0') i++;
                    i++; // pass over the \n or \0
                    continue;
                }

                // If next character is ( then read S-Expr 
                if (s[i] == L_PAREN)
                {
                    lval x = new lval(LVAL.SEXPR);
                    v.add(x);
                    i = lval_read_expr(x, s, i + 1, R_PAREN);
                    continue;
                }

                // If next character is { then read Q-Expr 
                if (s[i] == '{')
                {
                    lval x = new lval(LVAL.QEXPR);
                    v.add(x);
                    i = lval_read_expr(x, s, i + 1, '}');
                    continue;
                }

                // If next character is part of a symbol then read symbol 
                if ("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_+-*\\/=<>!&".Contains(s[i]))
                {
                    i = lval_read_sym(v, s, i);
                    continue;
                }

                // If next character is " then read string
                if (s[i] == '"')
                {
                    i = lval_read_str(v, s, i + 1);
                    continue;
                }

                // Encountered some unknown character
                v.add(new lval($"Unknown Character {s[i]}", LVAL.ERR));
                return s.Length + 1;
            }

            return i + 1; // added 1 because of the )
        }
        public static int lval_read_sym(lval v, string s, int i)
        {
            StringBuilder sb = new StringBuilder();

            // While valid identifier characters 
            while ("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_+-*\\/=<>!&".Contains(s[i]) && s[i] != '\0')
            {
                sb.Append(s[i]);
                i++;
            }

            // Check if Identifier looks like number 
            bool is_num = "-0123456789".Contains(sb[0]);
            for (int j = 1; j < sb.Length; j++)
            {
                if (!"0123456789".Contains(sb[j])) { is_num = false; break; }
            }

            // Add Symbol or Number as lval 
            if (is_num)
            {
                if (sb[0] == '-')
                {
                    switch (sb.Length)
                    {
                        case 1: v.add(new lval(sb.ToString(), LVAL.SYM)); break; // minus sign
                        default: // neg num case
                            long x = long.Parse(sb.ToString().Substring(1));
                            v.add(new lval(-x));
                            break;
                    }
                }
                else
                {
                    long x = long.Parse(sb.ToString());
                    v.add(new lval(x));
                }
            }
            else
            {
                v.add(new lval(sb.ToString(), LVAL.SYM));
            }

            // Return updated position in input
            return i;
        }
        // Possible unescapable characters 
        public static string lval_str_unescapable = "abfnrtv\\\'\"";
        // List of possible escapable characters 
        public static string lval_str_escapable = "\a\b\f\n\r\t\v\\\'\"";
        // string crap 
        public static int lval_read_str(lval v, string s, int i)
        {
            StringBuilder sb = new StringBuilder();

            while (s[i] != '"')
            {
                char c = s[i];

                // If end of input then there is an unterminated string literal
                if (c == '\0')
                {
                    v.add(new lval("Unexpected end of input at string literal", LVAL.ERR));
                    return s.Length;
                }

                // If backslash then unescape character after it 
                if (c == '\\')
                {
                    i++;
                    // Check next character is escapable 
                    if (lval_str_unescapable.Contains(s[i]))
                    {
                        c = lval_str_unescape(s[i]);
                    }
                    else
                    {
                        v.add(new lval($"Invalid escape character {c}", LVAL.ERR));
                        return s.Length;
                    }
                }
                // Append character to string 
                sb.Append(c);
                i++;
            }

            // Add lval and free temp string 
            v.add(new lval(sb.ToString(), LVAL.STR));

            return i + 1;
        }
        public static char lval_str_unescape(char x) // Function to unescape characters 
        {
            switch (x)
            {
                case 'a': return '\a';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return '\v';
                case '\\': return '\\';
                case '\'': return '\'';
                case '\"': return '\"';
            }
            return '\0';
        }
        public static string lval_str_escape(char x) // Function to escape characters 
        {
            switch (x)
            {
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\v': return "\\v";
                case '\\': return "\\\\";
                case '\'': return "\\\'";
                case '\"': return "\\\"";
            }
            return "";
        }
    }
    // Generic Environment
    public class Env<V> : Dictionary<string, V>
    {
        public Env<V> par = null; // parent env 
        public Env() : base() { }
        public V get(string k)
        {
            if (this.ContainsKey(k)) return this[k];

            // if no symbol check in parent otherwise err
            if (par != null) return par.get(k);
            else throw new Exception("Key not found in env");
        }
        public void put(string k, V v)
        {
            if (this.ContainsKey(k)) throw new Exception("Trying to modify existing value!");
            this[k] = v;
        }
        public void print(string offset = "")
        {
            if (Keys.Count == 0) Console.WriteLine($"{offset}<empty>");
            foreach (var k in Keys) Console.WriteLine($"{offset}{k}\t->\t{this[k]}");
            if (par != null)
            {
                if (this.par.par != null) Console.WriteLine($"{offset}  ||_p_a_r_e_n_t_|| :=");
                else Console.WriteLine($"{offset}  ||_G_L_O_B_A_L_|| :=");

                par.print(new String(' ', 4) + offset);
            }
        }
        /*
        public void put_in_global(string k, V v)
        {
            Env<V> e = this;
            while (e.par != null) e = e.par; // iterate till no parent
            e.put(k, v);
        }
        */
    }


    //   H I N D L E Y - M I L N E R   T Y P E   I N F E R E N C E 
    public class TypeScheme
    {
        public List<TypeVariable> type_variables; //- if i have a tree i might not need the list 
        public Type type;
        private TypeScheme() { }
        /// <summary>
        /// Generates a new type scheme with a fresh Type_Variable (binding level 0), and a tvs list containing that variable. 
        /// </summary>
        static public TypeScheme makeTypeSchemeFromNewTypeVar(int binding_level)
        {
            TypeVariable tv = TypeVariable.newTypeVar(binding_level);
            TypeScheme ts = new TypeScheme();
            ts.type = Type.makeNewTypeVarType(tv);
            ts.type_variables = new List<TypeVariable>();// { tv };
            return ts;
        }
        /// <summary>
        /// Instantiate a type_scheme from an existing Type t. Copy t into local type. If t has tag Type_Variable or Fun, add it to the list. 
        /// </summary>
        /// <param name="t"></param>
        static public TypeScheme makeTypeSchemeFromExistingType(Type t)
        {
            TypeScheme ts = new TypeScheme();
            //ts.type = t.copy();
            ts.type = t;
            ts.type_variables = new List<TypeVariable>();
            //if (t.tag == Tag.TypeVar) ts.type_variables.Add(t.type_var.copy()); // add type var to list
            if (t.tag == Tag.TypeVar) ts.type_variables.Add(t.type_var); // add type var to list
            if (t.tag == Tag.Fun)
            {
                foreach (var arg in t.args) // add any of the type variables in the function type to the list
                {
                    // since we are making a new Type_Scheme, we don't need to check if the type_var is already in it. 
                    //if (arg.type_scheme.type.tag == Tag.TypeVar) ts.type_variables.Add(arg.type_scheme.type.type_var.copy());
                    if (arg.tag == Tag.TypeVar) ts.type_variables.Add(arg.type_var);
                }
            }
            return ts;
        }
        // it makes no sense to instantiate a TypeScheme from a list of type_variables. lol. 
        // there is no need to instantiate a Type_Scheme from a type and a list of tvs - its done automatically with type 
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(" ");

            foreach (var type_var in this.type_variables)
                sb.Append(type_var.type_var_name + " ");
            return $"[{sb}], Type := {this.type}";
        }
        /*
        public TypeScheme copy()
        {
            TypeScheme ts = new TypeScheme();
            ts.type = this.type.copy();
            ts.type_variables = new List<TypeVariable>();
            foreach (var tv in this.type_variables)
                ts.type_variables.Add(tv);
            return ts;
        }
        */
    }
    public enum TVTag { NoLink, LinkTo } // TODO: see if i ever end up using this lol
    public class TypeVariable
    {
        public TVTag tag;
        // T Y P E   V A R I A B L E   :   it has a binding level, name, and possibly link to its eq class
        public int binding_level;
        // now a type_var also has a type_var_kind: either NoLink(string name) or LinkTo(Type t). 
        public string type_var_name;
        public Type next_friend;
        private TypeVariable(TVTag tag, int binding_level, string type_var_name, Type next_friend)
        {
            this.tag = tag;
            this.binding_level = binding_level;
            this.type_var_name = type_var_name;
            this.next_friend = next_friend;
        }
        public static TypeVariable makeNewTypeVariableNoLink(int binding_level, string type_var_name) => new TypeVariable(TVTag.NoLink, binding_level, type_var_name, null);
        public static TypeVariable makeNewTypeVariableLinkTo(int binding_level, Type next_friend) => new TypeVariable(TVTag.LinkTo, binding_level, null, next_friend);
        /*
        public TypeVariable copy()
        {
            TypeVariable t = new TypeVariable(this.tag, this.binding_level, this.type_var_name, null);
            if (this.next_friend != null)
                t.next_friend = this.next_friend;
            return t;
        }
        */
        public override string ToString()
        {
            return $"Type_Variable {type_var_name}; Tag: {tag}; Binding_lvl: {binding_level}; Next_friend: {next_friend}";
        }
        //   N E W   - generates new TypeVariable node, with fresh name - from Soystoft
        static int tyvarno = -1;
        public static TypeVariable newTypeVar(int level)
        {
            Console.WriteLine(tyvarno);
            /*
            string mkname(int i, string res)
            {
                if (i < 26) return ((char)(97 + i) + res).ToString();
                else return mkname(i / (26 - 1), (char)(97 + (i % 26)) + res);
            }
            string name = '\'' + mkname(tyvarno, "");
            */
            tyvarno++;
            string name = 't' + tyvarno.ToString();
            return TypeVariable.makeNewTypeVariableNoLink(level, name);
        }
    }
    public enum Tag { Int, Err, Sym, Str, Fun, Sexpr, Qexpr, TypeVar, META_ERROR, META_LAMBDA, }
    public class Type
    {
        // TODO: unions: google can we use unions in c#,explicit struct layout. For now make explicit memebers!
        public Tag tag; // mutable: can change from TypeVar to other!
        readonly public string m; // error message to propagate type error.
        // F U N C T I O N   T Y P E   :   we have the type of its input an output:
        public List<Type> args; // list since can be a -> b -> c -> d -> res ;; need to use lval to wrap Type otherwise its not a reference :(
        // TYpe Variable kind -
        public TypeVariable type_var;
        private Type(Tag tag, string m, List<Type> args, TypeVariable type_var)
        {
            this.tag = tag;
            this.m = m;
            this.args = args;
            this.type_var = type_var;
        }
        static public Type make_META_ERROR(string m) => new Type(Tag.META_ERROR, m, null, null);
        static public Type make_META_LAMBDA() => new Type(Tag.META_LAMBDA, null, null, null);
        static public Type makeNewSexprType() => new Type(Tag.Sexpr, null, null, null);
        static public Type makeNewNumType() => new Type(Tag.Int, null, null, null);
        static public Type makeNewQexprType() => new Type(Tag.Qexpr, null, null, null);
        static public Type makeNewStrType() => new Type(Tag.Str, null, null, null);
        static public Type makeNewErrType(string m) => new Type(Tag.Str, m, null, null);
        static public Type makeNewFunType(List<Type> args)
        {
            Type t = new Type(Tag.Fun, null, null, null);
            t.args = new List<Type>();
            foreach (var _arg in args)
                t.args.Add(_arg); // TODO: this will be a list of references.. which is exactly what we want! 
            return t;
        }
        static public Type makeNewTypeVarType(TypeVariable type_var) => new Type(Tag.TypeVar, null, null, type_var);
        /*
        public Type copy()
        {
            Type t = new Type(this.tag, this.m, null, null);
            t.args = new List<lval>();
            if (this.tag == Tag.Fun)
                foreach (var arg in this.args)
                    t.args.Add(arg.copy());
            if (this.tag == Tag.TypeVar)
                t.type_var = this.type_var.copy();
            return t;
        }
        */
        public override string ToString()
        {
            switch (tag)
            {
                case Tag.Int: return "int";
                case Tag.Err: return "error_type";
                case Tag.Fun:
                    // function should have at least length 2 for input, output.
                    StringBuilder sb = new StringBuilder($"{args[0]}");
                    foreach (var arg in args.Skip(1))
                        sb.Append($" -> {arg}");
                    return sb.ToString();
                case Tag.Str: return "string";
                case Tag.TypeVar:
                    if (this.type_var.tag == TVTag.NoLink)
                        return $"{type_var.type_var_name}";
                    else
                    {
                        var r = this.type_var.next_friend;
                        while (r.tag == Tag.TypeVar && r.type_var.tag == TVTag.LinkTo)
                            r = r.type_var.next_friend;
                        if (r.tag == Tag.TypeVar)
                            return $"{r.type_var.type_var_name}";
                        else return r.ToString();
                    }


                case Tag.META_ERROR: return m; // prints the actual error
                case Tag.Sym: throw new NotImplementedException();
                case Tag.Sexpr: return "S-Expr"; // this should never happen unless empty (), S-expr are fn calls
                case Tag.Qexpr: return "Q-Expr";
                case Tag.META_LAMBDA: return "META-LAMBDA";
            }
            throw new Exception("printing non existing type");
        }
        private static bool occursIn(string type_var_name, Type type) // tells if type_var_name occurs in type
        {
            switch (type.tag)
            {
                case Tag.TypeVar:
                    if (type.type_var.type_var_name == type_var_name) return true;
                    return false;
                case Tag.Fun:
                    foreach (var arg in type.args)
                        if (occursIn(type_var_name, arg)) return true;
                    return false;
                default:
                    return false;
            }
        }

        // S O Y S T O F T 
        public static Type normType(Type t0)
        {
            if (t0.tag == Tag.TypeVar)
            {
                if (t0.type_var.tag == TVTag.LinkTo)
                {
                    Type t1 = t0.type_var.next_friend;
                    Type t2 = normType(t1); // TODO: try replace recursion with loop
                    t0.type_var.tag = TVTag.LinkTo;
                    t0.type_var.next_friend = t2;
                    return t2;
                }
            }
            return t0;
        }
        public static List<TypeVariable> freeTypeVars(Type t_)
        {
            Type t = normType(t_);
            switch (t.tag)
            {
                case Tag.Int:
                case Tag.Str:
                case Tag.Err:
                    return new List<TypeVariable>();
                case Tag.TypeVar:
                    return new List<TypeVariable>() { t.type_var };
                case Tag.Fun:
                    List<TypeVariable> r = new List<TypeVariable>();
                    foreach (var _r in t.args)
                    {
                        r.AddRange(freeTypeVars(_r));
                    }
                    return r;
                default: throw new NotImplementedException();
            }
        }
        public static void pruneLevel(int maxLevel, List<TypeVariable> tvs)
        {
            foreach (var tyvar in tvs) tyvar.binding_level = Math.Min((int)tyvar.binding_level, maxLevel);
        }
        public static void occurCheck(TypeVariable tyvar, List<TypeVariable> tyvars)
        {
            if (mem(tyvar, tyvars)) throw new Exception("type error: circularity");
        }
        public static bool mem(TypeVariable x, List<TypeVariable> vs)
        {
            foreach (var v in vs)
            {
                if (v.type_var_name == x.type_var_name) return true;
            }
            return false;
        }
        public static List<TypeVariable> unique(List<TypeVariable> xs) => xs.Distinct().ToList();
        public static List<TypeVariable> union(List<TypeVariable> xs, List<TypeVariable> ys) => xs.Union(ys).ToList();
        public static void linkVarToType(TypeVariable tyvar, Type t) // U N I O N 
        {
            // makes tyvar link to t, so equal to it. first check it doesnt occur inside. reduce the level of all type variables in t to that of tyvar.
            var level = tyvar.binding_level;
            var fvs = freeTypeVars(t);
            occurCheck(tyvar, fvs);
            pruneLevel(level, fvs); // TODO: check if this actually changes values
            tyvar.next_friend = t;
            tyvar.tag = TVTag.LinkTo;
        }
        public static TypeScheme generalize(int level, Type t)
        {
            bool notfreeincontext(TypeVariable tyvar) => tyvar.binding_level > level;
            List<TypeVariable> tvs = freeTypeVars(t).Where(notfreeincontext).ToList();

            TypeScheme r = TypeScheme.makeTypeSchemeFromNewTypeVar(level);
            r.type_variables = unique(tvs);
            r.type = t;
            return r;
        }
        public static Type copyType(List<(TypeVariable, Type)> subst, Type t)
        {
            switch (t.tag)
            {
                case Tag.Int:
                case Tag.Err:
                case Tag.Str:
                    return t;
                case Tag.Fun:
                    // for my f: t0->t1->t0 example, _args is exactly the list [t0, t1, t0]. 
                    // and subst here is [(t0,t2), (t1,t3)]

                    // basically iterate over the function args and copy them (leaves concrete types alone, substitutes type_vars)
                    List<Type> _args = new List<Type>();
                    foreach (Type _arg in t.args)
                    {
                        Type _copied = copyType(subst, _arg);
                        _args.Add(_copied);
                    }
                    return Type.makeNewFunType(_args);


                case Tag.TypeVar:
                    // BASICALLY REPLACES THE TYPE_VAR IF ITS IN THE SUBST LIST, OTHERWISE RETURNS CANONICAL REP

                    TypeVariable tyvar = t.type_var; // extract the type variable 

                    // iterate over subst list - if the type_var names match, return that to-substitute-Type. 
                    foreach ((TypeVariable, Type) subs in subst) if (tyvar == subs.Item1) return subs.Item2;
                    // if the type_var is not in subs-list:
                    switch (tyvar.tag) // walks up the equivalence_relation to return the canonical representative of the type
                    {
                        case TVTag.NoLink: return t;
                        case TVTag.LinkTo: return copyType(subst, tyvar.next_friend);
                    }
                    throw new Exception("bruh moment in Loop");
            }
            throw new NotImplementedException("bruh moment in CopyTYpe");
        }
        public static Type specialize(int level, TypeScheme typescheme) // OK IT TAKES A TYPE SCHEME, 
        {
            // AND CREATES A REAL TYPE BY GENERATING NEW TYPE_VARS FOR ALL THE ONES IN THE LIST OF THE TYPE SCHEME
            // SO imagine if we had a type scheme for a function, then apply it at 2 diff places - this would yield diff types as need be
            // systemagically replace all type variables with new type_variables 
            var tvs = typescheme.type_variables;
            var t = typescheme.type;

            //foreach (var _tv in tvs) Console.WriteLine(_tv);
            //Console.WriteLine(t);

            if (tvs.Count == 0) return t; // monomorphic 
            else
            {
                // bind fresh - to each type variable, create a new Type from it 
                // basically we pass in a TypeScheme, which is a list of type variables, and the type made from them.
                // here we take the type variables, and for each one make a tuple of that tv, and a NEW TypeVarType.
                // so in my original function, f: t0->t1->t0. we make a list of tuples, [(t0,t2), (t1,t3)].
                // when it gets passed to copyType, it inductively places the new TypeVarTypes (t2 and t3) in the 
                // structure where t0 and t1 were before (considering the type as a tree). 
                List<(TypeVariable, Type)> subst = tvs.Select(tv => (tv, Type.makeNewTypeVarType(TypeVariable.newTypeVar(level)))).ToList();
                return copyType(subst, t);
            }
        }
        public static void unify(Type t1, Type t2)
        {
            Type t1_ = normType(t1);
            Type t2_ = normType(t2);

            if (t1_.tag == Tag.Int && t2_.tag == Tag.Int) return;
            else if (t1_.tag == Tag.Err && t2_.tag == Tag.Err) return;
            else if (t1_.tag == Tag.Str && t2_.tag == Tag.Str) return;
            else if (t1_.tag == Tag.Fun && t2_.tag == Tag.Fun)
            {
                // just unify the args in the types' args lists. ASSUMING THEY ARE SAME SIZE.
                for (int i = 0; i < t1_.args.Count; i++)
                {
                    unify(t1_.args[i], t2_.args[i]);
                }
                return;
            }
            else if (t1_.tag == Tag.TypeVar && t2_.tag == Tag.TypeVar)
            {
                var tv1 = t1_.type_var;
                var tv2 = t2_.type_var;
                var tv1level = tv1.binding_level;
                var tv2level = tv2.binding_level;
                if (tv1 == tv2) return;
                else if (tv1level < tv2level)
                {
                    linkVarToType(t1.type_var, t2);
                }
                else
                {
                    linkVarToType(t2.type_var, t1);
                }
            }
            else if (t1_.tag == Tag.TypeVar)
            {
                linkVarToType(t1.type_var, t2);
            }
            else if (t2_.tag == Tag.TypeVar)
            {
                linkVarToType(t2.type_var, t1);
            }
            else throw new Exception("type error of various ilk");
        }
        public static Type typeCheck(int lvl, Env<TypeScheme> env, lval e)
        {
            switch (e.pre_type)
            {
                case LVAL.QEXPR: return Type.makeNewQexprType(); // this paradigm is prob what will cause type errors lol
                case LVAL.ERR: return Type.makeNewErrType(e.sym_err);
                case LVAL.INT: return Type.makeNewNumType();
                case LVAL.STR: return Type.makeNewStrType();
                case LVAL.SYM:
                    // represents variable case in Micro-ML. Works nicely for builtins. 
                    var soy = env.get(e.sym_err);
                    Console.WriteLine(soy);
                    var symbol_type = specialize(lvl, soy); // "refreshes" the type scheme w new Type_Vars. 
                    // TODO: see if i can return an error type if the SYM is not in the tenv
                    return symbol_type;
                case LVAL.SEXPR:
                    switch (e.count)
                    {
                        case 0: return Type.makeNewSexprType(); // empty S-expr
                        case 1:
                            return typeCheck(lvl, env, e.cell[0]); // one thing wrapped in an S-expr
                        default: break; // according to my typing rules, this is Call type in MML
                    }

                    // type check the 1st element, meaning the function
                    Type op = typeCheck(lvl, env, e.cell[0]);

                    // if its a lambda, (also diverge here for diff special operators) then go that way - 
                    if (op.tag == Tag.META_LAMBDA) return typeCheckLambdaExpression(lvl, env, e);

                    // type check the rest of the exp, the "arguments" TODO: wat do in curry case?
                    var _exp_carrier = e.cell.Skip(1).Select(x => typeCheck(lvl, env, x)).ToList();

                    // add a result type - just a type_variable
                    _exp_carrier.Add(Type.makeNewTypeVarType(TypeVariable.newTypeVar(lvl)));

                    // the entire S-expr type, to be conpared to the operator's type
                    Type exp = Type.makeNewFunType(_exp_carrier);

                    Console.WriteLine(op);
                    Console.WriteLine(exp);

                    unify(op, exp);

                    Console.WriteLine();
                    Console.WriteLine(op);
                    Console.WriteLine(exp);

                    // L A T E R
                    // OK SO for example if I do (+ 2 2) then + has type int->int->int, 
                    // and the "rest of the expression" will have type int->int->t0, since t0 will be added as the result type.
                    // then we pass that to get unified, and return the original +'s type. 
                    // - First off, I think we should return the function's return type. 
                    // - second, in the case (+ 2) we can unify part of the function's type, but how would that 
                    // work when the op's type is a variable?

                    // TODO: what happens in a lambda, when the operator has a type variable?
                    
                    var ret_type = _exp_carrier.GetRange(e.cell.Count - 1, _exp_carrier.Count - e.cell.Count + 1);

                    return Type.makeNewFunType(ret_type); // needed for nested lambdas
            }
            return Type.make_META_ERROR("Type check failed bruv");
        }
        public static Type typeCheckLambdaExpression(int lvl, Env<TypeScheme> env, lval e)
        {
            if (e.cell.Count != 3) return Type.make_META_ERROR("Lambda body does not have 3 things!");
            if (e.cell[1].pre_type != LVAL.QEXPR) return Type.make_META_ERROR("Function parameters not a Q-Expression!");
            if (e.cell[2].pre_type != LVAL.QEXPR) return Type.make_META_ERROR("Lambda body not a Q-Expr!");

            // wow such clever idea - give it a temporary name for the sake of type checking the fucking thing

            int lvl1 = lvl + 1;
            var fType = TypeScheme.makeTypeSchemeFromNewTypeVar(lvl1);
            // at this point, make new type var is called twice for + and \ in setup, for his, never. so good by this pt.

            // env for the body 
            Env<TypeScheme> fBodyEnv = new Env<TypeScheme> { par = env };// we dont add f to env for recursive calls because *Lambda Calc only supports rec thru Y; dab*
            fBodyEnv.Add("LAMBDA", fType); // temp name for our nameless function 



            // iterate over the lambda params, add them to the local type_env, and construct the type for the lambda
            List<Type> _lambda_type = new List<Type>();
            foreach (var func_param in e.cell[1].cell)
            {
                // check that its a symbol
                if (func_param.pre_type != LVAL.SYM) return Type.make_META_ERROR("Non-Symbol found inside Lambda parameters!");

                // generate a new type scheme, and add it to the env and lambda-arg-list
                var one_param = TypeScheme.makeTypeSchemeFromNewTypeVar(lvl1);
                fBodyEnv.Add(func_param.sym_err, one_param);
                _lambda_type.Add(one_param.type);
            }




            // body's return type
            lval fBody = e.cell[2].copy();
            fBody.pre_type = LVAL.SEXPR; // run it as an S-Expr

            // goes in as (n), an S-expr, then as n, a symbol -> gets looked up, then generalized
            // ok the body is also Var"n" there so it goes thru variable path - gets looked up, then generalized

            Type rType = typeCheck(lvl1, fBodyEnv, fBody);

            // OK PROBLEM: in the original, rType is the same as n's type from the env, here its diff
            //Console.WriteLine("R TYPE: " + rType);
            //fBodyEnv.print();

            // so \n.n results in t3->t4 because when we typeCheck the body, the variabel gets generalized. 
            //Console.WriteLine(rType); // ok this is fine because in the original its 'c and 'd at this point.
            // they must get unified later. 

            // add return type to lambda's arg list 
            _lambda_type.Add(rType);

            // make new type out of the lambda-arg-list
            Type lambda_type = Type.makeNewFunType(_lambda_type);

            unify(fType.type, lambda_type); // TODO: this does literally nothing as f is not linked to a function name in the tenv



            var bodyEnv = new Env<TypeScheme> { par = env };
            bodyEnv.Add("LAMBDA", generalize(lvl, fType.type));

            // my way to resolve this: make the let-body be just the function itself, so it returns its type.
            lval letBody = new lval("LAMBDA", LVAL.SYM); // first try SYM, then try user-def-fn

            bodyEnv.print();
            return typeCheck(lvl, bodyEnv, letBody);
        }


        //   A D D   B U I L T I N S 
        static public void tenv_add_builtins(Env<TypeScheme> te)
        {
            // math functions
            var i1 = Type.makeNewNumType();
            var i2 = Type.makeNewNumType();
            var i3 = Type.makeNewNumType();
            Type i_i_i = Type.makeNewFunType(new List<Type>() { i1, i2, i3 }); // int -> int -> int
            TypeScheme ts = TypeScheme.makeTypeSchemeFromExistingType(i_i_i);

            tenv_add_builtin(te, "+", ts);
            tenv_add_builtin(te, "-", ts);
            tenv_add_builtin(te, "*", ts);
            tenv_add_builtin(te, "\\", TypeScheme.makeTypeSchemeFromExistingType(Type.make_META_LAMBDA())); // i dont think we actually need this line 

            // TEST - make some fn f that is generic to test what happens in "specialize"
            i1 = Type.makeNewTypeVarType(TypeVariable.newTypeVar(0));
            i2 = Type.makeNewTypeVarType(TypeVariable.newTypeVar(0));
            Type f = Type.makeNewFunType(new List<Type>() { i1, i2, i1 }); // a->b->a
            tenv_add_builtin(te, "f", TypeScheme.makeTypeSchemeFromExistingType(f));


            /*
            tenv_add_builtin(te, "/",  ts);
            tenv_add_builtin(te, ">",  ts);
            tenv_add_builtin(te, "<",  ts);
            tenv_add_builtin(te, ">=", ts);
            tenv_add_builtin(te, "<=", ts);

            // list functions
            tenv_add_builtin(e, "list", builtin_list);
            tenv_add_builtin(e, "head", builtin_head);
            tenv_add_builtin(e, "tail", builtin_tail);
            tenv_add_builtin(e, "eval", builtin_eval);
            tenv_add_builtin(e, "join", builtin_join);
            tenv_add_builtin(e, "def", builtin_def);
            tenv_add_builtin(e, "=", builtin_put);

            // comparison functions

            tenv_add_builtin(e, "if", builtin_if);
            tenv_add_builtin(e, "==", builtin_eq);
            tenv_add_builtin(e, "!=", builtin_ne);

            // misc functions
            tenv_add_builtin(e, "print", builtin_print);
            tenv_add_builtin(e, "error", builtin_error);
            tenv_add_builtin(e, "load", builtin_load);
            tenv_add_builtin(e, "type", builtin_type);
            */
        }
        static private void tenv_add_builtin(Env<TypeScheme> e, string name, TypeScheme ts) => e[name] = ts; // NO COPYING as far as im aware? also overrides immutability 
    }
}
