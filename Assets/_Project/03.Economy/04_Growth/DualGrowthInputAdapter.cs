using System;

namespace ND.Economy
{
    public enum DualGrowthInputAdapterFailureReason
    {
        None = 0,
        InvalidRequest,
        InvalidState,
        InvalidContentDefinition,
        GrowthIdMismatch,
        AxisMismatch
    }

    [Serializable]
    public sealed class DualGrowthStateSnapshot
    {
        public int PlayerGrowthLevel;
        public int CaravanGrowthLevel;
        public long DevelopmentCurrency;
    }

    public sealed class DualGrowthInputAdapterResult
    {
        public DualGrowthInputAdapterResult()
        {
            GrowthId = string.Empty;
        }

        public bool Success { get; internal set; }
        public DualGrowthInputAdapterFailureReason FailureReason { get; internal set; }
        public string GrowthId { get; internal set; }
        public GrowthAxis Axis { get; internal set; }
        public DualGrowthInput Input { get; internal set; }
    }

    public static class DualGrowthInputAdapter
    {
        public static DualGrowthInputAdapterResult Build(
            string requestedGrowthId,
            GrowthAxis requestedAxis,
            DualGrowthStateSnapshot state,
            GrowthAxisDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(requestedGrowthId))
                return Fail(DualGrowthInputAdapterFailureReason.InvalidRequest);
            if (state == null ||
                state.PlayerGrowthLevel < 0 ||
                state.CaravanGrowthLevel < 0 ||
                state.DevelopmentCurrency < 0)
            {
                return Fail(DualGrowthInputAdapterFailureReason.InvalidState);
            }
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.GrowthId) ||
                definition.MaximumLevel <= 0 ||
                definition.Levels == null)
            {
                return Fail(
                    DualGrowthInputAdapterFailureReason.InvalidContentDefinition);
            }
            if (!string.Equals(
                requestedGrowthId,
                definition.GrowthId,
                StringComparison.Ordinal))
            {
                return Fail(DualGrowthInputAdapterFailureReason.GrowthIdMismatch);
            }
            if (requestedAxis != definition.Axis)
                return Fail(DualGrowthInputAdapterFailureReason.AxisMismatch);

            return new DualGrowthInputAdapterResult
            {
                Success = true,
                FailureReason = DualGrowthInputAdapterFailureReason.None,
                GrowthId = requestedGrowthId,
                Axis = requestedAxis,
                Input = new DualGrowthInput
                {
                    RequestedGrowthId = requestedGrowthId,
                    RequestedAxis = requestedAxis,
                    PlayerGrowthLevel = state.PlayerGrowthLevel,
                    CaravanGrowthLevel = state.CaravanGrowthLevel,
                    DevelopmentCurrency = state.DevelopmentCurrency,
                    Definition = definition
                }
            };
        }

        private static DualGrowthInputAdapterResult Fail(
            DualGrowthInputAdapterFailureReason reason)
        {
            return new DualGrowthInputAdapterResult
            {
                FailureReason = reason
            };
        }
    }
}
