using System;
using System.ServiceModel;
using System.Threading;

namespace Harbour.Legacy.Service
{
    internal static class Program
    {
        private static void Main()
        {
            using (var host = new ServiceHost(typeof(ConsignmentService)))
            {
                host.Open();
                foreach (var address in host.BaseAddresses)
                {
                    Console.WriteLine("Legacy WCF service listening on " + address);
                }

                var stop = new ManualResetEvent(false);
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; stop.Set(); };
                stop.WaitOne();
            }
        }
    }
}
