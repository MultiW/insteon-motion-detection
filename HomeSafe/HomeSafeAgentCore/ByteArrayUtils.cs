using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSafeAgentCore
{
    internal class ByteArrayUtils
    {
        public static int Find(byte[] array, byte[] needle, int startIndex, int count)
        {
            while (count >= needle.Length)
            {
                int index = Array.IndexOf(array, needle[0], startIndex, count);

                if (index == -1)
                {
                    return -1;
                }

                int i;
                for (i = 0; i < needle.Length; i++)
                {
                    if (array[index + i] != needle[i])
                    {
                        break;
                    }
                }

                if (i == needle.Length)
                {
                    // needle is found
                    return index;
                }

                count = count - (index - startIndex) - 1;
                startIndex = index + 1;
            }

            return -1;
        }
    }
}
