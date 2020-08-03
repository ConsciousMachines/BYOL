using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

/*
p r e s s   ctrl m o / ctrl m l   t o   m i n i m i z e   /   e x p a n d
IDEAS
- make load work like Python import library
- add a base directory for scripts to import them
- MOST IMPORTANT: figure out why (((\ {n} {\ {m} {+ m n}}) 2) 3) doesnt cury the same way as (\ {n} {\ {m} {+ n m}})
    - this *might* fix the Y-combinator problem

- internal properties: for example, any curried variables in a fn closure
- pretty printer or python-like parsing? -> use the recursive print with 4 extra spaces each time
- check out my previous JIT projects and the Creel video on fn ptrs
- add some library fns
- go over bonus challenges
- META: script variables for a SHADER, movement of a cube, or in a game engine.
- META: once we have an AST, we can compile it (Nystrom) then create a JIT environment (C++) 
    and the code can now be run in that JIT area (assuming it's a simple stack, doesnt take more than 4kb)

- FINISHED: the copy problem!
- FINISHED: the neg num bs
- FINISHED: immutable env
*/


namespace Lispy
{
    public static class Tools
    {
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
            LASSERT(args, args.cell[index].type == expect,
                $"Function {func} passed incorrect type for argument {index}. Got {ltype_name(args.cell[index].type)}, Expected {ltype_name(expect)}.");
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
                case LVAL.NUM: return "Number";
                case LVAL.ERR: return "Error";
                case LVAL.SYM: return "Symbol";
                case LVAL.STR: return "String";
                case LVAL.SEXPR: return "S-Expression";
                case LVAL.QEXPR: return "Q-Expression";
                default: return "Unknown";
            }
        }

        // load a script
        public static void load_script(lenv e, string file_name)
        {
            lval a = new lval(LVAL.SEXPR);

            var sb = new StringBuilder(System.IO.File.ReadAllText(file_name, Encoding.ASCII));
            lval_read_expr(a, sb.Append('\0').ToString(), 0, '\0');
            
            // run script
            a.cell.ForEach(x => x.sev_print_then_eval(e));
        }

        // Possible unescapable characters 
        public static string lval_str_unescapable = "abfnrtv\\\'\"";

        // List of possible escapable characters 
        public static string lval_str_escapable = "\a\b\f\n\r\t\v\\\'\"";


        // Parsing 
        public static int lval_read_expr(lval v, string s, int i, char end)
        {
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
                if (s[i] == '(')
                {
                    lval x = new lval(LVAL.SEXPR);
                    v.add(x);
                    i = lval_read_expr(x, s, i + 1, ')');
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

    public enum LVAL // type of lval
    {
        ERR, NUM, SYM, SEXPR, QEXPR, FUN, STR
    }


    public delegate lval lbuiltin(lenv x, lval a);



    public class lenv : Dictionary<string, lval>
    {
        public lenv par = null; // parent env

        public lenv() : base() { }

        public lval get(lval k)
        {
            foreach (var key in this.Keys)
            {
                if (key == k.sym_err)
                {
                    return this[key].copy();
                }
            }

            // if no symbol check in parent otherwise error
            if (par != null)
            {
                return par.get(k);
            }
            else return new lval($"Unbound Symbol! {k.sym_err}", LVAL.ERR);

        }

        public void put(lval k, lval v) // for local env
        {
            if (this.ContainsKey(k.sym_err)) // this makes it immutable, BUT ONLY IN ONE SCOPE! (think one cactus leg)
            {
                Tools.LASSERT(null, false, $"Tried to mutate already defined variable: {k.sym_err}");
            }
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

        public lenv copy()
        {
            lenv n = new lenv();
            n.par = par;
            foreach (var key in Keys) n[key] = this[key].copy();
            return n;
        }
    }

    public class lval
    {
        // D A T A 
        public LVAL type; // must be mutable to change from Q to S expr

        // Basic
        readonly public long num; // too lazy to change the arithmetic functions
        readonly public string sym_err; // Error and Symbol types have some string data 

        // Function
        readonly lbuiltin builtin; // null = user-defined, else builtin
        readonly public lenv env;
        readonly lval formals;
        readonly lval body;

        // Expression - Count and Pointer to a list of "lval*"; 
        public int count => cell.Count;
        readonly public List<lval> cell;


        // C O N S T R U C T O R S
        public lval(LVAL type) // constructor for SEXPR, QEXPR
        {
            Tools.LASSERT(null, type == LVAL.SEXPR || type == LVAL.QEXPR, "Sev_Error: Type must be S or Q expr!");
            this.type = type;
            cell = new List<lval>();
        }

        public lval(string m, LVAL type) // Symbol, String
        {
            Tools.LASSERT(null, ((type == LVAL.SYM) || (type == LVAL.STR) || (type == LVAL.ERR)), "Sev_Error: Sym or Str Initialized with wrong Type!");
            this.type = type;
            this.sym_err = m;
        }

        public lval(long x) // number
        {
            type = LVAL.NUM;
            num = x;
        }

        public lval(lbuiltin func) // biultin function
        {
            type = LVAL.FUN;
            builtin = func;
        }

        public lval(lenv env, lval formals, lval body) // user defined function
        {
            type = LVAL.FUN;
            builtin = null; // not builtin fn
            this.env = env;
            this.formals = formals;
            this.body = body;
        }


        // I N S T A N C E   M E T H O D S 
        private void expr_print(string open, string close)
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

        private void print()
        {
            switch (type)
            {
                case LVAL.NUM: Console.Write(num); break;
                case LVAL.ERR: Console.Write($"Error: {sym_err}"); break;
                case LVAL.SYM: Console.Write(sym_err); break;
                case LVAL.SEXPR: expr_print("(", ")"); break;
                case LVAL.QEXPR: expr_print("{", "}"); break;
                case LVAL.STR:
                    // TODO: use stringbuilder instead
                    Console.Write('"');
                    /* Loop over the characters in the string */
                    for (int i = 0; i < sym_err.Length; i++)
                    {
                        if (Tools.lval_str_escapable.Contains(sym_err[i]))
                        {
                            /* If the character is escapable then escape it */
                            Console.Write(Tools.lval_str_escape(sym_err[i]));
                        }
                        else
                        {
                            /* Otherwise print character as it is */
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
        }

        private lval eval_sexpr(lenv e)
        {
            //Console.WriteLine("GOURDY");

            // evaluate children & error checking
            for (int i = 0; i < count; i++)
            {
                cell[i] = cell[i].eval(e);
                if (cell[i].type == LVAL.ERR) return take(i);
            }

            // empty expression ()
            if (count == 0) return this;

            // single expression (x)
            if (count == 1) return take(0);

            // ensure first element is a function after evaluation
            lval f = pop(0);
            if (f.type != LVAL.FUN)
            {
                return new lval("First element is not a function!", LVAL.ERR);
            }

            // if so call function to get result
            lval result = f.call(e, this);
            return result;
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

        private lval take(int i) => pop(i);

        private lval join(lval y)
        {
            // for each cell in y add it to this
            while (y.count > 0) add(y.pop(0));

            return this;
        }

        private lval call(lenv e, lval a)
        {
            // if builtin then simply apply that 
            if (builtin != null) return builtin(e, a);

            // record argument counts
            int given = a.count;
            int total = formals.count;

            // while arguments still remain to be processed
            while (a.count > 0)
            {
                // if we ran out of formal arguments to bind
                if (formals.count == 0)
                {
                    return new lval("Function passed too many args", LVAL.ERR);
                }

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
                if (formals.count != 2)
                {
                    return new lval("Function format invalid. Symbol '&' not followed by single symbol.", LVAL.ERR);
                }

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
                env.par = e;

                // eval and return 
                return builtin_eval(env, new lval(LVAL.SEXPR).add(body.copy()));
            }
            else
            {
                // otherwise return partially evaluated fn
                return this.copy();
            }
        }

        private void details()
        {
            Console.WriteLine(this.type);
        }


        // S T A T I C   M E T H O D S 

        static private void lenv_add_builtin(lenv e, string name, lbuiltin func)
        {
            lval k = new lval(name, LVAL.SYM);
            lval v = new lval(func);
            e.put(k, v);
        }

        static private lval builtin_op(lenv e, lval a, string op)
        {
            // ensure all args are numbers
            for (int i = 0; i < a.count; i++)
            {
                if (a.cell[i].type != LVAL.NUM) return new lval("Cannot operate on non-number!", LVAL.ERR);
            } // cell.Select(x => x.type == LVAL.NUM).Aggregate((x,y) => x && y)


            // pop first element
            //lval x = a.pop(0);
            long x = a.pop(0).num;

            // if no arguments and sub then perform unary negation
            //if ((op == "-") && a.count == 0) x.num = -x.num;
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

        static private lval builtin_head(lenv e, lval a)
        {
            Tools.LASSERT(a, a.count == 1, "Function 'head' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].type == LVAL.QEXPR, $"Function 'head' passed incorrect type! Got {Tools.ltype_name(a.cell[0].type)}, Expected {Tools.ltype_name(LVAL.QEXPR)}");
            Tools.LASSERT(a, a.cell[0].count != 0, "Function 'head' passed {}!");

            lval v = a.take(0);
            while (v.count > 1) v.pop(1);
            return v;
        }

        static private lval builtin_tail(lenv e, lval a)
        {
            Tools.LASSERT(a, a.count == 1, "Function 'tail' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].type == LVAL.QEXPR, "Function 'tail' passed incorrect type!");
            Tools.LASSERT(a, a.cell[0].count != 0, "Function 'tail' passed {}!");

            lval v = a.take(0);
            v.pop(0);
            return v;
        }

        static private lval builtin_list(lenv e, lval a)
        {
            a.type = LVAL.QEXPR;
            return a;
        }

        static private lval builtin_eval(lenv e, lval a)
        {
            Tools.LASSERT(a, a.count == 1, "Function 'eval' passed too many arguments!");
            Tools.LASSERT(a, a.cell[0].type == LVAL.QEXPR, "Function 'eval' passed incorrect type!");

            lval x = a.take(0);
            x.type = LVAL.SEXPR;
            return x.eval(e);
        }

        static private lval builtin_join(lenv e, lval a)
        {
            for (int i = 0; i < a.count; i++)
            {
                Tools.LASSERT(a, a.cell[i].type == LVAL.QEXPR, "Function 'join' passed incorrect type!");
            }

            lval x = a.pop(0);

            while (a.count > 0)
            {
                x.join(a.pop(0));
            }

            return x;
        }

        static private lval builtin_add(lenv e, lval a) => builtin_op(e, a, "+");
        static private lval builtin_sub(lenv e, lval a) => builtin_op(e, a, "-");
        static private lval builtin_mul(lenv e, lval a) => builtin_op(e, a, "*");
        static private lval builtin_div(lenv e, lval a) => builtin_op(e, a, "/");

        static private lval builtin_def(lenv e, lval a)
        {
            return builtin_var(e, a, "def");
        }

        static private lval builtin_put(lenv e, lval a)
        {
            return builtin_var(e, a, "=");
        }

        static private lval builtin_var(lenv e, lval a, string func)
        {
            Tools.LASSERT_TYPE(func, a, 0, LVAL.QEXPR);

            // first argument is symbol list 
            lval syms = a.cell[0];

            // ensure all elements of first lsit are symbols
            for (int i = 0; i < syms.count; i++)
            {
                Tools.LASSERT(a, syms.cell[i].type == LVAL.SYM, "Function 'def' canot define non-symbol!");
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
            // check two arguments, each of which are Q-expr
            Tools.LASSERT_NUM("\\", a, 2);
            Tools.LASSERT_TYPE("\\", a, 0, LVAL.QEXPR);
            Tools.LASSERT_TYPE("\\", a, 1, LVAL.QEXPR);

            // check first Q-expr contains only symbols
            for (int i = 0; i < a.cell[0].count; i++)
            {
                Tools.LASSERT(a, (a.cell[0].cell[i].type == LVAL.SYM), $"Cannot define non-symbol, Got {Tools.ltype_name(a.cell[0].cell[i].type)}, Expected {Tools.ltype_name(LVAL.SYM)}.");
            }

            // pop first two arguments and pass them to lval_lambda
            lval formals = a.pop(0);
            lval body = a.pop(0);

            return new lval(new lenv(), formals, body);
        }

        static private lval builtin_gt(lenv e, lval a) => builtin_ord(e, a, ">");
        static private lval builtin_lt(lenv e, lval a) => builtin_ord(e, a, "<");
        static private lval builtin_ge(lenv e, lval a) => builtin_ord(e, a, ">=");
        static private lval builtin_le(lenv e, lval a) => builtin_ord(e, a, "<=");

        static private lval builtin_ord(lenv e, lval a, string op)
        {
            Tools.LASSERT_NUM(op, a, 2);
            Tools.LASSERT_TYPE(op, a, 0, LVAL.NUM);
            Tools.LASSERT_TYPE(op, a, 1, LVAL.NUM);

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

        private static lval builtin_eq(lenv e, lval a) => builtin_cmp(e, a, "==");
        private static lval builtin_ne(lenv e, lval a) => builtin_cmp(e, a, "!=");

        private static lval builtin_if(lenv e, lval a)
        {
            Tools.LASSERT_NUM("if", a, 3);
            Tools.LASSERT_TYPE("if", a, 0, LVAL.NUM);
            Tools.LASSERT_TYPE("if", a, 1, LVAL.QEXPR);
            Tools.LASSERT_TYPE("if", a, 2, LVAL.QEXPR);

            // mark both expressions as evaluable
            lval x;
            a.cell[1].type = LVAL.SEXPR;
            a.cell[2].type = LVAL.SEXPR;

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

        private static lval builtin_print(lenv e, lval a)
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

        private static lval builtin_error(lenv e, lval a)
        {
            Tools.LASSERT_NUM("error", a, 1);
            Tools.LASSERT_TYPE("error", a, 0, LVAL.STR);

            // construct error from first argument
            lval err = new lval(a.cell[0].sym_err, LVAL.STR);
            return err;
        }

        private static lval builtin_load(lenv e, lval a)
        {
            Tools.LASSERT_NUM("load", a, 1);
            Tools.LASSERT_TYPE("load", a, 0, LVAL.STR);

            Tools.load_script(e, a.sym_err);

            return new lval(LVAL.SEXPR);
        }


        static private bool eq(lval x, lval y)
        {
            // diff types are always unequal
            if (x.type != y.type) return false;

            // compare based on type
            switch (x.type)
            {
                // compare nums
                case LVAL.NUM: return (x.num == y.num);

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

        // P U B L I C   M E T H O D S
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

            // sev functions
            lenv_add_builtin(e, "print", builtin_print);
            lenv_add_builtin(e, "error", builtin_error);
            lenv_add_builtin(e, "load", builtin_load); 

        }

        public lval copy()
        {
            lval x;

            switch (type)
            {
                case LVAL.FUN:
                    if (builtin != null) x = new lval(builtin);
                    else
                    {
                        x = new lval(env.copy(), formals.copy(), body.copy());
                    }
                    break;
                case LVAL.NUM: x = new lval(num); break;
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

            return x;
        }

        public lval add(lval x) // add an element to an S-expression 
        {
            cell.Add(x);
            return this;
        }

        public lval eval(lenv e)
        {
            if (type == LVAL.SYM) return e.get(this);

            // evaluate S-expressions 
            else if (type == LVAL.SEXPR) return eval_sexpr(e);
            // all other types remain the same 
            return this;
        }

        public void println()
        {
            this.print();
            Console.Write('\n');
        }

        public void sev_print_then_eval(lenv e)
        {
            println();
            eval(e).println();
        }
    }

    class Lispy
    {
        static void Main(string[] args)
        {
            var script = @"C:\Users\pwnag\source\repos\Lispy\Lispy\test.lispy";
            
            
            Console.WriteLine("Lispy Version 0.1.5");
            Console.WriteLine("Press Ctrl+C to Exit\n");

            lenv e = new lenv();
            lval.lenv_add_builtins(e);
            Tools.load_script(e, script);

            // run interactive prompt 
            while (true)
            {
                lval x = new lval(LVAL.SEXPR);
                Console.Write("lispy> ");
                StringBuilder input = new StringBuilder(Console.ReadLine());
                Tools.lval_read_expr(x, input.Append('\0').ToString(), 0, '\0');
                x.eval(e).println();
            }
        }
    }
}
