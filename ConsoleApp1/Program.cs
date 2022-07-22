using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace EC2LaunchInstance
{
    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // Class to launch an EC2 instance
    class Program
    {
        static async Task Main(string[] args)
        {
            // Parse the command line and show help if necessary
             var parsedArgs = CommandLine.Parse(args);
             if (parsedArgs.Count == 0)
             {
                 PrintHelp();
                 return;
             }

             // Get the application arguments from the parsed list
             string groupID =
               CommandLine.GetArgument(parsedArgs, null, "-g", "--group-id");
             string ami =
               CommandLine.GetArgument(parsedArgs, null, "-a", "--ami-id");
             string keyPairName =
               CommandLine.GetArgument(parsedArgs, null, "-k", "--keypair-name");
             string subnetID =
               CommandLine.GetArgument(parsedArgs, null, "-s", "--subnet-id");
             if ((string.IsNullOrEmpty(groupID) || !groupID.StartsWith("sg-"))
                || (string.IsNullOrEmpty(ami) || !ami.StartsWith("ami-"))
                || (string.IsNullOrEmpty(keyPairName))
                || (!string.IsNullOrEmpty(subnetID) && !subnetID.StartsWith("subnet-")))
                 CommandLine.ErrorExit(
                   "\nOne or more of the required arguments is missing or incorrect." +
                   "\nRun the command with no arguments to see help.");
         
            // Create an EC2 client
            var ec2Client = new AmazonEC2Client();

            // Create an object with the necessary properties
            RunInstancesRequest request = GetRequestData(groupID, ami, keyPairName, subnetID);
          

            // Launch the instances and wait for them to start running
            var instanceIds = await LaunchInstances(ec2Client, request);
            await CheckState(ec2Client, instanceIds);
        }
        /*
          Console.WriteLine("Processssssing ");
          string ami = "ami-02d1e544b84bf7502";
          string groupID = "sg-017bd7a009671e614";
          string subnetID = "subnet-09ef9c1efa33d7ddd";
          string keyPairName = "Ohio_AWS_EC2";*/

        //
        // Method to put together the properties needed to launch the instance.
        private static RunInstancesRequest GetRequestData(
          string groupID, string ami, string keyPairName, string subnetID)
        {
            // Common properties
            var groupIDs = new List<string>() { groupID };
            var request = new RunInstancesRequest()
            {
                // The first three of these would be additional command-line arguments or similar.
                InstanceType = InstanceType.T2Micro,
                MinCount = 1,
                MaxCount = 1,
                ImageId = ami,
                KeyName = keyPairName,
                
            };

            // Properties specifically for EC2 in a VPC.
            if (!string.IsNullOrEmpty(subnetID))
            {
                request.NetworkInterfaces =
                  new List<InstanceNetworkInterfaceSpecification>() {
            new InstanceNetworkInterfaceSpecification() {
              DeviceIndex = 0,
              SubnetId = subnetID,
              Groups = groupIDs,
              AssociatePublicIpAddress = true
            }
                  };
            }

            // Properties specifically for EC2-Classic
            else
            {
                request.SecurityGroupIds = groupIDs;
            }
            return request;
        }


        //
        // Method to launch the instances
        // Returns a list with the launched instance IDs
        private static async Task<List<string>> LaunchInstances(
          IAmazonEC2 ec2Client, RunInstancesRequest requestLaunch)
        {
            var instanceIds = new List<string>();
            RunInstancesResponse responseLaunch =
              await ec2Client.RunInstancesAsync(requestLaunch);

            Console.WriteLine("\nNew instances have been created.");
            foreach (Instance item in responseLaunch.Reservation.Instances)
            {
                instanceIds.Add(item.InstanceId);
                Console.WriteLine($"  New instance: {item.InstanceId}");
            }

            return instanceIds;
        }


        //
        // Method to wait until the instances are running (or at least not pending)
        private static async Task CheckState(IAmazonEC2 ec2Client, List<string> instanceIds)
        {
            Console.WriteLine(
              "\nWaiting for the instances to start." +
              "\nPress any key to stop waiting. (Response might be slightly delayed.)");

            int numberRunning;
            DescribeInstancesResponse responseDescribe;
            var requestDescribe = new DescribeInstancesRequest
            {
                InstanceIds = instanceIds
            };

            // Check every couple of seconds
            int wait = 2000;
            while (true)
            {
                // Get and check the status for each of the instances to see if it's past pending.
                // Once all instances are past pending, break out.
                // (For this example, we are assuming that there is only one reservation.)
                Console.Write(".");
                numberRunning = 0;
                responseDescribe = await ec2Client.DescribeInstancesAsync(requestDescribe);
                foreach (Instance i in responseDescribe.Reservations[0].Instances)
                {
                    // Check the lower byte of State.Code property
                    // Code == 0 is the pending state
                    if ((i.State.Code & 255) > 0) numberRunning++;
                }
                if (numberRunning == responseDescribe.Reservations[0].Instances.Count)
                    break;

                // Wait a bit and try again (unless the user wants to stop waiting)
                Thread.Sleep(wait);
                if (Console.KeyAvailable)
                    break;
            }

            Console.WriteLine("\nNo more instances are pending.");
            foreach (Instance i in responseDescribe.Reservations[0].Instances)
            {
                Console.WriteLine($"For {i.InstanceId}:");
                Console.WriteLine($"  VPC ID: {i.VpcId}");
                Console.WriteLine($"  Instance state: {i.State.Name}");
                Console.WriteLine($"  Public IP address: {i.PublicIpAddress}");
                Console.WriteLine($"  Public DNS name: {i.PublicDnsName}");
                Console.WriteLine($"  Key pair name: {i.KeyName}");
            }
        }


        //
        // Command-line help
        private static void PrintHelp()
        {
            Console.WriteLine(
              "\nUsage: EC2LaunchInstance -g <group-id> -a <ami-id> -k <keypair-name> [-s <subnet-id>]" +
              "\n  -g, --group-id: The ID of the security group." +
              "\n  -a, --ami-id: The ID of an Amazon Machine Image." +
              "\n  -k, --keypair-name - The name of a key pair." +
              "\n  -s, --subnet-id: The ID of a subnet. Required only for EC2 in a VPC.");
        }
    }


    // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
    // Class that represents a command line on the console or terminal.
    // (This is the same for all examples. When you have seen it once, you can ignore it.)
    static class CommandLine
    {
        //
        // Method to parse a command line of the form: "--key value" or "-k value".
        //
        // Parameters:
        // - args: The command-line arguments passed into the application by the system.
        //
        // Returns:
        // A Dictionary with string Keys and Values.
        //
        // If a key is found without a matching value, Dictionary.Value is set to the key
        //  (including the dashes).
        // If a value is found without a matching key, Dictionary.Key is set to "--NoKeyN",
        //  where "N" represents sequential numbers.
        public static Dictionary<string, string> Parse(string[] args)
        {
            var parsedArgs = new Dictionary<string, string>();
            int i = 0, n = 0;
            while (i < args.Length)
            {
                // If the first argument in this iteration starts with a dash it's an option.
                if (args[i].StartsWith("-"))
                {
                    var key = args[i++];
                    var value = key;

                    // Check to see if there's a value that goes with this option?
                    if ((i < args.Length) && (!args[i].StartsWith("-"))) value = args[i++];
                    parsedArgs.Add(key, value);
                }

                // If the first argument in this iteration doesn't start with a dash, it's a value
                else
                {
                    parsedArgs.Add("--NoKey" + n.ToString(), args[i++]);
                    n++;
                }
            }

            return parsedArgs;
        }

        //
        // Method to get an argument from the parsed command-line arguments
        //
        // Parameters:
        // - parsedArgs: The Dictionary object returned from the Parse() method (shown above).
        // - defaultValue: The default string to return if the specified key isn't in parsedArgs.
        // - keys: An array of keys to look for in parsedArgs.
        public static string GetArgument(
          Dictionary<string, string> parsedArgs, string defaultReturn, params string[] keys)
        {
            string retval = null;
            foreach (var key in keys)
                if (parsedArgs.TryGetValue(key, out retval)) break;
            return retval ?? defaultReturn;
        }

        //
        // Method to exit the application with an error.
        public static void ErrorExit(string msg, int code = 1)
        {
            Console.WriteLine("\nError");
            Console.WriteLine(msg);
            Environment.Exit(code);
        }
    }

}
