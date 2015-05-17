namespace Selection_Algorithm_Test
{
    public static class Hash
    {

        public static uint Calculate(uint a)
        {
            uint hash = HashSeed ^ a;
            uint b = a;
            uint x = 231232;
            uint y = 1232;
            Hashmix(ref b, ref x, ref hash);
            Hashmix(ref y, ref a, ref hash);
            return hash;
        }

        public static uint Calculate(uint a, uint b)
        {
            uint hash = HashSeed ^ a ^ b;
            uint x = 231232;
            uint y = 1232;
            Hashmix(ref a, ref b, ref hash);
            Hashmix(ref x, ref a, ref hash);
            Hashmix(ref b, ref y, ref hash);
            return hash;
        }

        public static uint Calculate(uint a, uint b, uint c)
        {
            uint hash = HashSeed ^ a ^ b ^ c;
            uint x = 231232;
            uint y = 1232;
            Hashmix(ref a, ref b, ref hash);
            Hashmix(ref c, ref x, ref hash);
            Hashmix(ref y, ref a, ref hash);
            Hashmix(ref b, ref x, ref hash);
            Hashmix(ref y, ref c, ref hash);
            return hash;
        }

        public static uint Calculate(uint a, uint b, uint c, uint d)
        {
            uint hash = HashSeed ^ a ^ b ^ c ^ d;
            uint x = 231232;
            uint y = 1232;
            Hashmix(ref a, ref b, ref hash);
            Hashmix(ref c, ref d, ref hash);
            Hashmix(ref a, ref x, ref hash);
            Hashmix(ref y, ref b, ref hash);
            Hashmix(ref c, ref x, ref hash);
            Hashmix(ref y, ref d, ref hash);
            return hash;
        }

        public static uint Calculate(uint a, uint b, uint c, uint d, uint e)
        {
            uint hash = HashSeed ^ a ^ b ^ c ^ d ^ e;
            uint x = 231232;
            uint y = 1232;
            Hashmix(ref a, ref b, ref hash);
            Hashmix(ref c, ref d, ref hash);
            Hashmix(ref e, ref x, ref hash);
            Hashmix(ref y, ref a, ref hash);
            Hashmix(ref b, ref x, ref hash);
            Hashmix(ref y, ref c, ref hash);
            Hashmix(ref d, ref x, ref hash);
            Hashmix(ref y, ref e, ref hash);
            return hash;
        }

        private const uint HashSeed = 1315423911;

        private static void Hashmix(ref uint a, ref uint b, ref uint c) 
        {
		    a = a-b;  a = a-c;  a = a^(c>>13);
		    b = b-c;  b = b-a;  b = b^(a<<8);
		    c = c-a;  c = c-b;  c = c^(b>>13);
		    a = a-b;  a = a-c;  a = a^(c>>12);
		    b = b-c;  b = b-a;  b = b^(a<<16);
		    c = c-a;  c = c-b;  c = c^(b>>5);
		    a = a-b;  a = a-c;  a = a^(c>>3);
		    b = b-c;  b = b-a;  b = b^(a<<10);
		    c = c-a;  c = c-b;  c = c^(b>>15);
	    }
    }
}
