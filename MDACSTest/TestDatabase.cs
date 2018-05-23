using MDACS.Test;
using MDACS.API;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.IO;
using System.Diagnostics;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace MDACSTest
{
public class TestPlatform
    {
        Process appProcess;
        Process authProcess;
        Process dbProcess;
        Process cmdProcess;        
        
        public string pathBase { get; }

        public TestPlatform(TestConfig cfg)
        {
            pathBase = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            File.Delete(pathBase);

            Directory.CreateDirectory(pathBase);

            //var dbCfg = new MDACS.Database.ProgramConfig();
            //var authCfg = new MDACS.Auth.ProgramConfig();
            //var appCfg = new MDACS.App.ProgramConfig();
            //var cmdCfg = new MDACS.Command.ProgramConfig();

            var configPath = Path.Combine(pathBase, "config");
            var dataPath = Path.Combine(pathBase, "data");
            var metajournalPath = Path.Combine(pathBase, "journal");

            var asm = System.Reflection.Assembly.GetExecutingAssembly();

            var securityPath = Path.Combine(pathBase, "security");
            var certPath = Path.Combine(securityPath, "cert.pfx");
            var usersPath = Path.Combine(pathBase, "users.json");

            Directory.CreateDirectory(configPath);
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(securityPath);

            FileStream fp;

            using (var asmStream = asm.GetManifestResourceStream("MDACSTest.test.pfx"))
            {
                fp = File.OpenWrite(certPath);
                asmStream.CopyTo(fp);
                fp.Dispose();
            }

            var dbCfg = new JObject();
            var authCfg = new JObject();
            var appCfg = new JObject();
            var cmdCfg = new JObject();

            dbCfg["auth_url"] = "http://localhost:34002";
            dbCfg["config_path"] = configPath;
            dbCfg["data_path"] = dataPath;
            dbCfg["metajournal_path"] = metajournalPath;
            dbCfg["port"] = 34001;
            //dbcfg.ssl_cert_path = cert_path;
            //dbcfg.ssl_cert_pass = "hello";

            authCfg["dataBasePath"] = pathBase;
            authCfg["port"] = 34002;
            authCfg["maximumChallengesOutstanding"] = 100;
            authCfg["cmdUrl"] = "http://localhost:34015";
            authCfg["authUrl"] = "http://localhost:34002";
            authCfg["username"] = "admin";
            authCfg["password"] = "abc";
            authCfg["serviceGuid"] = "XKSm@K#K2o3MDaslSKDMSDM2n32#@K#!K@wdaSM<dL21k#L@!MWMDamdqwmelmsla";
            //authcfg.sslCertPath = cert_path;
            //authcfg.sslCertPass = "hello";

            appCfg["auth_url"] = "http://localhost:34002";
            appCfg["db_url"] = "http://localhost:34001";
            appCfg["port"] = 34000;
            appCfg["web_resources_path"] = cfg.webResourcesPath;
            //appcfg.ssl_cert_path = cert_path;
            //appcfg.ssl_cert_pass = "hello";

            cmdCfg["authUrl"] = "http://localhost:34002";
            cmdCfg["port"] = 34015;

            byte[] buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dbCfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(pathBase, "dbconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cmdCfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(pathBase, "cmdconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();            

            buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(authCfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(pathBase, "authconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(appCfg, Formatting.Indented));
            fp = File.OpenWrite(Path.Combine(pathBase, "appconfig.json"));
            fp.Write(buf, 0, buf.Length);
            fp.Dispose();

            appProcess = Process.Start(cfg.appExecutablePath, $"{Path.Combine(pathBase, "appconfig.json")}");
            authProcess = Process.Start(cfg.authExecutablePath, $"{Path.Combine(pathBase, "authconfig.json")}");
            dbProcess = Process.Start(cfg.dbExecutablePath, $"{Path.Combine(pathBase, "dbconfig.json")}");
            cmdProcess = Process.Start(cfg.cmdExecutablePath, $"{Path.Combine(pathBase, "cmdconfig.json")}");
        }

        ~TestPlatform()
        {
            appProcess.Kill();
            authProcess.Kill();
            dbProcess.Kill();
            cmdProcess.Kill();

            appProcess.WaitForExit();
            authProcess.WaitForExit();
            dbProcess.WaitForExit();
            cmdProcess.WaitForExit();
        }
    }

    public class TestConfig {
        public string webResourcesPath;
        public string appExecutablePath;
        public string dbExecutablePath;
        public string authExecutablePath;
        public string cmdExecutablePath;
    }

    public class TestDatabase
    {
        TestPlatform    platform;
        Session         session;
        TestConfig      cfg;

        public TestDatabase(string configDataPath)
        {
            cfg = JsonConvert.DeserializeObject<TestConfig>(File.ReadAllText(configDataPath));

            CreateNewPlatformAndSession();
        }

        bool CreateNewPlatformAndSession()
        {
            bool CheckTrust(
                object sender, 
                X509Certificate cert, 
                X509Chain chain, 
                System.Net.Security.SslPolicyErrors err
                )
            {
                Debug.WriteLine($"checking cert {cert} {chain} {err}");
                return true;
            }

            ServicePointManager.ServerCertificateValidationCallback = CheckTrust;

            platform = new TestPlatform(cfg);

            Thread.Sleep(2000);

            session = new Session(
                "http://localhost:34002",
                "http://localhost:34001",
                "http://localhost:34015",
                "admin",
                "abc"
            );

            return true;
        }

        //[Fact(0)]
        //void TestBasicFunctionality()
        //{
        //    Task.Run(async () => await TestBasicFunctionalityInner()).Wait();
        //}

        public string sid;

        async Task<MDACS.API.Responses.UploadResponse> DoRandomUpload(
            string datatype,
            string datestr,
            string devicestr,
            string timestr,
            string userstr,
            long size
        )
        {
            var randomData = new MemoryStream();

            Random rng = new Random();

            var chunk = new byte[1024];

            for (long sent = 0; sent < size; sent += chunk.Length)
            {
                rng.NextBytes(chunk);
                await randomData.WriteAsync(chunk, 0, chunk.Length);
            }

            randomData.Seek(0, SeekOrigin.Begin);

            return await session.UploadAsync(
                randomData.Length,
                datatype,
                datestr,
                devicestr,
                timestr,
                userstr,
                randomData
            );
        }

        [FactAsync()]
        async Task TestUploadFirst()
        {
            var upresp = await DoRandomUpload(
                "trash",
                "2017-12-19",
                "testdevice",
                "010101",
                "kmcguire",
                1024 * 1024 * 2
            );

            this.sid = upresp.security_id;

            Assert.True(upresp.success);
        }

        [FactAsync("TestUploadFirst")]
        async Task TestCommitSetFirst() {
            var meta = new JObject();

            meta["note"] = "This is a note having been set.";

            var csresp = session.CommitSetAsync(sid, meta);

            await csresp;

            Assert.True(csresp.Result.success);
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestDataFirst()
        {
            var resp = await session.Data();

            foreach (var item in resp.data)
            {
                if (item.security_id.Equals(sid) && item.note.Equals("This is a note having been set."))
                {
                    return;
                }
            }

            Assert.Failed();
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestBatchSingleOps()
        {
            var rnd = new Random();

            var dt = DateTime.Now;

            // Upload a good many files.
            for (int x = 0; x < 6; ++x)
            {
                var random_size_value = (long)(rnd.NextDouble() * 1024 * 128);

                dt = dt.AddHours(1.0);

                var datestr = $"{dt.Year}-{dt.Month.ToString().PadLeft(2, '0')}-{dt.Day.ToString().PadLeft(2, '0')}";
                var timestr = $"{dt.Hour.ToString().PadLeft(2, '0')}{dt.Minute.ToString().PadLeft(2, '0')}{dt.Second.ToString().PadLeft(2, '0')}";

                Assert.True((await DoRandomUpload(
                    "random",
                    datestr,
                    "testdevice",
                    timestr,
                    "kmcguire",
                    random_size_value
                )).success);
            }

            // Download the listing and build batch single ops request.
            var listing = await session.Data();
            var ops = new List<MDACS.API.Requests.BatchSingleOp>();
            
            foreach (var item in listing.data)
            {
                ops.Add(new MDACS.API.Requests.BatchSingleOp()
                {
                    field_name = "note",
                    sid = item.security_id,
                    value = JToken.FromObject($"This is a note on {item.security_id}."),
                });
            }

            // Add one that is intended to fail.
            ops.Add(new MDACS.API.Requests.BatchSingleOp()
            {
                field_name = "note",
                sid = "this_should_not_exist",
                value = JToken.FromObject($"This is a note for something that does not exist."),
            });

            var resp = await session.BatchSingleOps(ops.ToArray());

            Assert.True(resp.success);
            Assert.True(resp.failed.Length == 1);
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestGetMP4Duration()
        {
            var asm = Assembly.GetExecutingAssembly();

            using (var asm_stream = asm.GetManifestResourceStream("MDACSTest.video.mp4"))
            {
                var resp = await session.UploadAsync(
                    asm_stream.Length,
                    "mp4",
                    "2030-01-01",
                    "mp4device",
                    "050001",
                    "mp4deviceuser",
                    asm_stream
                );

                Assert.True(resp.success);
            }

            var listing = await session.Data();

            foreach (var item in listing.data)
            {
                if (item.devicestr != null && item.devicestr.Equals("mp4device"))
                {
                    if (item.duration == 5.312)
                        return;
                }
            }

            Assert.Failed();
        }

        [FactAsync()]
        async Task TestCommitConfiguration()
        {
            var resp = await session.CommitConfigurationAsync("somedevice", "bobthebuilder", "{ \"haha\": true }");

            Assert.True(resp.success);
        }

        class ConfigFileData
        {
            public string userid;
            public string config_data;
        }

        [FactAsync("TestEnumerateConfigurations")]
        async Task TestDeviceConfig()
        {
            var resp = await session.DeviceConfig("somedevice", "{ \"haha\": false }");

            Assert.True(resp.success);

            var cfd = JsonConvert.DeserializeObject<ConfigFileData>(resp.config_data);

            Assert.True(cfd.userid.Equals("bobthebuilder"));
            Assert.True(cfd.config_data.Equals("{\r\n  \"haha\": true\r\n}"));
        }

        [FactAsync("TestCommitConfiguration")]
        async Task TestEnumerateConfigurations()
        {
            var resp = await session.EnumerateConfigurations();

            Assert.True(resp.success);
            Assert.True(resp.configs.Count == 1);
            Assert.True(resp.configs.ContainsKey("somedevice"));

            var cfd = JsonConvert.DeserializeObject<ConfigFileData>(resp.configs["somedevice"]);

            Assert.True(cfd.userid.Equals("bobthebuilder"));
            Assert.True(cfd.config_data.Equals("{ \"haha\": true }"));
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestSpaceInfo()
        {
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestDownload()
        {
        }

        /*
        [FactAsync("TestCommitSetFirst")]
        async Task TestDataPrivacy() {
            var session = new Session(
                "http://localhost:34002",
                "http://localhost:34001",
                "apple",
                "apple"
            );

            foreach (var item in (await session.Data()).data) {
                // Ensure this string which was placed by TestCommitSetFirst
                // is not present on any notes. This ensures that the privacy
                // feature has scrambled it.
                var a = item.note != null ? item.note.IndexOf("This is a note") > -1 : false;
                var b = item.devicestr.Equals("testdevice");
                var c = item.userstr.Equals("kmcguire");
                
                if (a || b || c) {
                    Assert.Failed();
                }
            }
        }
        */

        [FactAsync("TestDownload")]
        async Task TestNormalUserChangeState() {
            var session = new Session(
                "http://localhost:34002",
                "http://localhost:34001",
                "http://localhost:34015",
                "apple",
                "abc"
            );

            
            var resp = await session.Data();

            try {
                // This may fail with a 403 exception, which is OK.
                var r0 = await session.BatchSingleOps(new MDACS.API.Requests.BatchSingleOp[] {
                    new MDACS.API.Requests.BatchSingleOp() {
                        sid = resp.data[0].security_id,
                        field_name = "state",
                        value = JToken.FromObject("something"),
                    },
                });

                // This user should not be able to modify the state of this item.
                Assert.True(!r0.success);                
            } catch (WebException i) {
                if ((int)((HttpWebResponse)i.Response).StatusCode == 403) {
                    // An acceptable response.
                    return;
                }
            }
        }

        [FactAsync("TestDownload")]
        async Task TestNormalUserChangeNote() {
            var session = new Session(
                "http://localhost:34002",
                "http://localhost:34001",
                "http://localhost:34015",
                "apple",
                "abc"
            );

            var resp = await session.Data();

            var r1 = await session.BatchSingleOps(new MDACS.API.Requests.BatchSingleOp[] {
                new MDACS.API.Requests.BatchSingleOp() {
                    sid = resp.data[0].security_id,
                    field_name = "note",
                    value = JToken.FromObject("something"),
                },
            });

            // The user should be able to modify the item note field.
            Assert.True(r1.success);         
        }

        [FactAsync("TestDownload")]
        async Task TestNormalUserDelete() {
            var session = new Session(
                "http://localhost:34002",
                "http://localhost:34001",
                "http://localhost:34015",
                "apple",
                "abc"
            );

            var resp = await session.Data();

            var aitem = resp.data[1];

            var sid = aitem.security_id;

            try {
                await session.Delete(sid);
            } catch (WebException i) {
                if ((int)((HttpWebResponse)i.Response).StatusCode == 403) {
                    // An acceptable response.
                    return;
                }
            }

            resp = await session.Data();

            foreach (var item in resp.data)
            {
                // The item never disappears from the metadata; however, it does
                // have the `dqpath` forced to null to indicate there is no local
                // path to the actual data.
                if (item.security_id == sid && item.fqpath == null)
                {
                    Assert.Failed();
                }
            }                   
        }        

        [FactAsync("TestDownload")]
        async Task TestDelete()
        {
            var resp = await session.Data();

            var aitem = resp.data[1];

            var sid = aitem.security_id;

            await session.Delete(sid);

            resp = await session.Data();

            foreach (var item in resp.data)
            {
                // The item never disappears from the metadata; however, it does
                // have the `dqpath` forced to null to indicate there is no local
                // path to the actual data.
                if (item.security_id == sid && item.fqpath != null)
                {
                    Assert.Failed();
                }
            }
        }

        [FactAsync]
        async Task TestCommand() {
            await session.ExecuteCommandAsync(
                "auth",
                "test command from test program"
            );
        }
    }
}
