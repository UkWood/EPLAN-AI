using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Audeu.EplanAi.Contracts;
using Audeu.EplanAi.LegacyHost.Configuration;
using Audeu.EplanAi.LegacyHost.Spool;
using Xunit;

namespace Audeu.EplanAi.LegacyHost.Tests
{
    public sealed class SpoolStoreTests : IDisposable
    {
        private readonly string _root;

        public SpoolStoreTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "Audeu.EplanAi.Spools", Guid.NewGuid().ToString("N"));
        }

        [Fact]
        public void DefaultRoot_IsFixedUnderLocalApplicationData()
        {
            var paths = BridgePaths.CreateDefault();
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Audeu",
                "EplanAiBridge");

            Assert.Equal(expected, paths.RootPath);
        }

        [Fact]
        public void Paths_AreDerivedOnlyFromCanonicalGuid()
        {
            var requestId = Guid.Parse("7d64550e-9154-4d78-beb0-a622905a1372");
            var paths = new BridgePaths(_root);

            Assert.Equal(
                Path.Combine(_root, "requests", "7d64550e-9154-4d78-beb0-a622905a1372.request.json"),
                paths.GetRequestPath(requestId));
            Assert.Equal(
                Path.Combine(_root, "responses", "7d64550e-9154-4d78-beb0-a622905a1372.response.json"),
                paths.GetResponsePath(requestId));
        }

        [Fact]
        public void SizeLimits_ArePinnedToDesignValues()
        {
            Assert.Equal(1024 * 1024, SpoolStore.MaxRequestBytes);
            Assert.Equal(16 * 1024 * 1024, SpoolStore.MaxResponseBytes);
        }

        [Fact]
        public async Task WriteRequestAsync_WritesCompleteJsonThenRemovesTempFile()
        {
            var paths = new BridgePaths(_root);
            var store = new SpoolStore(paths);
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "system.status",
                Mode = "read",
                Arguments = new Dictionary<string, string> { ["key"] = "value" }
            };

            var finalPath = paths.GetRequestPath(request.RequestId);
            Assert.False(File.Exists(finalPath));

            await store.WriteRequestAsync(request, CancellationToken.None);

            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(finalPath + ".tmp"));

            var roundTripped = ReadJson<BridgeRequest>(finalPath);
            Assert.Equal(request.RequestId, roundTripped.RequestId);
            Assert.Equal("system.status", roundTripped.Operation);
            Assert.Equal("value", roundTripped.Arguments["key"]);
        }

        [Fact]
        public async Task WriteRequestAsync_RejectsPayloadLargerThanOneMiB()
        {
            var store = new SpoolStore(new BridgePaths(_root));
            var request = new BridgeRequest
            {
                RequestId = Guid.NewGuid(),
                Operation = "system.status",
                Mode = "read",
                Arguments = new Dictionary<string, string>
                {
                    ["oversized"] = new string('x', SpoolStore.MaxRequestBytes + 1)
                }
            };

            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.WriteRequestAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task ReadResponseAsync_RoundTripsTypedResponse()
        {
            var paths = new BridgePaths(_root);
            var store = new SpoolStore(paths);
            var requestId = Guid.NewGuid();
            var response = new BridgeResponse
            {
                RequestId = requestId,
                Success = true,
                DataJson = "{\"connected\":true}"
            };

            Directory.CreateDirectory(paths.ResponseDirectory);
            WriteJson(paths.GetResponsePath(requestId), response);

            var actual = await store.ReadResponseAsync(requestId, CancellationToken.None);

            Assert.True(actual.Success);
            Assert.Equal(requestId, actual.RequestId);
            Assert.Equal("{\"connected\":true}", actual.DataJson);
        }

        [Fact]
        public async Task ReadResponseAsync_RejectsFileLargerThanSixteenMiB()
        {
            var paths = new BridgePaths(_root);
            var store = new SpoolStore(paths);
            var requestId = Guid.NewGuid();
            Directory.CreateDirectory(paths.ResponseDirectory);

            using (var stream = new FileStream(paths.GetResponsePath(requestId), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(SpoolStore.MaxResponseBytes + 1L);
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.ReadResponseAsync(requestId, CancellationToken.None));
        }

        [Fact]
        public void CleanupStaleFiles_RemovesOnlyFilesOlderThanMaxAge()
        {
            var paths = new BridgePaths(_root);
            var store = new SpoolStore(paths);
            Directory.CreateDirectory(paths.RequestDirectory);
            Directory.CreateDirectory(paths.ResponseDirectory);

            var stale = paths.GetRequestPath(Guid.NewGuid());
            var fresh = paths.GetResponsePath(Guid.NewGuid());
            File.WriteAllText(stale, "stale");
            File.WriteAllText(fresh, "fresh");
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-25));
            File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow.AddHours(-23));

            store.CleanupStaleFiles(TimeSpan.FromHours(24));

            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(fresh));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private static T ReadJson<T>(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        private static void WriteJson<T>(string path, T value)
        {
            using (var stream = File.Create(path))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
            }
        }
    }
}
