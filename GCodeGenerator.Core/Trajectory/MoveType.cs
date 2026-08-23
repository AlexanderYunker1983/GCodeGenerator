namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// Types of G-code moves for visualization (plan item 6.2).
    /// Moved from <c>PreviewViewModel</c> (WPF layer) to Core so the scene
    /// can be built and tested without WPF types.
    /// </summary>
    public enum MoveType
    {
        Rapid,      // G0 - rapid positioning (no cutting)
        Linear,     // G1 - linear interpolation (cutting)
        ArcCW,      // G2 - circular interpolation clockwise
        ArcCCW      // G3 - circular interpolation counter-clockwise
    }
}
