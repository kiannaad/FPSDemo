using System;

namespace CGame
{
    public interface IPawnRegistration : IDisposable
    {
        bool IsActive { get; }
    }
}
