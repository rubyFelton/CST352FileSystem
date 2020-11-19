// VolatileDisk.cs
// Pete Myers
// Spring 2018

// NOTE: Simulates a disk in memory
// NOTE: Do not modify this implementation

using System;


namespace SimpleFileSystem
{
    public class VolatileDisk : DiskDriver
    {
        private class DiskSector
        {
            // LBA addressing
            public int lba;
            
            // data in sector
            public byte[] data;

            public DiskSector(int lba, int length)
            {
                this.lba = lba;
                data = new byte[length];
                for (int i = 0; i < length; i++)
                    data[i] = 0xff;
            }
        };

        private const int BYTES_PER_SECTOR = 256;
        private const int SECTOR_COUNT = 1024;

        private bool powerOn;
        private int serialNumber;
        
        private DiskSector[] sectors;

        public VolatileDisk(int serialNumber)
        {
            this.serialNumber = serialNumber;
            powerOn = false;
            
            // initialize sectors
            sectors = new DiskSector[SECTOR_COUNT];
            for (int i = 0; i < SECTOR_COUNT; i++)
            {
                sectors[i] = new DiskSector(i, BYTES_PER_SECTOR);
            }
        }

        // disk properties
        public int SerialNumber { get { return serialNumber; } }
        public int BytesPerSector { get { return BYTES_PER_SECTOR; } }
        public int SectorCount { get { return SECTOR_COUNT; } }

        // current state of disk
        public bool Ready { get { return powerOn; } }
        public void TurnOn() { powerOn = true; }
        public void TurnOff() { powerOn = false; }

        // read / write
        public byte[] ReadSector(int lba)
        {
            CheckReady();
            CheckLBA(lba);

            return sectors[lba].data;
        }

        public void WriteSector(int lba, byte[] data)
        {
            CheckReady();
            CheckLBA(lba);
            CheckSectorLength(data.Length);

            sectors[lba].data = data;
        }

        // helpers
        private void CheckReady()
        {
            if (!powerOn)
                throw new Exception("Disk currently powered off");
        }

        private void CheckLBA(int lba)
        {
            if (lba < 0 || lba >= SECTOR_COUNT)
                throw new Exception("LBA " + lba.ToString() + " invalid, expected less than " + SECTOR_COUNT.ToString());
        }

        private void CheckSectorLength(int len)
        {
            if (len != BYTES_PER_SECTOR)
                throw new Exception("Invalid sector size " + len.ToString() + ", expected " + BYTES_PER_SECTOR.ToString());
        }
    }
}
