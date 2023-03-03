using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KRPC.Client;
using KRPC.Client.Services.KRPC;
using KRPC.Client.Services.SpaceCenter;

namespace RendezvousTest
{
    internal class OldProgram
    {
        private static bool bottomStageSeparated = false;

        public static void Old(string[] args)
        {
            using (var connection = new Connection(
                       name: "DankConnection",
                       address: IPAddress.Parse("127.0.0.1"),
                       rpcPort: 50000,
                       streamPort: 50001
                   ))
            {
                var krpc = connection.KRPC();

                var currentVessel = connection.SpaceCenter().ActiveVessel;

                float turnStartAltitude = 250;
                float turnEndAltitude = 50000;
                float targetAltitude = 80000;

                var timeStream = connection.AddStream(() => connection.SpaceCenter().UT);
                var flight = currentVessel.Flight();
                var altitudeStream = connection.AddStream(() => flight.MeanAltitude);
                var apoapsisStream = connection.AddStream(() => currentVessel.Orbit.ApoapsisAltitude);
                var bottomStageResources = currentVessel.ResourcesInDecoupleStage(3, false);
                var topStageResources = currentVessel.ResourcesInDecoupleStage(1, false);

                var bottomStageFuel = connection.AddStream(() => bottomStageResources.Amount("LiquidFuel"));
                var topStageFuel = connection.AddStream(() => topStageResources.Amount("LiquidFuel"));

                currentVessel.Control.SAS = false;
                currentVessel.Control.RCS = false;
                currentVessel.Control.Throttle = 1;

                Console.WriteLine("3...");
                Thread.Sleep(1000);
                Console.WriteLine("2...");
                Thread.Sleep(1000);
                Console.WriteLine("1...");
                Thread.Sleep(1000);
                Console.WriteLine("Blastoff!");

                currentVessel.Control.ActivateNextStage();
                currentVessel.AutoPilot.Engage();
                currentVessel.AutoPilot.TargetPitchAndHeading(90, 90);

                var turnAngle = 0.0;

                while (true)
                {
                    var altitude = altitudeStream.Get();
                    if (altitude > turnStartAltitude && altitude < turnEndAltitude)
                    {
                        var frac = (altitude - turnStartAltitude) / (turnEndAltitude - turnStartAltitude);
                        var newTurnAngle = frac * 90.0;

                        if (Math.Abs(newTurnAngle - turnAngle) > 0.5)
                        {
                            turnAngle = newTurnAngle;
                            currentVessel.AutoPilot.TargetPitchAndHeading((float) (90 - turnAngle), 90);
                        }
                    }

                    if (!bottomStageSeparated && currentVessel.DoNextStageIfNeeded(bottomStageFuel))
                    {
                        Console.WriteLine("Stage separation confirmed.");
                        bottomStageSeparated = true;
                    }

                    if (apoapsisStream.Get() > targetAltitude * 0.95)
                    {
                        Console.WriteLine("Approaching target apoapsis");
                        break;
                    }
                }

                currentVessel.SetThrottle(0.25f);

                while (apoapsisStream.Get() < targetAltitude)
                {
                    if (!bottomStageSeparated && currentVessel.DoNextStageIfNeeded(bottomStageFuel))
                    {
                        Console.WriteLine("Stage separation confirmed.");
                        bottomStageSeparated = true;
                    }

                    Thread.Sleep(50);
                }

                Console.WriteLine("Target apoapsis reached.");
                currentVessel.SetThrottle(0);

                // Plan circularization
                Console.WriteLine("Planning circularization burn");
                double mu = currentVessel.Orbit.Body.GravitationalParameter;
                var r = currentVessel.Orbit.Apoapsis;
                var a1 = currentVessel.Orbit.SemiMajorAxis;
                var a2 = r;
                var v1 = Math.Sqrt(mu * ((2.0 / r) - (1.0 / a1)));
                var v2 = Math.Sqrt(mu * ((2.0 / r) - (1.0 / a2)));
                var deltaV = v2 - v1;
                
                // Create node
                var node = currentVessel.Control.AddNode(
                    timeStream.Get() + currentVessel.Orbit.TimeToApoapsis, prograde: (float) deltaV);

                // Calculate burn time (using rocket equation)
                double F = currentVessel.AvailableThrust;
                var isp = currentVessel.SpecificImpulse * 9.82;
                double m0 = currentVessel.Mass;
                var m1 = m0 / Math.Exp(deltaV / isp);
                var flowRate = F / isp;
                var burnTime = (m0 - m1) / flowRate;

                // Orient ship
                Console.WriteLine("Orienting ship for circularization burn");
                currentVessel.AutoPilot.ReferenceFrame = node.ReferenceFrame;
                currentVessel.AutoPilot.TargetDirection = Tuple.Create(0.0, 1.0, 0.0);
                currentVessel.AutoPilot.AutoTune = true;
                currentVessel.AutoPilot.Wait();
                
                while (altitudeStream.Get () < 70500) 
                {
                    // wait some more
                }

                // Wait until burn
                Console.WriteLine("Waiting until circularization burn");
                var burnUT = timeStream.Get() + currentVessel.Orbit.TimeToApoapsis - (burnTime / 2.0);
                var leadTime = 5.0;
                
                Console.WriteLine($"BurnUT is {burnUT}");
                connection.SpaceCenter().WarpTo(burnUT - leadTime);
                
                Console.WriteLine("All systems go for next maneuver");
                var timeToApoapsis = connection.AddStream(() => currentVessel.Orbit.TimeToApoapsis);
                while (timeToApoapsis.Get() - burnTime / 2.0 > 0)
                {
                    // wait some more
                }
                
                
                Console.WriteLine("Executing maneuver");
                currentVessel.SetThrottle(1);

                var periapsis = connection.AddStream(() => currentVessel.Orbit.PeriapsisAltitude);
                while (periapsis.Get() < targetAltitude)
                {
                    if (!bottomStageSeparated && currentVessel.DoNextStageIfNeeded(bottomStageFuel))
                    {
                        Console.WriteLine("Stage separation confirmed.");
                        bottomStageSeparated = true;
                    }

                    Thread.Sleep(50);
                }

                while (periapsis.Get() < targetAltitude)
                {
                    Console.WriteLine("Fine tuning...");
                    currentVessel.SetThrottle(0.25f);
                }

                currentVessel.SetThrottle(0);
                node.Remove();

                Console.WriteLine("Launch complete.");
            }
        }
    }
}
