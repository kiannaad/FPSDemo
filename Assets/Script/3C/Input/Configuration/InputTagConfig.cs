using System;
using System.Collections.Generic;
using CGame.GameplayTags;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    [CreateAssetMenu(menuName = "CGame/Input/Input Tag Config")]
    public sealed class InputTagConfig : ScriptableObject
    {
        [SerializeField] private InputTagBinding[] bindings = Array.Empty<InputTagBinding>();

        public IReadOnlyList<InputTagBinding> Bindings => bindings;

        public void SetBindings(IEnumerable<InputTagBinding> bindings)
        {
            this.bindings = bindings == null ? Array.Empty<InputTagBinding>() : new List<InputTagBinding>(bindings).ToArray();
        }
    }

    [Serializable]
    public sealed class InputTagBinding
    {
        [SerializeField] private InputActionReference actionReference;
        [SerializeField] private GameplayTag inputTag;

        public InputTagBinding(InputActionReference actionReference, GameplayTag inputTag)
        {
            this.actionReference = actionReference;
            this.inputTag = inputTag;
        }

        public InputActionReference ActionReference => actionReference;
        public GameplayTag InputTag => inputTag;
    }
}
