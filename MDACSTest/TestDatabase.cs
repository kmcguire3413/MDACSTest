using MDACS.Server;
using MDACS.Test;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MDACS.API;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;

namespace MDACSTest
{
    public class TestDatabase
    {
        private TestPlatform platform;
        private Session session;

        public TestDatabase()
        {
            CreateNewPlatformAndSession();
        }

        private bool CreateNewPlatformAndSession()
        {
            bool CheckTrust(
                object sender, 
                X509Certificate cert, 
                X509Chain chain, 
                System.Net.Security.SslPolicyErrors err
                )
            {
                Console.WriteLine($"checking cert {cert} {chain} {err}");
                return true;
            }

            ServicePointManager.ServerCertificateValidationCallback = CheckTrust;

            platform = new TestPlatform("https://epdmdacs.kmcg3413.net:34002", 34001);

            Thread.Sleep(2000);

            session = new Session(
                "https://epdmdacs.kmcg3413.net:34002",
                "https://localhost:34001",
                "kmcguire",
                "Z4fmv96s#7"
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
            var rand_data = new DoubleEndedStream();

            Random rng = new Random();

            var chunk = new byte[1024];

            for (long sent = 0; sent < size; sent += chunk.Length)
            {
                rng.NextBytes(chunk);
                await rand_data.WriteAsync(chunk, 0, chunk.Length);
            }

            rand_data.Dispose();

            return await session.UploadAsync(
                rand_data.Length,
                datatype,
                datestr,
                devicestr,
                timestr,
                userstr,
                rand_data
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

        [FactAsync("TestCommitSetFirst")]
        async Task TestCommitConfiguration()
        {

        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestDeviceConfig()
        {
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestEnumerateConfigurations()
        {
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestSpaceInfo()
        {
        }

        [FactAsync("TestCommitSetFirst")]
        async Task TestDownload()
        {
        }

        [FactAsync("TestDownload")]
        async Task TestDelete()
        {
        }
    }
}
