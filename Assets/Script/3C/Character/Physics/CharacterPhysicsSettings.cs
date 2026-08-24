using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu]
    public class CharacterPhysicsSettings : ScriptableObject
    {
        /// <summary>
        /// 决定系统是否自动模拟。
        /// 为 true 时，模拟会在 FixedUpdate 中执行
        /// </summary>
        [Tooltip("Determines if the system simulates automatically. If true, the simulation is done on FixedUpdate")]
        public bool AutoSimulation = true;
        /// <summary>
        /// 是否处理角色和 CharacterPhysicsMover 的插值
        /// </summary>
        [Tooltip("是否处理角色和移动平台的插值")]
        public bool Interpolate = false;
        /// <summary>

        /// 系统 Motor 列表的初始容量（需要时会自动扩容，但较高的初始容量有助于减少 GC 分配）
        /// </summary>
        [Tooltip("Initial capacity of the system's list of Motors (will resize automatically if needed, but setting a high initial capacity can help preventing GC allocs)")]
        public int MotorsListInitialCapacity = 100;
        /// <summary>
        /// 系统 Mover 列表的初始容量（需要时会自动扩容，但较高的初始容量有助于减少 GC 分配）
        /// </summary>
        [Tooltip("Initial capacity of the system's list of Movers (will resize automatically if needed, but setting a high initial capacity can help preventing GC allocs)")]
        public int MoversListInitialCapacity = 100;
    }
}
