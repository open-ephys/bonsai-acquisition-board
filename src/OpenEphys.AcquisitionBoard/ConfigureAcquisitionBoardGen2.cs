using System.ComponentModel;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Configures an Open Ephys Acquisition Board (Gen2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Open Ephys Acquisition Board (Gen2) is a system for acquiring electrophysiology data from headstages
    /// based on Intan RHD2000-series amplifier chips. It provides the following features:
    /// <list type="bullet">
    /// <item><description>4x headstage ports (A to D), each of which operates in dual MISO mode, supporting
    /// two amplifier chips. This model has no I2C support on its headstage ports.</description></item>
    /// <item><description>Analog and digital inputs sampled synchronously with the amplifier
    /// data.</description></item>
    /// <item><description>Analog and digital (TTL) outputs.</description></item>
    /// <item><description>A Harp clock synchronization input.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This operator configures all devices on the acquisition board as a group and should be
    /// placed directly after a <see cref="CreateContext"/> operator. Data from each device can then be
    /// acquired using the corresponding data IO operator, such as <see cref="HeadstageData"/> or <see
    /// cref="ExternalData"/>, linked to the device configuration through a shared <c>DeviceName</c>.
    /// </para>
    /// </remarks>
    [Description("Configures an Open Ephys Acquisition Board (Gen2).")]
    public class ConfigureAcquisitionBoardGen2 : AcquisitionBoardFactory
    {
        // NB: the I2C flags of this port controller are never exposed and stay false, so it writes 0 to
        // ENABLE_I2C, which the Gen2 firmware ignores
        readonly ConfigurePortController portControl = new(PortController.GEN2_HUB_ID);

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureAcquisitionBoardGen2"/> class.
        /// </summary>
        public ConfigureAcquisitionBoardGen2()
        {
        }

        internal override ConfigurePortController PortControl => portControl;
    }
}
