using System;
using System.Collections.Generic;

namespace CGame.Animation.Rig
{
    [Serializable]
    public sealed class KRigElementChain
    {
        public string Name;
        public List<KRigElement> Elements = new List<KRigElement>();
    }
}
