using System;

namespace CGame
{
    public interface ICharacterControllerBinding : IDisposable
    {
        bool IsActive { get; }
    }
}
