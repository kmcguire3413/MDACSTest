/// Testing framework based on a little I saw of xUnit. Could not get xUnit to work. Got tired
/// of playing with its silly requirements and difficult installation. Decided to just make something
/// that worked how I liked it. Took same amount of time to have figured xUnit out and I am happier.

using Newtonsoft.Json.Linq;
using System;
using MDACS;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;

namespace MDACS.Test
{
    class Fact: Attribute
    {
        public string fact_dep;

        public Fact()
        {
            this.fact_dep = null;
        }

        public Fact(string fact_dep)
        {
            this.fact_dep = fact_dep;
        }

        public bool FactDepsMeet(HashSet<string> fact_deps_ran)
        {
            if (fact_dep == null)
            {
                return true;
            }

            return fact_deps_ran.Contains(fact_dep);
        }

        public void Execute(object instance, MethodInfo minfo)
        {
            minfo.Invoke(instance, null);
        }
    }

    class FactAsync: Fact
    {
        public FactAsync() : base()
        {
        }

        public FactAsync(string fact_dep): base(fact_dep)
        {
        }

        public void ExecuteAsync(object instance, MethodInfo minfo)
        {
            var tsk = minfo.Invoke(instance, null) as Task;

            tsk.Wait();
        }
    }

    public static class Assert
    {
        public static void True(bool v)
        {
            if (!v)
            {
                throw new Exception("Assertion Failure");
            }
        }

        public static void Failed()
        {
            throw new Exception("Assertion Failure");
        }
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            bool sleepForever = false;

            if (args.Length < 1) {
                Console.WriteLine("Specify the location of the web resources directory as the first argument.");
                Console.WriteLine("This should be the /webres/ folder in the MDACSApp project.");
                return -1;
            }

            var webResourcesPath = args[0];

            if (!Directory.Exists(webResourcesPath)) {
                Console.WriteLine($"The provided path {webResourcesPath} does not appear to be accessible or exist.");
                return -1;
            }

            foreach (var arg in args) {
                if (arg.Equals("sleepforever")) {
                    sleepForever = true;
                }
            }

            foreach (var tdef in Assembly.GetExecutingAssembly().DefinedTypes)
            {
                var fact_methods = new List<(Fact, MethodInfo)>();

                foreach (var tmeth in tdef.DeclaredMethods)
                {
                    var fact = tmeth.GetCustomAttribute<Fact>(true);

                    if (fact == null)
                    {
                        continue;
                    }

                    if (!tmeth.Name.Equals("TestCommand")) {
                        continue;
                    }

                    fact_methods.Add((fact, tmeth));
                }

                if (fact_methods.Count < 1)
                {
                    continue;
                }

                var type_instance = Activator.CreateInstance(tdef, new object[] { args[0] });

                var fact_deps_ran = new HashSet<string>();
                var fact_deps_passed = new HashSet<string>();
                var fact_deps_failed = new HashSet<string>();

                while (fact_methods.Count > 0) {
                    for (int x = 0; x < fact_methods.Count; ++x) {
                        var pair = fact_methods[x];
                        var fact = pair.Item1;
                        var tmeth = pair.Item2;
                        var method_id = tmeth.Name;

                        // If all deps have passed then we can run this dep.
                        if (!fact.FactDepsMeet(fact_deps_passed))
                        {
                            // If not all deps have passed but all deps have ran then
                            // consider this fact as having failed.
                            if (fact.FactDepsMeet(fact_deps_ran))
                            {
                                fact_deps_failed.Add(method_id);
                                fact_deps_ran.Add(method_id);
                                fact_methods.RemoveAt(x);
                                break;
                            }

                            continue;
                        }

                        try
                        {
                            fact_deps_ran.Add(tmeth.Name);

                            Console.WriteLine($"Running test {tdef.Name}.{tmeth.Name}");

                            if (fact is FactAsync)
                            {
                                (fact as FactAsync).ExecuteAsync(type_instance, tmeth);
                            } else
                            {
                                fact.Execute(type_instance, tmeth);
                            }

                            fact_deps_passed.Add(tmeth.Name);

                            Console.WriteLine($"{tdef.Name}.{tmeth.Name} PASSED");
                        } catch (Exception ex)
                        {
                            fact_deps_failed.Add(tmeth.Name);

                            Console.WriteLine($"{tdef.Name}.{tmeth.Name} FAILED");
                            Console.WriteLine(ex.ToString());
                        } finally
                        {
                            fact_methods.RemoveAt(x);
                        }

                        break;
                    }
                }

                foreach (var result in fact_deps_passed)
                {
                    Console.WriteLine($"{result} PASSED");
                }

                foreach (var result in fact_deps_failed)
                {
                    Console.WriteLine($"{result} FAILED");
                }

                Console.WriteLine("Testing done.");

                if (sleepForever) {
                    Console.WriteLine("Sleeping forever. Services will remain running and operational.");
                    Thread.Sleep(1000 * 60 * 60 * 24);
                }

                if (fact_deps_failed.Count > 0) {
                    return -1;
                }
            }

            return 0;
        }
    }
}
