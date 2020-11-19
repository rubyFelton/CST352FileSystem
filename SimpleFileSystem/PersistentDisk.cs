// PersistentDisk.cs
// Pete Myers
// Spring 2018

// NOTE: Simulates a disk, stored in a single file for "real" persistence (Yay!)
// NOTE: Do not modify this implementation

using System;
using System.IO;


namespace SimpleFileSystem
{
    public class PersistentDisk : DiskDriver
    {
        private const int BYTES_PER_SECTOR = 256;
        private const int SECTOR_COUNT = 1024;

        private string filename;
        private System.IO.FileStream stream;
        private bool powerOn;
        private int serialNumber;

        public PersistentDisk(int serialNumber, string filename)
        {
            this.serialNumber = serialNumber;
            powerOn = false;
            this.filename = filename;
            this.stream = null;
        }

        // disk properties
        public int SerialNumber { get { return serialNumber; } }
        public int BytesPerSector { get { return BYTES_PER_SECTOR; } }
        public int SectorCount { get { return SECTOR_COUNT; } }

        // current state of disk
        public bool Ready { get { return powerOn; } }

        public void TurnOn()
        {
            // create persistent file if it doesn't exist yet
            if (!System.IO.File.Exists(filename))
            {
                System.IO.FileStream fs = System.IO.File.Create(filename, BYTES_PER_SECTOR);
                fs.SetLength(BYTES_PER_SECTOR * SECTOR_COUNT);
                fs.Flush();
                fs.Close();
            }

            stream = System.IO.File.Open(filename, FileMode.Open, FileAccess.ReadWrite);
            if (stream == null)
                throw new Exception("Failed to open file");

            powerOn = true;
        }

        public void TurnOff()
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }

            powerOn = false;
        }

        // read / write
        public byte[] ReadSector(int lba)
        {
            CheckReady();
            CheckLBA(lba);

            byte[] sector = new byte[BYTES_PER_SECTOR];
            stream.Seek(lba * BYTES_PER_SECTOR, SeekOrigin.Begin);
            if (stream.Read(sector, 0, BYTES_PER_SECTOR) != BYTES_PER_SECTOR)
            {
                throw new Exception("Failed to read all bytes for sector");
            }

            return sector;
        }

        public void WriteSector(int lba, byte[] data)
        {
            CheckReady();
            CheckLBA(lba);
            CheckSectorLength(data.Length);

            stream.Seek(lba * BYTES_PER_SECTOR, SeekOrigin.Begin);
            stream.Write(data, 0, BYTES_PER_SECTOR);
            stream.Flush();
        }

        // helpers
        private void CheckReady()
        {
            if (!powerOn || stream == null)
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
