using System.Threading;
using KRPC.Client;
using KRPC.Client.Services.SpaceCenter;

namespace RendezvousTest
{
    public static class VesselExtensions
    {
        public static bool DoNextStageIfNeeded(this Vessel vessel, Stream<float> stageFuel)
        {
            if (stageFuel.Get() >= 0.1) return false;

            vessel.Control.ActivateNextStage();
            Thread.Sleep(50);
            vessel.Control.ActivateNextStage();
            return true;
        }

        public static void SetThrottle(this Vessel vessel, float value)
        {
            var actualValue = value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
            vessel.Control.Throttle = actualValue;
        }
    }
}
