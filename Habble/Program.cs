using System;
using System.Threading.Tasks;

namespace Habble
{
    public class Program
    {
        public static void Main(string[] args) =>
            new Program().RunAsync().GetAwaiter().GetResult();

        public async Task RunAsync()
        {

        }
    }
}