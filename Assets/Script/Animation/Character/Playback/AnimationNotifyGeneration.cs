namespace CGame.Animation
{
    internal sealed class AnimationNotifyGeneration
    {
        public bool IsValid { get; private set; } = true;

        public void Invalidate()
        {
            IsValid = false;
        }
    }
}
