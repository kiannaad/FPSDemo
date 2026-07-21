using CGame.Animation;
using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 创建本地玩家角色所需的稳定输入。
    /// </summary>
    public readonly struct PlayerCharacterSpawnRequest
    {
        public PlayerCharacterSpawnRequest(
            Transform parent,
            GameObject visualPrefab,
            CharacterAnimationConfig animationConfig,
            InputHandle input,
            Vector3 position,
            Quaternion rotation,
            string name)
        {
            Parent = parent;
            VisualPrefab = visualPrefab;
            AnimationConfig = animationConfig;
            Input = input;
            Position = position;
            Rotation = rotation;
            Name = name;
        }

        public Transform Parent { get; }
        public GameObject VisualPrefab { get; }
        public CharacterAnimationConfig AnimationConfig { get; }
        public InputHandle Input { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public string Name { get; }
    }
}
