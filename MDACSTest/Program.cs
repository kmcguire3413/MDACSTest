using Newtonsoft.Json.Linq;
using System;
using MDACS;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Threading;
using MDACS.API.Responses;
using System.Threading.Tasks;
using MDACS.Server;

namespace MDACS.Test
{
    class TestException : Exception
    {

    }

    static class Database
    {
    }

    class TestPlatform
    {
        public String path_base { get; }

        public TestPlatform(
            String auth_url,
            ushort port
        )
        {
            path_base = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            File.Delete(path_base);

            Directory.CreateDirectory(path_base);

            var dbcfg = new MDACS.Database.ProgramConfig();

            var config_path = Path.Combine(path_base, "config");
            var data_path = Path.Combine(path_base, "data");
            var metajournal_path = Path.Combine(path_base, "journal");

            var asm = System.Reflection.Assembly.GetExecutingAssembly();

            var security_path = Path.Combine(path_base, "security");

            var cert_path = Path.Combine(security_path, "cert.pfx");

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

            dbcfg.auth_url = auth_url;
            dbcfg.config_path = config_path;
            dbcfg.data_path = data_path;
            dbcfg.metajournal_path = metajournal_path;
            dbcfg.port = port;
            dbcfg.cert_path = cert_path;
            dbcfg.cert_pass = "hello";

            byte[] buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dbcfg, Formatting.Indented));

            fp = File.OpenWrite(Path.Combine(path_base, "dbconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            var db_thread = new Thread(() =>
            {
                MDACS.Database.Program.Main(new string[] {
                   Path.Combine(path_base, "dbconfig.json")
                });
            });

            db_thread.Start();
        }

        ~TestPlatform()
        {

        }
    }

    static class Program
    {
        public static bool CheckTrust(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors err)
        {
            Console.WriteLine("checking cert", cert);
            return true;
        }

        static void Assert(bool condition)
        {
            if (!condition)
            {
                throw new Exception("Assertion failure.");
            }
        }

        static void Main(string[] args)
        {
            var platform = new TestPlatform("https://epdmdacs.kmcg3413.net:34002", 34001);

            Thread.Sleep(2000);

            ServicePointManager.ServerCertificateValidationCallback = CheckTrust;

            JObject config = new JObject();

            config["database_url"] = "https://localhost:34001";
            config["auth_url"] = "https://epdmdacs.kmcg3413.net:34002";
            config["username"] = "kmcguire";
            config["password"] = "Z4fmv96s#7";

            var session = new API.Session(
                "https://epdmdacs.kmcg3413.net:34002",
                "https://localhost:34001",
                "kmcguire",
                "Z4fmv96s#7"
            );

            var rand_data = new DoubleEndedStream();

            Random rng = new Random();

            var chunk = new byte[1024];

            for (long sent = 0; sent < 1024 * 1024 * 2; sent += chunk.Length)
            {
                rng.NextBytes(chunk);
                rand_data.Write(chunk, 0, chunk.Length);
            }

            var upresp = session.UploadAsync(
                rand_data.Length,
                "trash",
                "2017-12-19",
                "TESTDEVICE",
                "131211",
                "kmcguire",
                rand_data
            );

            upresp.Wait();

            Assert(upresp.Result.success);

            var sid = upresp.Result.security_id;

            var meta = new JObject();

            meta["note"] = "This is a note having been set.";

            var csresp = session.CommitSetAsync(sid, meta);

            csresp.Wait();

            Assert(csresp.Result.success);
        }
    }
}
