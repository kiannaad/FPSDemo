using Unity.Collections;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct VirtualElementHandle
    {
        [ReadOnly] public TransformStreamHandle TargetHandle;
        public TransformStreamHandle VirtualHandle;
    }
}
