namespace PlantDoctor.AR
{
    /// <summary>
    /// What the UI needs to know about AR placement of the plant twin. Implemented by
    /// PlantTwinPlacer, which only compiles with AR Foundation; the UI depends on this
    /// interface so it compiles everywhere.
    /// </summary>
    public interface ITwinPlacement
    {
        bool IsPlacing { get; }
        bool HasSurface { get; }
        /// <summary>True when the surface under the reticle is a properly detected plane (not an estimate).</summary>
        bool SurfaceIsPlane { get; }
        bool IsPlaced { get; }
        /// <summary>Seconds spent looking for a surface since placing started (0 when not placing).</summary>
        float SearchSeconds { get; }
        void EnsurePlaced();
        /// <summary>Explicit fallback when no surface turns up: stands the twin on the floor in front of the user.</summary>
        void PlaceInFrontOfCamera();
        /// <summary>Turns the placed twin by a fixed angle.</summary>
        void RotateStep(float degrees);
        /// <summary>Cycles the twin through small / medium / large.</summary>
        void CycleScale();
        /// <summary>Lets the user pick a new spot for the twin.</summary>
        void BeginReposition();
    }
}
