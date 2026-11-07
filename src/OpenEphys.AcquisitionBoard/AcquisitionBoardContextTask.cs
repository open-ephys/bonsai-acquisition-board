using System;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Encapsulates a single ONI context for an Open Ephys Acquisition Board and orchestrates interaction
    /// with its hardware.
    /// </summary>
    /// <remarks>
    /// The Acquisition Board is built on top of the <see href="https://open-ephys.github.io/ONI/">Open Neuro
    /// Interface (ONI)</see> specification, which requires a host application to hold a "context" containing
    /// handles for hardware communication, data acquisition parameters, etc. <see
    /// cref="AcquisitionBoardContextTask"/> fulfills this role for the Acquisition Board, which always
    /// communicates with the host computer through the "ft600" USB device driver. Instances of this class are
    /// created using the <see cref="CreateContext"/> operator, and data acquisition is started using the
    /// <see cref="Onix1.StartAcquisition"/> operator.
    /// <para>
    /// This library requires version 2.x.x of the Acquisition Board gateware. Instructions for updating the
    /// gateware can be found in the <see
    /// href="https://open-ephys.github.io/acq-board-docs/User-Manual/Gateware-Update.html">Acquisition Board
    /// documentation</see>.
    /// </para>
    /// </remarks>
    public class AcquisitionBoardContextTask : Onix1.ContextTask
    {
        internal const string Driver = "ft600";
        internal const int DefaultIndex = -1;

        internal AcquisitionBoardContextTask(int index, Onix1.ValidationLevel validationLevel = Onix1.ValidationLevel.Normal)
            : base(Driver, index, validationLevel)
        {
            var (major, _, _) = GetSemverComponents(GetHub(0).FirmwareVersion);
            if (major != 2)
            {
                throw new NotSupportedException("This library requires version 2.x.x of the Acquisition Board Gateware. "
                    + "Please perform a gateware update to use this library. Instructions can be found at "
                    + "https://open-ephys.github.io/acq-board-docs/User-Manual/Gateware-Update.html");
            }
        }

        static (byte major, byte minor, byte patch) GetSemverComponents(uint version)
        {
            return (major: (byte)((version >> 16) & 0xFF), minor: (byte)((version >> 8) & 0xFF), patch: (byte)(version & 0xFF));
        }

    }
}
