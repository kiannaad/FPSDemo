using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Network
{
    public sealed class DedicatedServerWorldConfiguration : WorldConfiguration
    {
        private IDedicatedServerBootstrapConfiguration bootstrap;
        private Scene levelScene;

        public void Initialize(IDedicatedServerBootstrapConfiguration source, Scene scene)
        {
            bootstrap = source != null ? source : throw new ArgumentNullException(nameof(source));
            levelScene = scene;
        }

        public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() =>
            new WorldSubSystem[] { new CharacterPhysicsSubSystem(bootstrap.CharacterPhysicsSettings) };

        public override LevelRuntime CreateLevelRuntime() =>
            new LevelRuntime(bootstrap.LevelDefinition, levelScene);
    }
}
