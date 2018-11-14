using System.Runtime.CompilerServices;
using Microsoft.ML;

[assembly: InternalsVisibleTo("ProjectB")]
[assembly: WantsToBeBestFriends]

namespace Bubba
{
    /*internal*/public class A // Should fail.
    {
        public const int Hello = 2; // Fine by itself, but reference A.Hello will fail.
        /*internal*/public static int My { get; } = 2; // Should also fail on its own merits.
    }

    [BestFriend]
    /*internal*/public class B // Should succeed.
    {
        [BestFriend]
        /*internal*/public const string Friend = "Wave back when you wave hello."; // Should succeed.
        public const string Stay = "Don't hold their nose and point at you."; // Should succeed.
        /*internal*/public const string Awhile = "Help you find your hat."; // Should Fail.

        public B() { } // Should succeed.
    }

    public class C : IA
    {
        /*internal*/public const int And = 2; // Should Fail.
        [BestFriend]
        /*internal*/public const int Listen = 2;// Should succeed.

        [BestFriend]
        private protected C(int a) { } // Should succeed.
        /*internal*/public C(float a) { } // Should Fail.
    }

    public class D : IB
    {
        [BestFriend]
        /*internal*/public D(int a) { } // Should succeed.
        private protected D(float a) { } // Should Fail.
    }

    /*internal*/public interface IA { } // Should Fail.
    [BestFriend]
    /*internal*/public interface IB { } // Should succeed.
}
