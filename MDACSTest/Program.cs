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
using MDACS.Server;
using System.Reflection;
using System.Collections.Generic;

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

    public class TestPlatform
    {
        public String path_base { get; }

        public TestPlatform()
        {
            path_base = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            File.Delete(path_base);

            Directory.CreateDirectory(path_base);

            var dbcfg = new MDACS.Database.ProgramConfig();
            var authcfg = new MDACS.Auth.ProgramConfig();
            var appcfg = new MDACS.App.ProgramConfig();

            var config_path = Path.Combine(path_base, "config");
            var data_path = Path.Combine(path_base, "data");
            var metajournal_path = Path.Combine(path_base, "journal");

            var asm = System.Reflection.Assembly.GetExecutingAssembly();

            var security_path = Path.Combine(path_base, "security");

            var cert_path = Path.Combine(security_path, "cert.pfx");

            var users_path = Path.Combine(path_base, "users.json");

            Directory.CreateDirectory(config_path);
            Directory.CreateDirectory(data_path);
            Directory.CreateDirectory(security_path);

            FileStream fp;

            using (var asm_stream = asm.GetManifestResourceStream("MDACSTest.test.pfx"))
            {
                fp = File.OpenWrite(cert_path);
                asm_stream.CopyTo(fp);
                fp.Dispose();
            }

            /*using (var asm_stream = asm.GetManifestResourceStream("MDACSTest.users.json"))
            {
                fp = File.OpenWrite(users_path);
                asm_stream.CopyTo(fp);
                fp.Dispose();
            }*/

            dbcfg.auth_url = "http://localhost:34002";
            dbcfg.config_path = config_path;
            dbcfg.data_path = data_path;
            dbcfg.metajournal_path = metajournal_path;
            dbcfg.port = 34001;
            //dbcfg.ssl_cert_path = cert_path;
            //dbcfg.ssl_cert_pass = "hello";

            authcfg.data_base_path = path_base;
            authcfg.port = 34002;
            //authcfg.ssl_cert_path = cert_path;
            //authcfg.ssl_cert_pass = "hello";

            appcfg.auth_url = "http://localhost:34002";
            appcfg.db_url = "http://localhost:34001";
            appcfg.port = 34000;
            appcfg.web_resources_path = @"/home/kmcguire/extra/old/source/repos/MDACSApp/webres";
            //appcfg.ssl_cert_path = cert_path;
            //appcfg.ssl_cert_pass = "hello";

            byte[] buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dbcfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(path_base, "dbconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(authcfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(path_base, "authconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(appcfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(path_base, "appconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            var db_thread = new Thread(() =>
            {
                MDACS.Database.Program.Main(new string[] {
                   Path.Combine(path_base, "dbconfig.json")
                });
            });

            db_thread.IsBackground = true;

            var auth_thread = new Thread(() =>
            {
                MDACS.Auth.Program.Main(new string[] {
                   Path.Combine(path_base, "authconfig.json")
                });
            });

            auth_thread.IsBackground = true;

            var app_thread = new Thread(() => 
            {
                MDACS.App.Program.Main(new string[] {
                    Path.Combine(path_base, "appconfig.json")
                });
            });

            app_thread.IsBackground = true;

            // Suppress the database output.
            //MDACS.Database.Program.logger_output_base += (JObject item) => true;

            auth_thread.Start();
            db_thread.Start();
            app_thread.Start();
        }

        ~TestPlatform()
        {

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
        public static void Main(string[] args)
        {
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

                    fact_methods.Add((fact, tmeth));
                }

                if (fact_methods.Count < 1)
                {
                    continue;
                }

                var type_instance = Activator.CreateInstance(tdef);

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

            }
        }
    }
}
