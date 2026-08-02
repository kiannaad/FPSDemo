namespace CGame
{
    public interface IComponent
    {
        int Priority { get; }

        /// <summary>
        /// 初始化组件并绑定所属 Pawn。
        /// </summary>
        void InitializingComponent(Pawn owner);

        /// <summary>
        /// 更新组件普通帧逻辑。
        /// </summary>
        void UpdatingComponent(float elapseSeconds);

        /// <summary>
        /// 更新组件固定帧逻辑。
        /// </summary>
        void FixedUpdatingComponent(float elapseSeconds);

        /// <summary>
        /// 更新组件渲染后逻辑。
        /// </summary>
        void LateUpdatingComponent(float elapseSeconds);

        /// <summary>
        /// 关闭并清理组件。
        /// </summary>
        void ShuttingDownComponent();
    }
}
