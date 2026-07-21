using System;

namespace CGame
{
    public interface IControllerRegistration : IDisposable
    {
        bool IsActive { get; }
    }
}
