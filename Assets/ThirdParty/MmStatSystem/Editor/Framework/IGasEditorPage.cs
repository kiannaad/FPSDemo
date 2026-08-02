namespace GAS.Editor
{
    /// <summary>
    /// GAS 编辑器页签接口
    /// </summary>
    public interface IGasEditorPage
    {
        /// <summary> 页签标题 </summary>
        string Title { get; }

        /// <summary>
        /// 页签启用
        /// </summary>
        void OnEnable();

        /// <summary>
        /// 绘制页签内容
        /// </summary>
        void OnGUI();

        /// <summary>
        /// 页签关闭
        /// </summary>
        void OnDisable();
    }
}
