namespace CGame
{
    /// <summary>
    /// 接收来自玩家、AI 或回放系统的角色控制意图。
    /// </summary>
    public interface ICharacterIntentSink
    {
        void SubmitControlIntent(in CharacterControlIntent intent);
    }
}
