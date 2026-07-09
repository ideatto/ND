namespace ND.Framework
{
    public sealed class FrameworkDebugCommands
    {
        private readonly GameTimeService gameTimeService;

        public FrameworkDebugCommands(GameTimeService gameTimeService)
        {
            this.gameTimeService = gameTimeService;
        }

        public void SetTimeScale(float scale)
        {
            gameTimeService.SetTimeScale(scale);
        }

        public void CompleteTradeImmediately()
        {
            FrameworkEvents.RaiseCompleteTradeRequested();
        }

        public void ForceLoadCompleted()
        {
            FrameworkEvents.RaiseLoadCompleted(FrameworkRoot.Instance.CurrentSaveData);
        }
    }
}
