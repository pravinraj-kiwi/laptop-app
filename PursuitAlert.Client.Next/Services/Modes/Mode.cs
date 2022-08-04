using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PursuitAlert.Client.Services.Modes
{
    public class Mode
    {
        #region Properties

        /// <summary>
        /// Whether or not the log should animate (pulsate) for this mode.
        /// </summary>
        public bool Animate { get; set; }

        /// <summary>
        /// The button number to which this mode is mapped.
        /// </summary>
        public int ButtonPosition { get; set; }

        /// <summary>
        /// The color to be used when displaying this mode.
        /// </summary>
        public string ColorName { get; set; }

        /// <summary>
        /// The heading to be displayed to the user. If this is not defined, the <see cref="Message"
        /// /> property will be used.
        /// </summary>
        public string Heading { get; set; }

        /// <summary>
        /// A delay that will occur before a mode is activated. This will trigger a countdown on the client.
        /// </summary>
        public int IncidentDelay { get; set; }

        /// <summary>
        /// Whether or not to include the vehicle's bearing with this mode
        /// </summary>
        public bool IncludeBearing { get; set; }

        /// <summary>
        /// Whether or not to include the vehicle's speed with this mode
        /// </summary>
        public bool IncludeSpeed { get; set; }

        /// <summary>
        /// The message that will be shown to the user.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The name of the mode. This will not be shown to the user and will be used in the application.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The interval between which payload will be emitted to the server.
        /// </summary>
        public int PayloadInterval { get; set; }

        /// <summary>
        /// The kind of payload that will be emitted.
        /// </summary>
        public PayloadKind PayloadKind { get; set; }

        /// <summary>
        /// Whether or not to play a sound when the mode is engaged and disengaged
        /// </summary>
        public bool PlaySound { get; set; }

        /// <summary>
        /// Send an All Clear message when this mode is deactivated
        /// </summary>
        public bool SendAllClearWhenEnded { get; set; }

        /// <summary>
        /// Once the vehicle has been in a mode and stationary for this amount of time, the mode
        /// will automatically change to move over mode
        /// </summary>
        public int TimeOutUntilMoveOverInSeconds { get; set; }

        /// <summary>
        /// The event type that will be sent to the MQTT service. Should be a single letter.
        /// </summary>
        public string Type { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Overide the default Equals to check for value equality (make sure the properties of the
        /// <see cref="Mode" /> object are the same). https://stackoverflow.com/a/20701995
        /// </summary>
        /// <param name="a">The Mode to be compared.</param>
        /// ///
        /// <param name="b">The Mode to be compared.</param>
        /// <returns></returns>
        public static bool operator !=(Mode a, Mode b) => !(a == b);

        /// <summary>
        /// Overide the default Equals to check for value equality (make sure the properties of the
        /// <see cref="Mode" /> object are the same). https://stackoverflow.com/a/20701995
        /// </summary>
        /// <param name="a">The Mode to be compared.</param>
        /// ///
        /// <param name="b">The Mode to be compared.</param>
        /// <returns></returns>
        public static bool operator ==(Mode a, Mode b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if ((object)a == null || (object)b == null)
                return false;

            return a.ValuesAreEqual(b);
        }

        /// <summary>
        /// Overide the default Equals to check for value equality (make sure the properties of the
        /// <see cref="Mode" /> object are the same). https://stackoverflow.com/a/20701995
        /// </summary>
        /// <param name="obj">The Mode to be compared.</param>
        /// <returns></returns>
        public override bool Equals(object obj) => ValuesAreEqual(obj);

        /// <summary>
        /// Overide the default Equals to check for value equality (make sure the properties of the
        /// <see cref="Mode" /> object are the same). https://stackoverflow.com/a/20701995
        /// </summary>
        /// <param name="a">The Mode to be compared.</param>
        /// ///
        /// <param name="b">The Mode to be compared.</param>
        /// <returns></returns>
        private bool ValuesAreEqual(object obj)
        {
            // Make sure we haven't been given a null object
            if (obj == null)
                return false;

            // Make sure we've been given a valid Mode
            if (!(obj is Mode referenceMode))
                return false;

            return Animate == referenceMode.Animate
                && ButtonPosition == referenceMode.ButtonPosition
                && (!string.IsNullOrWhiteSpace(ColorName) && ColorName.Equals(referenceMode.ColorName, StringComparison.InvariantCultureIgnoreCase))
                && (!string.IsNullOrWhiteSpace(Message) && Message.Equals(referenceMode.Message, StringComparison.InvariantCultureIgnoreCase))
                && (!string.IsNullOrWhiteSpace(Heading) && Heading.Equals(referenceMode.Heading, StringComparison.InvariantCultureIgnoreCase))
                && IncidentDelay == referenceMode.IncidentDelay
                && IncludeBearing == referenceMode.IncludeBearing
                && IncludeSpeed == referenceMode.IncludeSpeed
                && (!string.IsNullOrWhiteSpace(Name) && Name.Equals(referenceMode.Name, StringComparison.InvariantCultureIgnoreCase))
                && PayloadInterval == referenceMode.PayloadInterval
                && PayloadKind == referenceMode.PayloadKind
                && PlaySound == referenceMode.PlaySound
                && SendAllClearWhenEnded == referenceMode.SendAllClearWhenEnded
                && TimeOutUntilMoveOverInSeconds == referenceMode.TimeOutUntilMoveOverInSeconds
                && (!string.IsNullOrWhiteSpace(Type) && Type.Equals(referenceMode.Type, StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion Methods
    }
}