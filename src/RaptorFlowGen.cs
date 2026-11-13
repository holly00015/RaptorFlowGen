using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace RaptorFlowGen
{
    // ---------------- statement model ----------------
    abstract class Stmt { }
    class InStmt : Stmt { public string Var; }
    class OutStmt : Stmt { public string Expr; }
    class AsgStmt : Stmt { public string Text; }
    class IfStmt : Stmt
    {
        public string Cond;
        public List<Stmt> Then = new List<Stmt>();
        public List<Stmt> Else = new List<Stmt>();
    }
    class LoopStmt : Stmt
    {
        public string Cond;
        public List<Stmt> Body = new List<Stmt>();
    }

    class Options
    {
        public string RaptorDir;
        public string Spec;
        public string Out;
        public string Mode = "Intermediate";
    }

    class Program
    {
        static Options opt = new Options();
        static Dictionary<string, Assembly> asmCache = new Dictionary<string, Assembly>();
        static Assembly raptorAsm;
        static object form, start, end, kindAssign;

        static int Main(string[] args)
        {
            try
            {
                if (!ParseArgs(args)) { PrintUsage(); Environment.Exit(1); }
                Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (ex.InnerException != null) Console.Error.WriteLine("  inner: " + ex.InnerException.Message);
                Environment.Exit(2);
            }
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("RaptorFlowGen - generate Raptor .rap flowcharts from a text script");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RaptorFlowGen --raptor <raptor-install-dir> --spec <input.txt> --out <output.rap>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --raptor <dir>   folder that contains raptor.exe (or \"Raptor 汉化版.exe\")");
            Console.WriteLine("                   default: %%RAPTOR_DIR%% env var, or raptor.dir file next to this exe");
            Console.WriteLine("  --spec <file>    input script (UTF-8)");
            Console.WriteLine("  --out <file>     output .rap file");
            Console.WriteLine("  --mode <m>       Intermediate (default) / Novice / Expert");
            Console.WriteLine();
            Console.WriteLine("Script syntax (one statement per line, # starts a comment):");
            Console.WriteLine("  IN  <var>                  input symbol");
            Console.WriteLine("  OUT <expression>           output symbol, e.g. OUT \"sum=\" + s");
            Console.WriteLine("  SET <var> := <expression>  assignment symbol");
            Console.WriteLine("  IF <condition> ... ELSE ... ENDIF");
            Console.WriteLine("  LOOP <condition> ... ENDLOOP   (loop repeats UNTIL condition is true)");
        }

        static bool ParseArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string v = (i + 1 < args.Length) ? args[i + 1] : null;
                switch (a)
                {
                    case "--raptor": if (v == null) return false; opt.RaptorDir = v; i++; break;
                    case "--spec": if (v == null) return false; opt.Spec = v; i++; break;
                    case "--out": if (v == null) return false; opt.Out = v; i++; break;
                    case "--mode": if (v == null) return false; opt.Mode = v; i++; break;
                    case "-h":
                    case "--help": return false;
                    default:
                        Console.Error.WriteLine("unknown argument: " + a);
                        return false;
                }
            }
            if (opt.RaptorDir == null) opt.RaptorDir = DefaultRaptorDir();
            if (opt.RaptorDir == null || opt.Spec == null || opt.Out == null) return false;
            return true;
        }

        static string DefaultRaptorDir()
        {
            string env = Environment.GetEnvironmentVariable("RAPTOR_DIR");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string cfg = Path.Combine(ExeDir(), "raptor.dir");
            if (File.Exists(cfg))
            {
                string line = File.ReadAllText(cfg, Encoding.UTF8).Trim();
                if (line.Length > 0 && Directory.Exists(line)) return line;
            }
            return null;
        }

        static string ExeDir()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        // ---------------- reflection helpers ----------------
        static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
        {
            string simple = new AssemblyName(e.Name).Name;
            Assembly cached;
            if (asmCache.TryGetValue(simple, out cached)) return cached;
            string path = FindAssemblyFile(opt.RaptorDir, simple);
            if (path == null) return null;
            Assembly a = Assembly.Load(File.ReadAllBytes(path));
            asmCache[simple] = a;
            return a;
        }

        static string entryAssemblyPath;

        static string FindAssemblyFile(string dir, string simpleName)
        {
            string dll = Path.Combine(dir, simpleName + ".dll");
            if (File.Exists(dll)) return dll;
            string exe = Path.Combine(dir, simpleName + ".exe");
            if (File.Exists(exe)) return exe;
            // the Raptor entry assembly may be renamed (e.g. "Raptor 汉化版.exe")
            if (entryAssemblyPath != null && simpleName.Equals("raptor", StringComparison.OrdinalIgnoreCase))
                return entryAssemblyPath;
            return null;
        }

        static string FindRaptorEntry(string dir)
        {
            // raptor.exe is the historical name; the Chinese build may use a renamed exe
            string[] preferred = new string[] { "raptor.exe", "Raptor.exe", "Raptor 汉化版.exe" };
            foreach (string p in preferred)
            {
                string full = Path.Combine(dir, p);
                if (File.Exists(full)) return full;
            }
            // fall back: any exe whose name contains "raptor"
            foreach (string f in Directory.GetFiles(dir, "*.exe"))
            {
                if (Path.GetFileName(f).ToLowerInvariant().Contains("raptor")) return f;
            }
            return null;
        }

        static Type FindType(string name)
        {
            Type t = raptorAsm.GetType(name);
            if (t != null) return t;
            foreach (string f in Directory.GetFiles(opt.RaptorDir, "*.dll"))
            {
                try
                {
                    string simple = Path.GetFileNameWithoutExtension(f);
                    Assembly a;
                    if (!asmCache.TryGetValue(simple, out a))
                    {
                        a = Assembly.Load(File.ReadAllBytes(f));
                        asmCache[simple] = a;
                    }
                    t = a.GetType(name);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        static Type NeedType(string name)
        {
            Type t = FindType(name);
            if (t == null) throw new Exception("cannot find type " + name + " inside Raptor assembly");
            return t;
        }

        static object GetField(object o, string name)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) throw new Exception("field not found: " + name + " (unsupported Raptor version?)");
            return f.GetValue(o);
        }

        static void SetField(object o, string name, object value)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) throw new Exception("field not found: " + name + " (unsupported Raptor version?)");
            f.SetValue(o, value);
        }

        static object GetProp(object o, string name)
        {
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) throw new Exception("property not found: " + name + " (unsupported Raptor version?)");
            return p.GetValue(o, null);
        }

        static void SetProp(object o, string name, object value)
        {
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) throw new Exception("property not found: " + name + " (unsupported Raptor version?)");
            p.SetValue(o, value, null);
        }

        static object Call(object o, string name, params object[] args)
        {
            foreach (MethodInfo mi in o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (mi.Name != name) continue;
                if (mi.GetParameters().Length != args.Length) continue;
                return mi.Invoke(o, args);
            }
            throw new Exception("method not found: " + name + " (unsupported Raptor version?)");
        }

        static object Construct(string typeName, params object[] args)
        {
            Type t = NeedType(typeName);
            foreach (ConstructorInfo c in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (c.GetParameters().Length == args.Length) return c.Invoke(args);
            }
            throw new Exception("constructor not found for " + typeName);
        }

        // ---------------- flow chart building ----------------
        static object NewInput() { return Construct("raptor.Parallelogram", null, 60, 90, "Parallelogram", true); }
        static object NewOutput() { return Construct("raptor.Parallelogram", null, 60, 90, "Parallelogram", false); }
        static object NewAssign() { return Construct("raptor.Rectangle", 60, 90, "Rectangle", kindAssign); }
        static object NewIf() { return Construct("raptor.IF_Control", null, 60, 90, "IF_Control"); }
        static object NewLoop() { return Construct("raptor.Loop", null, 60, 90, "Loop"); }

        static void SetText(object comp, string text)
        {
            SetProp(comp, "Text", text);
        }

        static object Build(List<Stmt> stmts, object next)
        {
            object cur = next;
            for (int i = stmts.Count - 1; i >= 0; i--) cur = BuildOne(stmts[i], cur);
            return cur;
        }

        static object BuildOne(Stmt s, object next)
        {
            if (s is InStmt)
            {
                object c = NewInput();
                SetText(c, ((InStmt)s).Var);
                SetField(c, "Successor", next);
                return c;
            }
            if (s is OutStmt)
            {
                object c = NewOutput();
                SetText(c, ((OutStmt)s).Expr);
                SetField(c, "Successor", next);
                return c;
            }
            if (s is AsgStmt)
            {
                object c = NewAssign();
                SetText(c, ((AsgStmt)s).Text);
                SetField(c, "Successor", next);
                return c;
            }
            if (s is IfStmt)
            {
                IfStmt f = (IfStmt)s;
                object c = NewIf();
                SetText(c, f.Cond);
                // branches are joined by the IF node's own Successor
                object yes = Build(f.Then, null);
                object no = Build(f.Else, null);
                SetProp(c, "yes_child", yes);
                SetProp(c, "no_child", no);
                SetField(c, "Successor", next);
                return c;
            }
            if (s is LoopStmt)
            {
                LoopStmt l = (LoopStmt)s;
                object body = Build(l.Body, null);
                object c = NewLoop();
                SetText(c, l.Cond);
                SetProp(c, "before_Child", body);
                SetField(c, "Successor", next);
                return c;
            }
            throw new Exception("unsupported statement");
        }

        // ---------------- script parser ----------------
        static List<string> lines;
        static int pos;

        static List<Stmt> ParseBlock(string stop1, string stop2)
        {
            List<Stmt> list = new List<Stmt>();
            while (pos < lines.Count)
            {
                string s = lines[pos].Trim();
                pos++;
                if (s.Length == 0 || s.StartsWith("#")) continue;
                if (s == stop1 || s == stop2) return list;

                if (s.StartsWith("IN ")) list.Add(new InStmt { Var = s.Substring(3).Trim() });
                else if (s.StartsWith("OUT ")) list.Add(new OutStmt { Expr = s.Substring(4).Trim() });
                else if (s.StartsWith("SET ")) list.Add(new AsgStmt { Text = s.Substring(4).Trim() });
                else if (s.StartsWith("IF "))
                {
                    IfStmt f = new IfStmt { Cond = s.Substring(3).Trim() };
                    f.Then = ParseBlock("ELSE", "ENDIF");
                    if (pos - 1 < lines.Count && lines[pos - 1].Trim() == "ELSE")
                        f.Else = ParseBlock(null, "ENDIF");
                    list.Add(f);
                }
                else if (s.StartsWith("LOOP "))
                {
                    LoopStmt l = new LoopStmt { Cond = s.Substring(5).Trim() };
                    l.Body = ParseBlock(null, "ENDLOOP");
                    list.Add(l);
                }
                else
                {
                    throw new Exception("line " + pos + ": cannot understand '" + s + "'");
                }
            }
            return list;
        }

        static List<Stmt> ParseScript(string file)
        {
            lines = new List<string>(File.ReadAllLines(file, Encoding.UTF8));
            pos = 0;
            return ParseBlock(null, null);
        }

        // ---------------- main ----------------
        [STAThread]
        static int Run()
        {
            if (!Directory.Exists(opt.RaptorDir))
                throw new Exception("Raptor folder not found: " + opt.RaptorDir);
            string entry = FindRaptorEntry(opt.RaptorDir);
            if (entry == null)
                throw new Exception("raptor.exe not found in " + opt.RaptorDir);

            entryAssemblyPath = entry;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            raptorAsm = Assembly.Load(File.ReadAllBytes(entry));

            Type kindEnum = NeedType("raptor.Rectangle+Kind_Of");
            if (kindEnum == null) kindEnum = NeedType("raptor.Rectangle").GetNestedType("Kind_Of");
            kindAssign = Enum.Parse(kindEnum, "Assignment");

            // create the Raptor main form; its constructor initialises the runtime we need
            Type formType = NeedType("raptor.Visual_Flow_Form");
            foreach (ConstructorInfo c in formType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] ps = c.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(bool))
                {
                    form = c.Invoke(new object[] { true });
                    break;
                }
            }
            Console.WriteLine("step: form created");
            if (form == null) throw new Exception("cannot create Raptor main window (unsupported version?)");

            Type compType = NeedType("raptor.Component");
            PropertyInfo modeProp = compType.GetProperty("Current_Mode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (modeProp != null && modeProp.CanWrite)
                modeProp.SetValue(null, Enum.Parse(NeedType("raptor.Mode"), opt.Mode), null);

            object carlisle = GetField(form, "carlisle");
            int mainIndex = (int)Call(form, "get_mainIndex");
            object sub = ((TabControl)carlisle).TabPages[mainIndex];
            Console.WriteLine("step: subchart found");
            start = GetField(sub, "Start");
            end = GetField(sub, "End");

            int version = (int)compType.GetProperty("current_serialization_version",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
            Console.WriteLine("Raptor assembly : " + raptorAsm.GetName().Version);
            Console.WriteLine("file format ver : " + version);

            Console.WriteLine("step: script parsed");
            List<Stmt> prog = ParseScript(opt.Spec);
            object head = Build(prog, end);
            SetField(start, "Successor", (head == null) ? end : head);

            Console.WriteLine("step: chart built");
            Call(form, "my_layout");
            Call(form, "Perform_Save", opt.Out, false);

            Console.WriteLine("step: saved");
            if (!File.Exists(opt.Out)) throw new Exception("save failed, no output file produced");
            long size = new FileInfo(opt.Out).Length;
            Console.WriteLine("output          : " + Path.GetFullPath(opt.Out) + "  (" + size + " bytes)");
            Console.WriteLine("OK");
            Console.Out.Flush();
            Environment.Exit(0);
            return 0;
        }
    }
}
