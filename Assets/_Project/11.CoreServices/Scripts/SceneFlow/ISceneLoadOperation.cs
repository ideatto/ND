namespace ND.Framework
{
    /// <summary>Exposes scene loading progress and activation without leaking Unity AsyncOperation.</summary>
    public interface ISceneLoadOperation
    {
        /// <summary>The requested Unity scene name.</summary>
        string SceneName { get; }

        /// <summary>Normalized 0–1 loading progress; Unity's pre-activation 0.9 plateau is exposed as 1.</summary>
        float Progress01 { get; }

        /// <summary>True when scene data reached the activation boundary, even if activation remains deferred.</summary>
        bool IsReadyForActivation { get; }

        /// <summary>True after automatic or explicit activation permission has been granted.</summary>
        bool IsActivationAllowed { get; }

        /// <summary>True only after Unity reports actual scene activation completion.</summary>
        bool IsCompleted { get; }

        /// <summary>Allows a deferred scene to activate. Repeated calls have no additional effect.</summary>
        void AllowActivation();
    }
}
