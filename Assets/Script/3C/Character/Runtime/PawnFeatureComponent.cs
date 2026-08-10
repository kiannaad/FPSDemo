namespace CGame
{
    public abstract class PawnFeatureComponent : IPawnInitStateParticipant
    {
        protected PawnFeatureComponent(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool IsReady { get; private set; } = true;

        public bool IsBound { get; private set; }

        public bool IsShutdown { get; private set; }

        public void SetReady(bool ready)
        {
            IsReady = ready;
        }

        public virtual bool CanEnterState(PawnInitState nextState, PawnInitContext context)
        {
            return nextState != PawnInitState.GameplayReady || IsReady;
        }

        public virtual void EnterState(PawnInitState nextState, PawnInitContext context)
        {
            if (nextState == PawnInitState.DataInitialized)
            {
                IsBound = true;
            }
        }

        public virtual void Shutdown()
        {
            IsBound = false;
            IsShutdown = true;
        }
    }
}
