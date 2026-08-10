using System;
using UnityEngine;

namespace CGame
{
    public readonly struct PlayerStartInfo
    {
        public PlayerStartInfo(string id, Vector3 position, Quaternion rotation)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("PlayerStart id is required.", nameof(id))
                : id;
            Position = position;
            Rotation = rotation;
        }

        public string Id { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }
    }
}
