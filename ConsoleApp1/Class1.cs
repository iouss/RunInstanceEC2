using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace EC2TerminateInstance
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if ((args.Length == 1) && (args[0].StartsWith("i-")))
            {
                // Terminate the instance
                var ec2Client = new AmazonEC2Client();
                await TerminateInstance(ec2Client, args[0]);
            }
            else
            {
                Console.WriteLine("\nCommand-line argument missing or incorrect.");
                Console.WriteLine("\nUsage: EC2TerminateInstance instance-ID");
                Console.WriteLine("  instance-ID - The EC2 instance you want to terminate.");
                return;
            }
        }

        //
        // Method to terminate an EC2 instance
        private static async Task TerminateInstance(IAmazonEC2 ec2Client, string instanceID)
        {
            var request = new TerminateInstancesRequest
            {
                InstanceIds = new List<string>() { instanceID }
            };
            TerminateInstancesResponse response =
              await ec2Client.TerminateInstancesAsync(new TerminateInstancesRequest
              {
                  InstanceIds = new List<string>() { instanceID }
              });
            foreach (InstanceStateChange item in response.TerminatingInstances)
            {
                Console.WriteLine("Terminated instance: " + item.InstanceId);
                Console.WriteLine("Instance state: " + item.CurrentState.Name);
            }
        }
    }
}
