using System;
using System.Threading.Tasks;
using KRPC.Client;
using KRPC.Client.Services.MechJeb;
using KRPC.Client.Services.SpaceCenter;
using Service = KRPC.Client.Services.MechJeb.Service;

namespace RendezvousTest
{
    public class Maneuvers
    {
        private readonly Service mechJeb;
        private readonly Connection connection;

        public Maneuvers(Service mechJeb, Connection connection)
        {
            this.mechJeb = mechJeb;
            this.connection = connection;
        }
        
        public async Task LaunchToSpecificOrbit(double orbitAltitude = 80_000, double inclination = 0)
        {
            var ascent = mechJeb.AscentAutopilot;

            ascent.DesiredOrbitAltitude = orbitAltitude;
            ascent.DesiredInclination = inclination;
            ascent.ForceRoll = true;
            ascent.VerticalRoll = 90;
            ascent.TurnRoll = 90;

            ascent.Autostage = true;
            ascent.Enabled = true;

            var vessel = connection.SpaceCenter().ActiveVessel;
            Console.WriteLine("3...");
            await Task.Delay(1000);
            Console.WriteLine("2...");
            await Task.Delay(1000);
            Console.WriteLine("1...");
            await Task.Delay(1000);
            Console.WriteLine("Blastoff!");

            vessel.Control.ActivateNextStage();

            var enabledStream = connection.AddStream(() => ascent.Enabled);
            var apoapsisStream =
                connection.AddStream(() => connection.SpaceCenter().ActiveVessel.Orbit.ApoapsisAltitude);
            var periapsisStream =
                connection.AddStream(() => connection.SpaceCenter().ActiveVessel.Orbit.PeriapsisAltitude);
            while (enabledStream.Get())
            {
                Console.Write($"Current apoapsis: {(int)apoapsisStream.Get()} - Current periapsis: {(int)periapsisStream.Get()}\r");
                await Task.Delay(1000);
            }
        }

        public async Task HohmannTransfer()
        {
            Console.WriteLine("Planning Hohmann Transfer...");
            var planner = mechJeb.ManeuverPlanner;
            planner.OperationTransfer.MakeNodes();
            if (!string.IsNullOrWhiteSpace(planner.OperationTransfer.ErrorMessage))
            {
                Console.WriteLine($"Jeb borked: {planner.OperationTransfer.ErrorMessage}");
                Console.WriteLine("Try again?");
                var input = Console.ReadLine();
                if (input != null && (input.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                      input.Equals("y", StringComparison.OrdinalIgnoreCase)))
                {
                    await HohmannTransfer();
                    return;
                }
            }
                
            Console.WriteLine("Executing Transfer...");
            mechJeb.NodeExecutor.ExecuteAllNodes();
            await mechJeb.NodeExecutor.WaitForExecution(connection);
        }

        public async Task MatchVelocityWithTargetAndWaitForExecution(double tolerance = 0.1)
        {
            mechJeb.ManeuverPlanner.OperationKillRelVel.TimeSelector.TimeReference = TimeReference.ClosestApproach;
            mechJeb.ManeuverPlanner.OperationKillRelVel.MakeNodes();
            mechJeb.NodeExecutor.Tolerance = tolerance;
            mechJeb.NodeExecutor.ExecuteAllNodes();
            await mechJeb.NodeExecutor.WaitForExecution(connection);
        }

        public async Task LandUntargeted()
        {
            mechJeb.LandingAutopilot.DeployChutes = true;
            mechJeb.LandingAutopilot.DeployGears = true;
            mechJeb.LandingAutopilot.RcsAdjustment = true;
            mechJeb.LandingAutopilot.TouchdownSpeed = 10.0;
            mechJeb.LandingAutopilot.LandUntargeted();

            var landingEnabled = connection.AddStream(() => mechJeb.LandingAutopilot.Enabled);
            while (landingEnabled.Get())
            {
                await Task.Delay(1000);
            }
            landingEnabled.Remove();
        }

        public async Task FineTuneApproach(double interceptDistance = 50.0, double tolerance = 0.01)
        {
            mechJeb.ManeuverPlanner.OperationCourseCorrection.InterceptDistance = interceptDistance;
            mechJeb.ManeuverPlanner.OperationCourseCorrection.MakeNodes();
            mechJeb.NodeExecutor.Tolerance = tolerance;
            mechJeb.NodeExecutor.ExecuteAllNodes();
            await mechJeb.NodeExecutor.WaitForExecution(connection);
        }
    }
}
