namespace PursuitAlert.Domain.Modes.Events
{
    public enum ModeChangeType
    {
        /// <summary>
        /// Describes an event where a new mode was engaged
        /// </summary>
        ModeEngaged,

        /// <summary>
        /// Describes an event where an existing mode was disengaged
        /// </summary>
        ModeDisengaged
    }
}