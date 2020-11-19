// SimplePhysicalFS.cs
// Oregon Institute of Technology
// Spring 2018
// Instructor: Pete Myers
//
//Ruby Felton
//
// NOTE: This Program was created from a stub handed out by instructor
// It was worked on collaboratively as a class 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleFileSystem
{

    // use constructor to create a new sector and then write it's bytes to disk
    // use GetTypeFromBytes() and CreateFromBytes() for each class when reading bytes from disk

    public abstract class SECTOR
    {
        private const int TYPE_AT = 0;
        private const int NEXT_SECTOR_AT = 1;
        protected const int SECTOR_DATA_LEN = 5;

        public enum SectorType : byte { FREE_SECTOR=0, DRIVE_INFO=1, DIR_NODE=2, FILE_NODE=3, DATA_SECTOR=4 };

        private int bytesPerSector;
        private SectorType type;
        private int nextSectorAt;
        protected byte[] raw;

        protected SECTOR(int bytesPerSector, SectorType type, int nextSectorAt)
        {
            this.bytesPerSector = bytesPerSector;
            this.type = type;
            
            // construct raw bytes with all zeroes
            raw = new byte[bytesPerSector];
            raw[TYPE_AT] = (byte)type;
            NextSectorAt = nextSectorAt;
        }

        protected SECTOR(byte[] raw)
        {
            this.bytesPerSector = raw.Length;   // NOTE: assuming the raw bytes are correct length!!!!
            this.raw = raw;
            type = (SectorType)raw[TYPE_AT];
            nextSectorAt = BitConverter.ToInt32(raw, NEXT_SECTOR_AT);
        }

        public static SectorType GetTypeFromBytes(byte[] raw)
        {
            // determine the sector type based on the raw bytes
            return (SectorType)raw[TYPE_AT];
        }

        public int BytesPerSector => bytesPerSector;
        public SectorType Type => type;
        public byte[] RawBytes => raw;

        public int NextSectorAt
        {
            get { return nextSectorAt; }
            set
            {
                nextSectorAt = value;
                BitConverter.GetBytes(nextSectorAt).CopyTo(raw, NEXT_SECTOR_AT);
            }
        }
    }

    public class FREE_SECTOR : SECTOR
    {
        public FREE_SECTOR(int bytesPerSector) : base(bytesPerSector, SectorType.FREE_SECTOR, 0)
        {
            // nothing to see here, move along
        }

        private FREE_SECTOR(byte [] raw) : base(raw)
        {
            // nothing to see here, move along
        }

        public static FREE_SECTOR CreateFromBytes(byte[] raw)
        {
            if (GetTypeFromBytes(raw) != SectorType.FREE_SECTOR)
                throw new Exception("Expected a FREE_SECTOR!");

            return new FREE_SECTOR(raw);
        }
    }

    public class DRIVE_INFO : SECTOR
    {
        public DRIVE_INFO(int bytesPerSector, int rootNodeAt) : base(bytesPerSector, SectorType.DRIVE_INFO, rootNodeAt)
        {
            // nothing to see here, move along
        }

        private DRIVE_INFO(byte[] raw) : base(raw)
        {
            // nothing to see here, move along
        }

        // root node location is stored in the next sector field
        public int RootNodeAt => NextSectorAt;

        public static DRIVE_INFO CreateFromBytes(byte[] raw)
        {
            if (GetTypeFromBytes(raw) != SectorType.DRIVE_INFO)
                throw new Exception("Expected a DRIVE_INFO!");

            return new DRIVE_INFO(raw);
        }
    }

    public abstract class NODE : SECTOR
    {
        // abstract NODE class cannot be created directly, base class for directory and file nodes
        // NOTE: other file/directory meta data could be added to this class
        protected const int NODE_DATA_LEN = SECTOR.SECTOR_DATA_LEN + FSConstants.MAX_FILENAME;

        private const int NAME_AT = SECTOR.SECTOR_DATA_LEN;
        private string name;

        protected NODE(int bytesPerSector, SectorType type, int nextSectorAt, string name) : base(bytesPerSector, type, nextSectorAt)
        {
            Name = name;
        }

        protected NODE(byte[] raw) : base(raw)
        {
            // pull out the name from the raw bytes
            name = Encoding.ASCII.GetString(raw, NAME_AT, FSConstants.MAX_FILENAME).Trim('\0');
        }

        // first data sector location is stored in the next sector field
        public int FirstDataAt => NextSectorAt;
        public string Name
        {
            get { return name; }
            set
            {
                // encode name string as bytes
                if (value.Length > FSConstants.MAX_FILENAME)
                    throw new Exception($"Name {name} too long!");

                this.name = value;
                Encoding.ASCII.GetBytes(name).CopyTo(raw, NAME_AT);
            }
        }

        public static NODE CreateFromBytes(byte[] raw)
        {
            switch (GetTypeFromBytes(raw))
            {
                case SectorType.DIR_NODE:
                    return DIR_NODE.CreateFromBytes(raw);

                case SectorType.FILE_NODE:
                    return FILE_NODE.CreateFromBytes(raw);
            }

            throw new Exception("Expected a DIR_NODE or FILE_NODE!");
        }
    }

    public class DIR_NODE : NODE
    {
        private const int ENTRY_COUNT_AT = NODE.NODE_DATA_LEN;
        private int entryCount;

        public DIR_NODE(int bytesPerSector, int firstDataAt, string name, int entryCount) : base(bytesPerSector, SectorType.DIR_NODE, firstDataAt, name)
        {
            // entry count
            EntryCount = entryCount;
        }

        private DIR_NODE(byte[] raw) : base(raw)
        {
            // entry count
            entryCount = BitConverter.ToInt32(raw, ENTRY_COUNT_AT);
        }

        public int EntryCount
        {
            get { return entryCount; }
            set
            {
                entryCount = value;
                BitConverter.GetBytes(entryCount).CopyTo(raw, ENTRY_COUNT_AT);
            }
        }

        new public static DIR_NODE CreateFromBytes(byte[] raw)
        {
            if (GetTypeFromBytes(raw) != SectorType.DIR_NODE)
                throw new Exception("Expected a DIR_NODE!");

            return new DIR_NODE(raw);
        }
    }

    public class FILE_NODE : NODE
    {
        private const int FILE_SIZE_AT = NODE.NODE_DATA_LEN;
        private int fileSize;

        public FILE_NODE(int bytesPerSector, int firstDataAt, string name, int fileSize) : base(bytesPerSector, SectorType.FILE_NODE, firstDataAt, name)
        {
            // file size
            FileSize = fileSize;
        }

        private FILE_NODE(byte[] raw) : base(raw)
        {
            // file size
            fileSize = BitConverter.ToInt32(raw, FILE_SIZE_AT);
        }

        new public static FILE_NODE CreateFromBytes(byte[] raw)
        {
            if (GetTypeFromBytes(raw) != SectorType.FILE_NODE)
                throw new Exception("Expected a FILE_NODE!");

            return new FILE_NODE(raw);
        }

        public int FileSize
        {
            get { return fileSize; }
            set
            {
                fileSize = value;
                BitConverter.GetBytes(fileSize).CopyTo(raw, FILE_SIZE_AT);
            }
        }
    }

    public class DATA_SECTOR : SECTOR
    {
        public static int MaxDataLength(int bytesPerSector)
        {
            
            return bytesPerSector - SECTOR.SECTOR_DATA_LEN;
        }

        public DATA_SECTOR(int bytesPerSector, int nextDataAt, byte[] data) : base(bytesPerSector, SectorType.DATA_SECTOR, nextDataAt)
        {
            DataBytes = data;
        }

        private DATA_SECTOR(byte [] raw) : base(raw)
        {
            // nothing to see here, move along
        }

        public byte[] DataBytes
        {
            get { return raw.Skip(SECTOR.SECTOR_DATA_LEN).ToArray(); }
            set
            {
                if (value != null)
                {
                    if (value.Length > MaxDataLength(BytesPerSector))
                        throw new Exception("Too much data for sector!");

                    value.CopyTo(raw, SECTOR.SECTOR_DATA_LEN);
                }
                else
                {
                    // blank out the data
                    Array.Clear(raw, SECTOR.SECTOR_DATA_LEN, MaxDataLength(BytesPerSector));
                }
            }
        }

        public static DATA_SECTOR CreateFromBytes(byte[] raw)
        {
            if (GetTypeFromBytes(raw) != SectorType.DATA_SECTOR)
                throw new Exception($"Expected a DATA_SECTOR!");

            return new DATA_SECTOR(raw);
        }
    }
    
}
