using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagConfig : MonoBehaviour
    {
        [SerializeField] private List<GameplayTagSource> sources = new List<GameplayTagSource>();
        [SerializeField] private List<GameplayTagRedirect> redirects = new List<GameplayTagRedirect>();

        public IReadOnlyList<GameplayTagSource> Sources => sources.AsReadOnly();
        public IReadOnlyList<GameplayTagRedirect> Redirects => redirects.AsReadOnly();

        public void SetDefinition(
            IEnumerable<GameplayTagSource> newSources,
            IEnumerable<GameplayTagRedirect> newRedirects = null)
        {
            GameplayTagSource[] sourceSnapshot = newSources?.ToArray();
            GameplayTagRedirect[] redirectSnapshot = newRedirects?.ToArray();

            sources.Clear();
            redirects.Clear();

            if (sourceSnapshot != null)
            {
                sources.AddRange(sourceSnapshot);
            }

            if (redirectSnapshot != null)
            {
                redirects.AddRange(redirectSnapshot);
            }
        }
    }
}
