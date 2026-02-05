using System;
using System.Globalization;
using System.Text;
namespace TUA.Misc
{
    [Serializable]
    public struct Uuid : IComparable, IEquatable<Uuid>
    {
        private static Random _random = new();
        public int high;
        public int low;
        
        public static Uuid Empty => new()
        {
            high = 0,
            low = 0
        };
        
        public static Uuid New => new()
        {
            low = _random.Next(-2147483648, 2147483647),
            high = _random.Next(-2147483648, 2147483647)
        };
        
        public bool IsValid => low != 0 || high != 0;
        public Uuid(string input)
        {
            high = int.Parse(input.Substring(0, 8), NumberStyles.HexNumber);
            low = int.Parse(input.Substring(8, 8), NumberStyles.HexNumber);
        }
        
        public Uuid(int high, int low)
        {
            this.high = high;
            this.low = low;
        }
        
        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    break;
                case Uuid uuid:
                    var cmp = high.CompareTo(uuid.high);
                    return cmp == 0 ? low.CompareTo(uuid.low) : cmp;
            }
            return -1;
        }
        
        public override bool Equals(object obj)
        {
            return obj switch
            {
                null => false,
                Uuid uuid => uuid.high == high && uuid.low == low,
                _ => false
            };
        }
        
        public override int GetHashCode()
        {
            return low ^ high;
        }
        
        public override string ToString()
        {
            var str = new StringBuilder();
            str.Append(high.ToString("x8"));
            str.Append(low.ToString("x8"));
            return str.ToString();
        }
        
        public static bool TryParse(string input, out Uuid result)
        {
            try
            {
                result = new Uuid
                {
                    high = int.Parse(input.Substring(0, 8), NumberStyles.HexNumber),
                    low = int.Parse(input.Substring(8, 8), NumberStyles.HexNumber)
                };
                return true;
            }
            catch
            {
                result = new Uuid
                {
                    high = 0,
                    low = 0
                };
                return false;
            }
        }
        
        public static bool operator ==(Uuid a, Uuid b)
        {
           return a.high == b.high && a.low == b.low;
        }
        
        public static bool operator !=(Uuid a, Uuid b)
        {
            return a.high != b.high || a.low != b.low;
        }
        
        public bool Equals(Uuid other)
        {
            return high == other.high && low == other.low;
        }
    }
}
