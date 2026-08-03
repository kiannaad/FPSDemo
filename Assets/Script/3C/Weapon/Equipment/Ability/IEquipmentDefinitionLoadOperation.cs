using System;

namespace CGame
{
    public interface IEquipmentDefinitionLoadOperation : IDisposable
    {
        bool IsDone { get; }
        bool TryTakeLease(out IEquipmentDefinitionLease definitionLease);
    }
}
