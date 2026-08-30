using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CGame
{
    public interface IExperienceAssetLoader
    {
        Task<IDisposable> LoadAsync(IReadOnlyList<string> locations);
    }
}
