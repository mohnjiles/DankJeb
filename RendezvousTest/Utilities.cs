using System;
using System.Threading.Tasks;
using KRPC.Client;
using KRPC.Client.Services.MechJeb;

namespace RendezvousTest
{
    public static class Utilities
    {
        public static async Task WaitForTargetSelection(this KRPC.Client.Services.SpaceCenter.Service spaceCenter)
        {
            while (spaceCenter.TargetVessel == null)
            {
                Console.WriteLine("Please select a target");
                await Task.Delay(1000);
            }
        }
        
        public static async Task WaitForExecution(this NodeExecutor nodeExecutor, Connection connection)
        {
            var executorEnabledStream = connection.AddStream(() => nodeExecutor.Enabled);
            while (executorEnabledStream.Get())
            {
                await Task.Delay(1000);
            }

            executorEnabledStream.Remove();
        }
    }
}
