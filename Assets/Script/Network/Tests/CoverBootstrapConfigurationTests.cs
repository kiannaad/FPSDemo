using NUnit.Framework;
using UnityEngine;
using System.IO;

namespace CGame.Network.Tests
{
    public sealed class CoverBootstrapConfigurationTests
    {
        [Test]
        public void DedicatedBootstrap_ExposesCoverPointCatalogToRuntime()
        {
            string gameBootstrapPath = Path.Combine(Application.dataPath, "Script/GameMode/Runtime/GameBootstrap.cs");
            string runtimePath = Path.Combine(Application.dataPath, "Script/Network/Runtime/DedicatedServer/DedicatedServerRuntime.cs");

            Assert.That(File.ReadAllText(gameBootstrapPath), Does.Contain("CoverPointCatalog CoverPointCatalog"));
            Assert.That(File.ReadAllText(runtimePath), Does.Contain("ValidateCoverPointCatalog(dedicatedBootstrap.CoverPointCatalog)"));
        }
    }
}
