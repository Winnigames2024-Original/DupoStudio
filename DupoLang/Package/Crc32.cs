namespace DupoLang
{
    /// <summary>CRC32 для проверки целостности записей в .dapp.</summary>
    public static class Crc32
    {
        static readonly uint[] Table = BuildTable();

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
