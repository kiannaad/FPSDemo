using Unity.Collections;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct VirtualElementJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<VirtualElementHandle> Handles;

        public void ProcessAnimation(AnimationStream stream)
        {
            for (int index = 0; index < Handles.Length; index++)
            {
                VirtualElementHandle handle = Handles[index];
                handle.VirtualHandle.SetPosition(stream, handle.TargetHandle.GetPosition(stream));
                handle.VirtualHandle.SetRotation(stream, handle.TargetHandle.GetRotation(stream));
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }
    }
}
