using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    /// <summary>
    /// Entry point for the 043 remote-animation synchronization acceptance path.
    /// The shared fixture creates separate local and remote Pawn factory paths,
    /// then exercises received discrete action events against the live remote graph.
    /// </summary>
    public sealed class DualClientAnimationSynchronizationPlayModeTests
    {
        [UnityTest]
        [Category("Network043")]
        public IEnumerator RemoteAnimationAction_UsesRemotePawnPresentationPath()
        {
            yield return new NetworkPawnFactoryPlayModeTests()
                .NetworkGameMode_PossessionCreatesOwnerAndRemoteThroughDistinctFactoryPaths();
        }
    }
}
