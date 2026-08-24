using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.Tests.World
{
    public sealed class TickDomainOrderTests
    {
        [Test]
        public void PreAnimationTickRunsOnlyInPreAnimationDomain()
        {
            var calls = new List<string>();
            var manager = new TickTaskManager();
            var owner = new object();
            var preAnimation = new TickTaskNode(
                "Animation.Pre",
                TickGroup.TG_PreAnimation,
                _ => calls.Add("PreAnimation"));

            using (manager.RegisterOwner(owner, new[] { preAnimation }))
            {
                manager.SetOwnerEnabled(owner, true);

                manager.ExecuteDomain(TickDomain.Update, 0.016f);
                manager.ExecuteDomain(TickDomain.Fixed, 0.016f);
                CollectionAssert.IsEmpty(calls);

                manager.ExecuteDomain(TickDomain.PreAnimation, 0.016f);
                CollectionAssert.AreEqual(new[] { "PreAnimation" }, calls);

                manager.ExecuteDomain(TickDomain.Late, 0.016f);
                CollectionAssert.AreEqual(new[] { "PreAnimation" }, calls);
            }
        }

        [Test]
        public void FrameDomainsExecuteAnimationBeforeLateCameraPresentation()
        {
            var calls = new List<string>();
            var manager = new TickTaskManager();
            var owner = new object();
            TickTaskNode[] nodes =
            {
                CreateNode("Gameplay", TickGroup.TG_Gameplay, calls),
                CreateNode("Physics", TickGroup.TG_PhysicsMovement, calls),
                CreateNode("Animation.Pre", TickGroup.TG_PreAnimation, calls),
                CreateNode("Animation.Post", TickGroup.TG_PostAnimation, calls),
                CreateNode("Camera", TickGroup.TG_Camera, calls)
            };

            using (manager.RegisterOwner(owner, nodes))
            {
                manager.SetOwnerEnabled(owner, true);

                manager.ExecuteDomain(TickDomain.Update, 0.016f);
                manager.ExecuteDomain(TickDomain.Fixed, 0.016f);
                manager.ExecuteDomain(TickDomain.PreAnimation, 0.016f);
                manager.ExecuteDomain(TickDomain.Late, 0.016f);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "Gameplay",
                    "Physics",
                    "Animation.Pre",
                    "Animation.Post",
                    "Camera"
                },
                calls);
        }

        private static TickTaskNode CreateNode(
            string name,
            TickGroup group,
            ICollection<string> calls)
        {
            return new TickTaskNode(name, group, _ => calls.Add(name));
        }
    }
}
