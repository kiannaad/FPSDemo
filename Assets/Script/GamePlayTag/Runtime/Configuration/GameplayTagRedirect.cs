using System;
using UnityEngine;

namespace CGame.GameplayTags
{
    [Serializable]
    public sealed class GameplayTagRedirect
    {
        [SerializeField] private string oldName;
        [SerializeField] private string newName;

        public GameplayTagRedirect(string oldName, string newName)
        {
            this.oldName = oldName;
            this.newName = newName;
        }

        public string OldName => oldName ?? string.Empty;
        public string NewName => newName ?? string.Empty;
    }
}
