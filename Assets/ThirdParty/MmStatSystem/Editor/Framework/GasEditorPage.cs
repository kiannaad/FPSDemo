namespace GAS.Editor
{
    /// <summary>
    /// GAS 编辑器页签基类
    /// </summary>
    public abstract class GasEditorPage : IGasEditorPage
    {
        public abstract string Title { get; }

        /// <summary>
        /// 页签启用
        /// </summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// 绘制页签内容
        /// </summary>
        public abstract void OnGUI();

        /// <summary>
        /// 页签关闭
        /// </summary>
        public virtual void OnDisable()
        {
        }
    }
}
