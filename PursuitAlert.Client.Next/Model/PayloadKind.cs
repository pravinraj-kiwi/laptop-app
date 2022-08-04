namespace PursuitAlert.Domain.Modes.Models
{
    public enum PayloadKind
    {
        /// <summary>
        /// Describes a mode that should not emit a payload
        /// </summary>
        None,

        /// <summary>
        /// Describes a mode that emits a single payload
        /// </summary>
        OneTime,

        /// <summary>
        /// Describes a mode that emits a recurring payload. This should be used in conjuction with
        /// the <see cref="Mode.PayloadInterval" /> property.
        /// </summary>
        Repeating
    }
}