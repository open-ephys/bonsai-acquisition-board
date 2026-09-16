using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using oni;
using OpenEphys.Onix1;

namespace OpenEphys.AcquisitionBoard
{
    public class AcquisitionBoardContextTask : Onix1.ContextTask
    {
        const string Driver = "ft600";
        internal AcquisitionBoardContextTask(int index) : base(Driver, index) { }

        static (byte major, byte minor, byte patch) GetSemverComponents(uint version)
        {
            return (major: (byte)((version >> 16) & 0xFF), minor: (byte)((version >> 8) & 0xFF), patch: (byte)(version & 0xFF));
        }

        /// <inheritdoc/>
        protected override void ContextCreationChecks()
        {
            var (major, _, _) = GetSemverComponents(GetHub(0).FirmwareVersion);
            if (major!= 2)
            {
                throw new NotSupportedException("This library requires version 2.x.x of the Acquisition Board Gateware. "
                    + "Please perform a gateware update to use this library. Instructions can be found at "
                    + "https://open-ephys.github.io/acq-board-docs/User-Manual/Gateware-Update.html");
            }
        }

        internal TemporaryAcquisitionScope StartTemporaryAcquisition()
        {
            AssertConfigurationContext(); 
            return new TemporaryAcquisitionScope(this);
        }

        internal TemporaryAcquisitionScope StartTemporaryAcquisition(int blockReadSize)
        {
            AssertConfigurationContext();
            return new TemporaryAcquisitionScope(this, blockReadSize);
        }

        internal sealed class TemporaryAcquisitionScope : IDisposable
        {
            readonly AcquisitionBoardContextTask context;
            readonly int? originalReadsize;
            bool disposed;
            internal TemporaryAcquisitionScope(AcquisitionBoardContextTask parent)
            {
                this.context = parent;
                context.ctx.Start(true); 
            }

            internal TemporaryAcquisitionScope(AcquisitionBoardContextTask parent, int blockReadSize)
            {
                this.context = parent;
                this.originalReadsize = context.ctx.BlockReadSize;
                context.ctx.BlockReadSize = blockReadSize;
                context.ctx.Start(true);
                
            }

            public void DiscardFrames(int count)
            {
                if (disposed) throw new ObjectDisposedException(nameof(TemporaryAcquisitionScope));

                for (int i = 0; i < count; i++)
                {
                    using (context.ReadFrame()) { }
                }
            }

            public void ProcessNextFrame(Action<oni.Frame> action)
            {
                if (disposed) throw new ObjectDisposedException(nameof(TemporaryAcquisitionScope));

                using (var frame = context.ReadFrame())
                {
                    action(frame);
                }
            }

            public void ProcessNextFrame(Action<oni.Frame> action, uint deviceAddress)
            {
                bool found = false;

                do
                {
                    ProcessNextFrame(frame =>
                    {
                        if (frame.DeviceAddress == deviceAddress)
                        {
                            action(frame);
                            found = true;
                        }
                    });
                } while (!found);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    context.ctx.Stop();
                    if (originalReadsize.HasValue)
                    {
                        context.ctx.BlockReadSize = originalReadsize.Value;
                    }
                    disposed = true;
                }
            }

        }


    }
}
