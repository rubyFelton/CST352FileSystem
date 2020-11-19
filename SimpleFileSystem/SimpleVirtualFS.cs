// SimpleVirtualFS.cs
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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SimpleFileSystem
{
    // NOTE:  Blocks are used for file data, directory contents are just stored in linked sectors (not blocks)

    public class VirtualFS
    {
        private const int DRIVE_INFO_SECTOR = 0;
        private const int ROOT_DIR_SECTOR = 1;
        private const int ROOT_DATA_SECTOR = 2;

        private Dictionary<string, VirtualDrive> drives;    // mountPoint --> drive
        private VirtualNode rootNode;

        public VirtualFS()
        {
            this.drives = new Dictionary<string, VirtualDrive>();
            this.rootNode = null;
        }

        public void Format(DiskDriver disk)
        {
            // wipe all sectors of disk and create minimum required DRIVE_INFO, DIR_NODE and DATA_SECTOR
            
            //how many sectors
            int numSectors = disk.SectorCount;

            FREE_SECTOR freeSector = new FREE_SECTOR(disk.BytesPerSector);
            for (int lba = 0;  lba < numSectors; lba++)
            {
                disk.WriteSector(lba, freeSector.RawBytes);
            }

            DRIVE_INFO diSector1 = new DRIVE_INFO(disk.BytesPerSector, ROOT_DIR_SECTOR);
            disk.WriteSector(0, diSector1.RawBytes);

            DIR_NODE rootDirSector = new DIR_NODE(disk.BytesPerSector, ROOT_DATA_SECTOR, FSConstants.ROOT_DIR_NAME, 0);
            disk.WriteSector(ROOT_DIR_SECTOR, rootDirSector.RawBytes);


            DATA_SECTOR rootDataSector = new DATA_SECTOR(disk.BytesPerSector, 0, new byte[DATA_SECTOR.MaxDataLength(disk.BytesPerSector)]);
            disk.WriteSector(ROOT_DATA_SECTOR, rootDataSector.RawBytes);

        }

        public void Mount(DiskDriver disk, string mountPoint)
        {
            // read drive info from disk, load root node and connect to mountPoint
            // for the first mounted drive, expect mountPoint to be named FSConstants.ROOT_DIR_NAME as the root

            //read drive info for disk to determin what sector contains its root node
            DRIVE_INFO diSector = DRIVE_INFO.CreateFromBytes(disk.ReadSector(DRIVE_INFO_SECTOR));

            int rootNodeAt = diSector.RootNodeAt;

            VirtualDrive drive = new VirtualDrive(disk, DRIVE_INFO_SECTOR, diSector);

            DIR_NODE rootNodeSector = DIR_NODE.CreateFromBytes(disk.ReadSector(rootNodeAt));

            if (rootNode == null)
            {
                rootNode = new VirtualNode(drive, rootNodeAt, rootNodeSector, null);
            }

            drives.Add(mountPoint, drive);
        }

        public void Unmount(string mountPoint)
        {
            // remove drive mountPoint and root node

            drives.Remove(mountPoint);

            //rework for when multiples drives are mounted *look up by mount point
            rootNode = null;
        }

        public VirtualNode RootNode => rootNode;
    }

    public class VirtualDrive
    {
        private int bytesPerDataSector;
        private DiskDriver disk;
        private int driveInfoSector;
        private DRIVE_INFO sector;      // caching entire sector for now

        public VirtualDrive(DiskDriver disk, int driveInfoSector, DRIVE_INFO sector)
        {
            this.disk = disk;
            this.driveInfoSector = driveInfoSector;
            this.bytesPerDataSector = DATA_SECTOR.MaxDataLength(disk.BytesPerSector);
            this.sector = sector;
        }

        public int[] GetNextFreeSectors(int count)
        {
            // find count available free sectors on the disk and return their addresses
            //if not enough free sectors found throw exception
            int[] result = new int[count];
            count--;

            for (int lba = 0; lba <disk.SectorCount && count >= 0; lba++)
            {
                byte[] raw = disk.ReadSector(lba);

                if (SECTOR.GetTypeFromBytes(raw) == SECTOR.SectorType.FREE_SECTOR)
                {
                    result[count] = lba;
                    count--;
                }
            }

            if (count >= 0)
            {
                throw new Exception("can't find enough free sectors");
            }

            return result;
        }

        public DiskDriver Disk => disk;
        public int BytesPerDataSector => bytesPerDataSector;
    }

    public class VirtualNode
    {
        private VirtualDrive drive;
        private int nodeSector;
        private NODE sector;                                // caching entire sector for now
        private VirtualNode parent;
        private Dictionary<string, VirtualNode> children;   // child name --> child node
        private List<VirtualBlock> blocks;                  // cache of file blocks

        public VirtualNode(VirtualDrive drive, int nodeSector, NODE sector, VirtualNode parent)
        {
            this.drive = drive;
            this.nodeSector = nodeSector;
            this.sector = sector;
            this.parent = parent;
            this.children = null;                           // initially empty cache
            this.blocks = null;                             // initially empty cache
        }

        public VirtualDrive Drive => drive;
        public string Name => sector.Name;
        public VirtualNode Parent => parent;
        public bool IsDirectory { get { return sector.Type == SECTOR.SectorType.DIR_NODE; } }
        public bool IsFile { get { return sector.Type == SECTOR.SectorType.FILE_NODE; } }
        public int ChildCount => (sector as DIR_NODE).EntryCount;
        public int FileLength => (sector as FILE_NODE).FileSize;
        public int DataSectorAt => sector.FirstDataAt;
        public void Rename(string newName)
        {
            // rename this node, update parent as needed, save new name on disk
            string oldName = Name;
            parent.LoadChildren();
            parent.children.Remove(oldName);
            parent.children.Add(newName, this);
            parent.CommitChildren();
            sector.Name = newName;

            drive.Disk.WriteSector(nodeSector, sector.RawBytes);
        }

        public void Move(VirtualNode destination)
        {
            // remove this node from it's current parent and attach it to it's new parent
            // update the directory information for both parents on disk
            VirtualNode currentParent = parent;
            parent.LoadChildren();
            parent.children.Remove(Name);
            destination.LoadChildren();
            destination.children.Add(Name, this);

            parent = destination;
            currentParent.CommitChildren();
            destination.CommitChildren();
        }

        public void Delete()
        {
            if (IsDirectory)
            {
                //nuke children
                LoadChildren();
                foreach (VirtualNode child in children.Values.ToArray())
                {
                    child.Delete();
                }
                CommitChildren();
            }

            // make sectors free!
            //overwirte node sector
            FREE_SECTOR freeSector = new FREE_SECTOR(drive.Disk.BytesPerSector);
            drive.Disk.WriteSector(nodeSector, freeSector.RawBytes);

            //overwrite data sectors
            int lba = DataSectorAt;

            while(lba != 0)
            {
                DATA_SECTOR dataSector = DATA_SECTOR.CreateFromBytes(drive.Disk.ReadSector(lba));
                drive.Disk.WriteSector(lba, freeSector.RawBytes);
                lba = dataSector.NextSectorAt;
            }

            // remove this node from it's parent node
            parent.LoadChildren();
            parent.children.Remove(Name);
            parent.CommitChildren();

        }

        private void LoadChildren()
        {
            //test if we already have everything in cache?
            if (children != null)
            {
                return;
            }

            //create empty cache
            children = new Dictionary<string, VirtualNode>();

            //read this dir's data sector
            DATA_SECTOR dataSector = DATA_SECTOR.CreateFromBytes(drive.Disk.ReadSector(DataSectorAt));

            //create virtual nodes for each child and add to children dictionary
            byte[] data = dataSector.DataBytes;
            
            for (int i = 0; i < ChildCount; i++)
            {
                int childAt = BitConverter.ToInt32(data, i*4);

                NODE childNodeSector = NODE.CreateFromBytes(drive.Disk.ReadSector(childAt));

                VirtualNode childNode = new VirtualNode(drive, childAt, childNodeSector, this);
                children.Add(childNode.Name, childNode);
            }

        }

        private void CommitChildren()
        {
            //write list of this dir's children back to disk
            //check if cache has data if no data return
            if (children == null)
            {
                return;
            }

            //allocate list of children sector addresses
            byte[] childAddresses = GetChildAddresses();

            //write the sector addesses list to directory's data sector
            DATA_SECTOR dataSector = DATA_SECTOR.CreateFromBytes(drive.Disk.ReadSector(DataSectorAt));
            dataSector.DataBytes = childAddresses;
            drive.Disk.WriteSector(DataSectorAt, dataSector.RawBytes);
            byte[] dataBytes = new byte[dataSector.DataBytes.Length];
            childAddresses.CopyTo(dataBytes, 0);
            dataSector.DataBytes = dataBytes;

            drive.Disk.WriteSector(DataSectorAt, dataSector.RawBytes);

            //update number of children entries for this dir
            (sector as DIR_NODE).EntryCount = ChildCount;
            drive.Disk.WriteSector(nodeSector, sector.RawBytes);

        }

        private byte[] GetChildAddresses()
        {
            byte[] childAddresses = new byte[children.Count * 4];

            //loop through children in cache, build list of sector addresses
            int i = 0;
            foreach (VirtualNode child in children.Values)
            {
                int childAt = child.nodeSector;

                BitConverter.GetBytes(childAt).CopyTo(childAddresses, i);
                i += 4;
            }

            return childAddresses;
        }

        public VirtualNode CreateDirectoryNode(string name)
        {
            LoadChildren(); //insure child cached is full
            if (children.ContainsKey(name))
            {
                throw new Exception("Another child with that name already exists");
            }

            //where do we store new dir? which sector?
            //find 2 free sectors

            int[] sectors = drive.GetNextFreeSectors(2);
            int newDirectoryNodeSectorAt = sectors[0];
            int newDirDataSectorAt = sectors[1];

            //creat new dir on disk
            DIR_NODE newDirNodeSector = new DIR_NODE(drive.Disk.BytesPerSector, newDirDataSectorAt, name, 0);
            drive.Disk.WriteSector(newDirectoryNodeSectorAt, newDirNodeSector.RawBytes);
            
            //creat a data sector for dir
            DATA_SECTOR newDirDataSector = new DATA_SECTOR(drive.Disk.BytesPerSector, 0, null);
            drive.Disk.WriteSector(newDirDataSectorAt, newDirDataSector.RawBytes);

            //creat virtual node
            VirtualNode newDirectory = new VirtualNode(drive, newDirectoryNodeSectorAt, newDirNodeSector, this);

            //add dir to parent
            children.Add(name, newDirectory);
            CommitChildren();

            return newDirectory;
        }

        public VirtualNode CreateFileNode(string name)
        {
            LoadChildren(); //insure child cached is full
            if (children.ContainsKey(name))
            {
                throw new Exception("Another child with that name already exists");
            }

            //where do we store new file? which sector?
            //find 2 free sectors

            int[] sectors = drive.GetNextFreeSectors(2);
            int newFileNodeSectorAt = sectors[0];
            int newFileDataSectorAt = sectors[1];

            //creat new  on disk
            FILE_NODE newFileNodeSector = new FILE_NODE(drive.Disk.BytesPerSector, newFileDataSectorAt, name, 0);
            drive.Disk.WriteSector(newFileNodeSectorAt, newFileNodeSector.RawBytes);

            //creat a data sector for dir
            DATA_SECTOR newFileDataSector = new DATA_SECTOR(drive.Disk.BytesPerSector, 0, null);
            drive.Disk.WriteSector(newFileDataSectorAt, newFileDataSector.RawBytes);

            //creat virtual node
            VirtualNode newFile = new VirtualNode(drive, newFileNodeSectorAt, newFileNodeSector, this);

            //add dir to parent
            children.Add(name, newFile);
            CommitChildren();

            return newFile;
        }

        public IEnumerable<VirtualNode> GetChildren()
        {
            LoadChildren();
            return children.Values;
        }

        public VirtualNode GetChild(string name)
        {
            LoadChildren();

            if (children.ContainsKey(name))
            {
                return children[name];
            }

            return null;
        }

        private void LoadBlocks()
        {
            // read data secvtors from disk and create virtual blocks in memory

            //check if cahce is up to date
            if (blocks != null)
            {
                return;
            }

            //instantiate an empty cache
            blocks = new List<VirtualBlock>();

            //loop through data sectors for this file
            int nextDataSectorAt = DataSectorAt;

            while (nextDataSectorAt !=0)
            {
                //read sector
                DATA_SECTOR dataSector = DATA_SECTOR.CreateFromBytes(drive.Disk.ReadSector(nextDataSectorAt));
                //create virtual block to cache
                VirtualBlock block = new VirtualBlock(drive, nextDataSectorAt, dataSector);
                //add block to cache
                blocks.Add(block);

                //get the address of the next daa scetor
                nextDataSectorAt = dataSector.NextSectorAt;
            }

        }

        private void CommitBlocks()
        {
            //writes virtual blocks from memeory to disk

            foreach (VirtualBlock block in blocks )
            {
                //commit blocks back to disk
                block.CommitBlock();
            }
        }

        public byte[] Read(int index, int length)
        {
            //reads length data bytes from file starting at index byte 

            //fill cache by read current data from disk for this file
            LoadBlocks();

            //copy data from blocks
            return VirtualBlock.ReadBlockData(drive, blocks, index, length);
            
        }

        public void Write(int index, byte[] data)
        {
            //writes data bytes to the file starting at the index byte

            //fill cache by read current data from disk for this file
            LoadBlocks();

            //if data to be writen is beyond the end of the current number of blocks, extend blocks
            int currentFileLength = FileLength;
            int newFileLength = Math.Max(currentFileLength, index + data.Length);
            VirtualBlock.ExtendBlocks(drive, blocks, currentFileLength, newFileLength);
            
            //copy data from cache to blocks 
            VirtualBlock.WriteBlockData(drive, blocks, index, data);
            
            //commit cache back to disk
            CommitBlocks();

            if (newFileLength > currentFileLength)
            {
                (sector as FILE_NODE).FileSize = newFileLength;
                drive.Disk.WriteSector(nodeSector, sector.RawBytes);
            }
        }
    }

    public class VirtualBlock
    {
        private VirtualDrive drive;
        private DATA_SECTOR sector;
        private int sectorAddress;
        private bool dirty;

        public VirtualBlock(VirtualDrive drive, int sectorAddress, DATA_SECTOR sector, bool dirty = false)
        {
            this.drive = drive;
            this.sector = sector;
            this.sectorAddress = sectorAddress;
            this.dirty = dirty;
        }

        public int SectorAddress => sectorAddress;
        public DATA_SECTOR Sector => sector;
        public bool Dirty => dirty;

        public byte[] Data
        {
            get { return (byte[])sector.DataBytes.Clone(); }
            set
            {
                sector.DataBytes = value;
                dirty = true;
            }
        }

        public void CommitBlock()
        {
            //wrtie this blocks data to it's data sector if needed

            //if blocks dirty then it has data that needs to be written to disk
            if (dirty)
            {
                //write data sector to disk
                drive.Disk.WriteSector(SectorAddress, sector.RawBytes);

                //no longer dirty
                dirty = false;
            }
        }

        public static byte[] ReadBlockData(VirtualDrive drive, List<VirtualBlock> blocks, int startIndex, int length)
        {
            //given list of blocks in memory copy length bytes from blocks, starting at start index 

            //allocate resulting data array
            byte[] data = new byte[length];

            //case 1: initial bytes from middle of block to end of block
            //copy from block, starting somewhere to begining of data array

            int fromIndex = startIndex % drive.BytesPerDataSector; //where we start copying from the block?

            int copyCount = Math.Min(data.Length, drive.BytesPerDataSector - fromIndex); //min of data length and the # of bytes left

            int firstBlocktoRead = startIndex / drive.BytesPerDataSector; //the first block to be writen?
            VirtualBlock block = blocks[firstBlocktoRead];

            int toStart = 0; //index in data to start at

            CopyBytes(copyCount, block.Data, fromIndex, data, toStart); //change clone

            toStart += copyCount; //update to where to copy to next

            //case 2: read all bytes in block
            int nextBlocktoRead = firstBlocktoRead + 1;  //the next block to be writen?

            copyCount = drive.BytesPerDataSector;

            while (toStart + copyCount < data.Length)
            {
                block = blocks[nextBlocktoRead];
                nextBlocktoRead++;

                fromIndex = 0;
                CopyBytes(copyCount, block.Data, fromIndex, data, toStart); //change clone
                toStart += copyCount; //update to where to copy to next

            }

            //there is still data left to copy
            if (toStart < data.Length)
            {
                //case 3: final bytes from beginning of block to middle of that block

                int finalIndex = (startIndex + data.Length);
                int finalBlockToRead = finalIndex / drive.BytesPerDataSector;
                block = blocks[finalBlockToRead];
                copyCount = finalIndex % drive.BytesPerDataSector; //where to stop reading

                fromIndex = 0;

                CopyBytes(copyCount, block.Data, fromIndex, data, toStart);
            }

            return data;
        }

        public static void WriteBlockData(VirtualDrive drive, List<VirtualBlock> blocks, int startIndex, byte[] data)
        {
            //given list of blocks in memory copy data bytes starting at start index

            //case 1: initial bytes from middle of block to end of block

            int toStart = startIndex % drive.BytesPerDataSector; //where we start copying in the block?

            int copyCount = Math.Min(data.Length, drive.BytesPerDataSector - toStart); //min of data length and the # of bytes left
            
            int firstBlocktoWrite = startIndex/ drive.BytesPerDataSector; //the first block to be writen?
            VirtualBlock block = blocks[firstBlocktoWrite];

            int fromIndex = 0; //index in data to start at
            byte[] to = block.Data; //data clones
            
            CopyBytes(copyCount, data, fromIndex, to, toStart); //change clone
            block.Data = to; //write back changed data set dirty
            fromIndex += copyCount; //update to where to copy to next

            //case 2: overwrite all bytes in block
            int nextBlocktoWrite = firstBlocktoWrite + 1; //the next block to be writen?

            copyCount = drive.BytesPerDataSector;
            toStart = 0;

            while (fromIndex + copyCount < data.Length)
            {
                block = blocks[nextBlocktoWrite];
                nextBlocktoWrite++;

                to = block.Data;

                CopyBytes(copyCount, data, fromIndex, to, toStart); //change clone
                block.Data = to; //write back changed data set dirty
                fromIndex += copyCount; //update to where to copy to next
            }

            //there is still data left to copy
            if (fromIndex < data.Length)
            {
                //case 3: final bytes from beginning of block to middle of that block

                int finalIndex = (startIndex + data.Length);
                block = blocks[nextBlocktoWrite];
                copyCount = finalIndex % drive.BytesPerDataSector;
                to = block.Data; //data clones
                toStart = 0;

                CopyBytes(copyCount, data, fromIndex, to, toStart); //change clone
                block.Data = to; //write back changed data set dirty
            }

        }

        public static void ExtendBlocks(VirtualDrive drive, List<VirtualBlock> blocks, int initialFileLength, int finalFileLength)
        {
            //given initial list of blocks in memory add more block if necessary to grow file from initial to final length

            //determine # of blocks eeded for the file
            int currentBlockCount = blocks.Count;
            int finalBlockCount = BlocksNeeded(drive, finalFileLength);

            if (finalBlockCount <= currentBlockCount)
            {
                return;
            }

            //get sector addresses for new blocks
            int additionalBlocks = finalBlockCount - currentBlockCount;
            int[] additionalBlockAddresses = drive.GetNextFreeSectors(additionalBlocks);

            //update the last current block to point to the first new block
            blocks[currentBlockCount - 1].sector.NextSectorAt = additionalBlockAddresses[0];
            blocks[currentBlockCount - 1].dirty = true;

            //Allocate each block as a sector
            for (int i = 0; i < additionalBlocks; i++)
            {
                //wcreate new data sectorin memory
                DATA_SECTOR dataSector = new DATA_SECTOR(drive.Disk.BytesPerSector, 0, null);

                //set this sector's link to the data sector, if there is a next sector
                if (i < additionalBlocks - 1)
                {
                    dataSector.NextSectorAt = additionalBlockAddresses[i + 1];
                }

                //add new virtualblock to list which is drity so it will be writen to disk when commited
                blocks.Add(new VirtualBlock(drive, additionalBlockAddresses[i], dataSector, true));
            }

        }

        private static int BlocksNeeded(VirtualDrive drive, int numBytes)
        {
            //returns number of blocks needed for numbytes give bytes per data sector
            return Math.Max(1, (int)Math.Ceiling((double)numBytes / drive.BytesPerDataSector));
        }

        private static void CopyBytes(int copyCount, byte[] from, int fromStart, byte[] to, int toStart)
        {
            for (int i = 0; i < copyCount; i++)
            {
                to[toStart + i] = from[fromStart + i];
            }
        }
    }
}
