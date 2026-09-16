using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Xml.Serialization;
using Bonsai;
using Bonsai.IO;
using Bonsai.Reactive;
using OpenEphys.Onix1;
using Rhythm.Net;

namespace OpenEphys.AcquisitionBoard
{
    /// <summary>
    /// Represents an operator that produces a sequence of values.
    /// </summary>
    [Description("Produces a sequence of values.")]
    [Combinator(MethodName = nameof(Generate))]
    [WorkflowElementCategory(ElementCategory.Source)]
    public class CreateAcquisitionBoard : Onix1.IDeviceCollection
    {

        readonly ConfigureAcquisitionBoard acquisitionBoard = new();

        IEnumerable<IDeviceConfiguration> IDeviceCollection.GetDevices() => acquisitionBoard.GetDevices();

        
        // NB : We keep this values fixed at the same
        // value as the GUI
        const int WriteSize = 2 * 1024;
        const int ReadSize = 24 * 1024;

        /// <inheritdoc cref="ConfigureAcquisitionBoard"/>
        [Description("The unique name for this Acquisition Board instance")]
        [Category(DeviceFactory.ConfigurationCategory)]
        public string Name
        {
            get => acquisitionBoard.Name;
            set => acquisitionBoard.Name = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.BoardLeds"/>
        [Category(DeviceFactory.AcquisitionCategory)]
        [Description("Board LED status")]
        public bool BoardLeds
        {
            get => acquisitionBoard.BoardLeds;
            set => acquisitionBoard.BoardLeds = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.BoardIndex"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Range(-1, 100)]
        [Description("The index of the board to open (-1 to open the first available board).")]
        public int BoardIndex
        {
            get => acquisitionBoard.BoardIndex;
            set => acquisitionBoard.BoardIndex = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.SampleRate"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate
        {
            get => acquisitionBoard.SampleRate;
            set => acquisitionBoard.SampleRate = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.ExternalFastSettleEnabled"/>
        [Category(DeviceFactory.AcquisitionCategory)]
        [Description("Specifies whether the external fast settle channel (channel 0) is enabled.")]
        public bool ExternalFastSettleEnabled
        {
            get => acquisitionBoard.ExternalFastSettleEnabled;
            set => acquisitionBoard.ExternalFastSettleEnabled = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.LowerBandwidth"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The lower bandwidth of the amplifier on-board analog filter (Hz).")]
        public double LowerBandwidth
        {
            get => acquisitionBoard.LowerBandwidth;
            set => acquisitionBoard.LowerBandwidth = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.UpperBandwidth"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The upper bandwidth of the amplifier on-board analog filter (Hz).")]
        public double UpperBandwidth
        {
            get => acquisitionBoard.UpperBandwidth;
            set => acquisitionBoard.UpperBandwidth = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.DspCutoffFrequency"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("The cutoff frequency of the DSP offset removal filter (Hz).")]
        public double DspCutoffFrequency
        {
            get => acquisitionBoard.DspCutoffFrequency;
            set => acquisitionBoard.DspCutoffFrequency = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.GatewareVersion"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Externalizable(false)]
        [ReadOnly(true)]
        [XmlIgnore]
        public string GatewareVersion => acquisitionBoard.GatewareVersion;

        /// <inheritdoc cref="ConfigureRhythmDevice.DspEnabled"/>
        [Category(DeviceFactory.ConfigurationCategory)]
        [Description("Specifies whether the DSP offset removal filter is enabled.")]
        public bool DspEnabled
        {
            get => acquisitionBoard.DspEnabled;
            set => acquisitionBoard.DspEnabled = value;
        }

        /// <inheritdoc cref="ConfigureRhythmDevice.BufferSize"/>
        [Description("Number of samples that are collected before data is propagated.")]
        [Category(DeviceFactory.ConfigurationCategory)]
        public int BufferSize
        {
            get => acquisitionBoard.BufferSize;
            set => acquisitionBoard.BufferSize = value;
        }

        /// <summary>
        /// Starts data acquisition and frame distribution on a <see cref="AcquisitionBoardContextTask"/> and returns the
        /// sequence of all received <see cref="oni.Frame"/> objects, grouped by device address.
        /// </summary>
        /// <returns>
        /// A sequence of data frames 
        /// grouped by device address.
        /// </returns>
        public IObservable<IGroupedObservable<uint, oni.Frame>> Generate()
        {
            var context = Observable.Create<AcquisitionBoardContextTask>(observer =>
            {
                var index = BoardIndex;
                var context = new AcquisitionBoardContextTask(index);
                try
                {
                    observer.OnNext(context);
                    return context;
                }
                catch
                {
                    context.Dispose();
                    throw;
                }
            });
            return acquisitionBoard.Process(context).SelectMany(context =>
            {
                return Observable.Create<IGroupedObservable<uint, oni.Frame>>((observer, cancellationToken) =>
                {
                    var frameSubscription = context.GroupedFrames.SubscribeSafe(observer);
                    try
                    {
                        return context.StartAsync(ReadSize, WriteSize, cancellationToken)
                                      .ContinueWith(_ => frameSubscription.Dispose());
                    }
                    catch
                    {
                        frameSubscription.Dispose();
                        throw;
                    }
                });
            });
        }

    }
}
