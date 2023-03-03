using System;
using System.Net;
using System.Threading.Tasks;
using KRPC.Client;
using KRPC.Client.Services.KRPC;
using KRPC.Client.Services.SpaceCenter;
using Service = KRPC.Client.Services.MechJeb.Service;

namespace RendezvousTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            using (var connection = new Connection(
                       name: "DankConnection",
                       address: IPAddress.Parse("127.0.0.1"),
                       rpcPort: 50000,
                       streamPort: 50001
                   ))
            {
                var krpc = connection.KRPC();
                var jeb = new Service(connection);
                var maneuvers = new Maneuvers(jeb, connection);
                
                PrintMenu();
                var input = Console.ReadLine();
                while (!input?.Equals("q", StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    switch (input)
                    {
                        case "1":
                            await HandleOrbitalLaunch(maneuvers);
                            break;
                        case "2":
                            await connection.SpaceCenter().WaitForTargetSelection();
                            await maneuvers.HohmannTransfer();
                            break;
                        case "3":
                            await connection.SpaceCenter().WaitForTargetSelection();
                            await maneuvers.MatchVelocityWithTargetAndWaitForExecution();
                            break;
                        case "4":
                            await connection.SpaceCenter().WaitForTargetSelection();
                            await maneuvers.FineTuneApproach();
                            break;
                        case "5":
                            await maneuvers.LandUntargeted();
                            break;
                        case "6":
                            await DoFullLaunchAndIntercept(maneuvers, connection);
                            break;
                    }
                    
                    Console.WriteLine("Thank you for using DankJeb. Press any key to continue.");
                    Console.ReadLine();
                    PrintMenu();
                    input = Console.ReadLine();
                }
            }
        }

        private static async Task HandleOrbitalLaunch(Maneuvers maneuvers)
        {
            Console.Write("Please enter desired orbit altitude in km (default: 80,000) > ");
            var altitudeStr = Console.ReadLine();
            double desiredAltitude = 80_000;
            if (!string.IsNullOrWhiteSpace(altitudeStr))
            {
                var parsed = double.TryParse(altitudeStr, out var altitude);

                if (!parsed)
                {
                    Console.WriteLine("Could not parse value, using default 80,000 km");
                }
                else
                {
                    desiredAltitude = altitude;
                }
            }

            Console.Write("Please enter desired inclination in degrees (default: 0) > ");
            var inclinationStr = Console.ReadLine();
            double desiredInclination = 0;
            if (!string.IsNullOrWhiteSpace(inclinationStr))
            {
                var parsed = double.TryParse(inclinationStr, out var inclination);

                if (!parsed)
                {
                    Console.WriteLine("Could not parse value, using default 80,000 km");
                }
                else
                {
                    desiredInclination = inclination;
                }
            }

            await maneuvers.LaunchToSpecificOrbit(desiredAltitude, desiredInclination);
        }

        private async Task DoFullLaunchAndIntercept(Maneuvers maneuvers, Connection connection)
        {
            await maneuvers.LaunchToSpecificOrbit(80_000, 0);
            Console.WriteLine("Launch complete, thanks Jeb");
                
            // chill for a sec, then wait for a target
            await Task.Delay(5000);
            await connection.SpaceCenter().WaitForTargetSelection();

            // ok time to hohmann transfer
            await maneuvers.HohmannTransfer();

            // match velocity
            Console.WriteLine("Matching velocity with target...");
            await maneuvers.MatchVelocityWithTargetAndWaitForExecution();
                
            // FIne-tune approach
            Console.WriteLine("Fine-tuning approach to target...");
            await maneuvers.FineTuneApproach();
                
            // one last velocity match
            await maneuvers.MatchVelocityWithTargetAndWaitForExecution();
                
            Console.WriteLine("Rendezvous complete!");

            Console.WriteLine("Enter \"pls go\" in order to initiate landing maneuvers");
            while (Console.ReadLine() != "pls go")
            {
                await Task.Delay(100);
            }

            Console.WriteLine("k landing somewhere");
            await maneuvers.LandUntargeted();

            Console.WriteLine("Ok done");
        }

        private void PrintMenu()
        {
            Console.Clear();
            Console.WriteLine(@"
______            _      ___      _       _____       __  
|  _  \          | |    |_  |    | |     |  _  |     /  | 
| | | |__ _ _ __ | | __   | | ___| |__   | |/' |     `| | 
| | | / _` | '_ \| |/ /   | |/ _ \ '_ \  |  /| |      | | 
| |/ / (_| | | | |   </\__/ /  __/ |_) | \ |_/ /  _  _| |_
|___/ \__,_|_| |_|_|\_\____/ \___|_.__/   \___/  (_) \___/
");
            Console.WriteLine("==========================================================");
            Console.WriteLine("Please select an option:");
            Console.WriteLine("1. Launch to specific orbit & inclination");
            Console.WriteLine("2: Hohmann Transfer to target");
            Console.WriteLine("3: Match velocity with target");
            Console.WriteLine("4: Fine-tune approach to target");
            Console.WriteLine("5: Land somewhere");
            Console.WriteLine("6: Full launch, transfer, approach target, and landing");
            Console.WriteLine("Q: Quit");
            Console.WriteLine();
            Console.Write("> ");
        }
    }
}
