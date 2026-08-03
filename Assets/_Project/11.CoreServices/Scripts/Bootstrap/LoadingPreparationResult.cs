namespace ND.Framework
{
    /// <summary>Reports whether framework data preparation permits InGame scene loading.</summary>
    public readonly struct LoadingPreparationResult
    {
        private LoadingPreparationResult(bool succeeded, string errorSummary)
        {
            Succeeded = succeeded;
            ErrorSummary = errorSummary ?? string.Empty;
        }

        /// <summary>True only when all required loading preparation completed.</summary>
        public bool Succeeded { get; }

        /// <summary>Empty on success; otherwise a concise diagnostic summary suitable for logs or UI.</summary>
        public string ErrorSummary { get; }

        public static LoadingPreparationResult Success()
            => new LoadingPreparationResult(true, string.Empty);

        public static LoadingPreparationResult Failure(string errorSummary)
            => new LoadingPreparationResult(false, errorSummary);
    }
}
