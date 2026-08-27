using System;

namespace CGame.Animation.Rig
{
    [Serializable]
    public struct KRigElement
    {
        public KRigElement(int index, string name, int depth)
        {
            Index = index;
            Name = name;
            Depth = depth;
        }

        public int Index;
        public string Name;
        public int Depth;
    }
}
